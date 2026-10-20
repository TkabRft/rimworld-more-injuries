using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.Extensions;
using MoreInjuries.Integrations.CombatExtended;
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
    private PhysiologyEstimate _currentControlPrognosis;
    private bool _hasPrognosis;
    private int _prognosisTick = -1;
    private int _prognosisSettings;
    private readonly Hediff?[] _prognosisFailures = new Hediff?[6];
    private readonly float[] _prognosisFailureRates = new float[7];
    private readonly HediffDef[] _prognosisFailureDefs = new HediffDef[7];
    private CombatExtendedBleedingCapture? _combatExtendedBleeding;
    private Func<string>? _prognosisTooltipGetter;
    internal string PrognosisTooltip { get; private set; } = string.Empty;
    internal TipSignal PrognosisTip => new(_prognosisTooltipGetter ??= () => PrognosisTooltip,
        Patient.thingIDNumber ^ 0x4D495052);

    partial void InvalidatePrognosis() => InvalidateForecast();
    internal void InvalidateForecast() => _hasPrognosis = false;
    partial void BeginNativePrognosis();
    partial void CaptureNativeCondition(ref PhysiologySnapshot result, Hediff condition);
    partial void FinishNativePrognosis(ref PhysiologySnapshot result);

    internal bool TryGetPrognosis(out PhysiologySnapshot source, out PhysiologyEstimate estimate)
    {
        int now = Find.TickManager.TicksGame;
        int settings = (MoreInjuriesMod.Settings.EnableDelayedVitalFailure ? 1 : 0)
            | (MoreInjuriesMod.Settings.EnableHypovolemicShock ? 2 : 0)
            | (MoreInjuriesMod.Settings.PreventDeathByBloodLoss ? 4 : 0)
            | (MoreInjuriesMod.Settings.AnomalyEnableConditionsForShamblers ? 8 : 0)
            | (MoreInjuriesMod.Settings.PreventDeathByLethalDamageThreshold ? 16 : 0);
        if (!_hasPrognosis || now < _prognosisTick || now - _prognosisTick >= VitalFailureModel.INTERVAL
            || _prognosis.Ticks != int.MaxValue && now - _prognosisTick >= _prognosis.Ticks
            || _currentControlPrognosis.Ticks != int.MaxValue && now - _prognosisTick >= _currentControlPrognosis.Ticks
            || settings != _prognosisSettings)
        {
            PhysiologySnapshot current = CapturePrognosis();
            _prognosisSource = current;
            _prognosis = current.Relevant ? PhysiologyPrognosis.Estimate(in current, VitalFailureDefOf.MI_VitalFailureConfig,
                HediffDefOf.BloodLoss, KnownHediffDefOf.BrainDamage_Hypoxia) : new(int.MaxValue, null);
            current.HoldBleedingControl = true;
            _currentControlPrognosis = current.TemporaryBleedingRisk ? PhysiologyPrognosis.Estimate(in current, VitalFailureDefOf.MI_VitalFailureConfig,
                HediffDefOf.BloodLoss, KnownHediffDefOf.BrainDamage_Hypoxia) : _prognosis;
            string scenarios = current.TemporaryBleedingRisk
                ? "MI_Prognosis_Scenarios".Translate(FullStatus(in current, in _currentControlPrognosis, false),
                    FullStatus(in current, in _prognosis, current.BleedingUncertain)).ToString()
                : "MI_Prognosis_DeathShort".Translate(FullStatus(in current, in _prognosis, current.BleedingUncertain)).ToString();
            PrognosisTooltip = (current.BleedRate > 0f
                ? "MI_Prognosis_Footer".Translate(current.BleedRate.ToStringPercent(), scenarios).ToString() : scenarios) + "\n\n";
            if (current.TemporaryBleedingRisk)
            {
                PrognosisTooltip += "MI_Prognosis_CurrentScenario".Translate() + "\n"
                    + PrognosisRescueHints.Tooltip(in current, in _currentControlPrognosis) + "\n\n"
                    + "MI_Prognosis_UntendedScenario".Translate() + "\n"
                    + PrognosisRescueHints.Tooltip(in _prognosisSource, in _prognosis);
            }
            else { PrognosisTooltip += PrognosisRescueHints.Tooltip(in _prognosisSource, in _prognosis); }
            if (current.TemporaryBleedingRisk) { PrognosisTooltip += "\n" + "MI_Prognosis_TemporaryControl".Translate(); }
            if (current.BleedingUncertain) { PrognosisTooltip += "\n" + "MI_Prognosis_Uncertain".Translate(); }
            if (current.UnmodeledRisk) { PrognosisTooltip += "\n" + "MI_Prognosis_OtherRisks".Translate(); }
            if (current.CeStabilization) { PrognosisTooltip += "\n" + "MI_Prognosis_CEStabilization".Translate(); }
            if (_prognosis.Ticks != int.MaxValue && current.TrendCount > 0)
            { PrognosisTooltip += "\n" + "MI_Prognosis_CurrentConditions".Translate(); }
            _prognosisTick = now;
            _prognosisSettings = settings;
            _hasPrognosis = true;
        }
        source = _prognosisSource;
        estimate = new(_prognosis.Ticks == int.MaxValue ? int.MaxValue : Math.Max(0, _prognosis.Ticks - (now - _prognosisTick)), _prognosis.Cause, _prognosis.CapacityLabel);
        return source.Relevant;
    }

    private static string FullStatus(in PhysiologySnapshot source, in PhysiologyEstimate estimate, bool uncertain)
        => source.NativeComaProtection || source.Protected && source.ProtectionTicks <= 0 ? "MI_Prognosis_Protected".Translate()
            : uncertain && estimate.Ticks == int.MaxValue ? "MI_Prognosis_Uncertain".Translate()
            : estimate.Ticks == int.MaxValue ? "MI_Prognosis_NoPrediction".Translate()
            : "MI_Prognosis_Time".Translate(GenDate.ToStringTicksToPeriod(estimate.Ticks));

    internal bool TryGetPrognoses(out PhysiologySnapshot source, out PhysiologyEstimate held, out PhysiologyEstimate untended)
    {
        bool relevant = TryGetPrognosis(out source, out untended);
        int elapsed = Find.TickManager.TicksGame - _prognosisTick;
        held = new(_currentControlPrognosis.Ticks == int.MaxValue ? int.MaxValue
            : Math.Max(0, _currentControlPrognosis.Ticks - elapsed), _currentControlPrognosis.Cause, _currentControlPrognosis.CapacityLabel);
        return relevant;
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
        VitalAnatomy anatomy = VitalAnatomy.For(pawn.RaceProps.body);
        result.PumpAbsent = !VitalAnatomy.AnyPresent(pawn.health.hediffSet, anatomy.Pumps);
        BeginNativePrognosis();
        Array.Clear(_prognosisFailures, 0, _prognosisFailures.Length);
        bool deathresting = ModsConfig.BiotechActive && pawn.health.hediffSet.HasHediff(HediffDefOf.Deathrest), arrest = false;
        result.OxygenProtected |= deathresting;
        float hypothermia = 0f;
        foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
        {
            if (!PrognosisConditionPolicy.ShouldProject(hediff)) { continue; }
            if (result.PumpAbsent && hediff.def == KnownHediffDefOf.CardiacArrest) { continue; }
            CaptureNativeCondition(ref result, hediff);
            if (hediff.def == HediffDefOf.BloodLoss) { result.BloodLoss = hediff.Severity; }
            else if (hediff.def == KnownHediffDefOf.HypovolemicShock)
            {
                result.ShockPresent = true; result.Shock = hediff.Severity;
                result.TendQuality = ShockTreatment.Quality(hediff);
                if (hediff.TryGetComp<HediffComp_TendDuration>() is { IsTended: true } tend) { result.TendTicks = s_tendTicks(tend); }
            }
            else if (hediff.def == KnownHediffDefOf.BrainDamage_Hypoxia) { result.ClinicalBrain = CerebralInjury.ClinicalSeverity(hediff); }
            else if (hediff.def == KnownHediffDefOf.Hemodilution)
            {
                result.Dilution = hediff.Severity;
                result.DilutionDecayPerDay = Math.Max(0f, -KnownDailyRate(hediff));
            }
            else if (hediff.def == HediffDefOf.Hypothermia) { hypothermia = hediff.Severity; }
            else if (hediff.def == KnownHediffDefOf.AdrenalineRush)
            {
                result.Adrenaline = hediff.Severity;
                result.AdrenalineDecayPerDay = Math.Max(0f, -KnownDailyRate(hediff));
            }
            else if (hediff.def == NeurologicalDefOf.MI_ArtificialComa && !pawn.health.hediffSet.HasHediff(NeurologicalDefOf.WakeUpHigh))
            {
                result.ComaMultiplier = NeurologicalDefOf.MI_NeurologicalConfig.ComaHypoxiaMultiplier;
                AddPrognosisFailure(ref result, hediff, KnownDailyRate(hediff));
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
                result.Failures.Set(6, hediff.Severity);
                result.PersistentArrest |= hediff.TryGetComp<HediffComp_VitalArrest>() is not { MechanicalFailure: true };
            }
            else if (!result.PumpAbsent && IsTerminalHeartAttack(hediff)) { arrest = true; result.Arrest = true; result.EarlyArrest = true; result.PersistentArrest = true; }
            else if (hediff.def.GetModExtension<VitalFailureProperties>() is not null)
            {
                for (int i = 0; i < _prognosisFailures.Length; i++)
                { if (hediff.def == FailureDef(i)) { _prognosisFailures[i] = hediff; break; } }
            }
        }
        result.BleedRate = pawn.health.hediffSet.BleedRateTotal;
        CombatExtendedBridge.Capture(pawn, ref result, ref _combatExtendedBleeding);
        result.CanDevelopShock = !result.OxygenProtected && TraumaEligibility.Supports(pawn) && MoreInjuriesMod.Settings.EnableHypovolemicShock;
        result.DirectBloodDeath = !result.OxygenProtected && BloodLossPolicy.AllowDirectDeath(TraumaEligibility.Supports(pawn), MoreInjuriesMod.Settings.PreventDeathByBloodLoss);
        result.CanHaveBrain = VitalAnatomy.For(pawn.RaceProps.body).SurvivingBrain(pawn.health.hediffSet) is not null;
        if (result.VitalEnabled)
        {
            result.Breathing = VitalPhysiology.BreathesAir(pawn) ? Capacity(pawn, PawnCapacityDefOf.Breathing) : 1f;
            result.Pumping = arrest ? 0f : Capacity(pawn, PawnCapacityDefOf.BloodPumping);
            if (arrest) { result.RestartableHeart = CanRestoreCirculation(); }
            bool liver = VitalAnatomy.AllFailed(pawn.health.hediffSet, anatomy.Livers);
            bool renal = VitalAnatomy.AllFailed(pawn.health.hediffSet, anatomy.Kidneys);
            result.LiverFailed = liver; result.RenalFailed = renal; result.ManageFailures = true;
            for (int i = 0; i < _prognosisFailures.Length; i++)
            {
                HediffDef def = FailureDef(i);
                result.Failures.Set(i, _prognosisFailures[i]?.Severity ?? 0f);
                _prognosisFailureDefs[i] = def;
                _prognosisFailureRates[i] = def.GetModExtension<VitalFailureProperties>().SeverityPerDay;
            }
            _prognosisFailureDefs[6] = KnownHediffDefOf.CardiacArrest;
            _prognosisFailureRates[6] = (KnownHediffDefOf.CardiacArrest.comps?.Find(static props => props is HediffCompProperties_VitalArrest)
                as HediffCompProperties_VitalArrest)?.severityPerDay ?? 8f;
            result.FailureRates = _prognosisFailureRates; result.FailureDefs = _prognosisFailureDefs;
        }
        FinishNativePrognosis(ref result);
        if (result.Failure.Cause == NeurologicalDefOf.MI_ArtificialComa)
        {
            result.CanWakeUp = VitalPhysiology.CanReceiveCare(pawn) && Recipe_InduceArtificialComa.CanReverse(pawn);
        }
        result.ColdProtection = VitalFailureDefOf.MI_VitalFailureConfig.HypothermiaProtectionCurve.Evaluate(hypothermia);
        result.Relevant |= result.BleedRate > 0.01f || result.BloodLoss > 0f || result.ShockPresent || result.ClinicalBrain > 0f
            || result.Failure.Cause is not null || result.LiverFailed || result.RenalFailed
            || result.VitalEnabled && (result.Pumping < .5f || result.Breathing < .5f || result.Dilution > .45f
                || Capacity(pawn, PawnCapacityDefOf.BloodFiltration) <= VitalFailureModel.ZERO_FUNCTION
                || Capacity(pawn, VitalFailureDefOf.Metabolism) <= VitalFailureModel.ZERO_FUNCTION
                || Capacity(pawn, PawnCapacityDefOf.Consciousness) <= VitalFailureModel.ZERO_FUNCTION);
        return result;
    }

    private static int NextGate(Pawn pawn, int interval)
    {
        int phase = ((pawn.HashOffsetTicks() % interval) + interval) % interval;
        return phase == 0 ? interval : interval - phase;
    }

    private static void AddPrognosisFailure(ref PhysiologySnapshot result, Hediff condition, float rate, float terminal = -1f)
    {
        PrognosisTrend trend = new()
        {
            Cause = condition.def, Severity = condition.Severity, Maximum = condition.def.maxSeverity,
            Terminal = terminal > 0f ? terminal : condition.def.lethalSeverity, Rate = rate, ImmunityTicks = -1,
            Interval = 200, NextTick = NextGate(condition.pawn, 200)
        };
        int ticks = trend.UntilTerminal();
        if (ticks < result.Failure.Ticks) { result.Failure = new(ticks, condition.def); }
    }

    private static float KnownDailyRate(Hediff condition)
    {
        HediffComp_SeverityPerDay? comp = condition.TryGetComp<HediffComp_SeverityPerDay>();
        return comp?.GetType() == typeof(HediffComp_SeverityPerDay) ? comp.SeverityChangePerDay() : 0f;
    }

}
