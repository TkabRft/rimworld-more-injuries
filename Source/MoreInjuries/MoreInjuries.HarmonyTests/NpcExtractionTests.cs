using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Defs.WellKnown;
using Verse;
using Verse.AI;

namespace MoreInjuries.HarmonyTests;

[TestClass]
public sealed class NpcExtractionTests
{
    private static readonly Type s_coverDriver = Driver("CombatExtended"), s_shelterDriver = Driver("CelsiusPatchesAI");
    private static Type Driver(string assembly) => AssemblyBuilder.DefineDynamicAssembly(new(assembly), AssemblyBuilderAccess.Run)
        .DefineDynamicModule(assembly).DefineType("TransportSafetyFixture", TypeAttributes.Public).CreateType();
    private static Job Safety(string name, bool celsius = false) => new() { def = new() { defName = name, driverClass = celsius ? s_shelterDriver : s_coverDriver } };
    private static (Pawn Carrier, Pawn Patient, Job Job) Mission()
    {
        Pawn carrier = new() { Position = new(0) }, patient = new() { Downed = true, Position = new(0) };
        patient.Map = carrier.Map;
        Job job = new() { def = KnownJobDefOf.MI_NpcExtract, targetA = new(patient), targetB = new(new IntVec3(5)),
            targetC = new(patient.Position), startTick = Find.TickManager.TicksGame, count = 1 };
        carrier.CurJob = job;
        return (carrier, patient, job);
    }
    [TestCleanup]
    public void Cleanup() { NpcMedicalAi.OnHandoff = null; MoreInjuriesMod.Settings.EnableNpcCombatMedicine = true; }

    [TestMethod]
    public void RealDriverCarriesPlacesAndHandsOffItsOriginalPatient()
    {
        var (carrier, patient, job) = Mission();
        NpcMedicalAi.OnHandoff = (actor, target) =>
        {
            Assert.AreSame(carrier, actor); Assert.AreSame(patient, target);
            Assert.IsTrue(patient.Spawned); Assert.AreEqual(job.targetB.Cell, patient.Position);
            Assert.IsNull(carrier.carryTracker.CarriedThing);
            return new() { def = KnownJobDefOf.MI_NpcTend, targetA = new(patient), endAfterTendedOnce = true };
        };
        JobDriver_NpcExtract driver = new() { pawn = carrier };
        Assert.IsTrue(driver.TryMakePreToilReservations(false)); driver.Run();
        Assert.AreEqual(KnownJobDefOf.MI_NpcTend, carrier.CurJob.def);
        Assert.AreSame(patient, carrier.CurJob.targetA.Pawn);
        Assert.HasCount(0, carrier.Map.reservationManager.Claims);
        Assert.IsTrue(NpcMedicalAttempts.ExtractionAllowed(carrier, patient));
    }

    [TestMethod]
    public void NonDoctorCanExtractWithoutInventingMedicalCapability()
    {
        var (carrier, patient, job) = Mission(); carrier.DoctorCapable = false;
        new JobDriver_NpcExtract { pawn = carrier }.Run();
        Assert.IsTrue(patient.Spawned); Assert.AreEqual(job.targetB.Cell, patient.Position);
        Assert.IsFalse(carrier.DoctorCapable); Assert.IsNull(carrier.carryTracker.CarriedThing);
    }

    [TestMethod]
    public void StableToilsResumeWithAnAlreadyCarriedPatient()
    {
        var (carrier, patient, job) = Mission();
        List<Toil> original = new JobDriver_NpcExtract { pawn = carrier }.BuildToils();
        int carriedIndex = original.IndexOf(original[0].JumpTarget!);
        Assert.AreEqual(1, carrier.carryTracker.TryStartCarry(patient, 1));
        Assert.HasCount(original.Count, new JobDriver_NpcExtract { pawn = carrier }.BuildToils());
        new JobDriver_NpcExtract { pawn = carrier, ResumeIndex = carriedIndex }.Run();
        Assert.IsTrue(patient.Spawned); Assert.AreEqual(job.targetB.Cell, patient.Position);
        Assert.IsNull(carrier.carryTracker.CarriedThing);
    }

    [TestMethod]
    public void CompetingRescuersCannotTakeTheSamePatient()
    {
        var (first, patient, job) = Mission();
        Assert.IsTrue(new JobDriver_NpcExtract { pawn = first }.TryMakePreToilReservations(false));
        Pawn second = new() { Map = first.Map, CurJob = job };
        Assert.IsFalse(new JobDriver_NpcExtract { pawn = second }.TryMakePreToilReservations(false));
        Assert.IsTrue(first.Map.reservationManager.Claims.All(claim => claim.Pawn == first));
    }

    [TestMethod]
    public void InvalidDestinationsAndFailedRetrievalLeavePatientAndCargoIntact()
    {
        foreach (string failure in new[] { "destination", "route", "moving", "recovered", "dead", "capacity", "hands" })
        {
            var (carrier, patient, _) = Mission(); Thing cargo = new();
            switch (failure)
            {
                case "destination": carrier.DestinationValid = false; break;
                case "route": carrier.RouteValid = false; break;
                case "moving": patient.Position = new(1); break;
                case "recovered": patient.Downed = false; break;
                case "dead": patient.Dead = true; break;
                case "capacity": carrier.CarrierCapable = false; break;
                case "hands": carrier.carryTracker.innerContainer.Items.Add(cargo); break;
            }
            new JobDriver_NpcExtract { pawn = carrier }.Run();
            Assert.IsTrue(patient.Spawned, failure); Assert.AreNotEqual(new IntVec3(5), patient.Position, failure);
            Assert.IsFalse(patient.Destroyed, failure);
            if (failure == "hands") { Assert.AreSame(cargo, carrier.carryTracker.CarriedThing); }
        }
    }

    [TestMethod]
    public void InterruptedTransportDropsOnlyItsPatientAndDefersRetry()
    {
        var (carrier, patient, job) = Mission(); JobDriver_NpcExtract driver = new() { pawn = carrier };
        driver.BeforeToil = (_, _) =>
        {
            if (carrier.carryTracker.CarriedThing == patient) { carrier.jobs.EndCurrentJob(JobCondition.InterruptForced); }
        };
        driver.Run();
        Assert.IsTrue(patient.Spawned); Assert.AreEqual(carrier.Position, patient.Position);
        Assert.IsNull(carrier.carryTracker.CarriedThing);
        Assert.IsFalse(NpcMedicalAttempts.ExtractionAllowed(carrier, patient));
    }

    [TestMethod]
    public void ModuleToggleAllowsCurrentCarryToFinish()
    {
        var (carrier, patient, job) = Mission(); JobDriver_NpcExtract driver = new() { pawn = carrier };
        driver.BeforeToil = (_, _) => { if (carrier.carryTracker.CarriedThing == patient) { MoreInjuriesMod.Settings.EnableNpcCombatMedicine = false; } };
        driver.Run(); Assert.IsTrue(patient.Spawned); Assert.AreEqual(job.targetB.Cell, patient.Position);
    }

    [TestMethod]
    public void SuspendedDriverKeepsPatientAndResumedDriverCompletesPlacement()
    {
        var (carrier, patient, job) = Mission();
        JobDriver_NpcExtract first = new() { pawn = carrier };
        first.BeforeToil = (_, _) =>
        {
            if (carrier.carryTracker.CarriedThing != patient) { return; }
            NpcExtraction.Suspension(carrier, true);
            carrier.jobs.EndCurrentJob(JobCondition.InterruptForced);
        };
        first.Run();
        Assert.AreSame(patient, carrier.carryTracker.CarriedThing);
        Assert.IsFalse(patient.Spawned);
        NpcExtraction.Suspension(carrier, false); carrier.jobs.Ended = null;
        new JobDriver_NpcExtract { pawn = carrier }.Run();
        Assert.IsTrue(patient.Spawned); Assert.AreEqual(job.targetB.Cell, patient.Position);
        Assert.IsNull(carrier.carryTracker.CarriedThing);
    }

    [TestMethod]
    public void FailedPlacementKeepsTheSelectedPawnAliveAndReleasesClaims()
    {
        var (carrier, patient, _) = Mission(); carrier.carryTracker.DropAllowed = false;
        new JobDriver_NpcExtract { pawn = carrier }.Run();
        Assert.AreSame(patient, carrier.carryTracker.CarriedThing);
        Assert.IsFalse(patient.Destroyed); Assert.IsFalse(patient.Dead);
        Assert.HasCount(0, carrier.Map.reservationManager.Claims);
    }

    [TestMethod]
    public void RealHarmonyPreservesPatientForCeAndCelsiusSafetyAndResetsOnExceptions()
    {
        Harmony harmony = new("MI.Extraction.CarryFixture");
        try
        {
            harmony.CreateClassProcessor(typeof(NpcExtractionCarryPatch)).Patch();
            foreach (Job safety in new[] { Safety("RunForCover"), Safety("HunkerDown"), Safety("CPAI_SeekThermalShelter", true) })
            {
                var (carrier, patient, mission) = Mission(); carrier.carryTracker.TryStartCarry(patient, 1);
                carrier.jobs.DuringStart = () => Assert.IsTrue(NpcExtraction.Suspending(carrier));
                carrier.jobs.StartJob(safety);
                Assert.IsTrue(carrier.jobs.KeptCarry); Assert.IsTrue(carrier.jobs.Resumed);
                Assert.AreSame(patient, carrier.carryTracker.CarriedThing);
                Assert.AreSame(mission, carrier.jobs.jobQueue.Single().job);
                Assert.IsFalse(NpcExtraction.Suspending(carrier));
            }
            var (actor, target, _) = Mission(); actor.carryTracker.TryStartCarry(target, 1);
            actor.jobs.DuringStart = () => throw new InvalidOperationException("native failure");
            Assert.Throws<InvalidOperationException>(() => actor.jobs.StartJob(Safety("HunkerDown")));
            Assert.IsFalse(NpcExtraction.Suspending(actor));
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    [TestMethod]
    public void CarryGuardLeavesUnrelatedPlayerForcedAndIncapacitatedWorkAlone()
    {
        Harmony harmony = new("MI.Extraction.ExclusionFixture");
        try
        {
            harmony.CreateClassProcessor(typeof(NpcExtractionCarryPatch)).Patch();
            foreach (string excluded in new[] { "unrelated", "player", "forced", "downed", "cargo", "boundary" })
            {
                var (carrier, patient, mission) = Mission();
                carrier.carryTracker.TryStartCarry(excluded == "cargo" ? new Thing() : patient, 1);
                Job next = Safety("RunForCover");
                if (excluded == "unrelated") { next = new(); }
                if (excluded == "player") { carrier.ManagedByMi = false; }
                if (excluded == "forced") { mission.playerForced = true; }
                if (excluded == "downed") { carrier.Downed = true; }
                if (excluded == "boundary") { carrier.DestinationValid = false; }
                carrier.jobs.StartJob(next);
                Assert.IsFalse(carrier.jobs.KeptCarry, excluded); Assert.IsFalse(carrier.jobs.Resumed, excluded);
            }
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    [TestMethod]
    public void RiskAndLimitsKeepFallbackBoundedAndDoNotInventSafety()
    {
        Assert.IsFalse(NpcExtractionModel.Enabled(true, 0)); Assert.IsTrue(NpcExtractionModel.Enabled(true, 1));
        Assert.IsTrue(NpcExtractionModel.Enabled(true, 2)); Assert.IsFalse(NpcExtractionModel.Enabled(false, 2));
        Assert.IsTrue(NpcExtractionModel.Carrier(false, .5f, true, 10)); Assert.IsFalse(NpcExtractionModel.Carrier(false, .4f, true, 10));
        Assert.IsFalse(NpcExtractionModel.Carrier(false, 1, true, 90));
        NpcExtractionRisk origin = new(1, .8f, 0, .2f), shelter = new(0, 0, 0, 0);
        Assert.IsTrue(origin.Hazard); Assert.IsTrue(shelter.BetterThan(origin)); Assert.IsTrue(shelter.Safe);
        Assert.IsFalse(new NpcExtractionRisk(0, 0, 0, 0, false).Safe);
        Assert.IsTrue(NpcExtractionModel.Thermal(-10, 0, 30) > NpcExtractionModel.Thermal(-10, -20, 30));
        Assert.IsFalse(NpcExtractionModel.Finite(NpcExtractionModel.Thermal(float.NaN, 0, 30)));
        NpcExtractionLimits sharedPatient = new(); sharedPatient.Fallback(100); sharedPatient.Prepare(100); sharedPatient.Delivered(100);
        Assert.IsFalse(sharedPatient.CanFallback(2599)); Assert.IsTrue(sharedPatient.CanFallback(2600));
        Assert.IsFalse(sharedPatient.CanPrepare(2599)); Assert.IsTrue(sharedPatient.CanPrepare(2600));
        Assert.IsFalse(sharedPatient.CanRelocate(699, false)); Assert.IsTrue(sharedPatient.CanRelocate(700, false));
        Assert.IsTrue(sharedPatient.CanRelocate(101, true));
    }
}
