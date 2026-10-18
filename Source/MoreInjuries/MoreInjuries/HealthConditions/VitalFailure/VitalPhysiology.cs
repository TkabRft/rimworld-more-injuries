using RimWorld;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

// Race anatomy is cached separately; mutable gene/mutant state is read at refresh time.
internal static class VitalPhysiology
{
    internal static bool Supports(Pawn pawn) => pawn.RaceProps.IsFlesh;

    internal static bool BreathesAir(Pawn pawn) => !pawn.IsMutant || pawn.mutant.Def.breathesAir;

    internal static bool HasNativeCapacityDeath(Pawn pawn)
    {
        // Repeated gene extraction is a deliberate native lethal operation, not a rescueable coma.
        if (!ModsConfig.BiotechActive) { return false; }
        foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
        {
            if (hediff.def.defName == "XenogermReplicating" && hediff.Severity >= 2f) { return true; }
        }
        return false;
    }

    internal static bool CanReceiveCare(Pawn pawn) => (!pawn.IsMutant || pawn.mutant.Def.entitledToMedicalCare)
        && pawn.playerSettings?.medCare is not MedicalCareCategory.NoCare;
}
