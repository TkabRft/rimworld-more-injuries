using MoreInjuries.Caching;
using MoreInjuries.Extensions;
using MoreInjuries.Extensions.Bcl;
using MoreInjuries.HealthConditions.HeavyBleeding.Overrides.BleedRateModifiers;
using MoreInjuries.Localization;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace MoreInjuries.HealthConditions.HeavyBleeding.Overrides;

public class BetterInjuryState<TOwner>(TOwner owner) : IExposable, IInjuryState, IForecastHemostasis where TOwner : HediffWithComps, IInjuryStateOwner
{
    void IForecastHemostasis.CaptureHemostasis(out float constant, out float timedBase, out int remaining, out int duration)
    {
        bool timed = IsTemporarilyCoagulated;
        constant = EffectiveBleedRateMultiplier / (timed ? TemporarilyTamponadedMultiplier : 1f);
        timedBase = timed ? _temporarilyTamponadedMultiplierBase : 1f;
        remaining = timed ? RemainingNow : 0;
        duration = timed ? _reducedBleedRateTicksTotal : 0;
    }
    private static readonly WeakTimedDataCache<Pawn, IReadOnlyList<HediffCrossInteraction>, TimedDataEntry<IReadOnlyList<HediffCrossInteraction>>> s_crossInteractionCache = new
    (
        minCacheRefreshIntervalTicks: GenTicks.TickRareInterval,
        dataProvider: GetCrossInteractions
    );

    private int _coagulationFlags;
    private float _coagulationMultiplier = 1;
    private int _reducedBleedRateTicksRemaining;
    private int _reducedBleedRateTicksTotal;
    private float _temporarilyTamponadedMultiplierBase = 1;
    private TimedHemostasisClock _timedClock;
    private float _timedMultiplier = 1f;
    private int RemainingNow => _timedClock.RemainingAt(owner.ageTicks);

    private void ScheduleHemostasis()
    {
        _timedClock.Reset(_reducedBleedRateTicksTotal, _reducedBleedRateTicksRemaining, owner.ageTicks);
        _timedMultiplier = TimedHemostasis.Multiplier(_temporarilyTamponadedMultiplierBase, _reducedBleedRateTicksTotal, _reducedBleedRateTicksRemaining);
        if (owner.pawn is { } pawn) { MoreInjuries.HealthConditions.VitalFailure.VitalFailureComp.For(pawn)?.InvalidateForecast(); }
    }

    public CoagulationFlag CoagulationFlags
    {
        get => (CoagulationFlag)_coagulationFlags;
        set => _coagulationFlags = value;
    }

    public float CoagulationMultiplier
    {
        get => _coagulationMultiplier;
        set => _coagulationMultiplier = value;
    }

    private float IntrinsicBleedRateMultiplier
    {
        get
        {
            float multiplier = CoagulationMultiplier;
            if (IsTemporarilyCoagulated)
            {
                multiplier *= TemporarilyTamponadedMultiplier;
            }
            return owner.OverrideEffectiveBleedRateMultiplier(multiplier);
        }
    }

    public float EffectiveBleedRateMultiplier => IntrinsicBleedRateMultiplier * CrossInteractionMultiplier;

    public float CrossInteractionMultiplier
    {
        get
        {
            float multiplier = 1f;
            foreach (HediffCrossInteraction interaction in s_crossInteractionCache.GetData(owner.pawn))
            {
                multiplier *= interaction.Modifier.GetModifierFor(hediff: interaction.Hediff, bleedingHediff: owner);
            }
            return multiplier;
        }
    }

    public int ReducedBleedRateTicksTotal
    {
        get => _reducedBleedRateTicksTotal;
        set
        {
            _reducedBleedRateTicksTotal = value;
            _reducedBleedRateTicksRemaining = value;
            ScheduleHemostasis();
        }
    }

    public float TemporarilyTamponadedMultiplierBase
    {
        get => _temporarilyTamponadedMultiplierBase;
        set
        {
            _temporarilyTamponadedMultiplierBase = value;
            _timedMultiplier = TimedHemostasis.Multiplier(value, _reducedBleedRateTicksTotal, _reducedBleedRateTicksRemaining);
            if (owner.pawn is { } pawn) { MoreInjuries.HealthConditions.VitalFailure.VitalFailureComp.For(pawn)?.InvalidateForecast(); }
        }
    }

    public float TemporarilyTamponadedMultiplier
    {
        get
        {
            if (_reducedBleedRateTicksTotal <= 0)
            {
                return 1f;
            }
            return _timedMultiplier;
        }
    }

    public bool IsTimedTamponadeActive => !CoagulationFlags.IsEmpty && _reducedBleedRateTicksRemaining > 0;

    public bool IsTemporarilyCoagulated => this is
    {
        CoagulationFlags.IsEmpty: false,
        _reducedBleedRateTicksRemaining: > 0,
        _reducedBleedRateTicksTotal: > 0,
        _temporarilyTamponadedMultiplierBase: not 1
    };

    public string Label
    {
        get
        {
            if (!owner.Bleeding || IntrinsicBleedRateMultiplier == 1f)
            {
                return owner.BaseLabel;
            }
            bool hasPreviousElements = false;
            StringBuilder builder = new StringBuilder(owner.BaseLabel)
                .Append(" (");
            if (CoagulationFlags.IsSet(CoagulationFlag.Manual))
            {
                builder.AppendEnumerationItem("MI_InjuryBleeding_ManuallyReduced_Label".Translate(), ref hasPreviousElements);
            }
            if (IsTemporarilyCoagulated)
            {
                int durationHours = _reducedBleedRateTicksRemaining / 2500;
                builder.AppendEnumerationItem(
                    "MI_InjuryBleeding_TemporarilyReduced_Label".Translate(
                        Named.Keys.Format_TimeHours
                            .Translate(durationHours.Named(Named.Params.HOURS))
                            .Named(Named.Params.TIME)), ref hasPreviousElements);
            }
            owner.AddCustomLabelAnnotations(builder, ref hasPreviousElements);
            builder.Append(')');
            return builder.ToString();
        }
    }

    public string TipStringExtra
    {
        get
        {
            if (!owner.Bleeding || EffectiveBleedRateMultiplier == 1f)
            {
                return owner.BaseTipStringExtra;
            }
            StringBuilder builder = new(owner.BaseTipStringExtra);
            builder.AppendLine();
            bool hasCustomInfo = false;
            if (CoagulationFlags.IsSet(CoagulationFlag.Manual))
            {
                double bleedRateDecrease = Math.Round((1f - CoagulationMultiplier) * 100f, 2);
                builder.AppendLine("MI_InjuryBleeding_ManuallyReduced_Tooltip".Translate(bleedRateDecrease.Named(Named.Params.PERCENT)));
                hasCustomInfo = true;
            }
            if (IsTemporarilyCoagulated)
            {
                int durationHours = _reducedBleedRateTicksRemaining / 2500;
                double bleedRateDecrease = Math.Round((1f - TemporarilyTamponadedMultiplier) * 100f, 2);
                builder.AppendLine("MI_InjuryBleeding_TemporarilyReduced_Tooltip".Translate(
                    Named.Keys.Format_TimeHours
                        .Translate(durationHours.Named(Named.Params.HOURS))
                        .Named(Named.Params.TIME),
                    bleedRateDecrease.Named(Named.Params.PERCENT)));
                hasCustomInfo = true;
            }
            owner.AddCustomLabelAnnotations(builder, ref hasCustomInfo);
            if (hasCustomInfo)
            {
                double effectiveBleedRate = Math.Round(EffectiveBleedRateMultiplier * 100f, 2);
                builder.AppendLine("MI_InjuryBleeding_EffectiveBleedRate_Tooltip"
                    .Translate(effectiveBleedRate.Named(Named.Params.PERCENT)));
            }
            IReadOnlyList<HediffCrossInteraction> crossInteractions = s_crossInteractionCache.GetData(owner.pawn);
            for (int i = 0; i < crossInteractions.Count; i++)
            {
                if (i == 0)
                {
                    builder.AppendLine()
                        .AppendLine("MI_InjuryBleeding_CrossInteraction_Tooltip_Header".Translate());
                }
                HediffCrossInteraction interaction = crossInteractions[i];
                float modifier = interaction.Modifier.GetModifierFor(hediff: interaction.Hediff, bleedingHediff: owner);
                string modifierPercentage = ((modifier - 1f) * 100f).ToString("+0.##;-0.##;0");
                builder.AppendLine("MI_InjuryBleeding_CrossInteraction_Tooltip".Translate(
                    interaction.Hediff.Named(Named.Params.HEDIFF),
                    modifierPercentage.Named(Named.Params.PERCENT)));
                hasCustomInfo = true;
            }
            return builder.ToString();
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref _coagulationFlags, "coagulationFlags");
        Scribe_Values.Look(ref _temporarilyTamponadedMultiplierBase, "temporarilyTamponadedMultiplierBase");
        Scribe_Values.Look(ref _coagulationMultiplier, "overriddenBleedRate");
        Scribe_Values.Look(ref _reducedBleedRateTicksTotal, "reducedBleedRateTicksTotal");
        int remaining = Scribe.mode == LoadSaveMode.Saving ? RemainingNow : _reducedBleedRateTicksRemaining;
        Scribe_Values.Look(ref remaining, "reducedBleedRateTicksRemaining", 30000);
        if (Scribe.mode == LoadSaveMode.LoadingVars) { _reducedBleedRateTicksRemaining = remaining; }
        if (Scribe.mode == LoadSaveMode.PostLoadInit) { ScheduleHemostasis(); }
    }

    public void Tick()
    {
        if (CoagulationFlags.IsEmpty || !_timedClock.Advance(owner.ageTicks, out int remaining)) { return; }
        _reducedBleedRateTicksRemaining = remaining;
        ScheduleHemostasis();
        if (_reducedBleedRateTicksRemaining <= 0)
        {
            // revert the multiplier
            TemporarilyTamponadedMultiplierBase = 1;
            ReducedBleedRateTicksTotal = 0;
            // remove any temporary tamponade flags
            CoagulationFlags = CoagulationFlag.Unset(CoagulationFlags, CoagulationFlag.Timed);
        }
    }

    private static IReadOnlyList<HediffCrossInteraction> GetCrossInteractions(Pawn pawn)
    {
        List<HediffCrossInteraction>? crossInteractions = null;
        foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
        {
            if (hediff.def.GetCachedModExtension<BleedRateModifier_ModExtension>() is { Modifier: var modifier })
            {
                crossInteractions ??= [];
                crossInteractions.Add(new HediffCrossInteraction(hediff, modifier));
            }
        }
        return (IReadOnlyList<HediffCrossInteraction>?)crossInteractions ?? Array.Empty<HediffCrossInteraction>();
    }

    private readonly record struct HediffCrossInteraction(Hediff Hediff, BleedRateModifier Modifier);
}
