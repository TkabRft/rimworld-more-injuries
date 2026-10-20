using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

public sealed class JobDriver_NpcTend : JobDriver_TendPatient
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        Pawn patient = job.targetA.Pawn;
        NpcMedicalAttempts.Begin(pawn, job, patient, job.targetB.Thing);
        bool reserved = NpcMedicalReservations.DeliveryAllowed(pawn, patient, job.targetB.Thing)
            && pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed)
            && (job.targetB.Thing is not Thing medicine || pawn.Reserve(medicine, job, 10, 1, errorOnFailed: errorOnFailed));
        if (!reserved)
        {
            NpcMedicalAttempts.Finish(job, JobCondition.Incompletable);
            pawn.Map.reservationManager.ReleaseClaimedBy(pawn, job);
        }
        return reserved;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => !NpcDeparture.Continue(pawn, job));
        Job attemptJob = job;
        Pawn attemptPatient = job.targetA.Pawn;
        NpcMedicalAttempts.Begin(pawn, attemptJob, attemptPatient, job.targetB.Thing);
        AddFinishAction(condition =>
        {
            NpcMedicalAttempts.Finish(attemptJob, condition);
            NpcMedicalReservations.RestockUnusedMedicine(pawn, attemptJob);
        });
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOn(() => NpcMedicalSafety.YieldToExplosive(pawn, attemptJob));
        this.FailOn(() => job.targetA.Pawn is not Pawn patient || patient.Dead
            || patient.IsMutant && !patient.mutant.Def.entitledToMedicalCare
            || !NpcMedicalGeneration.CanDoctor(pawn) || !MedicalTiming.CanWork(pawn)
            || pawn != patient && pawn.HostileTo(patient)
            || !(pawn == patient && pawn.IsPrisoner) && (patient.playerSettings?.medCare == MedicalCareCategory.NoCare
                || job.targetB.Thing is Thing item && patient.playerSettings is { } settings && !settings.medCare.AllowsMedicine(item.def)));
        AddEndCondition(() => job.targetA.Pawn?.health.HasHediffsNeedingTend() == true ? JobCondition.Ongoing : JobCondition.Succeeded);
        // All branches exist on every rebuild. Persisted toil indices keep their
        // meaning after pickup, inventory transfer and loading an active job.
        Toil ready = Toils_General.Do(() =>
        {
            Thing? selected = job.targetB.Thing;
            if (selected is not null && (selected.Destroyed || !selected.def.IsMedicine
                || selected.stackCount != 1 || pawn.carryTracker.CarriedThing != selected)
                || !NpcMedicalReservations.DeliveryAllowed(pawn, job.targetA.Pawn, selected))
            { EndJobWith(JobCondition.Incompletable); return; }
            NpcMedicalAttempts.Stage(job, "delivery");
        });
        Toil takeInventory = Toils_General.Do(() =>
        {
            Thing medicine = job.targetB.Thing;
            if (medicine.ParentHolder is not Pawn_InventoryTracker holder
                || !NpcMedicalReservations.DeliveryAllowed(pawn, job.targetA.Pawn, medicine)
                || holder.pawn != pawn && !pawn.CanReachImmediate(holder.pawn, PathEndMode.Touch)
                || !NpcMedicalReservations.TakeMedicine(pawn, job, medicine, holder.innerContainer))
            { EndJobWith(JobCondition.Incompletable); }
        });
        Toil inventoryRoute = Toils_General.Do(() =>
        {
            if (job.targetB.Thing?.ParentHolder is not Pawn_InventoryTracker holder)
            { EndJobWith(JobCondition.Incompletable); return; }
            job.SetTarget(TargetIndex.C, holder.pawn);
        });
        Toil groundRoute = Travel(TargetIndex.B, PathEndMode.ClosestTouch);
        yield return Toils_Jump.JumpIf(ready, () => job.targetB.Thing is null || job.targetB.Thing == pawn.carryTracker.CarriedThing);
        yield return Toils_General.Do(() =>
        {
            if (!NpcMedicalReservations.DeliveryAllowed(pawn, job.targetA.Pawn, job.targetB.Thing))
            { EndJobWith(JobCondition.Incompletable); }
        });
        yield return Toils_Jump.JumpIf(inventoryRoute, () => job.targetB.Thing?.ParentHolder is Pawn_InventoryTracker);
        yield return groundRoute;
        yield return Toils_General.Do(() =>
        {
            if (!NpcMedicalReservations.DeliveryAllowed(pawn, job.targetA.Pawn, job.targetB.Thing))
            { EndJobWith(JobCondition.Incompletable); }
        });
        yield return Toils_MedicalDevice.PickupDevice(TargetIndex.B, job.targetA.Pawn, _ => 1);
        yield return Toils_Jump.Jump(ready);
        yield return inventoryRoute;
        yield return Toils_Jump.JumpIf(takeInventory, () => job.targetC.Pawn == pawn);
        yield return Travel(TargetIndex.C, PathEndMode.Touch);
        yield return takeInventory;
        yield return ready;
        Pawn patientPawn = job.targetA.Pawn;
        Toil application = Toils_General.Do(() =>
        {
            if (!NpcMedicalReservations.DeliveryAllowed(pawn, patientPawn, job.targetB.Thing)
                || patientPawn != pawn && !pawn.CanReachImmediate(patientPawn, PathEndMode.Touch))
            { EndJobWith(JobCondition.Incompletable); return; }
            NpcMedicalAttempts.Stage(job, "waiting");
        });
        yield return Toils_Jump.JumpIf(application, () => patientPawn == pawn);
        yield return Travel(TargetIndex.A, PathEndMode.Touch);
        yield return application;
        int ticks = MedicalTiming.Duration(pawn, 600);
        Toil wait = Toils_General.Wait(ticks).WithProgressBarToilDelay(TargetIndex.A)
            .PlaySustainerOrSound(SoundDefOf.Interact_Tend);
        wait.activeSkill = () => SkillDefOf.Medicine;
        if (patientPawn != pawn)
        {
            Job? ownedWait = null;
            wait.AddPreInitAction(() =>
            {
                if (patientPawn.IsColonistPlayerControlled || patientPawn.CurJob?.playerForced == true
                    || NpcMedicalCompatibility.SafetyOwned(patientPawn)) { return; }
                Job? previous = patientPawn.CurJob;
                PawnUtility.ForceWait(patientPawn, ticks, maintainPosture: true);
                if (patientPawn.CurJob != previous && (patientPawn.CurJobDef == JobDefOf.Wait
                    || patientPawn.CurJobDef == JobDefOf.Wait_MaintainPosture)) { ownedWait = patientPawn.CurJob; }
            });
            wait.AddFinishAction(() =>
            {
                if (ownedWait is not null && patientPawn.CurJob == ownedWait)
                { patientPawn.jobs.EndCurrentJob(JobCondition.InterruptForced); }
            });
            wait.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
        }
        yield return wait;
        // Preserve vanilla tending quality, medicine consumption, experience and health callbacks.
        Toil finalize = Toils_Tend.FinalizeTend(patientPawn);
        var nativeFinalize = finalize.initAction;
        finalize.initAction = () =>
        {
            if (!NpcDeparture.Continue(pawn, attemptJob) || !NpcMedicalReservations.DeliveryAllowed(pawn, patientPawn, attemptJob.targetB.Thing)
                || patientPawn != pawn && !pawn.CanReachImmediate(patientPawn, PathEndMode.Touch))
            { EndJobWith(JobCondition.Incompletable); return; }
            NpcMedicalAttempts.Applying(attemptJob);
            nativeFinalize();
        };
        finalize.AddFinishAction(() => NpcMedicalAttempts.Applied(attemptJob, accepted: true));
        yield return finalize;
    }

    private Toil Travel(TargetIndex target, PathEndMode end)
        => Toils_Goto.GotoThing(target, end).FailOnDespawnedNullOrForbidden(target)
            .FailOn(() => NpcMedicalModel.TravelExpired(Find.TickManager.TicksGame, job.startTick));
}

