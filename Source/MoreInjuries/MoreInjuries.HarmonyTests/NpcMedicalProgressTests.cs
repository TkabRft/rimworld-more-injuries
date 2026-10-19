using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.AI;
using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Defs.WellKnown;
using Verse;
using Verse.AI;

namespace MoreInjuries.HarmonyTests;

[TestClass, DoNotParallelize]
public sealed class NpcMedicalProgressTests
{
    private const string OWNER = "MoreInjuries.test.IngestionProgress";

    [TestInitialize]
    public void Initialize()
    {
        MoreInjuriesMod.Settings = new();
        Find.TickManager.TicksGame = 1000;
    }

    [TestCleanup]
    public void Cleanup() => new Harmony(OWNER).UnpatchAll(OWNER);

    private static void PatchIngestion() => new Harmony(OWNER).CreateClassProcessor(typeof(NpcMedicalIngestionPatch)).Patch();

    private static (Pawn Pawn, Thing Dose) Carrier()
        => (new() { CurJob = new() { def = KnownJobDefOf.MI_NpcPerformanceDrug, count = 1 } }, new());

    [TestMethod]
    [DataRow(false, true, 1)]
    [DataRow(true, false, 1)]
    [DataRow(true, true, 0)]
    public void DeniedIngestionTerminatesNativeCountRetryWithoutConsumingDose(bool enabled, bool safe, int maximum)
    {
        PatchIngestion();
        var (pawn, dose) = Carrier();
        MoreInjuriesMod.Settings.NpcPerformanceDrugs = enabled;
        MoreInjuriesMod.Settings.NpcPerformanceMaximum = maximum;
        pawn.SafeDrug = safe;
        int attempts = 0;
        // Native ingestion can retry while the requested count remains unconsumed.
        while (pawn.CurJob.count > 0 && attempts < 4)
        {
            int before = dose.stackCount;
            dose.Ingested(pawn, 0);
            pawn.CurJob.count -= before - dose.stackCount;
            attempts++;
        }
        Assert.AreEqual(1, attempts);
        Assert.AreEqual(0, pawn.CurJob.count);
        Assert.AreEqual(1, dose.stackCount);
        Assert.IsFalse(dose.Destroyed);
        Assert.AreEqual(0, NpcMedicalAi.State(pawn).Drugs.Doses);
        Assert.AreEqual(0, pawn.Remembered);
    }

    [TestMethod]
    public void AcceptedPerformanceDoseAndClinicalEpinephrineUseIndependentBudgets()
    {
        PatchIngestion();
        var (pawn, dose) = Carrier();
        dose.Ingested(pawn, 0);
        Assert.IsTrue(dose.Destroyed);
        Assert.AreEqual(1, NpcMedicalAi.State(pawn).Drugs.Doses);
        pawn.CurJob = new();
        Find.TickManager.TicksGame++;
        Thing epinephrine = new() { def = KnownThingDefOf.Epinephrine };
        epinephrine.Ingested(pawn, 0);
        Assert.AreEqual(1001, NpcMedicalAi.State(pawn).Drugs.LastClinical);
        Assert.AreEqual(1, NpcMedicalAi.State(pawn).Drugs.Doses);
        Assert.AreEqual(2, pawn.Remembered);
    }

    [TestMethod]
    public void ConsumedDoseRemainsAccountedWhenItsOutcomeReplacesTheCurrentJob()
    {
        PatchIngestion();
        var (pawn, dose) = Carrier();
        Job replacement = new() { count = 7 };
        dose.IngestionEffect = actor => actor.CurJob = replacement;
        dose.Ingested(pawn, 0);
        Assert.AreSame(replacement, pawn.CurJob);
        Assert.AreEqual(7, replacement.count);
        Assert.AreEqual(1, NpcMedicalAi.State(pawn).Drugs.Doses);
    }

    [TestMethod]
    public void NonManagedIngestionKeepsItsNativeOutcomeAndJobCount()
    {
        PatchIngestion();
        var (pawn, dose) = Carrier();
        pawn.ManagedByMi = false;
        MoreInjuriesMod.Settings.NpcPerformanceDrugs = false;
        dose.Ingested(pawn, 0);
        Assert.IsTrue(dose.Destroyed);
        Assert.AreEqual(1, pawn.CurJob.count);
        Assert.AreEqual(0, NpcMedicalAi.State(pawn).Drugs.Doses);
    }

    private static void Run(Toil toil, Pawn actor) { toil.actor = actor; toil.initAction(); }

    [TestMethod]
    public void PickupRejectsUnrelatedCargoWithoutRetargetingOrConsumingIt()
    {
        Pawn doctor = new(), patient = new(); Thing supply = new(), cargo = new();
        doctor.CurJob.targetB = new(supply);
        doctor.carryTracker.innerContainer.Items.Add(cargo);
        Run(Toils_MedicalDevice.PickupDevice(TargetIndex.B, patient, _ => 1), doctor);
        Assert.AreEqual(JobCondition.Incompletable, doctor.jobs.Ended);
        Assert.AreSame(cargo, doctor.carryTracker.CarriedThing);
        Assert.AreSame(supply, doctor.CurJob.targetB.Thing);
        Assert.AreEqual(1, supply.stackCount);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void UnavailableOrUncarryableSupplyEndsPickupWithoutNullRetargeting(int failure)
    {
        Pawn doctor = new(), patient = new(); Thing supply = new();
        doctor.CurJob.targetB = new(supply);
        if (failure == 0) { supply.Destroyed = true; }
        if (failure == 1) { doctor.carryTracker.MaxPickup = 0; }
        if (failure == 2) { doctor.Map.reservationManager.Reserve(new(), new(), supply); }
        Run(Toils_MedicalDevice.PickupDevice(TargetIndex.B, patient, _ => 1), doctor);
        Assert.AreEqual(JobCondition.Incompletable, doctor.jobs.Ended);
        Assert.IsNull(doctor.carryTracker.CarriedThing);
        Assert.AreSame(supply, doctor.CurJob.targetB.Thing);
    }

    [TestMethod]
    public void AlreadyCarriedDeviceDoesNotAttemptToPickItselfUpAgain()
    {
        Pawn doctor = new(), patient = new(); Thing supply = new() { stackCount = 2 };
        doctor.CurJob.targetB = new(supply);
        doctor.carryTracker.innerContainer.Items.Add(supply);
        doctor.carryTracker.MaxPickup = 0;
        Run(Toils_MedicalDevice.PickupDevice(TargetIndex.B, patient, _ => 5), doctor);
        Assert.IsNull(doctor.jobs.Ended);
        Assert.AreSame(supply, doctor.CurJob.targetB.Thing);
        Assert.AreEqual(2, supply.stackCount);
        Assert.AreEqual(3, doctor.CurJob.count);
    }

    [TestMethod]
    public void GroundPickupRetargetsOnlyAfterSuccessfulTransfer()
    {
        Pawn doctor = new(), patient = new(); Thing supply = new() { stackCount = 3 };
        doctor.CurJob.targetB = new(supply);
        doctor.carryTracker.MaxPickup = 1;
        Run(Toils_MedicalDevice.PickupDevice(TargetIndex.B, patient, _ => 2), doctor);
        Assert.IsNull(doctor.jobs.Ended);
        Assert.AreSame(doctor.carryTracker.CarriedThing, doctor.CurJob.targetB.Thing);
        Assert.AreEqual(2, supply.stackCount);
        Assert.AreEqual(1, doctor.carryTracker.CarriedThing!.stackCount);
        Assert.AreEqual(1, doctor.CurJob.count);
    }

    [TestMethod]
    public void ResolvedTreatmentExitsWithoutMakingZeroCountReservationsOrPickups()
    {
        Pawn doctor = new(), patient = new(); Thing supply = new();
        doctor.CurJob.targetB = new(supply);
        Run(Toils_MedicalDevice.ReserveDevice(TargetIndex.B, patient, _ => 0), doctor);
        Assert.AreEqual(JobCondition.Succeeded, doctor.jobs.Ended);
        Assert.IsEmpty(doctor.Map.reservationManager.Claims);
        doctor.jobs.Ended = null;
        Run(Toils_MedicalDevice.PickupDevice(TargetIndex.B, patient, _ => 0), doctor);
        Assert.AreEqual(JobCondition.Succeeded, doctor.jobs.Ended);
        Assert.IsNull(doctor.carryTracker.CarriedThing);
    }
}
