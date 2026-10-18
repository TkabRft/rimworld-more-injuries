using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.CardiacArrest;
using MoreInjuries.HealthConditions.Choking;
using MoreInjuries.Things;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.WorkGivers;

public class WorkGiver_ManageAirways : WorkGiver_MoreInjuriesTreatmentBase
{
    public override bool ShouldSkip(Pawn pawn, bool forced = false) => !KnownResearchProjectDefOf.Cpr.IsFinished;

    protected override bool CanTreat(Hediff hediff) => Array.IndexOf(JobDriver_UseSuctionDevice.TargetHediffDefs, hediff.def) != -1;

    protected override Job CreateJob(Pawn doctor, Pawn patient) =>
        AutomaticAirwayTreatment.CreateJob(doctor, patient);
}
