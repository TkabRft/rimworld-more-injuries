using MoreInjuries.AI.Jobs;
using MoreInjuries.AI.Jobs.Outcomes;
using MoreInjuries.HealthConditions.Drugs;
using System.Xml.Linq;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class DrugAdministrationTests
{
    private sealed class Driver : JobDriver_OutcomeDoerBase
    {
        public bool Apply(Pawn doctor, Pawn patient, Thing device) => ApplyDevice(doctor, patient, device);
    }

    private sealed class CallbackOutcome(Func<Pawn, Pawn, Thing, bool> callback) : JobOutcomeDoer
    {
        protected override bool DoOutcome(Pawn doctor, Pawn patient, Thing? device) => callback(doctor, patient, device!);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(10)]
    public void AdministeredDrugUsesOneLiveDoseAndOneVanillaIngestion(int count)
    {
        Pawn doctor = new();
        Pawn patient = new();
        int ingestions = 0;
        List<string> events = [];
        Thing stack = new() { stackCount = count };
        stack.OnIngested = (recipient, dose) =>
        {
            Assert.AreSame(patient, recipient);
            Assert.AreSame(doctor, MedicalDrugAdministration.GetDoctor(dose));
            Assert.IsNull(MedicalDrugAdministration.GetDoctor(new Thing()));
            ingestions++;
            events.Add("ingestion");
        };
        stack.def.Extension.OutcomeDoers =
        [
            new CallbackOutcome((actor, recipient, dose) =>
            {
                Assert.AreSame(doctor, actor);
                Assert.AreSame(patient, recipient);
                Assert.IsFalse(dose.Destroyed);
                events.Add("social");
                return true;
            }),
            new JobOutcomeDoer_IngestDrug()
        ];
        Assert.IsTrue(new Driver().Apply(doctor, patient, stack));
        Assert.AreEqual(1, ingestions);
        CollectionAssert.AreEqual(new[] { "social", "ingestion" }, events);
        Assert.AreEqual(count == 1, stack.Destroyed);
        Assert.AreEqual(count == 1 ? 1 : count - 1, stack.stackCount);
        Assert.IsNull(MedicalDrugAdministration.GetDoctor(stack));
    }

    [TestMethod]
    public void NonDrugDeviceRemainsLiveDuringOutcomeAndIsConsumedOnce()
    {
        Thing stack = new() { stackCount = 3 };
        Thing? applied = null;
        stack.def.Extension.OutcomeDoers = [new CallbackOutcome((_, _, dose) =>
        {
            applied = dose;
            Assert.IsFalse(dose.Destroyed);
            return true;
        })];
        Assert.IsTrue(new Driver().Apply(new(), new(), stack));
        Assert.AreEqual(2, stack.stackCount);
        Assert.IsFalse(stack.Destroyed);
        Assert.IsTrue(applied!.Destroyed);
    }

    [TestMethod]
    public void FailedOutcomeConsumesOnlyItsUnitAndStopsLaterOutcomes()
    {
        Thing stack = new() { stackCount = 3 };
        Thing? applied = null;
        stack.def.Extension.OutcomeDoers =
        [
            new CallbackOutcome((_, _, dose) => { applied = dose; return false; }),
            new CallbackOutcome((_, _, _) => throw new AssertFailedException("Executed after failure"))
        ];
        Driver driver = new();
        Assert.IsFalse(driver.Apply(new(), new(), stack));
        Assert.IsTrue(driver.Failed);
        Assert.AreEqual(2, stack.stackCount);
        Assert.IsTrue(applied!.Destroyed);
    }

    [TestMethod]
    public void NestedAdministrationAndExceptionsRestoreDoctorContext()
    {
        Pawn doctor = new();
        Pawn otherDoctor = new();
        Thing inner = new() { OnIngested = (_, dose) =>
        {
            Assert.AreSame(otherDoctor, MedicalDrugAdministration.GetDoctor(dose));
            throw new InvalidOperationException("outcome failed");
        }};
        Thing outer = new() { OnIngested = (_, dose) =>
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => MedicalDrugAdministration.Administer(otherDoctor, new(), inner));
            Assert.AreSame(doctor, MedicalDrugAdministration.GetDoctor(dose));
            Assert.IsNull(MedicalDrugAdministration.GetDoctor(inner));
        }};
        MedicalDrugAdministration.Administer(doctor, new(), outer);
        Assert.IsNull(MedicalDrugAdministration.GetDoctor(outer));
    }

    [TestMethod]
    [DataRow(0, 0.6f)]
    [DataRow(20, 0.375f)]
    public void ChloroformKeepsDoctorSkillDoseAndDirectUseBaseline(int skill, float expected)
    {
        Rand.RangeFraction = 0.5f;
        IngestionOutcomeDoer_ChloroformBuildup outcome = new() { severity = 0.35f };
        Pawn patient = new();
        Thing dose = new() { OnIngested = (recipient, ingested) => outcome.DoIngestionOutcome(recipient, ingested, 1) };
        MedicalDrugAdministration.Administer(new Pawn { MedicalSkill = skill }, patient, dose);
        Assert.AreEqual(expected, patient.health.hediffSet.GetFirstHediffOfDef(outcome.hediffDef)!.Severity, 0.00001f);
        Pawn directUser = new();
        outcome.DoIngestionOutcome(directUser, new(), 1);
        Assert.AreEqual(0.35f, directUser.health.hediffSet.GetFirstHediffOfDef(outcome.hediffDef)!.Severity, 0.00001f);
    }

    [TestMethod]
    public void EveryMedicalDrugHasExactlyOneSharedIngestionOutcome()
    {
        XDocument defs = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs", "ThingDefs", "Medicine", "Medicine_Drugs.xml"));
        foreach (string name in new[] { "Epinephrine", "Ketamine", "Morphine", "Chloroform" })
        {
            XElement drug = defs.Root!.Elements("ThingDef").Single(def => def.Element("defName")?.Value == name);
            XElement[] outcomes = drug.Element("modExtensions")!.Element("li")!.Element("outcomeDoers")!.Elements("li").ToArray();
            Assert.AreEqual(1, outcomes.Count(outcome => outcome.Attribute("Class")!.Value.EndsWith("JobOutcomeDoer_IngestDrug")), name);
            Assert.IsFalse(outcomes.Any(outcome => outcome.Attribute("Class")!.Value.Contains("HediffOffset")), name);
            Assert.IsTrue(drug.Element("ingestible")!.Element("outcomeDoers")!.Elements().Any(), name);
            if (name is "Ketamine" or "Chloroform") { Assert.AreEqual(3, outcomes.Length, "Preserve both social outcomes"); }
        }
    }
}
