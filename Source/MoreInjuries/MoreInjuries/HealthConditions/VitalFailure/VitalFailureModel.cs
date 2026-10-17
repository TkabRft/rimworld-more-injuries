using System;

namespace MoreInjuries.HealthConditions.VitalFailure;

internal static class VitalFailureModel
{
    internal const int INTERVAL = 250;
    internal const float ZERO_FUNCTION = 0.0001f;
    internal const float INITIAL_SEVERITY = 0.001f;

    internal static float CapacityDeprivation(float capacity) => Math.Max(0f, Math.Min(1f, 1f - capacity / 0.5f));

    internal static float Deprivation(float shock, float breathing, float pumping, float dilution)
        => Math.Max(Math.Max(shock, dilution), Math.Max(CapacityDeprivation(breathing), CapacityDeprivation(pumping)));

    internal static float Advance(float severity, float ratePerDay, float factor, float dayFraction)
        => severity + Math.Max(0f, ratePerDay) * Math.Max(0.8f, Math.Min(1.2f, factor)) * dayFraction;
}
