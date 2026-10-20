using System.Globalization;
using System.Xml.Linq;
using MoreInjuries.AI;
using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass, DoNotParallelize]
public sealed class NpcCombatMedicineTests
{
    [TestMethod]
    public void LocalExtractionIsDistinctFromExitEvacuationAndHasNativeResumeSupport()
    {
        XDocument jobs = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs", "Internal", "NpcMedicalJobs.xml"));
        XElement extraction = jobs.Root!.Elements("JobDef").Single(def => def.Element("defName")?.Value == "MI_NpcExtract");
        Assert.AreEqual("true", extraction.Element("suspendable")?.Value);
        Assert.AreEqual("false", extraction.Element("casualInterruptible")?.Value);
        Assert.AreEqual("MoreInjuries.AI.NpcCombatMedicine.JobDriver_NpcExtract", extraction.Element("driverClass")?.Value);
        Assert.IsTrue(jobs.Root.Elements("JobDef").Any(def => def.Element("defName")?.Value == "MI_NpcEvacuate"));
        Assert.AreEqual("false", jobs.Root.Elements("JobDef").Single(def => def.Element("defName")?.Value == "MI_NpcTend").Element("suspendable")?.Value);
    }

    private static XDocument Profiles() => XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs", "Internal", "NpcMedicalProfiles.xml"));
    [TestMethod]
    public void TravelDeadlineDoesNotResetWithDecisionReevaluation()
    {
        Assert.IsFalse(NpcMedicalModel.TravelExpired(3499, 1000));
        Assert.IsTrue(NpcMedicalModel.TravelExpired(3500, 1000));
        Assert.IsTrue(NpcMedicalModel.TravelExpired(8000, 1000));
        Assert.IsFalse(NpcMedicalModel.TravelExpired(8000, -1));
        Assert.AreEqual(600, NpcMedicalModel.REPOSITION_TIMEOUT);
    }

    [TestMethod]
    public void InvalidMedicalWorkRatesCannotCreateEffectivelyEndlessApplicationWaits()
    {
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Assert.IsFalse(EmergencyPriority.ValidWorkRate(invalid, 1f));
            Assert.IsFalse(EmergencyPriority.ValidWorkRate(1f, invalid));
        }
        Assert.IsTrue(EmergencyPriority.ValidWorkRate(.05f, .1f), "Finite, slow but capable doctors retain normal procedure timing.");
    }

    [TestMethod]
    public void TechnologyQuotasSaturateWithoutMakingTribalsUniversallyEquipped()
    {
        Assert.AreEqual(0f, NpcMedicalModel.Strength(50));
        Assert.AreEqual(.5f, NpcMedicalModel.Strength(1700));
        Assert.AreEqual(1f, NpcMedicalModel.Strength(100000));
        Assert.AreEqual(50, NpcMedicalModel.Quota(100, NpcMedicalModel.Interpolate(.35f, .5f, 1)));
        Assert.AreEqual(99, NpcMedicalModel.Quota(100, NpcMedicalModel.Interpolate(.9f, .99f, 1)));
        Assert.AreEqual(100, NpcMedicalModel.Quota(100, .99f, 2));
        Assert.AreEqual(0, NpcMedicalModel.Quota(100, .5f, 0));
        Assert.AreEqual(9, NpcMedicalModel.Quantity(6, 1, 1));
    }

    [TestMethod]
    public void EmpireTwoThousandPointQuotaUsesHeadcountAndCanRoundToZero()
    {
        XElement ultra = Profiles().Root!.Elements().Single(profile => profile.Element("techLevel")!.Value == "Ultra");
        float[] range = ultra.Element("medicChance")!.Value.Split('~').Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        float chance = NpcMedicalModel.Interpolate(range[0], range[1], NpcMedicalModel.Strength(2000));
        Assert.AreEqual(.13076923f, chance, .000001f);
        Assert.AreEqual(0, NpcMedicalModel.Quota(3, chance));
        Assert.AreEqual(1, NpcMedicalModel.Quota(4, chance));
        Assert.AreEqual(1, NpcMedicalModel.Quota(11, chance));
        Assert.AreEqual(2, NpcMedicalModel.Quota(12, chance));
        Assert.AreEqual(0, NpcMedicalModel.Quota(12, chance, 0));
    }

    [TestMethod]
    public void FluidsUseSeventyFivePercentSelectionRatherThanMandatoryQuantityBundles()
    {
        Random random = new(102019);
        int saline = Enumerable.Range(0, 100000).Count(_ => NpcMedicalModel.Saline((float)random.NextDouble()));
        Assert.IsTrue(saline is > 74500 and < 75500, saline.ToString());
        Assert.IsTrue(NpcMedicalModel.Saline(.749f));
        Assert.IsFalse(NpcMedicalModel.Saline(.75f));
        foreach (XElement item in Profiles().Descendants("li").Where(item => item.Element("fluid") is not null))
        { Assert.IsNull(item.Element("thingDef"), "All profile fluid slots must use the shared active-blood resolver."); }
    }

    [TestMethod]
    public void ClinicalEpinephrineRemainsAvailableAfterPerformanceUseAndHasItsOwnRepeatLimit()
    {
        NpcDrugBudget budget = new();
        budget.Threat(1000);
        budget.PerformanceTaken(1000);
        Assert.IsFalse(budget.PerformanceAllowed(1100, 1, 20000));
        Assert.IsTrue(budget.ClinicalAllowed(1100, 600));
        budget.ClinicalTaken(1100);
        Assert.IsFalse(budget.ClinicalAllowed(1699, 600));
        Assert.IsTrue(budget.ClinicalAllowed(1700, 600));
        budget.Threat(3500);
        Assert.AreEqual(0, budget.Doses, "A new engagement resets the count, not the independent minimum interval.");
        Assert.IsFalse(budget.PerformanceAllowed(3500, 1, 20000));
        Assert.IsTrue(budget.PerformanceAllowed(21000, 1, 20000));
        Assert.IsFalse(budget.PerformanceAllowed(21000, 0, 0));
    }

    [TestMethod]
    public void CarePermissionsKeepSelfCareAndRestrictStrangersToEmergencies()
    {
        Assert.AreEqual(0, NpcMedicalModel.CareRank(0, 10, false, false, false, false));
        Assert.AreEqual(-1, NpcMedicalModel.CareRank(1, 90, false, true, true, true));
        Assert.AreEqual(-1, NpcMedicalModel.CareRank(3, 60, true, true, true, true));
        Assert.AreEqual(2, NpcMedicalModel.CareRank(3, 70, true, true, true, true));
        Assert.AreEqual(-1, NpcMedicalModel.CareRank(3, 90, true, true, false, true));
        Assert.AreEqual(-1, NpcMedicalModel.CareRank(4, 100, true, true, true, false));
        Assert.AreEqual(1, NpcMedicalModel.CareRank(4, 100, true, true, true, true));
        Assert.AreEqual(-1, NpcMedicalModel.CareRank(-1, 100, true, true, true, true));
    }

    [TestMethod]
    public void SurvivalTakeoverIsCapabilitySpecificAndDoesNotCreatePrisonBreaks()
    {
        for (int mode = 0; mode < 2; mode++)
        { Assert.IsFalse(NpcMedicalModel.Survival(true, mode, true, true, false, false, false)); }
        Assert.IsTrue(NpcMedicalModel.Survival(true, 2, true, true, false, false, false));
        Assert.IsFalse(NpcMedicalModel.Survival(true, 2, true, true, false, true, false));
        Assert.IsTrue(NpcMedicalModel.Survival(true, 2, true, true, false, true, true));
        Assert.IsFalse(NpcMedicalModel.Survival(false, 2, true, true, false, false, false));
        Assert.IsFalse(NpcMedicalModel.Survival(true, 2, true, true, true, false, false));
    }

    [TestMethod]
    public void TriageRecognizesInternalVolumeAndOrganFailureWithoutBandageAssumptions()
    {
        Assert.AreEqual(100f, NpcMedicalModel.Urgency(true, true, 0, 0, 0, 1, 1, false));
        Assert.AreEqual(90f, NpcMedicalModel.Urgency(false, true, 0, 0, 0, .2f, 1, false));
        Assert.AreEqual(70f, NpcMedicalModel.Urgency(false, false, .5f, .3f, 0, 1, 1, false));
        Assert.AreEqual(70f, NpcMedicalModel.Urgency(false, false, 0, 0, 0, .3f, 1, false));
        Assert.AreEqual(70f, NpcMedicalModel.Urgency(false, false, 0, .5f, .1f, 1, 1, false));
        Assert.AreEqual(70f, NpcMedicalModel.Urgency(false, false, 0, 0, 0, 1, 1, false, .3f));
        Assert.AreEqual(10f, NpcMedicalModel.Urgency(false, false, 0, 0, 0, 1, 1, true));
    }

    [TestMethod]
    public void NpcContextUsesCarriedSuppliesWithoutColonyResearchAndSupportsSelfHemostasis()
    {
        Pawn pawn = VitalFailureTests.Patient();
        pawn.health.hediffSet.NativeBleeding = true;
        Hediff wound = VitalFailureTests.Add(pawn, new(), 1);
        wound.TestBleedingEligible = true; wound.TestBleedRate = 1;
        bool originalResearch = KnownResearchProjectDefOf.BasicAnatomy.IsFinished;
        try
        {
            KnownResearchProjectDefOf.BasicAnatomy.IsFinished = false;
            Thing bandage = new();
            pawn.Devices[KnownThingDefOf.Bandage] = bandage;
            EmergencyCare.NpcContext context = new() { Find = def => def == KnownThingDefOf.Bandage ? bandage : null };
            Assert.IsNull(EmergencyCare.CreateJob(pawn, pawn, false), "Existing player self-selection remains unchanged.");
            Job? job = EmergencyCare.CreateJob(pawn, pawn, false, context);
            Assert.IsNotNull(job);
            Assert.AreSame(KnownJobDefOf.UseBandage, job.def);
        }
        finally { KnownResearchProjectDefOf.BasicAnatomy.IsFinished = originalResearch; }
    }

    [TestMethod]
    public void ProfilesNeverPutGlitterworldMedicineInNormalPersonalKitsAndCarrySedativesOnlyAsSupplies()
    {
        List<XElement> profiles = Profiles().Root!.Elements().ToList();
        Assert.HasCount(5, profiles);
        foreach (XElement profile in profiles)
        {
            Assert.IsFalse(profile.Element("personal")!.Descendants("thingDef").Any(def => def.Value == "MedicineUltratech"));
            string[] drugs = profile.Element("performanceDrugs")?.Elements().Select(def => def.Value).ToArray() ?? [];
            CollectionAssert.IsSubsetOf(drugs, new[] { "Yayo", "GoJuice", "DV_AdrenalineShot" });
            foreach (XElement sedative in profile.Element("medic")!.Elements().Where(item => item.Element("thingDef")?.Value is "Ketamine" or "Chloroform"))
            { Assert.AreEqual("true", sedative.Element("unscaled")?.Value); }
            bool ultra = profile.Element("techLevel")!.Value == "Ultra";
            Assert.AreEqual(ultra, profile.Element("medic")!.Descendants("thingDef").Any(def => def.Value == "MedicineUltratech"));
        }
    }

    [TestMethod]
    public void EveryMedicalItemHasAnExplicitClassificationAndAuditedInheritedMassAndBulk()
    {
        Dictionary<string, (float Mass, float Bulk, string Category)> audit = new()
        {
            ["Bandage"] = (.05f, .05f, "personal"), ["HemostaticAgent"] = (.03f, .03f, "personal"),
            ["Tourniquet"] = (.08f, .08f, "personal"), ["Splint"] = (.1f, .1f, "medic"),
            ["Epinephrine"] = (.05f, .05f, "personal"), ["Morphine"] = (.05f, .05f, "medic"),
            ["Ketamine"] = (.05f, .05f, "carried-not-automated"), ["Chloroform"] = (.2f, .3f, "carried-not-automated"),
            ["SalineBag"] = (.25f, .25f, "personal"), ["WholeBloodBag"] = (.75f, .5f, "personal"),
            ["SuctionDevice"] = (.4f, 1.5f, "medic"), ["Defibrillator"] = (.5f, 2f, "medic"),
            ["Thoracoscope"] = (.75f, 2.5f, "surgery-only")
        };
        List<XElement> defs = Directory.EnumerateFiles(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs", "ThingDefs", "Medicine"), "*.xml")
            .SelectMany(file => XDocument.Load(file).Root!.Elements("ThingDef")).ToList();
        foreach (XElement def in defs.Where(def => def.Element("defName") is not null))
        {
            string name = def.Element("defName")!.Value;
            Assert.IsTrue(audit.Remove(name, out var expected), "New medical item requires an explicit kit/AI classification and mass/bulk review: " + name);
            Assert.Contains(expected.Category, new[] { "personal", "medic", "scavenged", "surgery-only", "carried-not-automated" });
            Assert.AreEqual(expected.Mass, Resolve(def, "Mass"), .00001f, name);
            Assert.AreEqual(expected.Bulk, Resolve(def, "Bulk"), .00001f, name);
        }
        Assert.IsEmpty(audit);
        float Resolve(XElement def, string stat)
        {
            if (def.Element("statBases")?.Element(stat) is XElement value)
            { return float.Parse(value.Value, CultureInfo.InvariantCulture); }
            string? parent = def.Attribute("ParentName")?.Value;
            XElement? baseDef = defs.SingleOrDefault(candidate => candidate.Attribute("Name")?.Value == parent);
            Assert.IsNotNull(baseDef, def.Element("defName")?.Value + " must explicitly inherit an audited " + stat);
            return Resolve(baseDef, stat);
        }
    }
}
