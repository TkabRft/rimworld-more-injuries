using MoreInjuries.Things;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI;

internal static class Toils_MedicalDevice
{
    public static Toil ReserveDevice(TargetIndex targetIndex, Pawn patient, Func<Pawn, int> getDeviceCountToFullyHeal)
    {
        Toil toil = ToilMaker.MakeToil(nameof(ReserveDevice));
        toil.initAction = () =>
        {
            Pawn actor = toil.actor;
            Job curJob = actor.jobs.curJob;
            Thing thing = curJob.GetTarget(targetIndex).Thing;
            int needed = getDeviceCountToFullyHeal(patient);
            if (needed <= 0)
            {
                actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                return;
            }
            if (thing is null || thing.Destroyed || thing.stackCount <= 0)
            {
                actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                return;
            }
            int availableDevices = actor.Map.reservationManager.CanReserveStack(actor, thing, MedicalDeviceHelper.MAX_MEDICAL_DEVICE_RESERVATIONS);
            if (availableDevices > 0 && actor.Reserve(thing, curJob, MedicalDeviceHelper.MAX_MEDICAL_DEVICE_RESERVATIONS, Mathf.Min(availableDevices, needed)))
            {
                return;
            }

            toil.actor.jobs.EndCurrentJob(JobCondition.Incompletable);
        };
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        toil.atomicWithPrevious = true;
        return toil;
    }

    public static Toil PickupDevice(TargetIndex targetIndex, Pawn patient, Func<Pawn, int> getDeviceCountToFullyHeal)
    {
        Toil toil = ToilMaker.MakeToil(nameof(PickupDevice));
        toil.initAction = () =>
        {
            Pawn actor = toil.actor;
            Job curJob = actor.jobs.curJob;
            Thing thing = curJob.GetTarget(targetIndex).Thing;
            int countToFullyHeal = getDeviceCountToFullyHeal(patient);
            if (countToFullyHeal <= 0)
            {
                actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                return;
            }
            if (thing is null || thing.Destroyed || thing.stackCount <= 0)
            {
                actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                return;
            }
            if (actor.carryTracker.CarriedThing is not null)
            {
                if (actor.carryTracker.CarriedThing.def != thing.def)
                {
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                countToFullyHeal -= actor.carryTracker.CarriedThing.stackCount;
                if (ReferenceEquals(actor.carryTracker.CarriedThing, thing))
                {
                    curJob.count = Mathf.Max(0, countToFullyHeal);
                    return;
                }
            }

            int count = Mathf.Min(actor.Map.reservationManager.CanReserveStack(actor, thing, MedicalDeviceHelper.MAX_MEDICAL_DEVICE_RESERVATIONS), countToFullyHeal);
            if (count > 0)
            {
                count = actor.carryTracker.TryStartCarry(thing, count);
                if (count <= 0)
                {
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
            }

            if (actor.carryTracker.CarriedThing is not Thing carried || carried.def != thing.def)
            {
                actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                return;
            }

            curJob.count = countToFullyHeal - count;
            if (thing.Spawned)
            {
                toil.actor.Map.reservationManager.Release(thing, actor, curJob);
            }

            curJob.SetTarget(targetIndex, actor.carryTracker.CarriedThing);
        };
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        return toil;
    }
}
