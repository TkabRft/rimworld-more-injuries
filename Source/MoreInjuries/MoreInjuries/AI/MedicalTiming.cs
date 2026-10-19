using RimWorld;
using Verse;

namespace MoreInjuries.AI;

internal static class MedicalTiming
{
    internal static bool CanWork(Pawn doctor) => EmergencyPriority.ValidWorkRate(doctor.GetStatValue(StatDefOf.MedicalTendSpeed),
        doctor.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation));
    internal static int Duration(Pawn doctor, int baseTicks)
        => EmergencyPriority.Duration(baseTicks, doctor.GetStatValue(StatDefOf.MedicalTendSpeed),
            doctor.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation));
}
