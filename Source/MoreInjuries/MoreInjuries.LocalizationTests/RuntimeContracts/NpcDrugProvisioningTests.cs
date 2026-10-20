using System.Globalization;
using System.Xml.Linq;
using MoreInjuries.AI.NpcCombatMedicine;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class NpcDrugProvisioningTests
{
    [TestMethod]
    [DataRow("Industrial", 1, 2, 1, 85, 15)]
    [DataRow("Spacer", 1, 4, 1, 60, 40)]
    [DataRow("Ultra", 2, 6, 2, 25, 75)]
    public void AdvancedProfilesHaveSeparateScaledClinicalAndBoosterTargets(string tier, int personal, int medic, int boosters, int yayo, int go)
    {
        XElement profile = Profiles().Single(def => def.Element("techLevel")!.Value == tier);
        foreach ((string section, int expected) in new[] { ("personal", personal), ("medic", medic) })
        {
            XElement entry = profile.Element(section)!.Elements().Single(item => item.Element("thingDef")?.Value == "Morphine");
            Assert.AreEqual(expected.ToString(), entry.Element("count")!.Value);
            Assert.IsNull(entry.Element("unscaled"));
            Assert.AreEqual(expected, NpcMedicalModel.Quantity(expected, 1f, 0f));
            Assert.AreEqual(0, NpcMedicalModel.Quantity(expected, 0f, 1f));
            Assert.AreEqual(expected * 3, NpcMedicalModel.Quantity(expected, 2f, 1f));
        }
        Assert.AreEqual(boosters.ToString(), profile.Element("performanceCount")!.Value);
        CollectionAssert.AreEqual(new[] { "Yayo", "GoJuice", "DV_AdrenalineShot" }, profile.Element("performanceDrugs")!.Elements().Select(item => item.Value).ToArray());
        int[] weights = profile.Element("performanceWeights")!.Elements().Select(item => int.Parse(item.Value, CultureInfo.InvariantCulture)).ToArray();
        CollectionAssert.AreEqual(new[] { yayo, go, 25 }, weights);
        Assert.AreEqual(.2f, (float)weights[2] / weights.Sum(), .00001f);
        Assert.AreEqual("det.battlemeds", profile.Element("performanceDrugs")!.Elements().Last().Attribute("MayRequire")!.Value);
        Assert.AreEqual("det.battlemeds", profile.Element("performanceWeights")!.Elements().Last().Attribute("MayRequire")!.Value);
    }

    [TestMethod]
    public void OptionalEarlyPowderDoesNotIntroduceBaselineTribalMorphineOrBoosters()
    {
        foreach (XElement profile in Profiles().Where(def => def.Element("techLevel")!.Value is "Neolithic" or "Medieval"))
        {
            Assert.AreEqual("0", profile.Element("performanceChance")!.Value);
            Assert.IsFalse(profile.Descendants("thingDef").Any(def => def.Value == "Morphine"));
            foreach (string section in new[] { "personal", "medic" })
            {
                XElement powder = profile.Element(section)!.Elements().Single(item => item.Element("thingDef")?.Value == "HerbalPowder");
                Assert.AreEqual("det.battlemeds", powder.Attribute("MayRequire")!.Value);
                Assert.AreEqual(section == "personal" ? "0~1" : profile.Element("techLevel")!.Value == "Neolithic" ? "1" : "2", powder.Element("count")!.Value);
            }
        }
    }

    [TestMethod]
    public void ClinicalReservesAndExistingBoostersAreCountedIndependently()
    {
        Assert.AreEqual(1, NpcDrugProvisioning.MissingBoosters(1, 4, 4, 4), "Four clinical morphine do not consume a booster slot.");
        Assert.AreEqual(0, NpcDrugProvisioning.MissingBoosters(1, 5, 4, 4), "An existing separate booster satisfies the target.");
        Assert.AreEqual(0, NpcDrugProvisioning.MissingBoosters(1, 5, 5, 4), "Morphine beyond the clinical target can serve as a booster.");
        Assert.AreEqual(1, NpcDrugProvisioning.MissingBoosters(2, 1, 0, 6));
        Assert.AreEqual(0, NpcDrugProvisioning.MissingBoosters(1, 12, 0, 6), "Existing supplies are retained, without topping up above target.");
        Assert.AreEqual(0, NpcDrugProvisioning.MissingBoosters(0, 0, 0, 0));
    }

    [TestMethod]
    public void DetSubstitutionPreservesTotalScaledClinicalQuantities()
    {
        foreach (int target in new[] { 0, 1, 2, 3, 4, 6, 9, 18 })
        {
            Assert.AreEqual(target / 2, NpcDrugProvisioning.RestorativeShare(target, true, false));
            Assert.AreEqual(target / 2, NpcDrugProvisioning.RestorativeShare(target, true, true));
        }
        Assert.AreEqual(0, NpcDrugProvisioning.RestorativeShare(1, false, false));
        Assert.AreEqual(1, NpcDrugProvisioning.RestorativeShare(1, false, true));
        Assert.AreEqual(1, NpcDrugProvisioning.RestorativeShare(2, false, true));
    }

    [TestMethod]
    public void CapacityKeepsCoreThenOneClinicalDoseBeforeConsumableStacksAndBoosters()
    {
        NpcMedicalLoadout<string> loadout = new([
            new("bandage", 20, 0, .05f, .05f, true, 0),
            new("device", 1, 0, .5f, 2f, true, 2),
            new("morphine", 6, 0, .05f, .05f, false, 2, clinical: true),
            new("booster", 2, 0, .1f, .2f, false, 2)]);
        foreach (NpcMedicalSpace space in new[] { new NpcMedicalSpace(.60f, 10f), new NpcMedicalSpace(10f, 2.10f) })
        {
            Dictionary<string, int> result = loadout.Allocate(space);
            Assert.AreEqual(1, result["device"]);
            Assert.AreEqual(1, result["bandage"]);
            Assert.AreEqual(1, result["morphine"]);
            Assert.IsFalse(result.ContainsKey("booster"));
        }
        NpcMedicalLoadout<string> stocked = new([new("morphine", 4, 3, .05f, .05f, false, 2, clinical: true)]);
        Assert.AreEqual(1, stocked.Allocate(new(1f, 1f))["morphine"]);
        Assert.IsEmpty(stocked.Allocate(new(0f, 0f)));
    }

    private static IEnumerable<XElement> Profiles() => XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot,
        "Defs", "Internal", "NpcMedicalProfiles.xml")).Root!.Elements();
}
