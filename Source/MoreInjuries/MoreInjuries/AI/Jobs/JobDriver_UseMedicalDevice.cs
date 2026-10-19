using MoreInjuries.Debug;
using MoreInjuries.Extensions;
using MoreInjuries.HealthConditions;
using MoreInjuries.Things;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.Jobs;

public abstract class JobDriver_UseMedicalDevice : JobDriver_MedicalBase<Pawn>
{
    private const int TICKS_BETWEEN_SELF_TEND_MOTES = 100;
    private const TargetIndex PATIENT_INDEX = TARGET_INDEX;
    private const TargetIndex DEVICE_HOLDER_INDEX = TargetIndex.C;

    protected PathEndMode _pathEndMode;
    protected bool _usesDevice;
    protected bool _fromInventoryOnly;
    protected bool _oneShot;
    protected bool _oneShotUsed;

    protected ExtendedJobParameters? Parameters => job.source as ExtendedJobParameters;

    protected abstract bool RequiresDevice { get; }

    protected abstract ThingDef DeviceDef { get; }

    protected Pawn Patient => job.targetA.Pawn;

    protected Thing? DeviceUsed => job.targetB.Thing;

    protected override Pawn GetTarget(ref readonly LocalTargetInfo targetInfo) => targetInfo.Pawn;

    protected abstract bool ApplyDevice(Pawn doctor, Pawn patient, Thing? device);

    protected virtual bool IsTreatable(Hediff hediff) => throw new NotSupportedException($"{nameof(IsTreatable)} must be overridden in {GetType().Name} to determine if a hediff is treatable with the device");

    protected virtual int GetMedicalDeviceCountToFullyHeal(Pawn patient) => MedicalDeviceHelper.GetMedicalDeviceCountToFullyHeal(patient, IsTreatable);

    private int GetRequiredDeviceCount(Pawn patient)
    {
        int total = GetMedicalDeviceCountToFullyHeal(patient);
        return _oneShot || job.endAfterTendedOnce || Parameters?.oneShot == true ? Math.Min(1, total) : total;
    }

    protected virtual bool RequiresTreatment(Pawn patient) => patient.health.hediffSet.hediffs.Any(IsTreatable);

    protected virtual bool MustReservePatient(Pawn doctor, Pawn patient) => true;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _usesDevice, "usesDevice");
        Scribe_Values.Look(ref _pathEndMode, "pathEndMode");
        Scribe_Values.Look(ref _fromInventoryOnly, "fromInventoryOnly");
        Scribe_Values.Look(ref _oneShot, "oneShot");
        Scribe_Values.Look(ref _oneShotUsed, "oneShotUsed");
    }

    public override void Notify_Starting()
    {
        base.Notify_Starting();
        // we abuse the automatically persisted job source field to pass additional parameters to our job driver
        // usually that field is used by ThinkNode_Duty, but that shouldn't give us any conflicts
        if (job.source is ExtendedJobParameters parameters)
        {
            _fromInventoryOnly = parameters.fromInventoryOnly;
            _oneShot = parameters.oneShot;
        }
        _oneShotUsed = false;
        _oneShot |= job.endAfterTendedOnce;
        _usesDevice = DeviceUsed is not null;
        if (Patient == Doctor)
        {
            _pathEndMode = PathEndMode.OnCell;
        }
        else if (Patient.InBed())
        {
            _pathEndMode = PathEndMode.InteractionCell;
        }
        else
        {
            _pathEndMode = PathEndMode.Touch;
        }
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        bool automatic = NpcCombatMedicine.NpcMedicalAttempts.Automatic(pawn, job);
        if (automatic) { NpcCombatMedicine.NpcMedicalAttempts.Begin(pawn, job, Patient, DeviceUsed); }
        bool reserved = (!automatic || NpcCombatMedicine.NpcMedicalReservations.DeliveryAllowed(pawn, Patient, DeviceUsed))
            && ReserveTreatment(errorOnFailed);
        if (automatic && !reserved)
        {
            NpcCombatMedicine.NpcMedicalAttempts.Finish(job, JobCondition.Incompletable);
            pawn.Map.reservationManager.ReleaseClaimedBy(pawn, job);
        }
        return reserved;
    }

    private bool ReserveTreatment(bool errorOnFailed)
    {
        Pawn doctor = Doctor;
        Pawn patient = Patient;
        if ((patient != doctor || NpcCombatMedicine.NpcMedicalAi.Managed(doctor))
            && !doctor.Reserve(Patient, job, errorOnFailed: errorOnFailed) && MustReservePatient(doctor, patient))
        {
            // we couldn't reserve the patient, so we can't do the job
            return false;
        }
        if (RequiresDevice && DeviceUsed is null)
        {
            // we need a device to do the job
            return false;
        }
        if (DeviceUsed is not null)
        {
            // we allow up to 10 pawns to concurrently reserve devices from the same stack (taken from JobDriver_TendPatient)
            int availableDevices = doctor.Map.reservationManager.CanReserveStack(doctor, DeviceUsed, maxPawns: MedicalDeviceHelper.MAX_MEDICAL_DEVICE_RESERVATIONS);
            int requiredDevices = GetRequiredDeviceCount(Patient);
            if (requiredDevices <= 0) { return false; }
            // attempt to reserve a device
            if (availableDevices >= 1 && doctor.Reserve(DeviceUsed, job, MedicalDeviceHelper.MAX_MEDICAL_DEVICE_RESERVATIONS,
                Mathf.Min(availableDevices, requiredDevices),
                errorOnFailed: errorOnFailed))
            {
                return true;
            }
            return false;
        }
        return true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        Pawn doctor = Doctor;
        Pawn patient = Patient;
        Job attemptJob = job;
        bool automatic = NpcCombatMedicine.NpcMedicalAttempts.Automatic(doctor, attemptJob);
        if (automatic)
        {
            NpcCombatMedicine.NpcMedicalAttempts.Begin(doctor, attemptJob, patient, DeviceUsed);
            AddFinishAction(condition =>
            {
                NpcCombatMedicine.NpcMedicalAttempts.Finish(attemptJob, condition);
                NpcCombatMedicine.NpcMedicalReservations.RestockSelected(doctor, attemptJob, DEVICE_INDEX);
            });
            this.FailOn(() => !NpcCombatMedicine.NpcMedicalReservations.DeliveryAllowed(doctor, patient, DeviceUsed));
        }
        this.FailOnDespawnedNullOrForbidden(PATIENT_INDEX);
        this.FailOn(() => doctor.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)
            || !doctor.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
            || !doctor.health.capacities.CapableOf(PawnCapacityDefOf.Sight)
            || !MedicalTiming.CanWork(doctor));
        this.FailOn(() => automatic && doctor != patient
            && (doctor.HostileTo(patient) || patient.playerSettings?.medCare == MedicalCareCategory.NoCare));
        this.FailOn(() =>
        {
            // we can't apply a device if the patient is set to no medical care
            if (patient.IsMutant && !patient.mutant.Def.entitledToMedicalCare
                || doctor.Faction.IsPlayerSafe() && patient.playerSettings?.medCare is MedicalCareCategory.NoCare
                // we can't apply a device if the doctor wants to tend himself but is set to no self-tend
                || doctor == patient && doctor.Faction.IsPlayerSafe() && doctor.playerSettings?.selfTend is false)
            {
                Logger.Warning($"Failed job {job.def} because of medical care restrictions");
                return true;
            }
            return false;
        });
        if (MustReservePatient(doctor, patient)
            && !(doctor == patient && NpcCombatMedicine.NpcMedicalAi.Managed(doctor) && NpcCombatMedicine.NpcMedicalAi.PrisonBreak(doctor)))
        {
            this.FailOnAggroMentalState(PATIENT_INDEX);
        }

        AddEndCondition(() =>
        {
            // if the job is a one-shot and it has been used
            if (_oneShot && _oneShotUsed)
            {
                return JobCondition.Succeeded;
            }
            // continue tending the patient if they have the target hediff ...
            if (RequiresTreatment(patient)
                // ... and the doctor is able to tend the patient ...
                && (doctor.Faction.IsPlayerSafe() && HealthAIUtility.ShouldEverReceiveMedicalCareFromPlayer(Patient)
                // or if the doctor is forced to tend the patient or belongs to a different faction (AI controlled)
                || job.playerForced || !doctor.Faction.IsPlayerSafe()))
            {
                return JobCondition.Ongoing;
            }
            Logger.LogDebug($"Finished applying {DeviceDef} to {patient.Name}");
            return JobCondition.Succeeded;
        });
        Toil? reserveDevices = null;
        Toil gotoPatientToil = Toils_Goto.Goto(PATIENT_INDEX, _pathEndMode);
        gotoPatientToil.FailOn(NpcTravelExpired);
        if (_usesDevice)
        {
            List<Toil> collectDeviceToils = CollectDevicesToils(doctor, patient, job, gotoPatientToil, out reserveDevices);
            foreach (Toil toil in collectDeviceToils)
            {
                yield return toil;
            }
        }
        gotoPatientToil.AddPreInitAction(() => { if (automatic) { NpcCombatMedicine.NpcMedicalAttempts.Stage(attemptJob, "delivery"); } });
        yield return gotoPatientToil;
        int ticks = CalculateTendDuration();
        Toil waitToil;
        if (doctor == patient)
        {
            waitToil = Toils_General.Wait(ticks: ticks);
        }
        else
        {
            Job? ownedPatientWait = null;
            PawnPosture previousPosture = default;
            waitToil = Toils_General.WaitWith(PATIENT_INDEX, ticks, maintainPosture: true, face: PATIENT_INDEX, pathEndMode: _pathEndMode);
            waitToil.initAction = () =>
            {
                if (automatic && (patient.IsColonistPlayerControlled || patient.CurJob?.playerForced == true
                    || NpcCombatMedicine.NpcMedicalCompatibility.SafetyOwned(patient))) { return; }
                Pawn doctorLocal = waitToil.actor;
                doctorLocal.pather.StopDead();
                if (waitToil.actor.CurJob.GetTarget(PATIENT_INDEX).Thing is not Pawn patientLocal)
                {
                    return;
                }
                previousPosture = patientLocal.jobs.posture;
                Job? previousJob = patientLocal.CurJob;
                PawnUtility.ForceWait(patientLocal, ticks, maintainPosture: true);
                if (patientLocal.CurJob != previousJob
                    && patientLocal.CurJob?.def is JobDef wait && (wait == JobDefOf.Wait || wait == JobDefOf.Wait_MaintainPosture))
                {
                    ownedPatientWait = patientLocal.CurJob;
                    if (!patientLocal.InBed()
                        && (!patientLocal.HostileTo(doctorLocal) || patientLocal.Downed)
                        && waitToil.actor.CurJobDef.GetModExtension<MedicalJobProperties_ModExtension>() is { ShouldEverBeTreatedFacingUp: true })
                    {
                        patientLocal.jobs.posture = PawnPosture.LayingOnGroundFaceUp;
                    }
                }
            };
            waitToil.AddFinishAction(() =>
            {
                if (ownedPatientWait is not null && patient.CurJob == ownedPatientWait)
                {
                    if (!patient.Downed && !patient.InBed()) { patient.jobs.posture = previousPosture; }
                    patient.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: true, canReturnToPool: true);
                }
            });
        }
        waitToil.WithProgressBarToilDelay(PATIENT_INDEX);
        SoundDef? soundDef = SoundDefProvider.GetSoundDef(doctor, patient, DeviceUsed);
        if (soundDef is not null)
        {
            waitToil.PlaySustainerOrSound(soundDef);
        }
        waitToil.activeSkill = () => SkillDefOf.Medicine;
        waitToil.handlingFacing = true;
        waitToil.tickAction = () =>
        {
            if (doctor == patient && !doctor.Faction.IsPlayerSafe() && doctor.IsHashIntervalTick(TICKS_BETWEEN_SELF_TEND_MOTES) && !doctor.Position.Fogged(doctor.Map))
            {
                // AI self-heal animation
                FleckMaker.ThrowMetaIcon(doctor.Position, doctor.Map, FleckDefOf.HealingCross, velocitySpeed: 0.42f);
            }
            if (doctor != patient)
            {
                doctor.rotationTracker.FaceTarget(patient);
            }
        };
        waitToil.FailOn(() =>
        {
            if (doctor == patient)
            {
                return false;
            }
            if (!doctor.CanReachImmediate(patient.SpawnedParentOrMe, _pathEndMode))
            {
                Logger.Warning($"Failed job {job.def} because {doctor} can't reach {patient}");
                return true;
            }
            return false;
        });
        yield return Toils_Jump.JumpIf(waitToil, () => !_usesDevice || DeviceUsed is not null and not { Destroyed: true }
            && (doctor.inventory.Contains(DeviceUsed) || automatic && doctor.carryTracker.CarriedThing == DeviceUsed));
        yield return Toils_MedicalDevice.PickupDevice(DEVICE_INDEX, patient, GetRequiredDeviceCount).FailOnDestroyedOrNull(DEVICE_INDEX);
        yield return waitToil;
        yield return FinalizeTreatmentToil();
        if (_usesDevice)
        {
            DebugAssert.IsNotNull(reserveDevices, $"{nameof(reserveDevices)} cannot be null when using a device");
            yield return FindMoreDevicesToil(doctor, patient, DEVICE_INDEX, job, reserveDevices!);
        }
        yield return Toils_Jump.Jump(gotoPatientToil);
    }

    protected override void FinalizeTreatment(Pawn doctor, Pawn target, Thing? thing)
    {
        Job applyingJob = job;
        NpcCombatMedicine.NpcMedicalAttempts.Applying(applyingJob);
        bool accepted = ApplyDevice(doctor, target, thing);
        NpcCombatMedicine.NpcMedicalAttempts.Applied(applyingJob, accepted);
        _oneShotUsed = true;
    }

    public override void Notify_DamageTaken(DamageInfo dinfo)
    {
        base.Notify_DamageTaken(dinfo);
        if (!dinfo.Def.ExternalViolenceFor(Doctor) || pawn.Faction.IsPlayerSafe() || Doctor != Patient)
        {
            return;
        }
        pawn.jobs.CheckForJobOverride();
    }

    private List<Toil> CollectDevicesToils(Pawn doctor, Pawn patient, Job job, Toil gotoToil, out Toil reserveDevices)
    {
        List<Toil> collect = [];
        Thing deviceUsed = job.targetB.Thing;
        Pawn_InventoryTracker? deviceHolderInventory = deviceUsed.ParentHolder as Pawn_InventoryTracker;

        reserveDevices = Toils_MedicalDevice.ReserveDevice(DEVICE_INDEX, patient, GetRequiredDeviceCount)
            .FailOnDespawnedNullOrForbidden(DEVICE_INDEX);
        collect.Add(Toils_Jump.JumpIf(gotoToil, () => deviceUsed is not null && (doctor.inventory.Contains(deviceUsed) || NpcCombatMedicine.NpcMedicalAttempts.Automatic(doctor, job) && doctor.carryTracker.CarriedThing == deviceUsed)));
        Toil jumpIfCarriedByOther = Toils_Goto.GotoThing(DEVICE_HOLDER_INDEX, PathEndMode.Touch)
            .FailOn(() => job.GetTarget(DEVICE_HOLDER_INDEX).Pawn is not Pawn holder
                || holder != deviceHolderInventory?.pawn || deviceUsed.ParentHolder != deviceHolderInventory || holder.IsForbidden(doctor))
            .FailOn(NpcTravelExpired);
        collect.Add(Toils_Haul.CheckItemCarriedByOtherPawn(deviceUsed, DEVICE_HOLDER_INDEX, jumpIfCarriedByOther));
        collect.Add(reserveDevices);
        collect.Add(Toils_Goto.GotoThing(DEVICE_INDEX, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(DEVICE_INDEX).FailOn(NpcTravelExpired));
        collect.Add(Toils_MedicalDevice.PickupDevice(DEVICE_INDEX, patient, GetRequiredDeviceCount)
            .FailOnDestroyedOrNull(DEVICE_INDEX));
        collect.Add(Toils_Haul.CheckForGetOpportunityDuplicate(reserveDevices, DEVICE_INDEX, TargetIndex.None, takeFromValidStorage: true));
        collect.Add(Toils_Jump.Jump(gotoToil));
        collect.Add(jumpIfCarriedByOther);
        collect.Add(Toils_General.Wait(25).WithProgressBarToilDelay(DEVICE_HOLDER_INDEX));
        collect.Add(Toils_Haul.TakeFromOtherInventory(
            deviceUsed,
            doctor.inventory.innerContainer,
            deviceHolderInventory?.innerContainer,
            GetRequiredDeviceCount(patient),
            DEVICE_INDEX));
        return collect;
    }

    private bool NpcTravelExpired() => (NpcCombatMedicine.NpcMedicalAi.Managed(pawn)
        || NpcCombatMedicine.NpcMedicalAttempts.Automatic(pawn, job))
        && NpcCombatMedicine.NpcMedicalModel.TravelExpired(Find.TickManager.TicksGame, job.startTick);

    private Toil FindMoreDevicesToil(Pawn doctor, Pawn patient, TargetIndex deviceIndex, Job job, Toil reserveDevice)
    {
        Toil toil = ToilMaker.MakeToil(nameof(FindMoreDevicesToil));
        toil.initAction = () =>
        {
            if (job.endAfterTendedOnce || _oneShot && _oneShotUsed) { return; }
            if (!job.GetTarget(deviceIndex).Thing.DestroyedOrNull())
            {
                return;
            }
            Thing? device = MedicalDeviceHelper.FindMedicalDevice(doctor, patient, DeviceDef, GetRequiredDeviceCount, _fromInventoryOnly);
            if (device is not null)
            {
                job.SetTarget(deviceIndex, device);
                doctor.jobs.curDriver.JumpToToil(reserveDevice);
            }
        };
        return toil;
    }

    [SuppressMessage(CODE_STYLE, STYLE_IDE1006_NAMING_STYLES, Justification = "XML serialization naming")]
    protected class ExtendedJobParameters : ILoadReferenceable, IExposable
    {
        public string? loadId;
        public bool fromInventoryOnly;
        public bool oneShot;

        public string GetUniqueLoadID() => loadId ??= this.GenerateUniqueLoadID();

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref loadId, "loadId");
            Scribe_Values.Look(ref fromInventoryOnly, "fromInventoryOnly");
            Scribe_Values.Look(ref oneShot, "oneShot");
        }

        public static T Create<T>(Pawn worker, bool fromInventoryOnly = false, bool oneShot = false) where T : ExtendedJobParameters, new()
        {
            T parameters = new()
            {
                fromInventoryOnly = fromInventoryOnly,
                oneShot = oneShot
            };
            parameters.loadId = parameters.GenerateUniqueLoadID();
            if (worker.TryGetComp(out MoreInjuryComp xmlSaveNode))
            {
                xmlSaveNode.PersistJobParameters(parameters);
            }
            else
            {
                Logger.Error($"Failed to get {nameof(MoreInjuryComp)} from {worker} as an XML save node for {typeof(T)}. This should never happen. Either a job was created for a non-humanoid pawn or the pawn is missing the comp.");
            }
            return parameters;
        }
    }
}
