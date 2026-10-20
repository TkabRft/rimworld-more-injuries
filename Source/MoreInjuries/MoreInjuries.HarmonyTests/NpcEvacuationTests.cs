using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Defs.WellKnown;
using Verse;
using Verse.AI;

namespace MoreInjuries.HarmonyTests;

[TestClass]
public sealed class NpcEvacuationTests
{
    [TestInitialize]
    public void Reset() { MoreInjuriesMod.Settings = new(); Find.TickManager.TicksGame = 1000; }
    [TestCleanup]
    public void Cleanup() { MoreInjuriesMod.Settings.NpcMedicalAiMode = 2; MoreInjuriesMod.Settings.EnableNpcCombatMedicine = true; }

    private static (Pawn Carrier, Pawn Patient, Job Job) Mission()
    {
        Pawn carrier = new() { Position = new(50, 0, 50) };
        Pawn patient = new() { Downed = true, Position = new(51, 0, 50), Map = carrier.Map };
        Job job = new() { def = KnownJobDefOf.MI_NpcEvacuate, targetA = new(patient), targetB = new(new IntVec3(99, 0, 50)), count = -1 };
        carrier.CurJob = job;
        return (carrier, patient, job);
    }

    [TestMethod]
    public void NegativeCountIsCorrectedAndOriginalFourToilLayoutExitsWithPatient()
    {
        var (carrier, patient, job) = Mission();
        JobDriver_NpcEvacuate driver = new() { pawn = carrier };
        Assert.AreEqual(4, driver.BuildToils().Count);
        // Use a fresh driver: rebuilding toils is a serialization boundary, not a second run.
        driver = new() { pawn = carrier };
        Assert.IsTrue(driver.TryMakePreToilReservations(false));
        driver.BeforeToil = (index, _) => { if (index == 2) { Assert.AreSame(patient, carrier.carryTracker.CarriedThing); } };
        driver.Run();
        Assert.IsTrue(carrier.Exited);
        Assert.AreSame(patient, carrier.carryTracker.CarriedThing);
        Assert.AreEqual(0, job.count);
        Assert.HasCount(0, carrier.Map.reservationManager.Claims);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void LowerModesRejectNewPickupButAllowSafeAlreadyCarriedContinuation(int mode)
    {
        var (carrier, patient, _) = Mission();
        MoreInjuriesMod.Settings.NpcMedicalAiMode = mode;
        Assert.IsFalse(new JobDriver_NpcEvacuate { pawn = carrier }.TryMakePreToilReservations(false));
        carrier.carryTracker.TryStartCarry(patient, 1);
        JobDriver_NpcEvacuate resumed = new() { pawn = carrier, ResumeIndex = 2 };
        Assert.IsTrue(resumed.TryMakePreToilReservations(false)); resumed.Run();
        Assert.IsTrue(carrier.Exited);
    }

    [TestMethod]
    public void DowngradeBetweenApproachAndPickupStopsWithoutTakingPatient()
    {
        var (carrier, patient, _) = Mission();
        JobDriver_NpcEvacuate driver = new() { pawn = carrier };
        driver.BeforeToil = (index, _) => { if (index == 1) { MoreInjuriesMod.Settings.NpcMedicalAiMode = 0; } };
        driver.Run();
        Assert.IsNull(carrier.carryTracker.CarriedThing); Assert.IsTrue(patient.Spawned); Assert.IsFalse(carrier.Exited);
    }

    [TestMethod]
    public void FullRouteRejectsRemoteCombatUnknownRiskAndBlockedCellsAndReleasesPaths()
    {
        foreach (int fault in new[] { 0, 1, 2 })
        {
            var (carrier, _, job) = Mission();
            IntVec3 remote = new(90, 0, 50);
            carrier.Map.pathFinder.Find = (start, end, mode) => new()
            { NodesReversed = mode == PathEndMode.Touch ? [end, start] : [end, remote, start] };
            if (fault == 0) { carrier.Map.Risks[remote] = new(1, 0, 0, 0); }
            if (fault == 1) { carrier.Map.Risks[remote] = new(0, 0, 0, 0, false); }
            if (fault == 2) { carrier.Map.Blocked.Add(remote); }
            Assert.IsFalse(NpcEvacuation.RouteValid(carrier, job));
            Assert.IsTrue(carrier.Map.pathFinder.Released > 0);
        }
    }

    [TestMethod]
    public void CandidatesCoverFourEdgesAndSafeSearchUsesBoundedAlternatives()
    {
        var (carrier, patient, _) = Mission();
        var cells = NpcEvacuation.Candidates(carrier).ToList();
        Assert.IsTrue(cells.Count <= 16);
        Assert.IsTrue(cells.Any(cell => cell.x == 0) && cells.Any(cell => cell.x == 99)
            && cells.Any(cell => cell.z == 0) && cells.Any(cell => cell.z == 99));
        Assert.IsTrue(NpcEvacuation.TryExit(carrier, patient, out var exit));
        Assert.IsTrue(NpcEvacuation.Edge(carrier.Map, exit));
        foreach (var cell in cells) { carrier.Map.Risks[cell] = new(1, 0, 0, 0); }
        Assert.IsFalse(NpcEvacuation.TryExit(carrier, patient, out _));
    }

    [TestMethod]
    public void NewDangerAfterPickupAbortsAndPreservesPatient()
    {
        var (carrier, patient, job) = Mission();
        JobDriver_NpcEvacuate driver = new() { pawn = carrier };
        driver.BeforeToil = (index, _) =>
        {
            if (index != 2) { return; }
            carrier.mindState.knownExploder = new() { Map = carrier.Map, PositionHeld = carrier.Position };
        };
        driver.Run();
        Assert.IsFalse(carrier.Exited); Assert.IsTrue(patient.Spawned);
        Assert.IsNull(carrier.carryTracker.CarriedThing);
    }

    [TestMethod]
    public void CompetingReservationAndUnrelatedCargoAreRejectedWithoutTakingOthersClaims()
    {
        var (carrier, patient, job) = Mission();
        Pawn other = new() { Map = carrier.Map };
        Assert.IsTrue(other.Reserve(patient, new(), errorOnFailed: false));
        Assert.IsFalse(new JobDriver_NpcEvacuate { pawn = carrier }.TryMakePreToilReservations(false));
        Assert.HasCount(1, carrier.Map.reservationManager.Claims);
        carrier.carryTracker.TryStartCarry(new Thing(), 1);
        Assert.IsFalse(NpcEvacuation.RouteValid(carrier, job));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void KnownExplosiveSafetyWorksInEveryModeAndIgnoresStaleWrongMapAndManualWork(int mode)
    {
        MoreInjuriesMod.Settings.NpcMedicalAiMode = mode;
        Pawn actor = new(); Job job = new();
        Thing explosive = new() { Map = actor.Map, PositionHeld = actor.Position };
        actor.mindState.knownExploder = explosive;
        Assert.IsTrue(NpcMedicalSafety.YieldToExplosive(actor, job));
        job.playerForced = true; Assert.IsFalse(NpcMedicalSafety.YieldToExplosive(actor, job));
        job.playerForced = false; actor.ManagedByMi = false;
        Assert.IsFalse(NpcMedicalSafety.YieldToExplosive(actor, job)); actor.ManagedByMi = true;
        explosive.Spawned = false; Assert.IsFalse(NpcMedicalSafety.ExplosiveDanger(actor));
        explosive.Spawned = true; explosive.Map = new(); Assert.IsFalse(NpcMedicalSafety.ExplosiveDanger(actor));
        explosive.Map = actor.Map; explosive.PositionHeld = new(10); Assert.IsFalse(NpcMedicalSafety.ExplosiveDanger(actor));
    }

    [TestMethod]
    public void ImmediateHostileAndRecentDamageDeferEvacuationUntilQuiet()
    {
        var (carrier, patient, job) = Mission();
        Pawn enemy = new() { Hostile = true, Map = carrier.Map, Position = new(55, 0, 50) };
        carrier.Map.mapPawns.AllPawnsSpawned.Add(enemy);
        Assert.IsFalse(NpcEvacuation.RouteValid(carrier, job));
        carrier.Map.mapPawns.AllPawnsSpawned.Clear();
        NpcMedicalSafety.NotifyDamage(patient, new(new() { isExplosive = true }), 1f);
        Assert.IsFalse(NpcEvacuation.RouteValid(carrier, job));
        Find.TickManager.TicksGame += 600;
        Assert.IsTrue(NpcEvacuation.RouteValid(carrier, job));
    }

    [TestMethod]
    public void RecoveryDeathAndCarrierIncapacityAfterPickupPreserveThePatient()
    {
        foreach (int change in new[] { 0, 1, 2 })
        {
            var (carrier, patient, _) = Mission();
            JobDriver_NpcEvacuate driver = new() { pawn = carrier };
            driver.BeforeToil = (index, _) =>
            {
                if (index != 2) { return; }
                if (change == 0) { patient.Downed = false; }
                if (change == 1) { patient.Dead = true; }
                if (change == 2) { carrier.CarrierCapable = false; }
            };
            driver.Run();
            Assert.IsFalse(carrier.Exited); Assert.IsTrue(patient.Spawned); Assert.IsFalse(patient.Destroyed);
        }
    }

    [TestMethod]
    public void NewNextCellHazardIsRejectedBeforeCachedRouteExpires()
    {
        var (carrier, patient, _) = Mission();
        JobDriver_NpcEvacuate driver = new() { pawn = carrier };
        driver.BeforeToil = (index, _) =>
        {
            if (index != 2) { return; }
            carrier.pather.MovingNow = true; carrier.pather.nextCell = new(52, 0, 50);
            carrier.Map.Risks[carrier.pather.nextCell] = new(0, 0, 1, 0);
        };
        driver.Run(); Assert.IsFalse(carrier.Exited); Assert.IsTrue(patient.Spawned);
    }

    [TestMethod]
    public void PeriodicRouteRecheckAndInvalidExitAbortWithoutLosingPatient()
    {
        foreach (bool invalidExit in new[] { false, true })
        {
            var (carrier, patient, job) = Mission();
            JobDriver_NpcEvacuate driver = new() { pawn = carrier };
            driver.BeforeToil = (index, _) =>
            {
                if (index != 2) { return; }
                if (invalidExit) { job.targetB = new(new IntVec3(60, 0, 50)); }
                else { carrier.Map.Risks[job.targetB.Cell] = new(0, 1, 0, 0); }
                Find.TickManager.TicksGame += 60;
            };
            driver.Run(); Assert.IsFalse(carrier.Exited); Assert.IsTrue(patient.Spawned);
        }
    }

    [TestMethod]
    public void FailedCleanupPlacementRetainsIntactCarriedPatientAndDisablingAllowsSafeContinuation()
    {
        var (carrier, patient, _) = Mission();
        carrier.carryTracker.TryStartCarry(patient, 1);
        carrier.carryTracker.DropAllowed = false; carrier.NativeSafety = true;
        new JobDriver_NpcEvacuate { pawn = carrier, ResumeIndex = 2 }.Run();
        Assert.AreSame(patient, carrier.carryTracker.CarriedThing); Assert.IsFalse(patient.Destroyed);
        var (continuing, casualty, _) = Mission();
        continuing.carryTracker.TryStartCarry(casualty, 1);
        MoreInjuriesMod.Settings.EnableNpcCombatMedicine = false;
        JobDriver_NpcEvacuate driver = new() { pawn = continuing, ResumeIndex = 2 };
        Assert.IsTrue(driver.TryMakePreToilReservations(false)); driver.Run(); Assert.IsTrue(continuing.Exited);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ProductionAssessmentIncludesPawnsAndTurretsBeyondOriginalPatientSnapshot(bool turret)
    {
        var (carrier, patient, job) = Mission();
        carrier.Map.Sight = (from, to) => to.x >= 85;
        if (turret)
        { carrier.Map.listerThings.Buildings.Add(new RimWorld.Building_Turret { Map = carrier.Map, PositionHeld = new(95, 0, 50) }); }
        else
        {
            // More than sixteen harmless earlier entries must not hide the route threat.
            for (int i = 0; i < 17; i++)
            { carrier.Map.mapPawns.AllPawnsSpawned.Add(new() { Hostile = true, Map = carrier.Map, Position = new(0, 0, i) }); }
            carrier.Map.mapPawns.AllPawnsSpawned.Add(new() { Hostile = true, Map = carrier.Map, Position = new(95, 0, 50) });
        }
        NpcExtraction.Assessment local = new(carrier, patient, patient.Position);
        NpcExtraction.Assessment full = new(carrier, patient, patient.Position, evacuation: true);
        Assert.IsTrue(local.At(new(90, 0, 50), out _).Safe);
        Assert.IsFalse(full.At(new(90, 0, 50), out _).Safe);
        Assert.IsFalse(NpcEvacuation.RouteValid(carrier, job));
    }
}
