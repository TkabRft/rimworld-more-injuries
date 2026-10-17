namespace MoreInjuries.HealthConditions.HeavyBleeding;

internal static class BloodLossPolicy
{
    internal static bool AllowDirectDeath(bool tracked, bool preventDirectDeath) => !tracked || !preventDirectDeath;

    internal static float NonlethalCapThreshold(float maxSeverity, float lethalSeverity) =>
        lethalSeverity > 0f && lethalSeverity <= maxSeverity ? maxSeverity + 0.001f : lethalSeverity;
}
