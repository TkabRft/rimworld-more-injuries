using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.Extensions;
using MoreInjuries.HealthConditions.BrainDamage;
using MoreInjuries.HealthConditions.HeavyBleeding;
using MoreInjuries.HealthConditions.HypovolemicShock;
using RimWorld;
using System;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

public sealed partial class VitalFailureComp
{
    private static readonly AccessTools.FieldRef<HediffComp_TendDuration, int> s_tendTicks
        = AccessTools.FieldRefAccess<HediffComp_TendDuration, int>("tendTicksLeft");
    private PhysiologySnapshot _prognosisSource;
    private PhysiologyEstimate _prognosis;
    private bool _hasPrognosis;
    private int _prognosisTick = -1;
    private int _prognosisSettings;
    internal string PrognosisTooltip { get; private set; } = string.Empty;

    partial void InvalidatePrognosis() => _hasPrognosis = false;

    internal bool TryGetPrognosis(out PhysiologySnapshot source, out PhysiologyEstimate estimate)
    {
        int now = Find.TickManager.TicksGame;
        int settings = (MoreInjuriesMod.Settings.EnableDelayedVitalFailure ? 1 : 0)
            | (MoreInjuriesMod.Settings.EnableHypovolemicShock ? 2 : 0)
            | (MoreInjuriesMod.Settings.PreventDeathByBloodLoss ? 4 : 0)
            | (MoreInjuriesMod.Settings.AnomalyEnableConditionsForShamblers ? 8 : 0);
        if (!_hasPrognosis || now < _prognosisTick || now - _prognosisTick >= VitalFailureModel.INTERVAL
            || _prognosis.Ticks != int.MaxValue && now - _prognosisTick >= _prognosis.Ticks
            || settings != _prognosisSettings)
        {
            PhysiologySnapshot current = CapturePrognosis();
            _prognosisSource = current;
            _prognosis = PhysiologyPrognosis.Estimate(in current, VitalFailureDefOf.MI_VitalFailureConfig,
                HediffDefOf.BloodLoss, KnownHediffDefOf.BrainDamage_Hypoxia);
            PrognosisTooltip = PrognosisRescueHints.Tooltip(in current, in _prognosis);
            _prognosisTick = now;
            _prognosisSettings = settings;
            _hasPrognosis = true;
        }
        source = _prognosisSource;
        estimate = new(_prognosis.Ticks == int.MaxValue ? int.MaxValue : Math.Max(0, _prognosis.Ticks - (now - _prognosisTick)), _prognosis.Cause);
        return source.Relevant;
    }

    // No Refresh(): that method reconciles failures and may add/remove hediffs.
    private PhysiologySnapshot CapturePrognosis()
    {
        Pawn pawn = Patient;
        PhysiologySnapshot result = new()
        {
            Relevant = false,
            Protected = pawn.health.hediffSet.HasPreventsDeath,
            VitalEnabled = EnabledFor(pawn),
            OxygenProtected = pawn.HasOxygenDeficiencyImmunity(),
            ColdProtection = 1f,
            Breathing = 1f, Pumping = 1f, ComaMultiplier = 1f,
            Failure = new(int.MaxValue, null),
            NextBloodTick = NextGate(pawn, 60), NextShockTick = NextGate(pawn, ShockPerfusionModel.CYCLE_LENGTH),
            NextVitalTick = NextGate(pawn, VitalFailureModel.INTERVAL)
        };
        if (!CanUpdate(pawn)) { return result; }
        bool deathresting = ModsConfig.BiotechActive && pawn.health.hediffSet.HasHediff(HediffDefOf.Deathrest), arrest = false;
        result.OxygenProtected |= deathresting;
        float hypothermia = 0f;
        foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
        {
            if (hediff.def == HediffDefOf.BloodLoss) { result.BloodLoss = hediff.Severity; }
            else if (hediff.def == KnownHediffDefOf.HypovolemicShock)
            {
                result.ShockPresent = true; result.Shock = hediff.Severity;
                result.TendQuality = ShockTreatment.Quality(hediff);
                if (hediff.TryGetComp<HediffComp_TendDuration>() is { IsTended: true } tend) { result.TendTicks = s_tendTicks(tend); }
            }
            else if (hediff.def == KnownHediffDefOf.BrainDamage_Hypoxia) { result.ClinicalBrain = CerebralInjury.ClinicalSeverity(hediff); }
            else if (hediff.def == KnownHediffDefOf.Hemodilution) { result.Dilution = hediff.Severity; }
            else if (hediff.def == HediffDefOf.Hypothermia) { hypothermia = hediff.Severity; }
            else if (hediff.def == KnownHediffDefOf.AdrenalineRush)
            {
                result.Adrenaline = hediff.Severity;
                result.AdrenalineDecayPerDay = Math.Max(0f, -(hediff.TryGetComp<HediffComp_SeverityPerDay>()?.SeverityChangePerDay() ?? 0f));
            }
            else if (hediff.def == NeurologicalDefOf.MI_ArtificialComa)
            {
                result.ComaMultiplier = NeurologicalDefOf.MI_NeurologicalConfig.ComaHypoxiaMultiplier;
                AddPrognosisFailure(ref result, hediff, hediff.TryGetComp<HediffComp_SeverityPerDay>()?.SeverityChangePerDay() ?? 0f);
            }
            else if (hediff.def == KnownHediffDefOf.ChokingOnBlood) { result.Choking = true; }
            else if (hediff.def == KnownHediffDefOf.LungCollapse)
            {
                result.CollapsedLung = true;
                result.LungNeedsSurgery |= hediff.Severity >= .45f;
            }
            if (!result.VitalEnabled) { continue; }
            if (hediff.def == KnownHediffDefOf.CardiacArrest)
            {
                arrest = true;
                result.Arrest = true;
                result.EarlyArrest |= hediff.CurStageIndex == 0;
                if (!result.OxygenProtected) { AddPrognosisFailure(ref result, hediff,
                    ((HediffCompProperties_SeverityPerDay)hediff.TryGetComp<HediffComp_VitalArrest>()!.props).severityPerDay, 1f); }
            }
            else if (IsTerminalHeartAttack(hediff)) { arrest = true; result.Arrest = true; result.EarlyArrest = true; }
            else if (hediff.def.GetModExtension<VitalFailureProperties>() is { } failure
                && (hediff.def != VitalFailureDefOf.MI_RespiratoryFailure && hediff.def != VitalFailureDefOf.MI_Coma || !result.OxygenProtected))
            { AddPrognosisFailure(ref result, hediff, failure.SeverityPerDay); }
        }
        result.BleedRate = pawn.health.hediffSet.BleedRateTotal;
        result.CanDevelopShock = !result.OxygenProtected && TraumaEligibility.Supports(pawn) && MoreInjuriesMod.Settings.EnableHypovolemicShock;
        result.DirectBloodDeath = !result.OxygenProtected && BloodLossPolicy.AllowDirectDeath(TraumaEligibility.Supports(pawn), MoreInjuriesMod.Settings.PreventDeathByBloodLoss);
        result.CanHaveBrain = VitalAnatomy.For(pawn.RaceProps.body).SurvivingBrain(pawn.health.hediffSet) is not null;
        if (result.VitalEnabled)
        {
            result.Breathing = VitalPhysiology.BreathesAir(pawn) ? Capacity(pawn, PawnCapacityDefOf.Breathing) : 1f;
            result.Pumping = arrest ? 0f : Capacity(pawn, PawnCapacityDefOf.BloodPumping);
            if (arrest) { result.RestartableHeart = CanRestoreCirculation(); }
        }
        if (result.Failure.Cause == NeurologicalDefOf.MI_ArtificialComa)
        {
            result.CanWakeUp = VitalPhysiology.CanReceiveCare(pawn) && Recipe_InduceArtificialComa.CanReverse(pawn);
        }
        result.ColdProtection = VitalFailureDefOf.MI_VitalFailureConfig.HypothermiaProtectionCurve.Evaluate(hypothermia);
        result.Relevant = result.BleedRate > 0.01f || result.BloodLoss > 0f || result.ShockPresent || result.ClinicalBrain > 0f
            || result.Failure.Cause is not null || result.VitalEnabled && (result.Pumping < .5f || result.Breathing < .5f || result.Dilution > .45f);
        return result;
    }

    private static int NextGate(Pawn pawn, int interval)
    {
        int phase = ((pawn.HashOffsetTicks() % interval) + interval) % interval;
        return phase == 0 ? interval : interval - phase;
    }

    private static void AddPrognosisFailure(ref PhysiologySnapshot result, Hediff condition, float rate, float terminal = -1f)
    {
        int ticks = PhysiologyPrognosis.UntilTerminal(condition.Severity, terminal > 0f ? terminal : condition.def.lethalSeverity, rate);
        if (ticks < result.Failure.Ticks) { result.Failure = new(ticks, condition.def); }
    }

}
