using MoreInjuries.Extensions;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI;

// Explicit drafted care: hostile patients are eligible only while downed.
// This policy is independent of NPC modes, faction-care flags and drug settings.
internal static class FirstAidTending
{
    internal static bool Allowed(Pawn doctor, Pawn patient)
        => doctor.Drafted && doctor.Spawned && !doctor.Downed && !doctor.Dead && patient.Spawned && !patient.Dead
            && doctor.Map == patient.Map && patient.Downed && !patient.IsActivelyHostileTo(doctor)
            && !patient.IsForbidden(doctor) && patient.playerSettings?.medCare != MedicalCareCategory.NoCare
            && (!patient.IsMutant || patient.mutant.Def.entitledToMedicalCare)
            && !doctor.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && MedicalTiming.CanWork(doctor)
            && doctor.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
            && doctor.health.capacities.CapableOf(PawnCapacityDefOf.Sight)
            && doctor.CanReach(patient, PathEndMode.Touch, Danger.Deadly)
            && doctor.CanReserve(patient, maxPawns: 1, stackCount: -1);

    internal static bool SupplyAllowed(Pawn doctor, Pawn patient, Thing medicine)
    {
        if (medicine.Destroyed || !medicine.def.IsMedicine || medicine.stackCount < 1 || medicine.IsForbidden(doctor)
            || !(patient.playerSettings?.medCare ?? MedicalCareCategory.Best).AllowsMedicine(medicine.def)
            || !doctor.CanReserve(medicine, maxPawns: 10, stackCount: 1)) { return false; }
        if (doctor.carryTracker.CarriedThing == medicine) { return true; }
        if (doctor.carryTracker.CarriedThing is not null || doctor.carryTracker.AvailableStackSpace(medicine.def) < 1) { return false; }
        if (medicine.ParentHolder is Pawn_InventoryTracker holder)
        { return (holder.pawn == doctor || holder.pawn == patient) && doctor.CanReach(holder.pawn, PathEndMode.Touch, Danger.Deadly); }
        return medicine.Spawned && medicine.Map == doctor.Map && doctor.CanReach(medicine, PathEndMode.ClosestTouch, Danger.Deadly);
    }

    internal static Thing? FindMedicine(Pawn doctor, Pawn patient)
    {
        if (doctor.carryTracker.CarriedThing is Thing carried)
        { return SupplyAllowed(doctor, patient, carried) ? carried : null; }
        Thing? Inventory(Pawn owner)
        {
            Thing? best = null;
            float potency = float.NegativeInfinity;
            foreach (Thing item in owner.inventory.innerContainer)
            {
                if (!SupplyAllowed(doctor, patient, item)) { continue; }
                float value = item.GetStatValue(StatDefOf.MedicalPotency);
                if (value > potency) { best = item; potency = value; }
            }
            return best;
        }
        Thing? medicine = Inventory(doctor) ?? Inventory(patient);
        if (medicine is not null || patient.playerSettings?.medCare == MedicalCareCategory.NoMeds) { return medicine; }
        Thing? Nearby(IntVec3 center) => GenClosest.ClosestThingReachable(center, doctor.Map,
            ThingRequest.ForGroup(ThingRequestGroup.Medicine), PathEndMode.ClosestTouch, TraverseParms.For(doctor),
            maxDistance: 12f, validator: item => SupplyAllowed(doctor, patient, item),
            searchRegionsMin: 0, searchRegionsMax: 12, forceAllowGlobalSearch: false);
        return Nearby(patient.Position) ?? Nearby(doctor.Position);
    }

    internal static bool Prepare(Pawn doctor, Pawn patient, Job job)
    {
        if (!Allowed(doctor, patient)) { return false; }
        Thing? medicine = FindMedicine(doctor, patient);
        // Unrelated cargo stays intact. Do not silently tend barehanded with occupied hands.
        if (doctor.carryTracker.CarriedThing is not null && doctor.carryTracker.CarriedThing != medicine) { return false; }
        job.SetTarget(TargetIndex.B, medicine is null ? LocalTargetInfo.Invalid : new LocalTargetInfo(medicine));
        job.SetTarget(TargetIndex.C, medicine?.ParentHolder is Pawn_InventoryTracker holder
            ? new LocalTargetInfo(holder.pawn) : LocalTargetInfo.Invalid);
        job.count = 1;
        return medicine is null || doctor.Reserve(medicine, job, maxPawns: 10, stackCount: 1, errorOnFailed: false);
    }

    internal static bool DeliveryAllowed(Pawn doctor, Pawn patient, Job job)
        => Allowed(doctor, patient) && patient.health.HasHediffsNeedingTend() && (job.targetB.Thing is Thing medicine
            ? SupplyAllowed(doctor, patient, medicine) : doctor.carryTracker.CarriedThing is null);
}
