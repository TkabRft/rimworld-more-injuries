using MoreInjuries.HealthConditions.HypovolemicShock;
using System;

namespace MoreInjuries.HealthConditions.Acidosis;

internal static class AcidosisProgressionModel
{
    internal static float ShockProductionPerDay(float curveRate, float quality, float adrenaline, float conversionFactor)
        => Math.Max(0f, curveRate) * ShockPerfusionModel.GrowthMultiplier(quality)
            / ShockPerfusionModel.Support(adrenaline) * Math.Max(0f, conversionFactor) / 0.015f;

    internal static float Advance(float severity, float injuryHistoryChange, float shockProductionPerDay,
        float recoveryPerDay, float shockedRecoveryPerDay, float dayFraction)
    {
        float injuryChange = Math.Max(0f, injuryHistoryChange);
        float production = Math.Max(injuryChange, shockProductionPerDay * dayFraction);
        float recovery = shockProductionPerDay > 0f ? shockedRecoveryPerDay : injuryChange > 0f ? 0f : recoveryPerDay;
        return Math.Max(0f, severity + production - recovery * dayFraction);
    }
}
