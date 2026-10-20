using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.Extensions;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.Jobs;

public sealed class JobDriver_ProvideFirstAid : JobDriver
{
    private const TargetIndex PATIENT_INDEX = TargetIndex.A;

    private Pawn Doctor => pawn;

    private Pawn Patient => job.targetA.Pawn;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if (Patient != Doctor && !Doctor.Reserve(Patient, job, errorOnFailed: errorOnFailed))
        {
            // we couldn't reserve the patient, so we can't do the job
            return false;
        }
        return true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(PATIENT_INDEX);
        this.FailOnAggroMentalState(PATIENT_INDEX);
        this.FailOn(() => !Doctor.Drafted || Patient.Dead || Patient.IsActivelyHostileTo(Doctor));
        Job mission = job;
        int identity = mission.loadID;
        JobDef action = mission.def;
        bool Current() => Doctor.CurJob == mission && mission.loadID == identity && mission.def == action;
        AddFinishAction(_ =>
        {
            if (mission.loadID == identity && mission.def == action)
            { NpcMedicalReservations.RestockUnusedMedicine(Doctor, mission); }
        });

        Toil toil = ToilMaker.MakeToil("TryScanForInjuries");
        toil.initAction = () =>
        {
            if (!Current()) { return; }
            Job? next = EmergencyCare.CreateJob(Doctor, Patient, aftercare: true);
            if (next?.def == JobDefOf.TendPatient)
            {
                if (!FirstAidTending.Prepare(Doctor, Patient, mission)) { EndJobWith(JobCondition.Incompletable); }
                return;
            }
            if (next is null) { EndJobWith(JobCondition.Succeeded); return; }
            next.endAfterTendedOnce = true;
            if (!StartJobAndScheduleScan(Doctor, Patient, next)) { EndJobWith(JobCondition.Incompletable); }
        };
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        // A rejected application must not create an unbounded chain of instant scans in one tick.
        // Keep the original wait/scan indices. Append one fixed field-tending graph.
        Toil rescan = Toils_General.Wait(1);
        yield return rescan;
        yield return toil;
        Toil ready = Toils_General.Do(() =>
        {
            Thing? medicine = mission.targetB.Thing;
            if (!FirstAidTending.DeliveryAllowed(Doctor, Patient, mission)
                || medicine is not null && Doctor.carryTracker.CarriedThing != medicine)
            { EndJobWith(JobCondition.Incompletable); }
        });
        Toil takeInventory = Toils_General.Do(() =>
        {
            Thing medicine = mission.targetB.Thing;
            if (Doctor.carryTracker.CarriedThing == medicine && FirstAidTending.DeliveryAllowed(Doctor, Patient, mission)) { return; }
            if (!FirstAidTending.DeliveryAllowed(Doctor, Patient, mission)
                || medicine.ParentHolder is not Pawn_InventoryTracker holder
                || holder.pawn != Doctor && !Doctor.CanReachImmediate(holder.pawn, PathEndMode.Touch)
                || !NpcMedicalReservations.TakeMedicine(Doctor, mission, medicine, holder.innerContainer))
            { EndJobWith(JobCondition.Incompletable); }
        });
        yield return Toils_Jump.JumpIf(ready, () => mission.targetB.Thing is null || mission.targetB.Thing == Doctor.carryTracker.CarriedThing);
        yield return Toils_Jump.JumpIf(takeInventory, () => mission.targetC.Pawn == Doctor);
        Toil ground = Travel(TargetIndex.B, PathEndMode.ClosestTouch);
        yield return Toils_Jump.JumpIf(ground, () => mission.targetC.Thing is null);
        yield return Travel(TargetIndex.C, PathEndMode.Touch);
        yield return takeInventory;
        yield return Toils_Jump.Jump(ready);
        yield return ground;
        yield return Toils_General.Do(() =>
        {
            if (!FirstAidTending.DeliveryAllowed(Doctor, Patient, mission)) { EndJobWith(JobCondition.Incompletable); }
        });
        yield return Toils_MedicalDevice.PickupDevice(TargetIndex.B, Patient, _ => 1);
        yield return ready;
        yield return Travel(PATIENT_INDEX, PathEndMode.Touch);
        Toil wait = Toils_General.Wait(MedicalTiming.Duration(Doctor, 600))
            .WithProgressBarToilDelay(PATIENT_INDEX).PlaySustainerOrSound(SoundDefOf.Interact_Tend);
        wait.activeSkill = () => SkillDefOf.Medicine;
        wait.FailOn(() => !FirstAidTending.DeliveryAllowed(Doctor, Patient, mission)
            || !Doctor.CanReachImmediate(Patient, PathEndMode.Touch));
        yield return wait;
        Toil finalize = Toils_Tend.FinalizeTend(Patient);
        System.Action nativeFinalize = finalize.initAction;
        finalize.initAction = () =>
        {
            if (!Current()) { return; }
            if (!FirstAidTending.DeliveryAllowed(Doctor, Patient, mission)
                || !Doctor.CanReachImmediate(Patient, PathEndMode.Touch))
            { EndJobWith(JobCondition.Incompletable); return; }
            List<Hediff> untreated = Patient.health.hediffSet.hediffs.Where(injury => injury.TendableNow()).ToList();
            nativeFinalize();
            if (!Current()) { return; }
            NpcMedicalReservations.RestockUnusedMedicine(Doctor, mission);
            if (untreated.Count > 0 && untreated.All(injury => Patient.health.hediffSet.hediffs.Contains(injury) && injury.TendableNow()))
            { EndJobWith(JobCondition.Incompletable); return; }
            if (mission.targetB.Thing is Thing selected) { Doctor.Map.reservationManager.Release(selected, Doctor, mission); }
            mission.SetTarget(TargetIndex.B, LocalTargetInfo.Invalid);
            mission.SetTarget(TargetIndex.C, LocalTargetInfo.Invalid);
        };
        yield return finalize;
        yield return Toils_Jump.Jump(rescan);
    }

    private Toil Travel(TargetIndex target, PathEndMode end)
        => Toils_Goto.GotoThing(target, end).FailOnDespawnedNullOrForbidden(target)
            .FailOn(() => !FirstAidTending.DeliveryAllowed(Doctor, Patient, job));

    private static bool StartJobAndScheduleScan(Pawn doctor, Pawn patient, Job job)
    {
        if (!doctor.jobs.TryTakeOrderedJob(job, requestQueueing: false)) { return false; }
        Job scan = new JobDescriptor(doctor, patient).CreateJob();
        doctor.jobs.TryTakeOrderedJob(scan, requestQueueing: true);
        return true;
    }

    public static IJobDescriptor GetDispatcher(Pawn doctor, Pawn patient) => new JobDescriptor(doctor, patient);

    private readonly struct JobDescriptor(Pawn doctor, Pawn patient) : IJobDescriptor
    {
        public readonly Job CreateJob()
        {
            Job job = JobMaker.MakeJob(KnownJobDefOf.ProvideFirstAid, patient);
            job.count = 1;
            return job;
        }

        public readonly void StartJob()
        {
            Job job = CreateJob();
            doctor.jobs.TryTakeOrderedJob(job);
        }
    }
}
