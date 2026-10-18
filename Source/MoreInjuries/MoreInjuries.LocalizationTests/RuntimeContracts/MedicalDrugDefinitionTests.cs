using System.Globalization;
using System.Xml.Linq;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class MedicalDrugDefinitionTests
{
    [TestMethod]
    [DataRow("Morphine", "MI_Morphine_Chemical", 0.05f)]
    [DataRow("Ketamine", "MI_Ketamine_Chemical", 0.02f)]
    [DataRow("Epinephrine", "", 0f)]
    [DataRow("Chloroform", "", 0f)]
    public void InheritedDrugComponentsExposeTheConfiguredChemicalAndChance(string name, string chemical, float chance)
    {
        XDocument defs = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs", "ThingDefs", "Medicine", "Medicine_Drugs.xml"));
        List<XElement> comps = ResolveComps(defs.Root!.Elements("ThingDef").Single(def => def.Element("defName")?.Value == name), defs);
        XElement[] drugs = comps.Where(comp => comp.Attribute("Class")?.Value == "CompProperties_Drug").ToArray();
        Assert.AreEqual(1, drugs.Length, $"{name}: inherited li nodes append; an earlier empty drug comp shadows the configured one.");
        // ThingDef.GetCompProperties<T> and DrugStatsUtility both use the FIRST matching comp.
        XElement first = drugs.First();
        Assert.AreEqual(chemical, first.Element("chemical")?.Value ?? "");
        Assert.AreEqual(chance, float.Parse(first.Element("addictiveness")?.Value ?? "0", CultureInfo.InvariantCulture));
        Assert.AreEqual(1, comps.Count(comp => comp.Attribute("Class")?.Value == "CompProperties_Forbiddable"), "Do not discard vanilla non-drug comps.");
        if (chemical.Length == 0) { return; }
        XElement chemicalDef = Directory.EnumerateFiles(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs", "HediffDefs"), "*.xml")
            .Select(XDocument.Load).SelectMany(document => document.Root!.Elements("ChemicalDef"))
            .Single(def => def.Element("defName")?.Value == chemical);
        Assert.IsFalse(string.IsNullOrEmpty(chemicalDef.Element("addictionHediff")?.Value));
        Assert.IsFalse(string.IsNullOrEmpty(chemicalDef.Element("toleranceHediff")?.Value));
    }

    private static List<XElement> ResolveComps(XElement def, XDocument defs)
    {
        string? parent = def.Attribute("ParentName")?.Value;
        List<XElement> comps;
        if (parent == "MakeableDrugPillBase")
        {
            // Minimal Core DrugBase -> MakeableDrugBase -> MakeableDrugPillBase fixture:
            // the only inherited comp is Forbiddable; none declares CompProperties_Drug.
            comps = [new XElement("li", new XAttribute("Class", "CompProperties_Forbiddable"))];
        }
        else if (parent is not null)
        {
            comps = ResolveComps(defs.Root!.Elements("ThingDef").Single(candidate => candidate.Attribute("Name")?.Value == parent), defs);
        }
        else
        {
            comps = [];
        }
        XElement? local = def.Element("comps");
        if (string.Equals(local?.Attribute("Inherit")?.Value, "false", StringComparison.OrdinalIgnoreCase)) { comps.Clear(); }
        // Match Verse.XmlInheritance: list entries append even when Class attributes match.
        if (local is not null) { comps.AddRange(local.Elements("li")); }
        return comps;
    }
}
