using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.HarmonyTests;

[TestClass]
public sealed class NpcMedicalRetryTests
{
    [TestInitialize]
    public void Reset() { Find.TickManager.TicksGame = 1000; MoreInjuriesMod.Settings.EnableNpcCombatMedicine = true; }

    [TestMethod]
    public void InterruptedTwoFloorStacksShareRetryDeadlineAndReleaseClaims()
    {
        Pawn doctor = new(), patient = Wounded();
        Thing first = Medicine(), second = Medicine();
        for (int failure = 0; failure < 5; failure++)
        {
            Job job = Tend(patient, failure % 2 == 0 ? first : second);
            doctor.CurJob = job; doctor.jobs.Ended = null;
            JobDriver_NpcTend driver = new() { pawn = doctor };
            Assert.IsTrue(driver.TryMakePreToilReservations(false));
            driver.BeforeToil = (_, _) =>
            {
                if (doctor.carryTracker.CarriedThing is not null) { doctor.jobs.EndCurrentJob(JobCondition.InterruptForced); }
            };
            driver.Run();
            Assert.IsTrue(patient.health.NeedsTend);
            Assert.IsNull(doctor.carryTracker.CarriedThing);
            Assert.AreEqual(1, doctor.inventory.innerContainer.Items.Sum(item => item.stackCount));
            Assert.HasCount(0, doctor.Map.reservationManager.Claims);
            Thing retained = doctor.inventory.innerContainer.Items.Single();
            // Model vanilla inventory unloading putting that same unused dose
            // back on the floor. Stack identity cannot reset the retry clock.
            doctor.inventory.innerContainer.Items.Clear(); retained.ParentHolder = null; retained.Spawned = true;
            int delay = new[] { 150, 300, 600, 1200, 2500 }[failure];
            Assert.IsFalse(NpcMedicalAttempts.Allowed(doctor, patient, KnownJobDefOf.MI_NpcTend, second));
            Find.TickManager.TicksGame += delay - 1;
            Assert.IsFalse(NpcMedicalAttempts.Allowed(doctor, patient, KnownJobDefOf.MI_NpcTend, first));
            Find.TickManager.TicksGame++;
            Assert.IsTrue(NpcMedicalAttempts.Allowed(doctor, patient, KnownJobDefOf.MI_NpcTend, second));
        }
    }

    [TestMethod]
    public void MaterialChangesAndIndependentActionsRemainEligible()
    {
        Pawn doctor = new(), patient = Wounded(), other = Wounded(); Thing supply = Medicine();
        Job job = Tend(patient, supply);
        NpcMedicalAttempts.Begin(doctor, job, patient, supply);
        NpcMedicalAttempts.Finish(job, JobCondition.Succeeded); // apparent success, no effect
        Assert.IsFalse(NpcMedicalAttempts.Allowed(doctor, patient, job.def, Medicine()));
        Assert.IsTrue(NpcMedicalAttempts.Allowed(doctor, other, job.def, supply));
        Assert.IsTrue(NpcMedicalAttempts.Allowed(doctor, patient, KnownJobDefOf.MI_NpcPainAssist, supply));
        patient.Position = new(1);
        Assert.IsTrue(NpcMedicalAttempts.Allowed(doctor, patient, job.def, supply));
        NpcMedicalAttempts.Begin(doctor, job, patient, supply); NpcMedicalAttempts.Finish(job, JobCondition.Incompletable);
        patient.playerSettings = new() { medCare = MedicalCareCategory.NoMeds };
        Assert.IsTrue(NpcMedicalAttempts.Allowed(doctor, patient, job.def, supply));
        NpcMedicalAttempts.Begin(doctor, job, patient, supply); NpcMedicalAttempts.Finish(job, JobCondition.Incompletable);
        doctor.DoctorCapable = false;
        Assert.IsTrue(NpcMedicalAttempts.Allowed(doctor, patient, job.def, supply));
        doctor.DoctorCapable = true;
        NpcMedicalAttempts.Begin(doctor, job, patient, supply); NpcMedicalAttempts.Finish(job, JobCondition.Incompletable);
        doctor.inventory.innerContainer.Items.Add(supply); supply.ParentHolder = doctor.inventory;
        Assert.IsTrue(NpcMedicalAttempts.Allowed(doctor, patient, job.def, supply));
    }

    [TestMethod]
    public void TendingConsumesOneSelectedDoseWithoutChangingOtherCargo()
    {
        foreach (bool inventory in new[] { false, true })
        {
            Pawn doctor = new(), patient = Wounded(); Thing medicine = Medicine(); medicine.stackCount = 4;
            if (inventory) { doctor.inventory.innerContainer.Items.Add(medicine); medicine.ParentHolder = doctor.inventory; medicine.Spawned = false; }
            doctor.CurJob = Tend(patient, medicine);
            JobDriver_NpcTend driver = new() { pawn = doctor };
            Assert.IsTrue(driver.TryMakePreToilReservations(false)); driver.Run();
            Assert.IsFalse(patient.health.NeedsTend);
            Assert.IsNull(doctor.carryTracker.CarriedThing);
            Assert.AreEqual(3, medicine.stackCount);
            Assert.IsTrue(NpcMedicalAttempts.Allowed(doctor, patient, doctor.CurJob.def, medicine));
        }
    }

    [TestMethod]
    public void RecentMortarHarmDefersTripsButAllowsPocketSelfCareAndAdjacentCriticalCare()
    {
        Pawn doctor = new(), patient = Wounded(); Thing supply = Medicine();
        DamageInfo mortar = new(new() { isExplosive = true });
        NpcMedicalSafety.NotifyDamage(patient, mortar, 10f);
        Assert.IsFalse(NpcMedicalSafety.Allowed(doctor, patient, supply));
        Assert.IsTrue(NpcMedicalSafety.Allowed(patient, patient, null));
        doctor.inventory.innerContainer.Items.Add(supply); supply.ParentHolder = doctor.inventory;
        Assert.IsFalse(NpcMedicalSafety.Allowed(doctor, patient, supply));
        patient.EmergencyUrgency = 95f;
        Assert.IsTrue(NpcMedicalSafety.Allowed(doctor, patient, supply));
        doctor.NativeSafety = true;
        Assert.IsFalse(NpcMedicalSafety.Allowed(doctor, patient, supply));
        Assert.IsFalse(NpcMedicalSafety.Allowed(doctor, doctor, supply));
        doctor.NativeSafety = false; patient.Position = new(2);
        Assert.IsFalse(NpcMedicalSafety.Allowed(doctor, patient, supply));
        Find.TickManager.TicksGame += 599;
        Assert.IsTrue(NpcMedicalSafety.Recent(patient));
        NpcMedicalSafety.NotifyDamage(patient, mortar, 1f);
        Find.TickManager.TicksGame += 600;
        Assert.IsFalse(NpcMedicalSafety.Recent(patient));
        Assert.IsTrue(NpcMedicalSafety.Allowed(doctor, patient, supply));
    }

    [TestMethod]
    public void OnlyActualViolentRangedOrExplosiveDamageStartsQuietWindow()
    {
        Pawn doctor = new();
        NpcMedicalSafety.NotifyDamage(doctor, new(new() { isRanged = true }), 0f);
        NpcMedicalSafety.NotifyDamage(doctor, new(new()), 10f);
        NpcMedicalSafety.NotifyDamage(doctor, new(new() { isExplosive = true, Violent = false }), 10f);
        Assert.IsFalse(NpcMedicalSafety.Recent(doctor));
        NpcMedicalSafety.NotifyDamage(doctor, new(new() { isRanged = true }), 1f);
        Assert.IsTrue(NpcMedicalSafety.Recent(doctor));
    }

    [TestMethod]
    public void UnusedSelectedDeviceRetainsIdentityAndCapacityLimits()
    {
        Pawn doctor = new(); Thing device = new(); doctor.carryTracker.innerContainer.Items.Add(device);
        Job job = Tend(doctor, device);
        doctor.FitCount = 0; NpcMedicalReservations.RestockSelected(doctor, job, TargetIndex.B);
        Assert.AreSame(device, doctor.carryTracker.CarriedThing);
        doctor.FitCount = 1; NpcMedicalReservations.RestockSelected(doctor, job, TargetIndex.B);
        Assert.AreSame(device, doctor.inventory.innerContainer.Items.Single());
        Thing cargo = new(); doctor.carryTracker.innerContainer.Items.Add(cargo);
        NpcMedicalReservations.RestockSelected(doctor, job, TargetIndex.B);
        Assert.AreSame(cargo, doctor.carryTracker.CarriedThing);
    }

    [TestMethod]
    public void FailedReservationsInaccessibleSuppliesAndFullHandsLeaveCargoIntact()
    {
        Pawn doctor = new(), patient = Wounded(), other = new() { Map = doctor.Map };
        Thing medicine = Medicine(); Job job = Tend(patient, medicine); doctor.CurJob = job;
        Job otherJob = new(); doctor.Map.reservationManager.Reserve(other, otherJob, patient);
        Assert.IsFalse(new JobDriver_NpcTend { pawn = doctor }.TryMakePreToilReservations(false));
        Assert.HasCount(1, doctor.Map.reservationManager.Claims);
        Assert.IsFalse(NpcMedicalAttempts.Allowed(doctor, patient, job.def, Medicine()));
        doctor.Map.reservationManager.ReleaseClaimedBy(other, otherJob);
        doctor.Reachable = false;
        Assert.IsFalse(NpcMedicalReservations.DeliveryAllowed(doctor, patient, medicine));
        doctor.Reachable = true; medicine.Forbidden = true;
        Assert.IsFalse(NpcMedicalReservations.DeliveryAllowed(doctor, patient, medicine));
        medicine.Forbidden = false; doctor.FitCount = 0;
        Assert.IsFalse(NpcMedicalReservations.DeliveryAllowed(doctor, patient, medicine));
        doctor.FitCount = 1; doctor.carryTracker.MaxPickup = 0;
        Assert.IsFalse(NpcMedicalReservations.DeliveryAllowed(doctor, patient, medicine));
        doctor.carryTracker.MaxPickup = 1; Thing cargo = new(); doctor.carryTracker.innerContainer.Items.Add(cargo);
        Assert.IsFalse(NpcMedicalReservations.DeliveryAllowed(doctor, patient, medicine));
        Assert.IsFalse(NpcMedicalReservations.DeliveryAllowed(doctor, patient, null), "Bare-handed care must not claim unrelated carried cargo either.");
        NpcMedicalReservations.RestockSelected(doctor, job, TargetIndex.B);
        Assert.AreSame(cargo, doctor.carryTracker.CarriedThing);
        Assert.AreEqual(1, medicine.stackCount);
    }

    [TestMethod]
    public void WorseningAfterAnApplicationIsNotProgressAndDeathIsNotFailure()
    {
        Pawn doctor = new(), patient = Wounded(); Thing supply = Medicine(); Job job = Tend(patient, supply);
        NpcMedicalAttempts.Begin(doctor, job, patient, supply); NpcMedicalAttempts.Applying(job);
        patient.health.hediffSet.hediffs[0].Severity += .01f;
        NpcMedicalAttempts.Applied(job, true); NpcMedicalAttempts.Finish(job, JobCondition.Succeeded);
        Assert.IsFalse(NpcMedicalAttempts.Allowed(doctor, patient, job.def, supply));
        Find.TickManager.TicksGame += 150;
        NpcMedicalAttempts.Begin(doctor, job, patient, supply); patient.Dead = true;
        NpcMedicalAttempts.Finish(job, JobCondition.Incompletable);
        Assert.IsTrue(NpcMedicalAttempts.Allowed(doctor, patient, job.def, supply));
    }

    [TestMethod]
    public void ClinicalEffectResetsFailuresWithoutSpendingEitherDrugBudget()
    {
        Pawn doctor = new(), patient = Wounded(); Thing dose = Medicine();
        Job job = new() { def = KnownJobDefOf.MI_NpcPainAssist, endAfterTendedOnce = true };
        NpcMedicalAttempts.Begin(doctor, job, patient, dose);
        NpcMedicalAttempts.Applying(job); patient.RawPain *= .5f;
        NpcMedicalAttempts.Applied(job, true); NpcMedicalAttempts.Finish(job, JobCondition.Succeeded);
        Assert.IsTrue(NpcMedicalAttempts.Allowed(doctor, patient, job.def, dose));
        Assert.IsTrue(NpcMedicalAi.State(patient).Drugs.ClinicalAllowed(Find.TickManager.TicksGame, 600));
        Assert.IsTrue(NpcMedicalAi.State(patient).Drugs.PerformanceAllowed(Find.TickManager.TicksGame, 1, 20000));
    }

    [TestMethod]
    public void ModuleDisabledDuringApplicationStillReturnsUnusedSelectedSupplies()
    {
        Pawn doctor = new(), patient = Wounded(); Thing supply = Medicine();
        doctor.CurJob = Tend(patient, supply);
        JobDriver_NpcTend driver = new() { pawn = doctor };
        driver.BeforeToil = (_, _) =>
        {
            if (doctor.carryTracker.CarriedThing is null) { return; }
            MoreInjuriesMod.Settings.EnableNpcCombatMedicine = false;
            doctor.jobs.EndCurrentJob(JobCondition.InterruptForced);
        };
        try
        {
            driver.Run();
            Assert.AreSame(supply, doctor.inventory.innerContainer.Items.Single());
            Assert.IsNull(doctor.carryTracker.CarriedThing);
        }
        finally { MoreInjuriesMod.Settings.EnableNpcCombatMedicine = true; }
    }

    [TestMethod]
    public void ManualAndColonistJobsDoNotAcquireAutomaticFailureHistory()
    {
        Pawn doctor = new(), patient = Wounded(); Thing supply = Medicine(); Job job = Tend(patient, supply);
        job.playerForced = true;
        Assert.IsFalse(NpcMedicalAttempts.Automatic(doctor, job));
        NpcMedicalAttempts.Begin(doctor, job, patient, supply); NpcMedicalAttempts.Finish(job, JobCondition.Incompletable);
        Assert.IsTrue(NpcMedicalAttempts.Allowed(doctor, patient, job.def, supply));
        job.playerForced = false; doctor.ManagedByMi = false;
        Assert.IsFalse(NpcMedicalAttempts.Automatic(doctor, job));
        NpcMedicalAttempts.Begin(doctor, job, patient, supply); NpcMedicalAttempts.Finish(job, JobCondition.Incompletable);
        Assert.IsTrue(NpcMedicalAttempts.Allowed(doctor, patient, job.def, supply));
    }

    [TestMethod]
    public void DevelopmentDiagnosticsLogTransitionsRatherThanEveryEvaluation()
    {
        MoreInjuriesMod.Settings.EnableLogging = true;
        Pawn doctor = new(), patient = Wounded(); Thing supply = Medicine(); Job job = Tend(patient, supply);
        bool previous = Prefs.DevMode;
        try
        {
            Prefs.DevMode = true; int initial = Log.Messages.Count;
            for (int i = 0; i < 20; i++) { Assert.IsTrue(NpcMedicalAttempts.Allowed(doctor, patient, job.def, supply)); }
            Assert.AreEqual(initial + 1, Log.Messages.Count);
            NpcMedicalAttempts.Begin(doctor, job, patient, supply);
            NpcMedicalAttempts.Stage(job, "delivery"); NpcMedicalAttempts.Stage(job, "delivery");
            NpcMedicalAttempts.Finish(job, JobCondition.InterruptForced);
            for (int i = 0; i < 20; i++) { Assert.IsFalse(NpcMedicalAttempts.Allowed(doctor, patient, job.def, supply)); }
            Assert.AreEqual(initial + 5, Log.Messages.Count);
        }
        finally { Prefs.DevMode = previous; }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void ClearedNativeJobCompletesUsingCapturedActionExactlyOnce(int mode)
    {
        MoreInjuriesMod.Settings.NpcMedicalAiMode = mode;
        Pawn doctor = new(), patient = Wounded();
        Job job = Tend(patient, Medicine()); job.loadID = 123;
        var attempt = NpcMedicalAttempts.Begin(doctor, job, patient, job.targetB.Thing)!;
        job.def = null!; job.loadID = -1;
        Assert.IsFalse(attempt.Matches(job));
        NpcMedicalAttempts.Finish(attempt, JobCondition.InterruptOptional);
        NpcMedicalAttempts.Finish(attempt, JobCondition.InterruptOptional);
        Assert.IsFalse(NpcMedicalAttempts.Ready(doctor, patient, KnownJobDefOf.MI_NpcTend, job.targetB.Thing));
        Find.TickManager.TicksGame += 150;
        Assert.IsTrue(NpcMedicalAttempts.Ready(doctor, patient, KnownJobDefOf.MI_NpcTend, job.targetB.Thing));
    }

    [TestMethod]
    public void DelayedObserverCannotCompleteReplacementUsingSamePooledJob()
    {
        Pawn doctor = new(), patient = Wounded(), replacementPatient = Wounded();
        Job job = Tend(patient, Medicine()); job.loadID = 123;
        var old = NpcMedicalAttempts.Begin(doctor, job, patient, job.targetB.Thing)!;
        job.loadID = 124; job.def = KnownJobDefOf.MI_NpcPainAssist;
        var replacement = NpcMedicalAttempts.Begin(doctor, job, replacementPatient, null)!;
        NpcMedicalAttempts.Finish(old, JobCondition.InterruptOptional);
        Assert.IsTrue(replacement.Matches(job));
        Assert.IsFalse(replacement.Completed);
        NpcMedicalAttempts.Finish(replacement, JobCondition.Incompletable);
        Assert.IsFalse(NpcMedicalAttempts.Ready(doctor, replacementPatient, KnownJobDefOf.MI_NpcPainAssist, null));
    }

    private static Pawn Wounded()
    {
        Pawn patient = new(); patient.health.hediffSet.hediffs.Add(new() { pawn = patient, def = new(), Severity = .2f }); return patient;
    }
    private static Thing Medicine() => new() { def = new() { IsMedicine = true } };
    private static Job Tend(Pawn patient, Thing medicine) => new()
    { def = KnownJobDefOf.MI_NpcTend, targetA = new(patient), targetB = new(medicine), endAfterTendedOnce = true, startTick = Find.TickManager.TicksGame };
}
