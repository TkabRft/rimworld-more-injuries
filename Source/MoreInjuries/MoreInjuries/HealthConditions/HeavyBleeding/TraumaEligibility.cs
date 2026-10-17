using Verse;

namespace MoreInjuries.HealthConditions.HeavyBleeding;

internal static class TraumaEligibility
{
    internal static bool Supports(Pawn pawn) => Supports(
        pawn.HasComp<MoreInjuryComp>(), ModsConfig.AnomalyActive,
        pawn.RaceProps.IsAnomalyEntity, pawn.RaceProps.IsFlesh, pawn.RaceProps.bleedRateFactor);

    internal static bool Supports(bool tracked, bool anomalyActive, bool anomalyEntity, bool flesh, float bleedRate)
        => tracked || anomalyActive && anomalyEntity && flesh && bleedRate > 0f;
}
