using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

// Off-map carrying is deliberately stricter than local extraction or self-withdrawal.
internal static class NpcEvacuation
{
    internal enum Failure { None, Actor, Mode, Incapable, Patient, Cargo, RecentDamage, Safety, CloseHostile, Reservation, Route, ApproachBudget }
    internal static bool Enabled => MoreInjuriesMod.Settings.EnableNpcCombatMedicine
        && MoreInjuriesMod.Settings.NpcMedicalAiMode >= 2;

    internal static bool Eligible(Pawn carrier, Pawn patient) => Rejection(carrier, patient) == Failure.None;

    private static Failure Rejection(Pawn carrier, Pawn patient, bool ignoreRecent = false)
    {
        if (!carrier.Spawned || carrier.Map is null) { return Failure.Actor; }
        bool carrying = carrier.carryTracker.CarriedThing == patient;
        if (!carrying && !Enabled) { return Failure.Mode; }
        if (!NpcExtraction.Carrier(carrier)) { return Failure.Incapable; }
        if (!NpcExtraction.PatientAllowed(carrier, patient) || patient == carrier || patient.Faction != carrier.Faction)
        { return Failure.Patient; }
        if (!carrying && (!patient.Spawned || carrier.carryTracker.CarriedThing is not null && !NpcDeparture.ProspectiveHands(carrier)
            || (NpcDeparture.ProspectiveHands(carrier) ? carrier.carryTracker.MaxStackSpaceEver(patient.def)
                : carrier.carryTracker.AvailableStackSpace(patient.def)) < 1)) { return Failure.Cargo; }
        if (!ignoreRecent && (NpcMedicalSafety.Recent(carrier) || NpcMedicalSafety.Recent(patient))) { return Failure.RecentDamage; }
        if (NpcMedicalCompatibility.SafetyOwned(carrier) || NpcMedicalCompatibility.SafetyOwned(patient)) { return Failure.Safety; }
        if (carrier.Map.mapPawns.AllPawnsSpawned.Any(enemy => !enemy.Dead && !enemy.Downed && enemy.HostileTo(carrier)
            && (enemy.Position.InHorDistOf(carrier.Position, 8f) || enemy.Position.InHorDistOf(patient.PositionHeld, 8f))))
        { return Failure.CloseHostile; }
        return Failure.None;
    }

    internal static IEnumerable<IntVec3> Candidates(Pawn carrier)
    {
        int x = carrier.Map.Size.x - 1, z = carrier.Map.Size.z - 1;
        // The four nearest projections precede evenly spaced alternatives.
        IEnumerable<IntVec3> Points()
        {
            yield return new(0, 0, carrier.Position.z);
            yield return new(x, 0, carrier.Position.z);
            yield return new(carrier.Position.x, 0, 0);
            yield return new(carrier.Position.x, 0, z);
            for (int quarter = 1; quarter <= 3; quarter++)
            {
                yield return new(0, 0, z * quarter / 4);
                yield return new(x, 0, z * quarter / 4);
                yield return new(x * quarter / 4, 0, 0);
                yield return new(x * quarter / 4, 0, z);
            }
        }
        return Points().Distinct();
    }

    internal static bool Edge(Map map, IntVec3 cell) => cell.InBounds(map)
        && (cell.x == 0 || cell.z == 0 || cell.x == map.Size.x - 1 || cell.z == map.Size.z - 1);

    private static bool Path(Pawn carrier, NpcExtraction.Assessment assessment, IntVec3 start, IntVec3 end,
        PathEndMode mode, out float ticks, out IntVec3 arrival)
    {
        ticks = 0f; arrival = IntVec3.Invalid;
        PawnPath path = carrier.Map.pathFinder.FindPathNow(start, end, carrier, peMode: mode);
        try
        {
            if (!path.Found || path.NodesReversed.Count == 0) { return false; }
            foreach (IntVec3 cell in path.NodesReversed)
            {
                if (!assessment.At(cell, out bool allowed).Safe || !allowed) { return false; }
            }
            ticks = path.TotalCost; arrival = path.NodesReversed[0];
            return true;
        }
        finally { path.ReleaseToPool(); }
    }

    internal static bool TryExit(Pawn carrier, Pawn patient, out IntVec3 exit)
        => TryPlan(carrier, patient, out exit, out _);

    internal static bool TryPlan(Pawn carrier, Pawn patient, out IntVec3 exit, out Failure failure,
        bool departure = false, bool ignoreRecent = false)
    {
        exit = IntVec3.Invalid;
        void Report(string reason)
        {
            if (!ignoreRecent) { NpcMedicalDecision.Report(carrier, patient, MoreInjuriesMod.Settings.NpcMedicalAiMode, "exit route", reason); }
        }
        failure = Rejection(carrier, patient, ignoreRecent);
        if (failure != Failure.None) { Report(failure.ToString()); return false; }
        if (!NpcMedicalReservations.PatientAvailable(carrier, patient)) { failure = Failure.Reservation; Report(failure.ToString()); return false; }
        // Ignoring fading impacts is a wait-feasibility probe only. Drivers and
        // committed exits always call the strict, non-probing assessment.
        NpcExtraction.Assessment approach = new(carrier, carrier, carrier.Position, evacuation: true, ignoreImpacts: ignoreRecent);
        NpcExtraction.Assessment transport = new(carrier, patient, patient.PositionHeld, evacuation: true, ignoreImpacts: ignoreRecent);
        if (!transport.At(patient.PositionHeld, out bool allowed).Safe || !allowed
            || !Path(carrier, approach, carrier.Position, patient.Position, PathEndMode.Touch, out float approachTicks, out IntVec3 pickup))
        { failure = Failure.Route; Report("unsafe/unknown casualty position or approach"); return false; }
        if (departure && approachTicks > NpcMedicalModel.TRAVEL_TIMEOUT)
        { failure = Failure.ApproachBudget; Report(failure.ToString()); return false; }
        float best = float.PositiveInfinity;
        foreach (IntVec3 candidate in Candidates(carrier))
        {
            if (!Edge(carrier.Map, candidate) || !Path(carrier, transport, pickup, candidate, PathEndMode.OnCell,
                out float ticks, out _) || ticks >= best) { continue; }
            best = ticks; exit = candidate;
        }
        Report(exit.IsValid ? "safe route available" : "no checked safe exit route");
        failure = exit.IsValid ? Failure.None : Failure.Route;
        return exit.IsValid;
    }

    internal static bool RouteValid(Pawn carrier, Job job)
    {
        if (job.targetA.Pawn is not Pawn patient || !Eligible(carrier, patient) || !Edge(carrier.Map, job.targetB.Cell)) { return false; }
        NpcExtraction.Assessment transport = new(carrier, patient, patient.PositionHeld, evacuation: true);
        IntVec3 pickup = carrier.Position;
        if (carrier.carryTracker.CarriedThing != patient)
        {
            NpcExtraction.Assessment approach = new(carrier, carrier, carrier.Position, evacuation: true);
            if (!transport.At(patient.Position, out bool allowed).Safe || !allowed
                || !Path(carrier, approach, carrier.Position, patient.Position, PathEndMode.Touch, out float approachTicks, out pickup)
                || NpcDeparture.IsWork(job) && approachTicks > NpcMedicalModel.TRAVEL_TIMEOUT) { return false; }
        }
        return Path(carrier, transport, pickup, job.targetB.Cell, PathEndMode.OnCell, out _, out _);
    }

    internal static bool StepAllowed(Pawn carrier, Job job)
    {
        if (!carrier.pather.MovingNow || !carrier.pather.nextCell.IsValid) { return true; }
        Pawn patient = carrier.carryTracker.CarriedThing == job.targetA.Thing ? job.targetA.Pawn : carrier;
        NpcExtraction.Assessment assessment = new(carrier, patient, patient.PositionHeld, evacuation: true);
        return assessment.At(carrier.pather.nextCell, out bool allowed).Safe && allowed;
    }
}
