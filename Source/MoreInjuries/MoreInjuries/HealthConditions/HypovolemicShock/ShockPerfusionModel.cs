using System;

namespace MoreInjuries.HealthConditions.HypovolemicShock;

internal static class ShockPerfusionModel
{
    internal const int CYCLE_LENGTH = 150;

    // Game balance, not clinical cutoffs. The fork maps a full blood-loss gauge to 50% missing volume.
    // Keep routine donation (0.449) below onset. Existing hypoxic complications start above 0.5 shock.
    internal static float TargetSeverity(float bloodLoss)
    {
        if (bloodLoss <= 0.45f)
        {
            return 0f;
        }
        if (bloodLoss < 0.6f)
        {
            return (bloodLoss - 0.45f) / 0.15f * 0.5f;
        }
        if (bloodLoss < 0.8f)
        {
            return 0.5f + (bloodLoss - 0.6f) / 0.2f * 0.35f;
        }
        return Math.Min(1f, 0.85f + (bloodLoss - 0.8f) / 0.2f * 0.15f);
    }

    internal static float Support(float adrenaline) => 1f + Math.Min(0.5f, Math.Max(0f, adrenaline / 2f));

    internal static float GrowthMultiplier(float quality) => 1f - 0.5f * Math.Min(1f, Math.Max(0f, quality));

    internal static float ChanceMultiplier(float quality, float fullQualityMultiplier)
        => 1f - (1f - fullQualityMultiplier) * Math.Min(1f, Math.Max(0f, quality));

    internal static float EffectiveShock(float severity, float bloodLoss) => Math.Min(severity, TargetSeverity(bloodLoss));

    internal static float Advance(float severity, float bloodLoss, float adrenaline, float quality, bool recovering)
    {
        float support = Support(adrenaline);
        float target = recovering ? 0f : TargetSeverity(bloodLoss);
        if (severity < target)
        {
            float increase = (0.015f + 0.045f * target) * GrowthMultiplier(quality) / support;
            return Math.Min(target, severity + increase);
        }
        float recovery = 0.0075f * support * (2f - GrowthMultiplier(quality));
        return Math.Max(target, severity - recovery);
    }
}
