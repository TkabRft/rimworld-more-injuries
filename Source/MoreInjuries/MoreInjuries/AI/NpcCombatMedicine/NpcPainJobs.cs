using System.Collections.Generic;
using MoreInjuries.HealthConditions.Drugs;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

public sealed class JobDriver_NpcPainSelf : JobDriver_Ingest
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        NpcMedicalAttempts.Begin(pawn, job, pawn, job.targetA.Thing);
        bool reserved = job.targetA.Thing is { } dose
            && NpcMedicalReservations.DeliveryAllowed(pawn, pawn, dose)
            && pawn.Reserve(pawn, job, errorOnFailed: errorOnFailed)
            && pawn.Reserve(dose, job, 10, 1, errorOnFailed: errorOnFailed);
        if (!reserved)
        {
            NpcMedicalAttempts.Finish(job, JobCondition.Incompletable);
            pawn.Map.reservationManager.ReleaseClaimedBy(pawn, job);
        }
        return reserved;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        Job attemptJob = job;
        NpcMedicalAttempts.Begin(pawn, attemptJob, pawn, attemptJob.targetA.Thing);
        AddFinishAction(condition =>
        {
            NpcMedicalAttempts.Finish(attemptJob, condition);
            NpcMedicalReservations.RestockUnusedDrug(pawn, attemptJob, TargetIndex.A);
        });
        this.FailOn(() => pawn.Downed || job.targetA.Thing is not { Destroyed: false, IngestibleNow: true }
            || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation));
        this.FailOn(() => !NpcPainCare.SourceAllowed(pawn, pawn, job.targetA.Thing));
        // Keep the toil graph identical after save/load, including when the
        // selected inventory stack has already become a single carried dose.
        Toil ready = Toils_General.Do(() =>
        {
            if (pawn.carryTracker.CarriedThing != job.targetA.Thing || pawn.carryTracker.CarriedThing?.stackCount != 1)
            { EndJobWith(JobCondition.Incompletable); return; }
            job.count = 1;
        });
        Toil takeInventory = Toils_General.Do(() =>
        {
            Thing dose = job.targetA.Thing;
            if (dose.ParentHolder is not Pawn_InventoryTracker holder || !NpcPainCare.SourceAllowed(pawn, pawn, dose)
                || holder.pawn != pawn && !pawn.CanReachImmediate(holder.pawn, PathEndMode.Touch)
                || !NpcMedicalReservations.TakeDrug(pawn, job, TargetIndex.A, dose, holder.innerContainer))
            { EndJobWith(JobCondition.Incompletable); }
        });
        Toil ground = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(TargetIndex.A)
            .FailOn(() => NpcMedicalModel.TravelExpired(Find.TickManager.TicksGame, job.startTick));
        yield return Toils_Jump.JumpIf(ready, () => pawn.carryTracker.CarriedThing == job.targetA.Thing);
        yield return Toils_Jump.JumpIf(takeInventory, () => job.targetA.Thing.ParentHolder == pawn.inventory);
        yield return Toils_Jump.JumpIf(ground, () => job.targetA.Thing.Spawned);
        yield return Toils_General.Do(() =>
        {
            if (job.targetA.Thing.ParentHolder is not Pawn_InventoryTracker holder)
            { EndJobWith(JobCondition.Incompletable); return; }
            job.SetTarget(TargetIndex.C, holder.pawn);
        });
        yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch)
            .FailOnDespawnedNullOrForbidden(TargetIndex.C)
            .FailOn(() => NpcMedicalModel.TravelExpired(Find.TickManager.TicksGame, job.startTick)
                || job.targetA.Thing.ParentHolder is not Pawn_InventoryTracker holder || holder.pawn != job.targetC.Pawn);
        yield return takeInventory;
        yield return Toils_Jump.Jump(ready);
        yield return ground;
        yield return Toils_Haul.StartCarryThing(TargetIndex.A);
        yield return ready;
        // Native chewing and final ingestion preserve ingest ticks, interruption,
        // tolerance, addiction, overdose, genes and recipient social consequences.
        yield return Toils_Ingest.ChewIngestible(pawn, 1f, TargetIndex.A, TargetIndex.B)
            .FailOn(() => pawn.carryTracker.CarriedThing != job.targetA.Thing);
        Toil finalize = Toils_Ingest.FinalizeIngest(pawn, TargetIndex.A);
        finalize.AddPreInitAction(() => NpcMedicalAttempts.Applying(attemptJob));
        finalize.AddFinishAction(() => NpcMedicalAttempts.Applied(attemptJob, accepted: true));
        yield return finalize;
    }
}

public sealed class JobDriver_NpcPainAssist : JobDriver_UseMedicalDrug
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
        => (pawn.carryTracker.CarriedThing is null || pawn.carryTracker.CarriedThing == job.targetB.Thing)
            && base.TryMakePreToilReservations(errorOnFailed);

    protected override ThingDef DeviceDef => DeviceUsed!.def;
    protected override bool RequiresTreatment(Pawn patient) => NpcPainCare.Priority(patient) > 0f;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        AddFinishAction(_ => NpcMedicalReservations.RestockUnusedDrug(pawn, job, TargetIndex.B));
        this.FailOn(() => DeviceUsed is { Destroyed: false } dose && !NpcPainCare.SourceAllowed(pawn, Patient, dose));
        foreach (Toil toil in base.MakeNewToils()) { yield return toil; }
    }

    protected override bool ApplyDevice(Pawn doctor, Pawn patient, Thing? device)
    {
        if (device is null || !NpcPainCare.FinalCheck(doctor, patient, device)) { return false; }
        if (device.ParentHolder == doctor.inventory
            && !NpcMedicalReservations.TakeDrug(doctor, job, TargetIndex.B, device, doctor.inventory.innerContainer)) { return false; }
        Thing? dose = DeviceUsed;
        if (dose is null || dose != doctor.carryTracker.CarriedThing || dose.stackCount != 1) { return false; }
        MedicalDrugAdministration.Administer(doctor, patient, dose, clinicalPain: true);
        if (!dose.Destroyed) { return false; }
        if (ModsConfig.IdeologyActive)
        {
            Find.HistoryEventsManager.RecordEvent(new(HistoryEventDefOf.AdministeredDrug, doctor.Named(HistoryEventArgsNames.Doer)));
            if (dose.def.ingestible.drugCategory == DrugCategory.Hard)
            { Find.HistoryEventsManager.RecordEvent(new(HistoryEventDefOf.AdministeredHardDrug, doctor.Named(HistoryEventArgsNames.Doer))); }
            else if (dose.def.ingestible.drugCategory == DrugCategory.Social)
            { Find.HistoryEventsManager.RecordEvent(new(HistoryEventDefOf.AdministeredRecreationalDrug, doctor.Named(HistoryEventArgsNames.Doer))); }
        }
        return true;
    }
}
