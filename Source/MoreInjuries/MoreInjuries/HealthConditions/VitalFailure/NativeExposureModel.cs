using System;

namespace MoreInjuries.HealthConditions.VitalFailure;

// Native 1.6 mean rates, in severity/day. Native health processing still owns all exposure effects.
internal static class NativeExposureModel
{
    internal const float STARVATION_PER_DAY = .453f;
    internal static float Heat(float adjustedOverage) => Math.Max(.000375f, adjustedOverage * .0000645f) * 1000f;
    internal static float Cold(float overage) => Math.Max(.00075f, overage * .0000645f) * 1000f;
    internal static float Toxic(float exposure, float resistance, float environmentResistance)
        => .4f * exposure * Math.Max(0f, 1f - resistance) * Math.Max(0f, 1f - environmentResistance);
    internal static float Vacuum(float vacuum, float resistance) => vacuum < .5f ? 0f : 20f * vacuum * Math.Max(0f, 1f - resistance);
}
