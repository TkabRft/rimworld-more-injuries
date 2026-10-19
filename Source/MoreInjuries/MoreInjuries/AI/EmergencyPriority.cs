using System;

namespace MoreInjuries.AI;

internal static class EmergencyPriority
{
    internal const int CRITICAL_BUDGET = 120;
    internal const float MINOR_BLEEDING = .1f;
    internal static float Clamp(float value) => Math.Max(0f, Math.Min(1f, value));
    internal static float BleedingUrgency(float rate) => 40f * Clamp(rate);
    internal static float Suitability(bool hemostatic, float rate, bool superclotting = false)
        => superclotting ? hemostatic ? 1f : 2f
            : hemostatic ? rate < MINOR_BLEEDING ? 1f : rate < .5f ? 1.2f : 1.5f : rate < MINOR_BLEEDING ? 2f : 1f;
    internal static float TransfusionUrgency(float loss, float dilution, float shock)
        => 10f + 40f * Clamp((Math.Max(loss, Math.Max(dilution, shock)) - .45f) / .55f);
    internal static float Score(float urgency, float benefit, int application, int retrieval, float cost)
        => urgency * benefit / (600f + Math.Max(0, application) + Math.Max(0, retrieval) + 20f * Math.Max(0f, cost));
    internal static int Duration(int baseTicks, float speed, float manipulation)
        => Math.Max(1, (int)(baseTicks / (Math.Max(.001f, speed) * Math.Max(.05f, manipulation))));
    internal static bool ValidWorkRate(float speed, float manipulation) => speed > 0f && manipulation > 0f
        && !float.IsNaN(speed) && !float.IsInfinity(speed) && !float.IsNaN(manipulation) && !float.IsInfinity(manipulation);
}
