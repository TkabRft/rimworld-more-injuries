using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

// Off-map carrying is deliberately stricter than local extraction or self-withdrawal.
internal static class NpcEvacuation
{
    internal static bool Enabled => MoreInjuriesMod.Settings.EnableNpcCombatMedicine
        && MoreInjuriesMod.Settings.NpcMedicalAiMode >= 2;

    internal static bool Eligible(Pawn carrier, Pawn patient) => Rejection(carrier, patient) is null;

    private static string? Rejection(Pawn carrier, Pawn patient)
    {
        if (!carrier.Spawned || carrier.Map is null) { return "carrier not on a map"; }
        bool carrying = carrier.carryTracker.CarriedThing == patient;
        if (!carrying && !Enabled) { return "mode or disabled module prevents new pickup"; }
        if (!NpcExtraction.Carrier(carrier)) { return "incapable carrier"; }
        if (!NpcExtraction.PatientAllowed(carrier, patient) || patient == carrier || patient.Faction != carrier.Faction)
        { return "patient no longer eligible"; }
        if (!carrying && (!patient.Spawned || carrier.carryTracker.CarriedThing is not null
            || carrier.carryTracker.AvailableStackSpace(patient.def) < 1)) { return "carrying hands or patient unavailable"; }
        if (NpcMedicalSafety.Recent(carrier) || NpcMedicalSafety.Recent(patient)) { return "recent incoming damage"; }
        if (NpcMedicalCompatibility.SafetyOwned(carrier) || NpcMedicalCompatibility.SafetyOwned(patient)) { return "native safety work"; }
        if (carrier.Map.mapPawns.AllPawnsSpawned.Any(enemy => !enemy.Dead && !enemy.Downed && enemy.HostileTo(carrier)
            && (enemy.Position.InHorDistOf(carrier.Position, 8f) || enemy.Position.InHorDistOf(patient.PositionHeld, 8f))))
        { return "hostile within eight cells"; }
        return null;
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
    {
        exit = IntVec3.Invalid;
        void Report(string reason) => NpcMedicalDecision.Report(carrier, patient, MoreInjuriesMod.Settings.NpcMedicalAiMode, "exit route", reason);
        if (Rejection(carrier, patient) is string refusal) { Report(refusal); return false; }
        if (!NpcMedicalReservations.PatientAvailable(carrier, patient)) { Report("patient reservation unavailable"); return false; }
        NpcExtraction.Assessment approach = new(carrier, carrier, carrier.Position, evacuation: true);
        NpcExtraction.Assessment transport = new(carrier, patient, patient.PositionHeld, evacuation: true);
        if (!transport.At(patient.PositionHeld, out bool allowed).Safe || !allowed
            || !Path(carrier, approach, carrier.Position, patient.Position, PathEndMode.Touch, out _, out IntVec3 pickup))
        { Report("unsafe/unknown casualty position or approach"); return false; }
        float best = float.PositiveInfinity;
        foreach (IntVec3 candidate in Candidates(carrier))
        {
            if (!Edge(carrier.Map, candidate) || !Path(carrier, transport, pickup, candidate, PathEndMode.OnCell,
                out float ticks, out _) || ticks >= best) { continue; }
            best = ticks; exit = candidate;
        }
        Report(exit.IsValid ? "safe route available" : "no checked safe exit route");
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
                || !Path(carrier, approach, carrier.Position, patient.Position, PathEndMode.Touch, out _, out pickup)) { return false; }
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
