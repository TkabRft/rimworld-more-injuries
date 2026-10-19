using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static class NpcMedicalReservations
{
    // Self-treatment must respect a different doctor's claim on this patient too.
    internal static bool PatientAvailable(Pawn doctor, Pawn patient)
        => doctor.CanReserve(patient, maxPawns: 1, stackCount: -1, ignoreOtherReservations: false);

    internal static bool PrepareTacticalJob(Pawn pawn, Job job)
    {
        // CE calls StartJob directly instead of the think-tree reservation preflight.
        if (job.TryMakePreToilReservations(pawn, errorOnFailed: false))
        {
            return true;
        }
        // A driver's failed preflight can have reserved its first target already.
        // Release this proposed job only, preserving the current job and other doctors.
        pawn.Map.reservationManager.ReleaseClaimedBy(pawn, job);
        return false;
    }

    internal static bool DeliveryAllowed(Pawn doctor, Pawn patient, Thing? supply)
        => !patient.Dead && !patient.IsForbidden(doctor) && (doctor == patient || !doctor.HostileTo(patient))
            && PatientAvailable(doctor, patient)
            && (doctor.carryTracker.CarriedThing is null || doctor.carryTracker.CarriedThing == supply)
            && (doctor == patient || doctor.CanReach(patient, PathEndMode.Touch, Danger.Some))
            && (supply is null || !supply.Destroyed && !supply.IsForbidden(doctor)
                && supply.stackCount > 0 && doctor.CanReserve(supply, 10, 1)
                && (doctor.carryTracker.CarriedThing == supply || doctor.carryTracker.CarriedThing is null
                    && doctor.carryTracker.AvailableStackSpace(supply.def) > 0)
                && NpcPainCare.SourceAllowed(doctor, patient, supply));

    internal static bool TakeMedicine(Pawn pawn, Job job, Thing medicine, ThingOwner source)
    {
        if (medicine.Destroyed || !medicine.def.IsMedicine || medicine.stackCount <= 0) { return false; }
        if (pawn.carryTracker.CarriedThing == medicine) { return true; }
        if (pawn.carryTracker.CarriedThing is not null || !source.Contains(medicine)) { return false; }
        if (source.TryTransferToContainer(medicine, pawn.carryTracker.innerContainer, 1, out Thing transferred) != 1
            || transferred != pawn.carryTracker.CarriedThing) { return false; }
        job.SetTarget(TargetIndex.B, transferred);
        NpcMedicalCompatibility.InventoryChanged(pawn);
        return true;
    }

    internal static void RestockUnusedMedicine(Pawn pawn, Job job)
    {
        if (job.targetB.Thing?.def.IsMedicine == true) { RestockSelected(pawn, job, TargetIndex.B); }
    }

    internal static void RestockSelected(Pawn pawn, Job job, TargetIndex target)
    {
        Thing? selected = pawn.carryTracker.CarriedThing;
        if (selected is null || selected.Destroyed || job.GetTarget(target).Thing != selected) { return; }
        NpcMedicalCompatibility.InventoryChanged(pawn);
        int count = Math.Min(selected.stackCount, NpcMedicalCompatibility.Fit(pawn, selected));
        if (count > 0 && pawn.carryTracker.innerContainer.TryTransferToContainer(selected, pawn.inventory.innerContainer, count) > 0)
        { NpcMedicalCompatibility.InventoryChanged(pawn); }
    }

    internal static bool TakeDrug(Pawn pawn, Job job, TargetIndex target, Thing dose, ThingOwner source)
    {
        if (dose.Destroyed || !NpcMedicalGeneration.IsPerformance(dose.def) || dose.stackCount < 1) { return false; }
        if (pawn.carryTracker.CarriedThing == dose) { return dose.stackCount == 1; }
        if (pawn.carryTracker.CarriedThing is not null || !source.Contains(dose)) { return false; }
        if (source.TryTransferToContainer(dose, pawn.carryTracker.innerContainer, 1, out Thing transferred) != 1
            || transferred != pawn.carryTracker.CarriedThing) { return false; }
        job.SetTarget(target, transferred);
        NpcMedicalCompatibility.InventoryChanged(pawn);
        return true;
    }

    internal static void RestockUnusedDrug(Pawn pawn, Job job, TargetIndex target)
    {
        Thing? dose = pawn.carryTracker.CarriedThing;
        if (dose is null || dose.Destroyed || job.GetTarget(target).Thing != dose
            || !NpcMedicalGeneration.IsPerformance(dose.def)) { return; }
        NpcMedicalCompatibility.InventoryChanged(pawn);
        int count = Math.Min(dose.stackCount, NpcMedicalCompatibility.Fit(pawn, dose));
        if (count > 0) { pawn.carryTracker.innerContainer.TryTransferToContainer(dose, pawn.inventory.innerContainer, count); }
        NpcMedicalCompatibility.InventoryChanged(pawn);
    }
}
