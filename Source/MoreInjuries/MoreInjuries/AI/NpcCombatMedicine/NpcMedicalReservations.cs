using System;
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
        Thing? medicine = pawn.carryTracker.CarriedThing;
        if (medicine is null || medicine.Destroyed || !medicine.def.IsMedicine || job.targetB.Thing != medicine)
        {
            return;
        }
        NpcMedicalCompatibility.InventoryChanged(pawn);
        int count = Math.Min(medicine.stackCount, NpcMedicalCompatibility.Fit(pawn, medicine));
        if (count > 0 && pawn.carryTracker.innerContainer.TryTransferToContainer(medicine, pawn.inventory.innerContainer, count) > 0)
        {
            NpcMedicalCompatibility.InventoryChanged(pawn);
        }
    }
}
