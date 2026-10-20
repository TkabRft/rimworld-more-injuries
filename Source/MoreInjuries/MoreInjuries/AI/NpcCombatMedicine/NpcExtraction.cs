using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static class NpcExtraction
{
    private sealed class PatientState
    {
        internal readonly NpcExtractionLimits Limits = new();
        internal int ClearedAt = -1, ArrivedAt = -1;
        internal Map? Map;
        internal IntVec3 Cell;
    }
    private sealed class Mission
    {
        internal Job? Extraction, Stabilization;
        internal int ExtractionId, StabilizationId;
        internal bool Suspending;
    }
    private static readonly ConditionalWeakTable<Pawn, PatientState> s_patients = new();
    private static readonly ConditionalWeakTable<Pawn, Mission> s_missions = new();
    internal static bool Enabled => NpcExtractionModel.Enabled(MoreInjuriesMod.Settings.EnableNpcCombatMedicine, MoreInjuriesMod.Settings.NpcMedicalAiMode);
    internal static bool Carrier(Pawn pawn) => NpcMedicalAi.ActorEligible(pawn)
        && NpcExtractionModel.Carrier(pawn.Downed, pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving),
            pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation), NpcMedicalAi.Urgency(pawn));
    internal static bool PatientAllowed(Pawn carrier, Pawn patient) => !patient.Dead && patient.Downed
        && patient.MapHeld == carrier.Map && NpcMedicalAi.Relation(carrier, patient) >= 0
        && patient.playerSettings?.medCare != MedicalCareCategory.NoCare;

    internal sealed class Assessment
    {
        private readonly Pawn _carrier, _patient;
        private readonly IntVec3 _origin;
        private readonly List<Thing> _threats;
        internal Assessment(Pawn carrier, Pawn patient, IntVec3 origin)
        {
            _carrier = carrier; _patient = patient; _origin = origin;
            _threats = GenRadial.RadialDistinctThingsAround(origin, carrier.Map, 35f, true)
                .Where(thing => thing.HostileTo(carrier) && (thing is Pawn enemy && !enemy.Downed && !enemy.Dead
                    || thing is Building_Turret)).Take(16).ToList();
        }
        internal NpcExtractionRisk At(IntVec3 cell, out bool allowed)
        {
            Map map = _carrier.Map;
            allowed = NpcExtractionEnvironment.Boundary(_carrier, _patient, _origin, cell);
            float thermal = NpcExtractionEnvironment.Thermal(_carrier, _patient, _origin, cell, out bool known, out bool thermalAllowed);
            allowed &= thermalAllowed;
            float combat = NpcExtractionEnvironment.CeDanger(map, cell, out bool ceKnown);
            known &= ceKnown;
            foreach (Thing threat in _threats)
            {
                if (!threat.Spawned || threat.Destroyed) { continue; }
                if (threat.Position.InHorDistOf(cell, 4f)) { combat = Math.Max(combat, 2f); }
                else if (GenSight.LineOfSight(threat.Position, cell, map))
                { combat = Math.Max(combat, 1f - CoverUtility.CalculateOverallBlockChance(cell, threat.Position, map)); }
            }
            float fire = cell.ContainsStaticFire(map) ? 2f
                : GenRadial.RadialCellsAround(cell, 1.5f, false).Any(next => next.InBounds(map) && next.ContainsStaticFire(map)) ? .5f : 0f;
            return new(combat, NpcExtractionEnvironment.RecentImpact(map, cell), fire, thermal, known);
        }
        internal bool Path(IntVec3 start, IntVec3 end, PathEndMode mode, out float ticks, out float exposure, bool carrying = true)
        {
            ticks = exposure = 0f;
            PawnPath path = _carrier.Map.pathFinder.FindPathNow(start, end, _carrier, peMode: mode);
            try
            {
                if (!path.Found || path.TotalCost > NpcExtractionModel.TRAVEL_BUDGET) { return false; }
                ticks = path.TotalCost;
                foreach (IntVec3 cell in path.NodesReversed)
                {
                    // A path may leave a burning origin; never walk into another
                    // burning/forbidden/confinement-violating cell to rescue.
                    NpcExtractionRisk risk = At(cell, out bool allowed);
                    // Before pickup, the casualty stays at its origin. Only the
                    // approaching actor crosses these cells and room boundaries.
                    if (!carrying) { allowed = NpcExtractionEnvironment.Boundary(_carrier, _carrier, _carrier.Position, cell); }
                    if (cell != start && (!allowed || risk.Combat >= 2f)) { return false; }
                    exposure += risk.Worst;
                }
                return true;
            }
            finally { path.ReleaseToPool(); }
        }
    }

    internal static Job? Consider(Pawn carrier, Pawn patient, out bool unsafePatient)
    {
        unsafePatient = false;
        if (!Enabled || patient == carrier || !PatientAllowed(carrier, patient)) { return null; }
        Assessment assessment = new(carrier, patient, patient.Position);
        NpcExtractionRisk origin = assessment.At(patient.Position, out _);
        unsafePatient = origin.Hazard;
        if (!unsafePatient || !Carrier(carrier) || carrier.carryTracker.CarriedThing is not null
            || carrier.carryTracker.AvailableStackSpace(patient.def) < 1
            || !NpcMedicalReservations.PatientAvailable(carrier, patient)
            || !NpcMedicalAttempts.ExtractionAllowed(carrier, patient)) { return null; }
        PatientState state = s_patients.GetOrCreateValue(patient);
        int now = Find.TickManager.TicksGame;
        if (!state.Limits.CanRelocate(now, origin.Fire > 0f || origin.Combat >= 2f || origin.Thermal >= 1f
            || state.ArrivedAt >= 0 && NpcMedicalSafety.ImpactAfter(patient, state.ArrivedAt + 1))) { return null; }
        if (!assessment.Path(carrier.Position, patient.Position, PathEndMode.Touch, out float approach, out float approachRisk, carrying: false)) { return null; }
        var candidates = GenRadial.RadialCellsAround(patient.Position, 12f, false)
            .Where(cell => cell.InBounds(carrier.Map) && cell != patient.Position)
            .Select(cell => (Cell: cell, Risk: assessment.At(cell, out bool allowed), Allowed: allowed))
            .Where(candidate => candidate.Allowed && candidate.Risk.Combat < 2f)
            .OrderBy(candidate => candidate.Risk.Worst).ThenBy(candidate => candidate.Risk.Total)
            .ThenBy(candidate => candidate.Cell.DistanceToSquared(patient.Position)).Take(16).ToList();
        IntVec3 destination = IntVec3.Invalid;
        NpcExtractionRisk selected = default;
        float bestRoute = float.PositiveInfinity, bestTicks = float.PositiveInfinity;
        foreach (var candidate in candidates)
        {
            bool better = candidate.Risk.Known && candidate.Risk.BetterThan(origin);
            if (!better && (!state.Limits.CanFallback(now) || candidate.Risk.Worst > origin.Worst + .02f
                || candidate.Risk.Total >= origin.Total - .01f)) { continue; }
            if (!carrier.CanReserve(candidate.Cell) || !assessment.Path(patient.Position, candidate.Cell, PathEndMode.OnCell,
                out float travel, out float exposure) || approach + travel > NpcExtractionModel.TRAVEL_BUDGET) { continue; }
            if (destination.IsValid && (candidate.Risk.Worst > selected.Worst + .02f
                || Math.Abs(candidate.Risk.Worst - selected.Worst) <= .02f && candidate.Risk.Total > selected.Total + .01f)) { continue; }
            if (destination.IsValid && candidate.Risk == selected && (exposure + approachRisk > bestRoute
                || exposure + approachRisk == bestRoute && approach + travel >= bestTicks)) { continue; }
            destination = candidate.Cell; selected = candidate.Risk; bestRoute = exposure + approachRisk; bestTicks = approach + travel;
        }
        if (!destination.IsValid) { return null; }
        Job extraction = JobMaker.MakeJob(KnownJobDefOf.MI_NpcExtract, patient, destination, patient.Position);
        extraction.count = 1;
        Mission mission = s_missions.GetOrCreateValue(carrier);
        mission.Extraction = extraction; mission.ExtractionId = extraction.loadID;
        // One bounded critical round may precede this committed transport.
        if (state.Limits.CanPrepare(now) && NpcMedicalGeneration.CanDoctor(carrier) && (NpcMedicalAi.Urgency(patient) >= 90f
            || patient.health.hediffSet.BleedRateTotal >= .5f
                && HealthUtility.TicksUntilDeathDueToBloodLoss(patient) <= bestTicks + 150f))
        {
            Job? immediate = NpcMedicalAi.ImmediateStabilization(carrier, patient);
            if (immediate is not null)
            { state.Limits.Prepare(now); mission.Stabilization = immediate; mission.StabilizationId = immediate.loadID; return immediate; }
        }
        mission.Stabilization = null;
        return extraction;
    }

    internal static Job? Resume(Pawn carrier)
    {
        if (!Enabled || !s_missions.TryGetValue(carrier, out Mission? mission) || mission.Stabilization is not Job previous
            || mission.Extraction is not Job extraction || extraction.loadID != mission.ExtractionId) { return null; }
        if (carrier.CurJob == previous && previous.loadID == mission.StabilizationId) { return null; }
        mission.Stabilization = null;
        return Carrier(carrier) && PatientAllowed(carrier, extraction.targetA.Pawn) && DestinationValid(carrier, extraction)
            && carrier.carryTracker.CarriedThing is null ? extraction : null;
    }

    internal static bool PickupValid(Pawn carrier, Job job)
    {
        Pawn patient = job.targetA.Pawn;
        if (patient.Position != job.targetC.Cell || !DestinationValid(carrier, job)) { return false; }
        Assessment assessment = new(carrier, patient, job.targetC.Cell);
        return assessment.Path(carrier.Position, patient.Position, PathEndMode.Touch, out float approach, out _, carrying: false)
            && assessment.Path(patient.Position, job.targetB.Cell, PathEndMode.OnCell, out float travel, out _)
            && approach + travel <= NpcExtractionModel.TRAVEL_BUDGET;
    }

    internal static bool DestinationValid(Pawn carrier, Job job)
    {
        if (!PatientAllowed(carrier, job.targetA.Pawn) || !Carrier(carrier)) { return false; }
        Assessment assessment = new(carrier, job.targetA.Pawn, job.targetC.Cell);
        NpcExtractionRisk risk = assessment.At(job.targetB.Cell, out bool allowed);
        NpcExtractionRisk origin = assessment.At(job.targetC.Cell, out _);
        return allowed && risk.Combat < 2f && risk.Worst <= Math.Max(origin.Worst, .15f) + .02f;
    }

    internal static void CommitCarry(Pawn carrier, Job job)
    {
        Assessment assessment = new(carrier, job.targetA.Pawn, job.targetC.Cell);
        NpcExtractionRisk destination = assessment.At(job.targetB.Cell, out _), origin = assessment.At(job.targetC.Cell, out _);
        if (!destination.Known || !destination.BetterThan(origin))
        { s_patients.GetOrCreateValue(job.targetA.Pawn).Limits.Fallback(Find.TickManager.TicksGame); }
    }

    internal static bool StepAllowed(Pawn carrier, Job job)
        => !carrier.pather.MovingNow || !carrier.pather.nextCell.IsValid
            || NpcExtractionEnvironment.Boundary(carrier, job.targetA.Pawn, job.targetC.Cell, carrier.pather.nextCell);

    internal static void Arrived(Pawn carrier, Job job)
    {
        Pawn patient = job.targetA.Pawn;
        PatientState state = s_patients.GetOrCreateValue(patient);
        Assessment assessment = new(carrier, patient, job.targetC.Cell);
        NpcExtractionRisk risk = assessment.At(patient.Position, out bool allowed);
        state.Map = carrier.Map; state.Cell = patient.Position;
        state.ArrivedAt = Find.TickManager.TicksGame;
        state.Limits.Delivered(Find.TickManager.TicksGame);
        state.ClearedAt = allowed && risk.Safe ? Find.TickManager.TicksGame : -1;
        NpcMedicalAttempts.Progress(job);
    }
    internal static bool Cleared(Pawn carrier, Pawn patient)
    {
        if (!s_patients.TryGetValue(patient, out PatientState? state) || state.ClearedAt < 0
            || state.Map != carrier.Map || patient.Position != state.Cell || Find.TickManager.TicksGame >= state.Limits.HoldUntil
            || NpcMedicalSafety.ImpactAfter(carrier, state.ClearedAt) || NpcMedicalSafety.ImpactAfter(patient, state.ClearedAt)) { return false; }
        return new Assessment(carrier, patient, patient.Position).At(patient.Position, out bool allowed).Safe && allowed;
    }

    internal static Job? Active(Pawn carrier)
    {
        if (carrier.CurJob is Job current && current.def == KnownJobDefOf.MI_NpcExtract) { return current; }
        // Native queued jobs provide save/load recovery without saved MI history.
        return carrier.jobs.jobQueue.FirstOrDefault(queued => queued.job.def == KnownJobDefOf.MI_NpcExtract
            && queued.job.targetA.Thing == carrier.carryTracker.CarriedThing)?.job;
    }
    internal static bool CanCarryThroughSafety(Pawn carrier, Pawn patient, Job extraction, Job safety)
    {
        if (!safety.targetA.IsValid) { return false; }
        Assessment assessment = new(carrier, patient, extraction.targetC.Cell);
        return NpcExtractionEnvironment.Boundary(carrier, patient, extraction.targetC.Cell, safety.targetA.Cell)
            && assessment.Path(carrier.Position, safety.targetA.Cell, PathEndMode.OnCell, out _, out _);
    }

    internal static bool Owns(Pawn carrier) => Carrier(carrier) && Active(carrier) is not null;
    internal static bool Suspending(Pawn carrier) => s_missions.TryGetValue(carrier, out Mission? state) && state.Suspending;
    internal static void Suspension(Pawn carrier, bool value) => s_missions.GetOrCreateValue(carrier).Suspending = value;
}
