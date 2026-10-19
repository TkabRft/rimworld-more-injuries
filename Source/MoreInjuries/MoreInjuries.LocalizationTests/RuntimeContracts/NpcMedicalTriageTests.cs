using MoreInjuries.AI;
using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.Choking;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass, DoNotParallelize]
public sealed class NpcMedicalTriageTests
{
    [TestCleanup]
    public void Cleanup()
    {
        Rand.ChanceHandler = null!;
        MoreInjuriesMod.Settings.EnableAdrenaline = false;
        KnownResearchProjectDefOf.Cpr.IsFinished = true;
        KnownResearchProjectDefOf.EmergencyMedicine.IsFinished = true;
    }

    private static EmergencyCare.NpcContext Context(Pawn doctor) => new()
    {
        Cpr = true, Emergency = true,
        Find = def => doctor.Devices.TryGetValue(def, out Thing? device) ? device : null
    };

    [TestMethod]
    public void IsolatedNonTendableChokingEntersNpcTriageBeforeBreathingCollapse()
    {
        foreach ((float severity, float breathing) in new[] { (.05f, .95f), (.2f, .85f), (.35f, .7f), (.5f, .55f), (.75f, .35f), (.85f, .15f) })
        {
            Pawn doctor = VitalFailureTests.Patient(), patient = VitalFailureTests.Patient();
            VitalFailureTests.Add(patient, KnownHediffDefOf.ChokingOnBlood, severity);
            patient.health.capacities.Levels[PawnCapacityDefOf.Breathing] = breathing;
            Assert.IsFalse(patient.health.HasHediffsNeedingTend());
            Assert.AreEqual(0f, patient.health.hediffSet.BleedRateTotal);
            float urgency = NpcMedicalTriage.Urgency(patient);
            Assert.IsTrue(urgency >= 70f);
            Assert.AreEqual(1, NpcMedicalModel.CareRank(4, urgency, true, true, true, true));
            Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, false, Context(doctor))!.def);
            doctor.Devices[KnownThingDefOf.SuctionDevice] = new();
            Assert.AreSame(KnownJobDefOf.UseSuctionDevice, EmergencyCare.CreateJob(doctor, patient, false, Context(doctor))!.def);
        }
    }

    [TestMethod]
    public void NpcSuctionWorksWithoutCprAndCanClearItsOwnAirway()
    {
        Pawn doctor = VitalFailureTests.Patient(); doctor.MedicalSkill = 8;
        VitalFailureTests.Add(doctor, KnownHediffDefOf.ChokingOnBlood, .5f);
        Thing device = new(); doctor.Devices[KnownThingDefOf.SuctionDevice] = device;
        KnownResearchProjectDefOf.Cpr.IsFinished = false;
        KnownResearchProjectDefOf.EmergencyMedicine.IsFinished = false;
        EmergencyCare.NpcContext context = Context(doctor); context.Cpr = false;
        Job? job = EmergencyCare.CreateJob(doctor, doctor, false, context);
        Assert.AreSame(KnownJobDefOf.UseSuctionDevice, job!.def);
        Assert.IsTrue(job.endAfterTendedOnce);
        Assert.IsNull(EmergencyCare.CreateJob(doctor, doctor, false), "Player self-care selection must keep its existing policy.");
        doctor.Devices.Clear(); context.Cpr = true;
        Assert.IsNull(EmergencyCare.CreateJob(doctor, doctor, false, context), "Self-suction must not enable self-CPR.");
        doctor.Devices[KnownThingDefOf.SuctionDevice] = device;
        Rand.ChanceHandler = chance => chance >= 1f;
        new JobDriver_UseSuctionDevice().TestApply(doctor, doctor, device);
        Assert.IsFalse(doctor.health.hediffSet.HasHediff(KnownHediffDefOf.ChokingOnBlood));
        Assert.IsFalse(device.Destroyed);
        Assert.AreEqual(0f, NpcMedicalTriage.Urgency(doctor));
    }

    [TestMethod]
    public void AirwayEquipmentStillHonorsCareCapabilitiesAndItemAvailability()
    {
        Pawn doctor = VitalFailureTests.Patient(), patient = VitalFailureTests.Patient(); patient.playerSettings = new();
        VitalFailureTests.Add(patient, KnownHediffDefOf.ChokingOnBlood, .5f);
        Thing device = new(); doctor.Devices[KnownThingDefOf.SuctionDevice] = device;
        EmergencyCare.NpcContext context = Context(doctor); context.Cpr = false;
        device.Reservable = false; Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false, context));
        device.Reservable = true; device.Forbidden = true; Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false, context));
        device.Forbidden = false; patient.playerSettings.medCare = MedicalCareCategory.NoCare;
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false, context));
        patient.playerSettings.medCare = MedicalCareCategory.Best;
        doctor.health.capacities.Incapable.Add(PawnCapacityDefOf.Manipulation);
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false, context));
    }

    [TestMethod]
    public void VolumeDeficitsRemainEligibleAfterBleedingStopsAndPermitFeasibleSelfCare()
    {
        foreach (HediffDef condition in new[] { HediffDefOf.BloodLoss, KnownHediffDefOf.Hemodilution })
        {
            Pawn doctor = VitalFailureTests.Patient();
            VitalFailureTests.Add(doctor, condition, .6f);
            Assert.IsFalse(doctor.health.HasHediffsNeedingTend());
            Assert.AreEqual(0f, doctor.health.hediffSet.BleedRateTotal);
            Assert.AreEqual(70f, NpcMedicalTriage.Urgency(doctor));
            doctor.Devices[KnownThingDefOf.BloodBag] = new();
            Assert.AreSame(KnownJobDefOf.Blood, EmergencyCare.CreateJob(doctor, doctor, false, Context(doctor))!.def);
        }
        Pawn salineUser = VitalFailureTests.Patient();
        VitalFailureTests.Add(salineUser, HediffDefOf.BloodLoss, .6f);
        salineUser.Devices[KnownThingDefOf.SalineBag] = new();
        Assert.AreSame(KnownJobDefOf.Saline, EmergencyCare.CreateJob(salineUser, salineUser, false, Context(salineUser))!.def);
    }

    [TestMethod]
    public void VolumeTriageUsesTheClinicalThresholdWithoutTreatingRoutineDonation()
    {
        Pawn doctor = VitalFailureTests.Patient();
        Hediff loss = VitalFailureTests.Add(doctor, HediffDefOf.BloodLoss, .449f);
        doctor.Devices[KnownThingDefOf.BloodBag] = new();
        Assert.AreEqual(0f, NpcMedicalTriage.Urgency(doctor));
        Assert.IsNull(EmergencyCare.CreateJob(doctor, doctor, false, Context(doctor)));
        loss.Severity = .4495f;
        Assert.AreEqual(70f, NpcMedicalTriage.Urgency(doctor));
        Assert.AreSame(KnownJobDefOf.Blood, EmergencyCare.CreateJob(doctor, doctor, false, Context(doctor))!.def);
    }

    [TestMethod]
    public void UntreatableLifeThreatAndHeartAttackAreRecognizedWithoutInventingJobs()
    {
        Pawn doctor = VitalFailureTests.Patient(), patient = VitalFailureTests.Patient();
        Hediff organFailure = VitalFailureTests.Add(patient, new(), .6f); organFailure.IsCurrentlyLifeThreatening = true;
        Assert.AreEqual(70f, NpcMedicalTriage.Urgency(patient));
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false, Context(doctor)));
        patient.health.RemoveHediff(organFailure);
        VitalFailureTests.Add(patient, KnownHediffDefOf.HeartAttack, .1f);
        Assert.AreEqual(70f, NpcMedicalTriage.Urgency(patient));
        doctor.Devices[KnownThingDefOf.Defibrillator] = new();
        Assert.AreSame(KnownJobDefOf.UseDefibrillator, EmergencyCare.CreateJob(doctor, patient, false, Context(doctor))!.def);
    }

    [TestMethod]
    public void TourniquetAftercareRemainsEligibleAfterTendingAndStopsAfterRemoval()
    {
        Pawn doctor = VitalFailureTests.Patient(), patient = VitalFailureTests.Patient();
        Hediff tourniquet = VitalFailureTests.Add(patient, KnownHediffDefOf.TourniquetApplied, .2f); tourniquet.Part = new();
        Assert.AreEqual(10f, NpcMedicalTriage.Urgency(patient));
        Assert.AreSame(KnownJobDefOf.RemoveTourniquet, EmergencyCare.CreateSafeTourniquetRemoval(doctor, patient)!.def);
        patient.health.RemoveHediff(tourniquet);
        Assert.AreEqual(0f, NpcMedicalTriage.Urgency(patient));
        Assert.IsNull(EmergencyCare.CreateSafeTourniquetRemoval(doctor, patient));
    }
}
