using MoreInjuries.Defs.WellKnown;
using System.Collections.Generic;
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

        Toil toil = ToilMaker.MakeToil(nameof(TryScanForInjuries));
        toil.initAction = () =>
        {
            if (!TryScanForInjuries())
            {
                EndJobWith(JobCondition.Succeeded);
            }
        };
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        return [toil];
    }

    private bool TryScanForInjuries()
    {
        Job? next = EmergencyCare.CreateJob(Doctor, Patient, aftercare: true);
        if (next is not null) { next.endAfterTendedOnce = true; }
        return next is not null && StartJobAndScheduleScan(Doctor, Patient, next);
    }

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
