using System.Collections.Generic;
using MoreInjuries.Defs.WellKnown;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

public sealed class JobDriver_NpcExtract : JobDriver
{
    private int _nextSafetyCheck;
    private bool _destinationValid = true;
    private bool DestinationStillValid(Job mission)
    {
        int now = Find.TickManager.TicksGame;
        if (now < _nextSafetyCheck) { return _destinationValid; }
        _nextSafetyCheck = now + 60;
        return _destinationValid = NpcExtraction.DestinationValid(pawn, mission);
    }
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        NpcMedicalAttempts.Begin(pawn, job, job.targetA.Pawn, null);
        bool reserved = (NpcExtraction.Enabled || pawn.carryTracker.CarriedThing == job.targetA.Thing)
            && NpcExtraction.PatientAllowed(pawn, job.targetA.Pawn)
            && NpcMedicalReservations.PatientAvailable(pawn, job.targetA.Pawn)
            && NpcExtraction.DestinationValid(pawn, job)
            && (pawn.carryTracker.CarriedThing is null || pawn.carryTracker.CarriedThing == job.targetA.Thing)
            && pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed)
            && pawn.Reserve(job.targetB, job, errorOnFailed: errorOnFailed);
        if (!reserved)
        {
            NpcMedicalAttempts.Finish(job, JobCondition.Incompletable);
            pawn.Map.reservationManager.ReleaseClaimedBy(pawn, job);
        }
        return reserved;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        Job mission = job;
        Pawn patient = mission.targetA.Pawn;
        this.FailOn(() => !NpcDeparture.Continue(pawn, mission));
        NpcMedicalAttempts.Begin(pawn, mission, patient, null);
        this.FailOn(() => NpcMedicalSafety.YieldToExplosive(pawn, mission));
        AddFinishAction(condition =>
        {
            if (NpcExtraction.Suspending(pawn)) { return; }
            NpcMedicalAttempts.Finish(mission, condition);
            // Only the selected patient, never unrelated carried cargo.
            if (pawn.Spawned && pawn.carryTracker.CarriedThing == patient)
            { pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _); }
        });
        this.FailOn(() => !NpcExtraction.PatientAllowed(pawn, patient) || !NpcExtraction.Carrier(pawn)
            || !DestinationStillValid(mission) || !NpcExtraction.StepAllowed(pawn, mission)
            || pawn.carryTracker.CarriedThing is Thing carried && carried != patient);
        Toil carrying = Toils_General.Do(() =>
        {
            if (pawn.carryTracker.CarriedThing != patient) { EndJobWith(JobCondition.Incompletable); return; }
            NpcExtraction.CommitCarry(pawn, mission);
            NpcMedicalAttempts.Stage(mission, "carrying patient");
        });
        // Stable indices for initial collection and a carried patient after load.
        yield return Toils_Jump.JumpIf(carrying, () => pawn.carryTracker.CarriedThing == patient);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedNullOrForbidden(TargetIndex.A)
            .FailOn(() => NpcMedicalModel.TravelExpired(Find.TickManager.TicksGame, mission.startTick));
        yield return Toils_General.Do(() =>
        {
            if (!NpcDeparture.Continue(pawn, mission) || !NpcExtraction.PickupValid(pawn, mission) || !NpcMedicalReservations.PatientAvailable(pawn, patient)
                || !pawn.CanReachImmediate(patient, PathEndMode.Touch) || !patient.Spawned
                || pawn.carryTracker.AvailableStackSpace(patient.def) < 1)
            { EndJobWith(JobCondition.Incompletable); return; }
            mission.count = 1;
        });
        yield return Toils_Haul.StartCarryThing(TargetIndex.A);
        yield return carrying;
        yield return Toils_General.Do(() =>
        {
            NpcExtraction.Assessment assessment = new(pawn, patient, mission.targetC.Cell);
            if (!assessment.Path(pawn.Position, mission.targetB.Cell, PathEndMode.OnCell, out _, out _))
            { EndJobWith(JobCondition.Incompletable); }
        });
        yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell)
            .FailOn(() => NpcMedicalModel.TravelExpired(Find.TickManager.TicksGame, mission.startTick));
        yield return Toils_General.Do(() =>
        {
            if (pawn.carryTracker.CarriedThing != patient || pawn.Position != mission.targetB.Cell
                || !NpcExtraction.DestinationValid(pawn, mission)
                || !pawn.carryTracker.TryDropCarriedThing(mission.targetB.Cell, ThingPlaceMode.Direct, out Thing placed)
                || placed != patient || !patient.Spawned || patient.Position != mission.targetB.Cell)
            { EndJobWith(JobCondition.Incompletable); return; }
            NpcExtraction.Arrived(pawn, mission);
            NpcMedicalAttempts.Stage(mission, "treatment handoff");
        });
        yield return Toils_General.Do(() =>
        {
            Job? treatment = NpcMedicalAi.Handoff(pawn, patient);
            if (treatment is null) { EndJobWith(JobCondition.Succeeded); return; }
            // Reserve the next application before releasing transport ownership.
            if (!NpcMedicalReservations.PrepareTacticalJob(pawn, treatment))
            { EndJobWith(JobCondition.Succeeded); return; }
            if (!NpcDeparture.Handoff(pawn, mission, treatment))
            {
                pawn.Map.reservationManager.ReleaseClaimedBy(pawn, treatment);
                NpcMedicalAttempts.Finish(treatment, JobCondition.InterruptOptional);
                NpcQuickAid.Finish(treatment, treatment.loadID, treatment.def);
                EndJobWith(JobCondition.Succeeded); return;
            }
            pawn.jobs.StartJob(treatment, JobCondition.Succeeded);
        });
    }
}
