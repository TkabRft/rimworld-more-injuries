using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.CardiacArrest;
using MoreInjuries.Things;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.HealthConditions.Choking;

// Called only when a doctor has selected an actual patient/job, never from health ticks or work scans.
internal static class AutomaticAirwayTreatment
{
    internal const float SEARCH_RADIUS = 12f;
    internal const int SEARCH_REGIONS = 12;

    internal static Job CreateJob(Pawn doctor, Pawn patient)
    {
        if (!patient.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest)
            && KnownResearchProjectDefOf.EmergencyMedicine.IsFinished)
        {
            Thing? device = MedicalDeviceHelper.FindMedicalDevice(doctor, patient, KnownThingDefOf.SuctionDevice,
                JobDriver_UseSuctionDevice.TargetHediffDefs, fromInventoryOnly: true);
            if (device is null && doctor.Map is { } map && patient.MapHeld == map
                && patient.playerSettings?.medCare is not RimWorld.MedicalCareCategory.NoCare
                && (!patient.IsMutant || patient.mutant.Def.entitledToMedicalCare))
            {
                device = GenClosest.ClosestThingReachable(patient.PositionHeld, map,
                    ThingRequest.ForDef(KnownThingDefOf.SuctionDevice), PathEndMode.ClosestTouch, TraverseParms.For(doctor),
                    maxDistance: SEARCH_RADIUS,
                    validator: thing => !thing.IsForbidden(doctor)
                        && doctor.CanReserve(thing, MedicalDeviceHelper.MAX_MEDICAL_DEVICE_RESERVATIONS, stackCount: 1),
                    searchRegionsMin: 0, searchRegionsMax: SEARCH_REGIONS, forceAllowGlobalSearch: false);
            }
            if (device is not null)
            {
                return JobDriver_UseSuctionDevice.GetDispatcher(doctor, patient, device).CreateJob();
            }
        }
        return JobDriver_PerformCpr.GetDispatcher(doctor, patient).CreateJob();
    }
}
