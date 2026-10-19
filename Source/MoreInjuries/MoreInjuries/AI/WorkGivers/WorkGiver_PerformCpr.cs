using MoreInjuries.Defs.WellKnown;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.WorkGivers;

// Retain this workgiver's def/public type while sharing coordinated patient-level selection.
public class WorkGiver_PerformCpr : WorkGiver_MoreInjuriesTreatmentBase
{
    public override bool ShouldSkip(Pawn pawn, bool forced = false) => !KnownResearchProjectDefOf.Cpr.IsFinished;

    protected override bool CanTreat(Pawn doctor, Pawn patient)
        => EmergencyCare.CreateJob(doctor, patient, aftercare: false)?.def == KnownJobDefOf.PerformCpr;

    protected override Job CreateJob(Pawn doctor, Pawn patient)
        => EmergencyCare.CreateJob(doctor, patient, aftercare: false)!;
}
