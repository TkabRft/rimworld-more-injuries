using MoreInjuries.Defs.WellKnown;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.WorkGivers;

// Retain this workgiver's def/public type while sharing coordinated patient-level selection.
public class WorkGiver_UseBloodBag : WorkGiver_MoreInjuriesTreatmentBase
{
    public override bool ShouldSkip(Pawn pawn, bool forced = false) => !KnownResearchProjectDefOf.BasicFirstAid.IsFinished;

    protected override bool IsValidPatient(Pawn doctor, Thing thing, out Pawn patient)
        => base.IsValidPatient(doctor, thing, out patient)
            && patient.playerSettings?.medCare is not RimWorld.MedicalCareCategory.NoCare and not RimWorld.MedicalCareCategory.NoMeds;

    public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        => base.HasJobOnThing(pawn, thing, forced);

    protected override bool CanTreat(Pawn doctor, Pawn patient)
        => EmergencyCare.CreateJob(doctor, patient, aftercare: false)?.def == KnownJobDefOf.UseBloodBag;

    protected override Job CreateJob(Pawn doctor, Pawn patient)
        => EmergencyCare.CreateJob(doctor, patient, aftercare: false)!;
}
