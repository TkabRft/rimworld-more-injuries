using MoreInjuries.AI.Audio;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.Jobs;

public abstract class JobDriver_MedicalBase<TTarget> : JobDriver where TTarget : ThingWithComps
{
    protected const TargetIndex TARGET_INDEX = TargetIndex.A;
    protected const TargetIndex DEVICE_INDEX = TargetIndex.B;

    protected virtual int BaseTendDuration => 600;

    protected virtual float BaseExperience => 500f;

    protected Pawn Doctor => pawn;

    protected abstract TTarget GetTarget(ref readonly LocalTargetInfo targetInfo);

    protected virtual ISoundDefProvider<TTarget> SoundDefProvider => CachedSoundDefProvider.Of<TTarget>(SoundDefOf.Interact_Tend);

    protected virtual int CalculateTendDuration()
    {
        Pawn doctor = Doctor;
        float manipulationCapacity = Mathf.Max(doctor.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation), 0.05f);
        float medicalTendSpeed = doctor.GetStatValue(StatDefOf.MedicalTendSpeed);
        Logger.LogDebug($"Doctor {doctor.Name} has manipulation capacity of {manipulationCapacity} and medical tend speed of {medicalTendSpeed}");
        return MedicalTiming.Duration(doctor, BaseTendDuration);
    }

    protected abstract void FinalizeTreatment(Pawn doctor, TTarget target, Thing? thing);

    protected virtual Toil FinalizeTreatmentToil()
    {
        Toil toil = ToilMaker.MakeToil(nameof(FinalizeTreatment));
        toil.initAction = () =>
        {
            Pawn actor = toil.actor;
            Job applyingJob = actor.CurJob;
            Thing? thing = applyingJob.targetB.Thing;
            if (actor.skills is not null)
            {
                float gainFactor = thing?.def.MedicineTendXpGainFactor ?? 0.5f;
                actor.skills.Learn(SkillDefOf.Medicine, BaseExperience * gainFactor);
            }
            if (actor.CurJob != applyingJob) { return; }
            TTarget target = GetTarget(in applyingJob.targetA);
            FinalizeTreatment(actor, target, thing);
            if (actor.CurJob != applyingJob)
            {
                if (actor.CurJob is null && !ReferenceEquals(actor, target))
                {
                    Logger.Warning($"{nameof(FinalizeTreatment)}: Job is null after finalizing treatment. What's going on? Did our doctor die?");
                }
                else
                {
                    Logger.LogVerbose($"{nameof(FinalizeTreatment)}: The application ended or replaced its job; leaving the doctor's new job unchanged.");
                }
                return;
            }
            if (target is Pawn patient)
            { EmergencyCare.NotifyTreatmentCompleted(patient, applyingJob.def); }
            if (thing is { Destroyed: true })
            {
                // set null to prevent double-usage
                actor.CurJob.SetTarget(DEVICE_INDEX, LocalTargetInfo.Invalid);
            }

            if (!actor.CurJob.endAfterTendedOnce)
            {
                return;
            }

            actor.jobs.EndCurrentJob(JobCondition.Succeeded);
        };
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        return toil;
    }
}
