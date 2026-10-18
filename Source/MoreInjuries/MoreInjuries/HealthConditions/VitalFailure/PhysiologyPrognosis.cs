using MoreInjuries.HealthConditions.HypovolemicShock;
using RimWorld;
using System;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

internal readonly struct PhysiologyEstimate(int ticks, HediffDef? cause, string? capacityLabel = null)
{
    internal int Ticks { get; } = ticks;
    internal HediffDef? Cause { get; } = cause;
    internal string? CapacityLabel { get; } = capacityLabel;
}

internal struct PhysiologySnapshot
{
    internal bool Relevant, Protected, VitalEnabled, CanDevelopShock, ShockPresent, CanHaveBrain, OxygenProtected, DirectBloodDeath;
    internal float BloodLoss, BleedRate, Shock, ClinicalBrain, Breathing, Pumping, Dilution, ColdProtection;
    internal float Adrenaline, AdrenalineDecayPerDay, TendQuality, ComaMultiplier;
    internal int TendTicks, NextBloodTick, NextShockTick, NextVitalTick;
    internal PhysiologyEstimate Failure;
    internal PrognosisTrend[]? Trends;
    internal int TrendCount, ProtectionTicks;
    internal float DilutionDecayPerDay, CerebralStructuralLimit;
    internal bool UnmodeledRisk;
    internal float[]? RawCapacities, MinimumCapacities;
    internal PrognosisCapacityTerm[]? CapacityTerms;
    internal int CapacityTermCount, ApplicableCapacities;
    internal bool ManageFailures, LiverFailed, RenalFailed, PersistentArrest;
    internal PrognosisFailures Failures;
    internal float[]? FailureRates;
    internal HediffDef[]? FailureDefs;
    internal bool NativeComaProtection;
    internal float BrainNaturalRecoveryPerDay, BrainTendedRecoveryPerDay;
    internal int BrainTendTicks, NextHealingTick;
    internal int NativeDeathCapacityMask;
    internal float[]? NativeCapacityThresholds;
    internal string[]? CapacityLabels;
    internal bool Arrest, EarlyArrest, RestartableHeart, Choking, CollapsedLung, LungNeedsSurgery, CanWakeUp;
}

// Pure forecast: no Pawn access, RNG, health mutation, or heap allocation.
internal static class PhysiologyPrognosis
{
    internal const int HORIZON = GenDate.TicksPerDay;

    internal static int UntilTerminal(float severity, float terminal, float dailyRate)
    {
        if (dailyRate <= 0f || terminal <= 0f) { return int.MaxValue; }
        double ticks = Math.Max(0d, terminal - severity) / dailyRate * GenDate.TicksPerDay;
        return ticks >= int.MaxValue ? int.MaxValue : (int)Math.Ceiling(ticks);
    }

    internal static PhysiologyEstimate Estimate(in PhysiologySnapshot source, VitalFailureConfigDef config,
        HediffDef bloodLossDef, HediffDef cerebralDef)
    {
        if (source.NativeComaProtection || source.Protected && source.ProtectionTicks <= 0) { return new(int.MaxValue, null); }
        PhysiologyEstimate best = source.Failure;
        if (source.Protected && best.Ticks != int.MaxValue) { best = new(Math.Max(best.Ticks, source.ProtectionTicks), best.Cause, best.CapacityLabel); }
        for (int i = 0; i < source.TrendCount; i++)
        {
            PrognosisTrend trend = source.Trends![i];
            int deadline = trend.UntilTerminal();
            if (source.Protected && deadline < source.ProtectionTicks)
            {
                PrognosisTrend later = trend.After(source.ProtectionTicks);
                int remaining = later.UntilTerminal();
                deadline = remaining == int.MaxValue || (long)remaining + source.ProtectionTicks >= int.MaxValue
                    ? int.MaxValue : remaining + source.ProtectionTicks;
            }
            if (deadline < best.Ticks) { best = new(deadline, trend.Cause); }
        }
        float terminal = source.CerebralStructuralLimit > 0f
            ? Math.Min(config.TerminalCerebralSeverity, source.CerebralStructuralLimit) : config.TerminalCerebralSeverity;
        float blood = source.BloodLoss, shock = source.Shock, brain = source.ClinicalBrain;
        float adrenaline = source.Adrenaline;
        bool shockPresent = source.ShockPresent;
        PrognosisFailures failures = source.Failures;
        int bloodTick = Math.Max(1, source.NextBloodTick), shockTick = Math.Max(1, source.NextShockTick);
        int vitalTick = Math.Max(1, source.NextVitalTick), previous = 0;
        int healingTick = source.NextHealingTick > 0 ? source.NextHealingTick : 600;
        int protectionTick = source.Protected && source.ProtectionTicks > 0 ? source.ProtectionTicks : int.MaxValue;
        int capacityFailure = NativeCapacityDeath(in source, 0);
        if (!source.Protected && capacityFailure >= 0) { return new(0, null, source.CapacityLabels![capacityFailure]); }
        if (!source.Protected && source.VitalEnabled && source.CanHaveBrain && !source.OxygenProtected && brain >= terminal)
        { return new(0, cerebralDef); }
        if (!source.Protected && source.DirectBloodDeath && blood >= 1f) { return new(0, bloodLossDef); }
        while (previous < HORIZON)
        {
            int now = Math.Min(protectionTick, Math.Min(healingTick, Math.Min(bloodTick, Math.Min(shockTick, vitalTick))));
            if (now > HORIZON || now >= best.Ticks) { break; }
            adrenaline = Math.Max(0f, adrenaline - source.AdrenalineDecayPerDay * (now - previous) / GenDate.TicksPerDay);
            float quality = now < source.TendTicks ? source.TendQuality : 0f;
            if (now == protectionTick) { protectionTick = int.MaxValue; }
            capacityFailure = NativeCapacityDeath(in source, now);
            if ((!source.Protected || now >= source.ProtectionTicks) && capacityFailure >= 0)
            { return new(now, null, source.CapacityLabels![capacityFailure]); }
            if (now == bloodTick)
            {
                // Native bleeding giver: gains at >= 0.1/day, otherwise recovers each 60 ticks.
                blood = Math.Max(0f, Math.Min(1f, blood + (source.BleedRate >= 0.1f
                    ? source.BleedRate * 0.001f : -0.00033333333f)));
                bloodTick += 60;
                if (source.DirectBloodDeath && blood >= 1f && (!source.Protected || now >= source.ProtectionTicks)) { return new(now, bloodLossDef); }
            }
            if (now == shockTick)
            {
                if (shockPresent)
                { shock = ShockPerfusionModel.Advance(shock, blood, adrenaline, quality, recovering: !source.CanDevelopShock); }
                shockTick += ShockPerfusionModel.CYCLE_LENGTH;
            }
            if (now == healingTick)
            {
                if (Deprivation(in source, config, shock, blood, adrenaline, quality, shockPresent, failures.Get(6) > 0f, now) <= 0f)
                {
                    float recovery = source.BrainNaturalRecoveryPerDay + (now < source.BrainTendTicks ? source.BrainTendedRecoveryPerDay : 0f);
                    brain = Math.Max(0f, brain - recovery * 600f / GenDate.TicksPerDay);
                }
                healingTick += 600;
            }
            if (source.VitalEnabled && source.CanHaveBrain && !source.OxygenProtected && brain >= terminal
                && (!source.Protected || now >= source.ProtectionTicks)) { return new(now, cerebralDef); }
            if (now == vitalTick)
            {
                if (source.ManageFailures)
                {
                    int failed = failures.Advance(in source, now);
                    if (failed >= 0) { return new(now, source.FailureDefs![failed]); }
                }
                // Blood-loss onset and vital failure both use a 250-tick gate.
                if (!shockPresent && source.CanDevelopShock && blood > 0.45f) { shock = 0.01f; shockPresent = true; }
                if (source.VitalEnabled && source.CanHaveBrain && !source.OxygenProtected)
                {
                    float deprivation = Deprivation(in source, config, shock, blood, adrenaline, quality, shockPresent, failures.Get(6) > 0f, now);
                    if (deprivation > 0f)
                    {
                        if (brain <= 0f) { brain = VitalFailureModel.INITIAL_SEVERITY; }
                        brain = VitalFailureModel.Advance(brain, config.CerebralSeverityPerDay * deprivation * source.ComaMultiplier,
                            1f, (float)VitalFailureModel.INTERVAL / GenDate.TicksPerDay);
                        if (brain >= terminal && (!source.Protected || now >= source.ProtectionTicks)) { return new(now, cerebralDef); }
                    }
                }
                vitalTick += VitalFailureModel.INTERVAL;
            }
            previous = now;
        }
        // Longer existing organ failures: continue only their small numeric timer system, not cerebral/hemorrhage forecasting.
        if (source.ManageFailures && failures.HasActive)
        {
            int end = Math.Min(best.Ticks, HORIZON * 6);
            for (int tick = vitalTick; tick < end; tick += VitalFailureModel.INTERVAL)
            {
                int failed = failures.Advance(in source, tick);
                if (failed >= 0) { return new(tick, source.FailureDefs![failed]); }
            }
        }
        return best;
    }

    private static float Deprivation(in PhysiologySnapshot source, VitalFailureConfigDef config, float shock, float blood,
        float adrenaline, float quality, bool shockPresent, bool arrest, int now)
    {
        if (source.OxygenProtected) { return 0f; }
        float shockSource = shockPresent && source.CanDevelopShock
            ? config.ShockDeprivationCurve.Evaluate(ShockPerfusionModel.EffectiveShock(shock, blood))
                * ShockPerfusionModel.GrowthMultiplier(quality) / ShockPerfusionModel.Support(adrenaline) : 0f;
        float dilution = Math.Max(0f, source.Dilution - source.DilutionDecayPerDay * now / GenDate.TicksPerDay);
        float cold = source.ColdProtection;
        for (int i = 0; i < source.TrendCount; i++)
        {
            if (source.Trends![i].Cause == HediffDefOf.Hypothermia)
            { cold = config.HypothermiaProtectionCurve.Evaluate(source.Trends[i].At(now)); break; }
        }
        return VitalFailureModel.Deprivation(shockSource, PrognosisCapacity.Level(in source, 0, now),
            (source.ManageFailures ? arrest : source.Arrest) ? 0f : PrognosisCapacity.Level(in source, 1, now),
            config.DilutionDeprivationCurve.Evaluate(dilution)) * cold;
    }

    private static int NativeCapacityDeath(in PhysiologySnapshot source, int now)
    {
        for (int i = 0; i < 5; i++)
        {
            if ((source.NativeDeathCapacityMask & (1 << i)) != 0
                && PrognosisCapacity.Level(in source, i, now) <= source.NativeCapacityThresholds![i]) { return i; }
        }
        return -1;
    }
}
