using MoreInjuries.HealthConditions.HeavyBleeding;
using MoreInjuries.HealthConditions.HypovolemicShock;
using System.Xml.Linq;
using System.Xml.XPath;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class TraumaCompatibilityTests
{
    [TestMethod]
    public void EligibilityExcludesOrdinaryAnimalsAndNonbleedingEntities()
    {
        Assert.IsTrue(TraumaEligibility.Supports(true, false, false, true, 1f), "Existing supported humanlike/HAR.");
        Assert.IsTrue(TraumaEligibility.Supports(false, true, true, true, 0.5f), "Fleshbeast.");
        Assert.IsFalse(TraumaEligibility.Supports(false, true, false, true, 1f), "Ordinary animal.");
        Assert.IsFalse(TraumaEligibility.Supports(false, false, true, true, 1f), "DLC inactive.");
        Assert.IsFalse(TraumaEligibility.Supports(false, true, true, false, 1f), "Mechanical entity.");
        Assert.IsFalse(TraumaEligibility.Supports(false, true, true, true, 0f), "Nonbleeding entity.");
        Assert.IsFalse(BloodLossPolicy.AllowDirectDeath(TraumaEligibility.Supports(false, true, true, true, 0.5f), true));
        Assert.IsTrue(BloodLossPolicy.AllowDirectDeath(TraumaEligibility.Supports(false, true, false, true, 1f), true));
    }

    [TestMethod]
    public void OnsetIsReliableAboveThresholdAndRespectsImmunity()
    {
        HediffModifier_HypovolemicShockOnset onset = new();
        Pawn pawn = new();
        Hediff loss = new() { pawn = pawn };
        foreach (float severity in new[] { 0f, 0.449f, 0.45f, 0.4501f, 0.6f, 0.9f, 1f })
        {
            loss.Severity = severity;
            Assert.AreEqual(severity > 0.45f ? 1f : 0f, onset.GetModifier(loss, null!));
        }
        pawn.OxygenImmune = true;
        Assert.AreEqual(0f, onset.GetModifier(loss, null!));
    }

    [TestMethod]
    public void HarPatchHandlesDefaultFieldsMixedCompListsOptOutsAndRepeatedApplication()
    {
        const string comp = "MoreInjuries.HealthConditions.MoreInjuryCompProperties";
        XDocument defs = XDocument.Parse($"""
            <Defs>
              <AlienRace.ThingDef_AlienRace><defName>Default</defName></AlienRace.ThingDef_AlienRace>
              <AlienRace.ThingDef_AlienRace><defName>ExistingComps</defName><comps><li Class="OtherComp"/></comps></AlienRace.ThingDef_AlienRace>
              <AlienRace.ThingDef_AlienRace><defName>DefaultInBlock</defName><alienRace><compatibility><hasBlood>true</hasBlood></compatibility></alienRace></AlienRace.ThingDef_AlienRace>
              <AlienRace.ThingDef_AlienRace><defName>ExplicitTrue</defName><alienRace><compatibility><isFlesh>True</isFlesh></compatibility></alienRace></AlienRace.ThingDef_AlienRace>
              <AlienRace.ThingDef_AlienRace><defName>ExistingSupport</defName><comps><li Class="{comp}"/></comps></AlienRace.ThingDef_AlienRace>
              <AlienRace.ThingDef_AlienRace><defName>OptOut</defName><alienRace><compatibility><isFlesh>false</isFlesh></compatibility></alienRace></AlienRace.ThingDef_AlienRace>
              <AlienRace.ThingDef_AlienRace><defName>OptOutUpper</defName><alienRace><compatibility><isFlesh>FALSE</isFlesh></compatibility></alienRace></AlienRace.ThingDef_AlienRace>
              <AlienRace.ThingDef_AlienRace Abstract="True"><defName>Abstract</defName></AlienRace.ThingDef_AlienRace>
              <ThingDef><defName>Animal</defName><race><intelligence>Animal</intelligence></race></ThingDef>
            </Defs>
            """);
        XDocument patch = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Patches", "Mod_HumanoidAlienRaces.xml"));
        foreach (int pass in new[] { 1, 2 })
        {
            foreach (XElement operation in patch.Descendants("operations").Single().Elements("li"))
            {
                XElement? add = defs.XPathSelectElements(operation.Element("xpath")!.Value).Any() ? operation.Element("match") : null;
                if (add is null) { continue; }
                foreach (XElement target in defs.XPathSelectElements(add.Element("xpath")!.Value).ToArray())
                {
                    target.Add(add.Element("value")!.Elements().Select(value => new XElement(value)));
                }
            }
            foreach (XElement def in defs.Root!.Elements())
            {
                string name = def.Element("defName")!.Value;
                int expected = name is "OptOut" or "OptOutUpper" or "Abstract" or "Animal" ? 0 : 1;
                Assert.AreEqual(expected, def.Elements("comps").Elements("li").Count(li => li.Attribute("Class")?.Value == comp), $"{name}, pass {pass}");
            }
        }
    }
}
