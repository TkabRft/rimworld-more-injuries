using MoreInjuries.Caching;
using MoreInjuries.Extensions.Bcl;
using MoreInjuries.Localization;
using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace MoreInjuries.HealthConditions.HeavyBleeding.Overrides;

public class BetterInjury : Hediff_Injury, IStatefulInjury, IInjuryStateOwner
{
    private static readonly WeakTimedDataCache<Pawn, IReadOnlyList<BodyPartRecord>, TimedDataEntry<IReadOnlyList<BodyPartRecord>>> s_blockingExternalParts = new
    (
        minCacheRefreshIntervalTicks: GenTicks.TickRareInterval,
        dataProvider: CollectBlockingExternalParts
    );

    private readonly BetterInjuryState<BetterInjury> _state;

    public BetterInjury()
    {
        _state = new BetterInjuryState<BetterInjury>(this);
    }

    public IInjuryState State => _state;

    public float OverrideEffectiveBleedRateMultiplier(float multiplier)
    {
        if (IsClosedInternalWound)
        {
            multiplier *= MoreInjuriesMod.Settings.ClosedInternalWouldBleedingModifier;
        }
        return multiplier;
    }

    public bool IsInternalInjury => Part is { depth: BodyPartDepth.Inside };

    /// <summary>
    /// Returns true if this is an internal injury that is still bleeding and all related external injuries are tended, hemostat applied, or aren't bleeding
    /// </summary>
    public bool IsClosedInternalWound
    {
        get
        {
            // prefer early return
            if (Part is null || !IsInternalInjury || def.injuryProps.bleedRate <= 0 || this.IsPermanent())
            {
                return false;
            }
            return !HasRelatedBlockingExternalWound(forceRefresh: false);
        }
    }

    public bool GetIsClosedInternalWound(bool forceRefresh = false) => !HasRelatedBlockingExternalWound(forceRefresh);

    public override int CurStageIndex
    {
        get
        {
            if (def != MoreInjuries.Defs.WellKnown.KnownHediffDefOf.BrainDamage_Hypoxia)
            {
                return base.CurStageIndex;
            }
            float severity = MoreInjuries.HealthConditions.BrainDamage.CerebralInjury.ClinicalSeverity(this);
            for (int i = def.stages.Count - 1; i >= 0; i--)
            {
                if (severity >= def.stages[i].minSeverity) { return i; }
            }
            return 0;
        }
    }

    public override float BleedRate => base.BleedRate * _state.EffectiveBleedRateMultiplier;

    public override string Label => _state.Label;

    public string BaseLabel => base.Label;

    public override string TipStringExtra => _state.TipStringExtra;

    public string BaseTipStringExtra => base.TipStringExtra;

    public override void Tick()
    {
        base.Tick();
        if (_state.IsTimedTamponadeActive)
        {
            _state.Tick();
        }
    }

    public override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        _state.Tick();
    }

    private bool HasRelatedBlockingExternalWound(bool forceRefresh)
    {
        BodyPartRecord? part = Part;
        IReadOnlyList<BodyPartRecord> blockingParts = s_blockingExternalParts.GetData(pawn, forceRefresh);
        for (int i = 0; i < blockingParts.Count; i++)
        {
            BodyPartRecord other = blockingParts[i];
            if (other == part || (part is not null && (other == part.parent || other.parent == part || other.parent == part.parent)))
            {
                return true;
            }
        }
        return false;
    }

    private static IReadOnlyList<BodyPartRecord> CollectBlockingExternalParts(Pawn pawn)
    {
        List<BodyPartRecord>? blockingParts = null;
        foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
        {
            if (hediff is IStatefulInjury injury and HediffWithComps { Part: { depth: BodyPartDepth.Outside } part, def.injuryProps.bleedRate: > 0 }
                && hediff.TendableNow()
                && injury.State.CoagulationFlags.IsEmpty
                && !hediff.IsTended())
            {
                blockingParts ??= [];
                blockingParts.Add(part);
            }
        }
        return (IReadOnlyList<BodyPartRecord>?)blockingParts ?? Array.Empty<BodyPartRecord>();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        _state.ExposeData();
    }

    public void AddCustomLabelAnnotations(StringBuilder builder, ref bool hasPreviousInfo)
    {
        if (IsClosedInternalWound)
        {
            builder.AppendEnumerationItem("MI_InjuryEnclosed_Label".Translate(), ref hasPreviousInfo);
        }
    }

    public void AddCustomTipStringAnnotations(StringBuilder builder, ref bool hasCustomInfo)
    {
        if (IsClosedInternalWound)
        {
            double bleedRatePercentage = Math.Round((1f - MoreInjuriesMod.Settings.ClosedInternalWouldBleedingModifier) * 100f, 2);
            builder.AppendLine("MI_InjuryEnclosed_Tooltip".Translate(bleedRatePercentage.Named(Named.Params.PERCENT)));
            hasCustomInfo = true;
        }
    }
}
