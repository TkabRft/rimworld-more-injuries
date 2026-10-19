namespace MoreInjuries.AI.NpcCombatMedicine;

internal enum NpcPainCollapse { None, Pain, Other, Mixed }

internal readonly struct NpcPainAssessment(float pain, float threshold, bool inPainShock, bool downed, bool otherCause)
{
    internal float Pain { get; } = pain;
    internal float Threshold { get; } = threshold;
    internal bool InPainShock { get; } = inPainShock;
    internal bool Downed { get; } = downed;
    internal NpcPainCollapse Collapse { get; } = !downed ? NpcPainCollapse.None
        : inPainShock ? otherCause ? NpcPainCollapse.Mixed : NpcPainCollapse.Pain : NpcPainCollapse.Other;
}

// Engine-independent decisions shared by live assessment and contract tests.
internal static class NpcPainPolicy
{
    internal static float Priority(float pain, float threshold, bool inPainShock, bool downed, float preventiveFraction)
    {
        if (!(pain > 0f)) { return 0f; }
        if (inPainShock) { return 65f; }
        if (!(threshold > 0f)) { return 0f; }
        // A non-pain collapse is not a reason to administer an analgesic.
        if (downed) { return 0f; }
        if (pain < threshold * preventiveFraction) { return 0f; }
        if (pain >= threshold * .85f) { return 55f; }
        return 15f;
    }

    internal static bool Effective(float before, float after)
        => after >= 0f && after < before - .005f;

    internal static bool SafeCapacity(float before, float after, float minimum)
        => after > minimum + .01f || before <= minimum && after >= before - .001f;

    internal static bool SafeOverdose(float existing, float ordinaryMaximum)
        => existing >= 0f && ordinaryMaximum >= 0f && existing + ordinaryMaximum < .5f;
}
