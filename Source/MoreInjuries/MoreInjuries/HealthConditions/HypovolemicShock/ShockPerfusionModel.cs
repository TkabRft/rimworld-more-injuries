using System;

namespace MoreInjuries.HealthConditions.HypovolemicShock;

internal static class ShockPerfusionModel
{
    internal const int CYCLE_LENGTH = 150;

    // Game balance, not clinical cutoffs. The fork maps a full blood-loss gauge to 50% missing volume.
    // Keep routine donation (0.449) below onset. Existing hypoxic complications start above 0.5 shock.
    internal static float TargetSeverity(float bloodLoss)
    {
        if (bloodLoss <= 0.45f) { return 0f; }
        if (bloodLoss < 0.6f) { return (bloodLoss - 0.45f) / 0.15f * 0.5f; }
        if (bloodLoss < 0.8f) { return 0.5f + (bloodLoss - 0.6f) / 0.2f * 0.35f; }
        return Math.Min(1f, 0.85f + (bloodLoss - 0.8f) / 0.2f * 0.15f);
    }

    internal static float Advance(float severity, float bloodLoss, float adrenaline, bool tended, bool stabilized, bool recovering)
    {
        float support = 1f + Math.Min(0.5f, Math.Max(0f, adrenaline / 2f));
        float target = recovering ? 0f : TargetSeverity(bloodLoss) / support;
        if (severity < target)
        {
            float treatmentFactor = tended ? (stabilized ? 0.5f : 0.75f) : 1f;
            float increase = (0.015f + 0.045f * target) * treatmentFactor / support;
            return Math.Min(target, severity + increase);
        }
        float recovery = 0.0075f * support * (tended ? 1.5f : 1f);
        return Math.Max(target, severity - recovery);
    }
}
