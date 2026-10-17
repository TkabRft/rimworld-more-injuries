using MoreInjuries.Extensions;
using MoreInjuries.HealthConditions.HeavyBleeding;
using UnityEngine;
using Verse;

namespace MoreInjuries.HealthConditions.HypovolemicShock;

internal static class ShockTreatment
{
    internal static float Quality(Hediff shock) => shock.TryGetComp<HediffComp_TendDuration>() is { IsTended: true } tend
        ? Mathf.Clamp01(tend.tendQuality) : 0f;

    internal static bool CanDriveAcidosis(Pawn pawn, bool deathresting) => MoreInjuriesMod.Settings.EnableHypovolemicShock
        && MoreInjuriesMod.Settings.EnableAdvancedTraumaSimulation
        && MoreInjuriesMod.Settings.HypoxiaAcidosisConversionFactor > 0f
        && TraumaEligibility.Supports(pawn)
        && !pawn.HasOxygenDeficiencyImmunity()
        && !deathresting;
}
