using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.AI;
using MoreInjuries.AI.Jobs;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.HarmonyTests;

[TestClass, DoNotParallelize]
public sealed class FirstAidTests
{
    [TestInitialize]
    public void Initialize()
    {
        new NpcPainCareTests().Initialize();
        EmergencyCare.Emergency = null; Toils_Tend.TendLimit = int.MaxValue;
    }
    [TestCleanup]
    public void Cleanup() { EmergencyCare.Emergency = null; Toils_Tend.TendLimit = int.MaxValue; }

    private static (Pawn Doctor, Pawn Patient) Pair(int wounds = 1)
    {
        Pawn doctor = new() { Drafted = true, ManagedByMi = false, Position = new(10, 0, 10) };
        Pawn patient = new() { Downed = true, Hostile = true, Map = doctor.Map, Position = new(13, 0, 10), playerSettings = new() };
        for (int index = 0; index < wounds; index++)
        { patient.health.hediffSet.hediffs.Add(new() { pawn = patient, def = new() }); }
        doctor.CurJob = JobDriver_ProvideFirstAid.GetDispatcher(doctor, patient).CreateJob();
        doctor.CurJob.playerForced = true;
        return (doctor, patient);
    }
    private static Thing Medicine(Pawn owner, int count = 4, float potency = 1f)
    {
        Thing medicine = new() { def = new() { IsMedicine = true }, stackCount = count, MedicinePotency = potency,
            Spawned = false, ParentHolder = owner.inventory, Map = owner.Map };
        owner.inventory.innerContainer.Items.Add(medicine); return medicine;
    }
    private static JobDriver_ProvideFirstAid Driver(Pawn doctor) => new() { pawn = doctor, StepLimit = 200 };

    [TestMethod]
    public void ExplicitOrderTendsAllEnemyWoundsWithOwnInventoryWhileNpcMedicineIsDisabled()
    {
        (Pawn doctor, Pawn patient) = Pair(6); Thing medicine = Medicine(doctor, 10);
        MoreInjuriesMod.Settings.EnableNpcCombatMedicine = false; Toils_Tend.TendLimit = 1;
        JobDriver_ProvideFirstAid driver = Driver(doctor);
        Assert.IsTrue(driver.TryMakePreToilReservations(false));
        driver.Run();
        Assert.IsTrue(patient.health.hediffSet.hediffs.All(wound => wound.IsTended()));
        Assert.AreEqual(4, medicine.stackCount); Assert.IsNull(doctor.carryTracker.CarriedThing);
        Assert.AreEqual(JobCondition.Succeeded, doctor.jobs.Ended);
        Assert.HasCount(0, doctor.jobs.jobQueue);
        Assert.IsTrue(doctor.CurJob.def == KnownJobDefOf.ProvideFirstAid, "No bare native TendPatient job is dispatched.");
    }

    [TestMethod]
    public void InventorySelectionUsesFeasibleMedicineAndHonorsMedicalCare()
    {
        (Pawn doctor, Pawn patient) = Pair();
        Thing weak = Medicine(doctor, potency: .5f), strong = Medicine(doctor, potency: 1.6f);
        Assert.AreSame(strong, FirstAidTending.FindMedicine(doctor, patient));
        strong.Forbidden = true;
        Assert.AreSame(weak, FirstAidTending.FindMedicine(doctor, patient));
        strong.Forbidden = false;
        Pawn other = new() { Map = doctor.Map }; doctor.Map.reservationManager.Reserve(other, new(), strong);
        Assert.AreSame(weak, FirstAidTending.FindMedicine(doctor, patient));
        patient.playerSettings.medCare = MedicalCareCategory.NoMeds;
        Driver(doctor).Run(); Assert.IsTrue(patient.health.hediffSet.hediffs.All(wound => wound.IsTended()));
        Assert.AreEqual(4, weak.stackCount); Assert.AreEqual(4, strong.stackCount);
    }

    [TestMethod]
    public void PatientInventoryAndReachableGroundSuppliesAreRetrievedPhysically()
    {
        foreach (bool inventory in new[] { true, false })
        {
            (Pawn doctor, Pawn patient) = Pair(); Thing medicine = Medicine(patient);
            if (!inventory)
            {
                patient.inventory.innerContainer.Items.Remove(medicine); medicine.ParentHolder = null;
                medicine.Spawned = true; medicine.PositionHeld = new(15, 0, 10);
                doctor.Supplies.Add(medicine);
            }
            Driver(doctor).Run(); Assert.IsTrue(patient.health.hediffSet.hediffs.All(wound => wound.IsTended()));
            Assert.AreEqual(3, medicine.stackCount); Assert.IsFalse(medicine.Destroyed);
            Assert.AreEqual(patient.Position, doctor.Position);
        }
    }

    [TestMethod]
    public void ForeignReservationAndUnrelatedCargoPreventFieldTendingWithoutItemLoss()
    {
        foreach (bool reserved in new[] { true, false })
        {
            (Pawn doctor, Pawn patient) = Pair(); Thing medicine = Medicine(doctor), cargo = new();
            if (reserved) { doctor.Map.reservationManager.Reserve(new() { Map = doctor.Map }, new(), patient); }
            else { doctor.carryTracker.innerContainer.Items.Add(cargo); }
            Driver(doctor).Run(); Assert.IsFalse(patient.health.hediffSet.hediffs.Single().IsTended());
            Assert.AreEqual(4, medicine.stackCount);
            if (!reserved) { Assert.AreSame(cargo, doctor.carryTracker.CarriedThing); }
        }
    }

    [TestMethod]
    public void RescanPreemptsFurtherTendingForAnEmergencyAndQueuesOnlyAcceptedWork()
    {
        foreach (bool accepted in new[] { true, false })
        {
            (Pawn doctor, Pawn patient) = Pair(3); Thing medicine = Medicine(doctor);
            Toils_Tend.TendLimit = 1; doctor.jobs.AcceptOrderedJobs = accepted;
            Job rescue = new() { def = new() { defName = "emergency-resuscitation" }, targetA = new(patient) };
            EmergencyCare.Emergency = (_, target) => target.health.hediffSet.hediffs.Any(wound => wound.IsTended()) ? rescue : null;
            Driver(doctor).Run();
            Assert.AreEqual(1, patient.health.hediffSet.hediffs.Count(wound => wound.IsTended()));
            Assert.AreEqual(3, medicine.stackCount);
            Assert.AreEqual(accepted ? 1 : 0, doctor.jobs.jobQueue.Count);
            if (accepted) { Assert.AreSame(rescue, doctor.CurJob); }
            else { Assert.AreEqual(JobCondition.Incompletable, doctor.jobs.Ended); }
        }
    }

    [TestMethod]
    public void InterruptionRecoveryAndFinalCareChangesPreserveUnusedMedicine()
    {
        foreach (int interruption in Enumerable.Range(0, 5))
        {
            (Pawn doctor, Pawn patient) = Pair(); Thing medicine = Medicine(doctor);
            JobDriver_ProvideFirstAid driver = Driver(doctor);
            driver.BeforeToil = (_, _) =>
            {
                if (doctor.carryTracker.CarriedThing is null) { return; }
                switch (interruption)
                {
                    case 0: doctor.jobs.EndCurrentJob(JobCondition.InterruptForced); break;
                    case 1: patient.Downed = false; break;
                    case 2: patient.playerSettings.medCare = MedicalCareCategory.NoMeds; break;
                    case 3: doctor.DoctorCapable = false; break;
                    case 4: patient.Dead = true; break;
                }
            };
            driver.Run();
            Assert.IsFalse(patient.health.hediffSet.hediffs.Single().IsTended());
            Assert.AreEqual(4, doctor.inventory.innerContainer.Items.Where(item => item.def.IsMedicine).Sum(item => item.stackCount));
            Assert.IsNull(doctor.carryTracker.CarriedThing);
            Assert.IsFalse(medicine.Destroyed);
        }
    }

    [TestMethod]
    public void SavedInventoryAndCarriedContinuationKeepStableIndicesAndCompleteCare()
    {
        foreach (bool carried in new[] { true, false })
        {
            (Pawn doctor, Pawn patient) = Pair(); Thing medicine = Medicine(doctor);
            List<Toil> original = Driver(doctor).BuildToils();
            Assert.IsTrue(FirstAidTending.Prepare(doctor, patient, doctor.CurJob));
            int resume = carried ? 6 : 1;
            if (carried)
            { Assert.IsTrue(MoreInjuries.AI.NpcCombatMedicine.NpcMedicalReservations.TakeMedicine(doctor, doctor.CurJob, medicine, doctor.inventory.innerContainer)); }
            JobDriver_ProvideFirstAid loaded = Driver(doctor); loaded.ResumeIndex = resume;
            Assert.HasCount(original.Count, loaded.BuildToils()); loaded.Run();
            Assert.IsTrue(patient.health.hediffSet.hediffs.Single().IsTended()); Assert.AreEqual(3, medicine.stackCount);
        }
    }

    [TestMethod]
    public void ARejectedNativeTendDoesNotConsumeAnUnboundedCourseOfMedicine()
    {
        (Pawn doctor, Pawn patient) = Pair(); Thing medicine = Medicine(doctor, 20);
        Toils_Tend.TendLimit = 0; Driver(doctor).Run();
        Assert.AreEqual(JobCondition.Incompletable, doctor.jobs.Ended);
        Assert.AreEqual(19, medicine.stackCount); Assert.IsFalse(patient.health.hediffSet.hediffs.Single().IsTended());
    }

    [TestMethod]
    public void MissingOrForbiddenMedicineFallsBackToHandsWithoutUsingOtherItems()
    {
        (Pawn doctor, Pawn patient) = Pair(); Thing medicine = Medicine(doctor); medicine.Forbidden = true;
        Thing painkiller = new() { def = KnownThingDefOf.Morphine, ParentHolder = doctor.inventory, Spawned = false };
        doctor.inventory.innerContainer.Items.Add(painkiller);
        Driver(doctor).Run();
        Assert.IsTrue(patient.health.hediffSet.hediffs.Single().IsTended());
        Assert.AreEqual(4, medicine.stackCount); Assert.IsFalse(painkiller.Destroyed);
    }
}
