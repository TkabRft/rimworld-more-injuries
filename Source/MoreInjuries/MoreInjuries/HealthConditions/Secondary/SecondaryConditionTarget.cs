using Verse;

namespace MoreInjuries.HealthConditions.Secondary;

internal static class SecondaryConditionTarget
{
    // A failed organ lookup must not silently become a whole-body condition.
    internal static bool IsValid(HediffSet set, BodyPartRecord? part, bool allowWholeBody)
        => part is null ? allowWholeBody : !set.PartIsMissing(part);
}
