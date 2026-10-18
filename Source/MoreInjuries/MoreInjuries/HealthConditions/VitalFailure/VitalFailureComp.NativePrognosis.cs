using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.Extensions;
using MoreInjuries.HealthConditions.BrainDamage;
using MoreInjuries.HealthConditions.Choking;
using MoreInjuries.HealthConditions.Secondary;
using RimWorld;
using System;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

public sealed partial class VitalFailureComp
{
    private static readonly AccessTools.FieldRef<HediffComp_Immunizable, float> s_diseaseFactor
        = AccessTools.FieldRefAccess<HediffComp_Immunizable, float>("severityPerDayNotImmuneRandomFactor");
    private PrognosisTrend[] _nativeTrends = new PrognosisTrend[16];
    private int _nativeTrendCount;
    private bool _indefiniteProtection;
    private PrognosisCapacityTerm[] _nativeCapacityTerms = new PrognosisCapacityTerm[32];
    private int _nativeCapacityTermCount;
    private readonly float[] _rawForecastCapacities = new float[5], _minimumForecastCapacities = new float[5];
    private readonly float[] _nativeCapacityThresholds = new float[5];
    private readonly string[] _nativeCapacityLabels = new string[5];

    partial void BeginNativePrognosis()
    {
        _nativeTrendCount = 0;
        _indefiniteProtection = false;
        _nativeCapacityTermCount = 0;
    }

    private void AddNativeTrend(ref PhysiologySnapshot result, PrognosisTrend trend)
    {
        if (_nativeTrendCount == _nativeTrends.Length) { Array.Resize(ref _nativeTrends, _nativeTrends.Length * 2); }
        _nativeTrends[_nativeTrendCount++] = trend;
        result.Relevant |= trend.Terminal > 0f && (trend.Rate > 0f || trend.Severity >= trend.Terminal);
    }

    partial void CaptureNativeCondition(ref PhysiologySnapshot result, Hediff condition)
    {
        if (condition.def == NeurologicalDefOf.MI_ArtificialComa && Patient.health.hediffSet.HasHediff(NeurologicalDefOf.WakeUpHigh)) { return; }
        // These conditions have dedicated adapters; never count their native components again.
        bool dedicated = condition.def == HediffDefOf.BloodLoss || condition.def == KnownHediffDefOf.BrainDamage_Hypoxia
            || condition.def == KnownHediffDefOf.HypovolemicShock || condition.def.GetModExtension<VitalFailureProperties>() is not null
            || condition.def == KnownHediffDefOf.CardiacArrest && result.VitalEnabled;
        if (condition.def.preventsDeath)
        {
            HediffComp_Disappears? disappears = condition.TryGetComp<HediffComp_Disappears>();
            int remaining = disappears?.GetType() == typeof(HediffComp_Disappears) ? Math.Max(1, disappears.ticksToDisappear) : int.MaxValue;
            float rate = KnownDailyRate(condition);
            if (rate < 0f)
            { remaining = Math.Min(remaining, (int)Math.Min(int.MaxValue, Math.Max(1d, Math.Ceiling(condition.Severity / -rate * GenDate.TicksPerDay)))); }
            if (remaining == int.MaxValue) { _indefiniteProtection = true; }
            else { result.ProtectionTicks = Math.Max(result.ProtectionTicks, remaining); }
        }
        if (condition.CurStage?.deathMtbDays > 0f || condition is Hediff_HeartAttack && !IsTerminalHeartAttack(condition))
        { result.UnmodeledRisk = true; result.Relevant = true; }
        if (condition.TryGetComp<HediffComp_SecondaryCondition>() is not null) { result.UnmodeledRisk = true; }
        if (dedicated)
        {
            CaptureCapacityTerms(condition.def, -1, condition.def == KnownHediffDefOf.BrainDamage_Hypoxia
                ? CerebralInjury.ClinicalSeverity(condition) : condition.Severity, condition.CurStage);
            return;
        }
        PrognosisTrend trend = new()
        {
            Cause = condition.def, Severity = condition.Severity, Maximum = condition.def.maxSeverity,
            Terminal = condition.IsLethal ? condition.def.lethalSeverity : -1f,
            ImmunityTicks = -1, Interval = 200, NextTick = NextGate(Patient, 200)
        };
        bool known = condition.GetType() == typeof(HediffWithComps) || condition.GetType() == typeof(Hediff);
        bool unsupportedProgression = false;
        if (condition is HediffWithComps withComps && withComps.comps is { } comps)
        {
            foreach (HediffComp comp in comps)
            {
                Type type = comp.GetType();
                if (type == typeof(HediffComp_SeverityPerDay) || type == typeof(HediffComp_SeverityPerDay_WithModifiers)
                    || type == typeof(HediffComp_VitalArrest) && !result.VitalEnabled)
                {
                    HediffComp_SeverityPerDay daily = (HediffComp_SeverityPerDay)comp;
                    float rate = daily.SeverityChangePerDay(), stageFactor = condition.CurStage?.severityGainFactor ?? 1f;
                    trend.StageRate += stageFactor != 0f ? rate / stageFactor : 0f;
                    if (stageFactor == 0f) { result.UnmodeledRisk = true; }
                    known = true;
                }
                else if (type == typeof(HediffComp_TendDuration))
                {
                    HediffComp_TendDuration tend = (HediffComp_TendDuration)comp;
                    trend.TendTicks = tend.IsTended ? (tend.TProps.TendIsPermanent ? int.MaxValue : s_tendTicks(tend)) : 0;
                    trend.TendedRate = tend.IsTended ? tend.SeverityChangePerDay() : 0f;
                }
                else if (type == typeof(HediffComp_Immunizable) || type == typeof(HediffComp_ImmunizableToxic))
                {
                    HediffComp_Immunizable immune = (HediffComp_Immunizable)comp;
                    float factor = 1f;
                    if (immune.Props.severityFactorsFromHediffs is { } factors)
                    {
                        foreach (HediffDefFactor modifier in factors)
                        { if (Patient.health.hediffSet.HasHediff(modifier.HediffDef)) { factor *= modifier.Factor; } }
                    }
                    trend.Rate += immune.SeverityChangePerDay();
                    trend.ImmuneRate += immune.Props.severityPerDayImmune * factor;
                    if (immune.FullyImmune) { trend.ImmunityTicks = 0; }
                    else
                    {
                        float gain = Patient.health.immunity.GetImmunityRecord(condition.def)?.ImmunityChangePerTick(Patient, true, condition) ?? 0f;
                        trend.ImmunityTicks = gain > 0f ? (int)Math.Min(int.MaxValue, Math.Ceiling((1d - immune.Immunity) / gain)) : -1;
                        // Keep the saved per-disease roll, rather than drawing another factor.
                        trend.Rate = immune.Props.severityPerDayNotImmune * s_diseaseFactor(immune) * factor
                            + (trend.Rate - immune.SeverityChangePerDay());
                    }
                    known = true;
                }
                else if (comp is HediffComp_SeverityModifierBase) { result.UnmodeledRisk = true; unsupportedProgression = true; }
            }
        }
        string name = condition.def.defName;
        if (name is "Heatstroke" or "Hypothermia" or "ToxicBuildup" or "VacuumExposure" or "Malnutrition") { return; }
        if (condition.TryGetComp<HediffComp_Choking>() is { } choking)
        {
            Hediff_Injury? source = choking.GetSource(validate: false);
            float increase = .1f, decrease = 0f;
            if (source is { BleedRate: > .01f }) { increase += Math.Max(.05f, Math.Min(.25f, source.BleedRate / 5f)); }
            else { decrease = source is null || source.IsTended() ? .075f : .025f; }
            float negative = -decrease * decrease / (2f * (increase + decrease));
            float positive = increase * increase / (2f * (increase + decrease));
            if (condition.def.GetModExtension<HediffModifier_SeverityModifiers_ModExtension>() is { } modifiers)
            { negative = modifiers.ApplyTo(negative, condition, choking); positive = modifiers.ApplyTo(positive, condition, choking); }
            bool coughs = source is not { Bleeding: true } && (Capacity(Patient, PawnCapacityDefOf.Consciousness) > .3f
                || ModsConfig.BiotechActive && Patient.health.hediffSet.HasHediff(HediffDefOf.Deathrest));
            trend.Rate += (positive + negative - (coughs ? .1f : 0f)) * GenDate.TicksPerDay / choking.Properties.ChokingIntervalTicks;
            result.UnmodeledRisk = true; result.Relevant = true;
        }
        if (!known && condition.IsCurrentlyLifeThreatening) { result.UnmodeledRisk = true; result.Relevant = true; }
        int trendIndex = -1;
        if (known && !unsupportedProgression && (trend.Terminal > 0f || trend.Rate != 0f || trend.StageRate != 0f || trend.TendedRate != 0f))
        { trendIndex = _nativeTrendCount; AddNativeTrend(ref result, trend); }
        CaptureCapacityTerms(condition.def, trendIndex, condition.Severity, condition.CurStage);
    }

    partial void FinishNativePrognosis(ref PhysiologySnapshot result)
    {
        if (_indefiniteProtection) { result.ProtectionTicks = 0; }
        CaptureEnvironment(ref result);
        float totalInjuries = 0f;
        float naturalFactor = 1f;
        int naturalCount = 0, tendedCount = 0;
        Hediff? selected = null;
        foreach (Hediff condition in Patient.health.hediffSet.hediffs)
        {
            if (condition.CurStage is { naturalHealingFactor: >= 0f } stage) { naturalFactor *= stage.naturalHealingFactor; }
            if (condition.CurStage?.regeneration > 0f) { result.UnmodeledRisk = true; }
            if (condition is Hediff_Injury injury)
            {
                totalInjuries += condition.Severity;
                if (injury.CanHealNaturally()) { naturalCount++; }
                if (injury.CanHealFromTending()) { tendedCount++; }
            }
            if (condition.def == KnownHediffDefOf.BrainDamage_Hypoxia)
            {
                selected = condition;
                if (CerebralInjury.ClinicalSeverity(condition) >= VitalFailureDefOf.MI_VitalFailureConfig.TerminalCerebralSeverity
                    && result.VitalEnabled && !result.OxygenProtected && !result.Protected) { result.Failure = new(0, condition.def); }
            }
        }
        BodyPartRecord? brain = selected?.Part ?? VitalAnatomy.For(Patient.RaceProps.body).SurvivingBrain(Patient.health.hediffSet);
        float scale = CerebralInjury.Scale(Patient, brain);
        if (Patient.HasOxygenDeficiencyImmunity() && Patient.health.hediffSet.GetBrain() is { } nativeBrain
            && !Patient.health.hediffSet.PartIsMissing(nativeBrain) && Patient.health.hediffSet.GetPartHealth(nativeBrain) > 0f)
        { result.NativeComaProtection = true; }
        if (selected is Hediff_Injury brainInjury && Patient.needs?.food?.Starving is not true)
        {
            float healing = Patient.HealthScale * Patient.GetStatValue(StatDefOf.InjuryHealingFactor) / scale;
            if (brainInjury.CanHealNaturally() && naturalCount > 0)
            {
                float baseRate = 8f;
                if (Patient.GetPosture() != PawnPosture.Standing) { baseRate += 4f + (Patient.CurrentBed()?.def.building.bed_healPerDay ?? 0f); }
                result.BrainNaturalRecoveryPerDay = baseRate * naturalFactor * healing / naturalCount;
            }
            if (brainInjury.CanHealFromTending() && tendedCount > 0 && brainInjury.TryGetComp<HediffComp_TendDuration>() is { } tend)
            {
                result.BrainTendedRecoveryPerDay = 8f * (.5f + UnityEngine.Mathf.Clamp01(tend.tendQuality)) * healing / tendedCount;
                result.BrainTendTicks = tend.TProps.TendIsPermanent ? int.MaxValue : s_tendTicks(tend);
            }
            result.NextHealingTick = NextGate(Patient, 600);
        }
        float remaining = Math.Max(0f, Patient.health.LethalDamageThreshold - totalInjuries) / scale;
        if (brain is not null)
        {
            int survivors = 0;
            foreach (BodyPartRecord part in VitalAnatomy.For(Patient.RaceProps.body).Brains)
            { if (!Patient.health.hediffSet.PartIsMissing(part) && Patient.health.hediffSet.GetPartHealth(part) > 0f) { survivors++; } }
            if (survivors == 1) { remaining = Math.Min(remaining, Patient.health.hediffSet.GetPartHealth(brain) / scale); }
        }
        result.CerebralStructuralLimit = Math.Max(.0001f, result.ClinicalBrain + remaining);
        result.Trends = _nativeTrends;
        result.TrendCount = _nativeTrendCount;
        for (int i = 0; i < 5; i++)
        {
            PawnCapacityDef def = ForecastCapacityDef(i);
            bool applicable = def.Worker.CanHaveCapacity(Patient.RaceProps.body);
            if (applicable) { result.ApplicableCapacities |= 1 << i; }
            bool previous = ReadingUnderlying;
            ReadingUnderlying = true;
            try { _rawForecastCapacities[i] = applicable ? def.Worker.CalculateCapacityLevel(Patient.health.hediffSet) : 1f; }
            finally { ReadingUnderlying = previous; }
            _minimumForecastCapacities[i] = def.minValue;
            _nativeCapacityThresholds[i] = def.minForCapable;
            _nativeCapacityLabels[i] = def.LabelCap;
            if (def.lethalFlesh && (!result.VitalEnabled || VitalPhysiology.HasNativeCapacityDeath(Patient)
                || VitalAnatomy.For(Patient.RaceProps.body).BrainsDestroyed(Patient.health.hediffSet)))
            { result.NativeDeathCapacityMask |= 1 << i; }
        }
        if (!VitalPhysiology.BreathesAir(Patient)) { result.ApplicableCapacities &= ~1; }
        if (ModsConfig.BiotechActive && Patient.genes is { } genes)
        {
            foreach (Gene gene in genes.GenesListForReading)
            {
                if (!gene.Active || gene.def.capMods is not { } modifiers) { continue; }
                foreach (PawnCapacityModifier modifier in modifiers) { CaptureCapacityModifier(modifier, -1, 1f, 0f, float.MaxValue, null); }
            }
        }
        result.RawCapacities = _rawForecastCapacities; result.MinimumCapacities = _minimumForecastCapacities;
        result.CapacityTerms = _nativeCapacityTerms; result.CapacityTermCount = _nativeCapacityTermCount;
        result.NativeCapacityThresholds = _nativeCapacityThresholds;
        result.CapacityLabels = _nativeCapacityLabels;
        foreach (PawnCapacityDef capacity in DefDatabase<PawnCapacityDef>.AllDefsListForReading)
        {
            if (capacity.lethalFlesh && !MayDelay(capacity) && capacity.Worker.CanHaveCapacity(Patient.RaceProps.body)
                && Capacity(Patient, capacity) <= capacity.minForCapable)
            {
                if (!result.Protected) { result.Failure = new(0, null, capacity.LabelCap); }
                result.UnmodeledRisk = true; result.Relevant = true; break;
            }
        }
    }

    private void CaptureEnvironment(ref PhysiologySnapshot result)
    {
        Pawn pawn = Patient;
        // Holder/world patients never query terrain, pressure or map positions.
        if (pawn.Spawned)
        {
            FloatRange safe = pawn.SafeTemperatureRange(), comfortable = pawn.ComfortableTemperatureRange();
            float ambient = pawn.AmbientTemperature;
            CaptureEnvironmentalTrend(ref result, HediffDefOf.Heatstroke,
                ambient > safe.max ? NativeExposureModel.Heat(HediffGiver_Heat.TemperatureOverageAdjustmentCurve.Evaluate(ambient - safe.max)) : 0f,
                ambient < comfortable.max);
            CaptureEnvironmentalTrend(ref result, HediffDefOf.Hypothermia,
                ambient < safe.min ? NativeExposureModel.Cold(safe.min - ambient) : 0f,
                ambient > comfortable.min || pawn.Position.GetTerrain(pawn.Map).heatPerTick > 0f);
            float toxicity = pawn.Position.GetTerrain(pawn.Map).toxicBuildupFactor;
            if (ModsConfig.BiotechActive && pawn.Position.IsPolluted(pawn.Map)) { toxicity += 1f; }
            if (!pawn.Position.Roofed(pawn.Map))
            {
                foreach (GameCondition condition in pawn.Map.GameConditionManager.ActiveConditions)
                { if (condition is GameCondition_ToxicFallout) { toxicity += 1f; } }
            }
            float toxicGain = NativeExposureModel.Toxic(toxicity, pawn.GetStatValue(StatDefOf.ToxicResistance), pawn.GetStatValue(StatDefOf.ToxicEnvironmentResistance));
            CaptureEnvironmentalTrend(ref result, HediffDefOf.ToxicBuildup, toxicGain, false, 3451);
            if (ModsConfig.OdysseyActive)
            {
                float pressure = pawn.Position.GetVacuum(pawn.Map);
                float vacuumGain = pawn.Map.Biome.inVacuum && pawn.HarmedByVacuum && pressure >= .5f
                    ? NativeExposureModel.Vacuum(pressure, pawn.GetStatValue(StatDefOf.VacuumResistance)) : 0f;
                CaptureEnvironmentalTrend(ref result, HediffDefOf.VacuumExposure, vacuumGain, false, 60,
                    vacuumRecoveryBlocked: pressure >= .5f && pawn.GetStatValue(StatDefOf.VacuumResistance) < 1f);
            }
        }
        else
        {
            foreach (Hediff condition in pawn.health.hediffSet.hediffs)
            {
                if (condition.def.defName is "Heatstroke" or "Hypothermia" or "ToxicBuildup" or "VacuumExposure")
                { result.UnmodeledRisk = true; result.Relevant = true; }
            }
        }
        if (pawn.needs?.food is { } food && (food.Starving || pawn.health.hediffSet.HasHediff(HediffDefOf.Malnutrition)))
        {
            Hediff? malnutrition = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            int index = _nativeTrendCount;
            AddNativeTrend(ref result, new()
            {
                Cause = HediffDefOf.Malnutrition, Severity = malnutrition?.Severity ?? 0f,
                Maximum = HediffDefOf.Malnutrition.maxSeverity, Terminal = HediffDefOf.Malnutrition.lethalSeverity,
                Rate = NativeExposureModel.STARVATION_PER_DAY * (food.Starving ? 1f : -1f), ImmunityTicks = -1,
                Interval = 150, NextTick = NextGate(pawn, 150)
            });
            CaptureCapacityTerms(HediffDefOf.Malnutrition, index, malnutrition?.Severity ?? 0f, malnutrition?.CurStage);
        }
    }

    private void CaptureEnvironmentalTrend(ref PhysiologySnapshot result, HediffDef def, float gain, bool recovers, int interval = 60,
        bool vacuumRecoveryBlocked = false)
    {
        Hediff? condition = Patient.health.hediffSet.GetFirstHediffOfDef(def);
        if (condition is null && gain <= 0f) { return; }
        float rate = gain;
        if (!recovers && condition is not null)
        {
            if (condition.TryGetComp<HediffComp_ImmunizableToxic>() is { } toxic) { rate += toxic.SeverityChangePerDay(); }
            if (condition.TryGetComp<HediffComp_VacuumExposure>() is { } vacuum && gain <= 0f && !vacuumRecoveryBlocked)
            { rate += vacuum.Props.severityPerSecondUnexposed * 1000f; }
        }
        int index = _nativeTrendCount;
        AddNativeTrend(ref result, new()
        {
            Cause = def, Severity = condition?.Severity ?? 0f, Maximum = def.maxSeverity, Terminal = condition?.IsLethal == false ? -1f : def.lethalSeverity,
            Rate = rate, ImmunityTicks = -1, Interval = interval, NextTick = NextGate(Patient, interval), TemperatureRecovery = recovers && gain <= 0f
        });
        CaptureCapacityTerms(def, index, condition?.Severity ?? 0f, condition?.CurStage);
    }

    private static PawnCapacityDef ForecastCapacityDef(int index) => index switch
    {
        0 => PawnCapacityDefOf.Breathing, 1 => PawnCapacityDefOf.BloodPumping, 2 => PawnCapacityDefOf.Consciousness,
        3 => PawnCapacityDefOf.BloodFiltration, _ => VitalFailureDefOf.Metabolism
    };

    private void CaptureCapacityTerms(HediffDef def, int trend, float severity, HediffStage? current)
    {
        if (def.stages is not { } stages) { return; }
        for (int i = 0; i < stages.Count; i++)
        {
            HediffStage stage = stages[i];
            if (trend < 0 && stage != current || stage.capMods is not { } modifiers) { continue; }
            float next = i + 1 < stages.Count ? stages[i + 1].minSeverity : float.MaxValue;
            foreach (PawnCapacityModifier modifier in modifiers)
            { CaptureCapacityModifier(modifier, trend, severity, stage.minSeverity, next, stage); }
        }
    }

    private void CaptureCapacityModifier(PawnCapacityModifier modifier, int trend, float severity, float minimum, float next, HediffStage? stage)
    {
        for (int index = 0; index < 5; index++)
        {
            if (modifier.capacity != ForecastCapacityDef(index)) { continue; }
            if (_nativeCapacityTermCount == _nativeCapacityTerms.Length) { Array.Resize(ref _nativeCapacityTerms, _nativeCapacityTerms.Length * 2); }
            float factor = modifier.postFactor;
            if (stage?.capacityFactorEffectMultiplier is { } stat) { factor = StatWorker.ScaleFactor(factor, Patient.GetStatValue(stat)); }
            _nativeCapacityTerms[_nativeCapacityTermCount++] = new()
            {
                Capacity = index, Trend = trend, Severity = severity, MinimumSeverity = minimum, NextSeverity = next,
                Offset = modifier.offset, Factor = factor, Maximum = modifier.EvaluateSetMax(Patient)
            };
            break;
        }
    }
}
