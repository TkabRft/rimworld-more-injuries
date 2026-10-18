using System.Globalization;
using System.Xml.Linq;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.VitalFailure;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class AirwayRegressionTests
{
    [TestMethod]
    public void CprChokingReductionIsScaledButSuctionStillClearsFully()
    {
        foreach (int skill in new[] { 0, 5, 10, 15, 20 })
        foreach (float roll in new[] { 0f, .5f, 1f })
        {
            Pawn pawn = VitalFailureTests.Patient(); pawn.MedicalSkill = skill;
            Rand.RangeFraction = roll;
            Hediff choking = VitalFailureTests.Add(pawn, KnownHediffDefOf.ChokingOnBlood, .5f);
            new MoreInjuries.HealthConditions.CardiacArrest.JobDriver_PerformCpr().TestApply(pawn, pawn);
            float raw = (1f / (1f + MathF.Exp(-10f * (skill / 15f - .5f))) - .1f + .2f * roll) * .35f;
            Assert.AreEqual(.5f - raw, choking.Severity, .00001f);
            Rand.ChanceHandler = chance => chance >= 1;
            pawn.MedicalSkill = 8;
            new MoreInjuries.HealthConditions.Choking.JobDriver_UseSuctionDevice().TestApply(pawn, pawn, new Thing());
            Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.ChokingOnBlood));
        }
        Rand.ChanceHandler = null!;
    }

    [TestMethod]
    public void AutomaticSuctionSearchIsBoundedAndRespectsPriorityAndRestrictions()
    {
        Pawn patient = VitalFailureTests.Patient(), doctor = VitalFailureTests.Patient();
        Map map = new(); patient.MapHeld = doctor.Map = map;
        VitalFailureTests.Add(patient, KnownHediffDefOf.ChokingOnBlood, 1);
        GenClosest.Calls = 0; GenClosest.Device = new Thing { PositionHeld = 8 };
        KnownResearchProjectDefOf.EmergencyMedicine.IsFinished = true;
        Assert.AreSame(KnownJobDefOf.UseSuctionDevice, MoreInjuries.HealthConditions.Choking.AutomaticAirwayTreatment.CreateJob(doctor, patient).def);
        doctor.CarriedDevice = new(); int calls = GenClosest.Calls;
        MoreInjuries.HealthConditions.Choking.AutomaticAirwayTreatment.CreateJob(doctor, patient);
        Assert.AreEqual(calls, GenClosest.Calls);
        doctor.CarriedDevice = null;
        foreach (int distance in new[] { 13, 1000 })
        {
            GenClosest.Device.PositionHeld = distance;
            Assert.AreSame(KnownJobDefOf.PerformCpr, MoreInjuries.HealthConditions.Choking.AutomaticAirwayTreatment.CreateJob(doctor, patient).def);
        }
        GenClosest.Device.PositionHeld = 1; GenClosest.Device.Forbidden = true;
        Assert.AreSame(KnownJobDefOf.PerformCpr, MoreInjuries.HealthConditions.Choking.AutomaticAirwayTreatment.CreateJob(doctor, patient).def);
        GenClosest.Device.Forbidden = false; GenClosest.Device.Reservable = false;
        Assert.AreSame(KnownJobDefOf.PerformCpr, MoreInjuries.HealthConditions.Choking.AutomaticAirwayTreatment.CreateJob(doctor, patient).def);
        VitalFailureTests.Add(patient, KnownHediffDefOf.CardiacArrest, .1f); calls = GenClosest.Calls;
        Assert.AreSame(KnownJobDefOf.PerformCpr, MoreInjuries.HealthConditions.Choking.AutomaticAirwayTreatment.CreateJob(doctor, patient).def);
        Assert.AreEqual(calls, GenClosest.Calls);
        GenClosest.Device = null;
    }

    [TestMethod]
    public void XmlChokingCeilingMustReachControllerAndReadOnlyForecast()
    {
        Pawn pawn = VitalFailureTests.Patient();
        XElement xml = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs/HediffDefs/Hediffs_Choking.xml"))
            .Root!.Elements().Single(node => node.Element("defName")?.Value == "ChokingOnBlood");
        KnownHediffDefOf.ChokingOnBlood.stages = xml.Element("stages")!.Elements().Select(node => new HediffStage
        {
            minSeverity = float.Parse(node.Element("minSeverity")?.Value ?? "0", CultureInfo.InvariantCulture),
            capMods = [new() { capacity = PawnCapacityDefOf.Breathing,
                setMax = float.Parse(node.Element("capMods")!.Element("li")!.Element("setMax")!.Value, CultureInfo.InvariantCulture) }]
        }).ToList();
        try
        {
            Hediff choking = VitalFailureTests.Add(pawn, KnownHediffDefOf.ChokingOnBlood, 1f);
            VitalFailureTests.Add(pawn, HediffDefOf.BloodLoss, .5f);
            VitalFailureTests.Add(pawn, KnownHediffDefOf.HypovolemicShock, .3f);
            Assert.AreEqual(1f, PawnCapacityDefOf.Breathing.Worker.CalculateCapacityLevel(pawn.health.hediffSet));
            Assert.AreEqual(0f, PawnCapacityUtility.CalculateCapacityLevel(pawn.health.hediffSet, PawnCapacityDefOf.Breathing));
            pawn.Vital!.TryGetPrognosis(out PhysiologySnapshot snapshot, out PhysiologyEstimate estimate);
            Assert.AreEqual(0f, snapshot.Breathing);
            Assert.IsTrue(estimate.Ticks < int.MaxValue);
            VitalFailureTests.Tick(pawn, 250);
            Assert.IsTrue(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_RespiratoryFailure));
            Hediff injury = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.BrainDamage_Hypoxia)!;
            Assert.IsNotNull(injury);
            float previous = injury.Severity;
            pawn.health.RemoveHediff(choking); pawn.Vital.Invalidate();
            VitalFailureTests.Tick(pawn, 500);
            Assert.IsFalse(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_RespiratoryFailure));
            Assert.AreEqual(previous, injury.Severity);
        }
        finally { KnownHediffDefOf.ChokingOnBlood.stages = null; }
    }
}
