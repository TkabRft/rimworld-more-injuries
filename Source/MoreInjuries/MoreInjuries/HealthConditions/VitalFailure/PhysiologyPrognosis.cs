using MoreInjuries.HealthConditions.HypovolemicShock;
using RimWorld;
using System;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

internal readonly struct PhysiologyEstimate(int ticks, HediffDef? cause)
{
    internal int Ticks { get; } = ticks;
    internal HediffDef? Cause { get; } = cause;
}

internal struct PhysiologySnapshot
{
    internal bool Relevant, Protected, VitalEnabled, CanDevelopShock, ShockPresent, CanHaveBrain, OxygenProtected, DirectBloodDeath;
    internal float BloodLoss, BleedRate, Shock, ClinicalBrain, Breathing, Pumping, Dilution, ColdProtection;
    internal float Adrenaline, AdrenalineDecayPerDay, TendQuality, ComaMultiplier;
    internal int TendTicks, NextBloodTick, NextShockTick, NextVitalTick;
    internal PhysiologyEstimate Failure;
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
        if (source.Protected) { return new(int.MaxValue, null); }
        PhysiologyEstimate best = source.Failure;
        float blood = source.BloodLoss, shock = source.Shock, brain = source.ClinicalBrain;
        float adrenaline = source.Adrenaline;
        bool shockPresent = source.ShockPresent;
        int bloodTick = Math.Max(1, source.NextBloodTick), shockTick = Math.Max(1, source.NextShockTick);
        int vitalTick = Math.Max(1, source.NextVitalTick), previous = 0;
        if (source.VitalEnabled && source.CanHaveBrain && !source.OxygenProtected && brain >= config.TerminalCerebralSeverity)
        { return new(0, cerebralDef); }
        if (source.DirectBloodDeath && blood >= 1f) { return new(0, bloodLossDef); }
        while (previous < HORIZON)
        {
            int now = Math.Min(bloodTick, Math.Min(shockTick, vitalTick));
            if (now > HORIZON || now >= best.Ticks) { break; }
            adrenaline = Math.Max(0f, adrenaline - source.AdrenalineDecayPerDay * (now - previous) / GenDate.TicksPerDay);
            float quality = now < source.TendTicks ? source.TendQuality : 0f;
            if (now == bloodTick)
            {
                // Native bleeding giver: gains at >= 0.1/day, otherwise recovers each 60 ticks.
                blood = Math.Max(0f, Math.Min(1f, blood + (source.BleedRate >= 0.1f
                    ? source.BleedRate * 0.001f : -0.00033333333f)));
                bloodTick += 60;
                if (source.DirectBloodDeath && blood >= 1f) { return new(now, bloodLossDef); }
            }
            if (now == shockTick)
            {
                if (shockPresent)
                { shock = ShockPerfusionModel.Advance(shock, blood, adrenaline, quality, recovering: !source.CanDevelopShock); }
                shockTick += ShockPerfusionModel.CYCLE_LENGTH;
            }
            if (now == vitalTick)
            {
                // Blood-loss onset and vital failure both use a 250-tick gate.
                if (!shockPresent && source.CanDevelopShock && blood > 0.45f) { shock = 0.01f; shockPresent = true; }
                if (source.VitalEnabled && source.CanHaveBrain && !source.OxygenProtected)
                {
                    float shockSource = shockPresent && source.CanDevelopShock
                        ? config.ShockDeprivationCurve.Evaluate(ShockPerfusionModel.EffectiveShock(shock, blood))
                            * ShockPerfusionModel.GrowthMultiplier(quality) / ShockPerfusionModel.Support(adrenaline) : 0f;
                    float deprivation = VitalFailureModel.Deprivation(shockSource, source.Breathing, source.Pumping,
                        config.DilutionDeprivationCurve.Evaluate(source.Dilution)) * source.ColdProtection;
                    if (deprivation > 0f)
                    {
                        if (brain <= 0f) { brain = VitalFailureModel.INITIAL_SEVERITY; }
                        brain = VitalFailureModel.Advance(brain, config.CerebralSeverityPerDay * deprivation * source.ComaMultiplier,
                            1f, (float)VitalFailureModel.INTERVAL / GenDate.TicksPerDay);
                        if (brain >= config.TerminalCerebralSeverity) { return new(now, cerebralDef); }
                    }
                }
                vitalTick += VitalFailureModel.INTERVAL;
            }
            previous = now;
        }
        return best;
    }
}
