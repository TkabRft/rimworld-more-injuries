using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class EmergencyBalanceTests
{
    [TestMethod]
    public void ReleaseTreatmentGettersUseEightyAndFortyTicks()
    {
        using PEReader reader = new(File.OpenRead(Path.Combine(RuntimeDefinitionTests.ModRoot,
            "Source/MoreInjuries/artifacts/bin/MoreInjuries/release/MoreInjuries.dll")));
        MetadataReader metadata = reader.GetMetadataReader();
        Dictionary<string, byte> expected = new() { ["JobDriver_UseBandage"] = 80, ["JobDriver_UseHemostaticAgent"] = 40 };
        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            if (!expected.Remove(metadata.GetString(type.Name), out byte ticks)) { continue; }
            MethodDefinition getter = type.GetMethods().Select(metadata.GetMethodDefinition)
                .Single(method => metadata.GetString(method.Name) == "get_BaseTendDuration");
            CollectionAssert.AreEqual(new byte[] { 0x1f, ticks, 0x2a }, reader.GetMethodBody(getter.RelativeVirtualAddress).GetILBytes()!);
        }
        Assert.IsEmpty(expected);
    }

    [TestMethod]
    public void ConsumableEconomyChangesIngredientsAndValuesWhileRetainingWorkAndDuration()
    {
        XDocument Bandage(string area) => XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs", area, "Medicine_Bandage.xml"));
        XDocument recipe = Bandage("RecipeDefs/Things");
        CollectionAssert.AreEqual(new[] { 1f, 5f }, recipe.Descendants("ingredients").SelectMany(node => node.Elements()).Select(node => (float)node.Element("count")!).ToArray());
        CollectionAssert.AreEqual(new[] { 50f, 250f }, recipe.Descendants("workAmount").Select(node => (float)node).ToArray());
        XDocument hemostat = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs/RecipeDefs/Things/Medicine_HemostaticAgent.xml"));
        CollectionAssert.AreEqual(new[] { 5f, 10f }, hemostat.Descendants("ingredients").Single().Elements().Select(node => (float)node.Element("count")!).ToArray());
        Assert.AreEqual(750f, (float)hemostat.Descendants("workAmount").Single());
        foreach (var values in new[] { (File: "Medicine_Bandage.xml", Value: 2f, Reduction: .2f, Ticks: 30000), (File: "Medicine_HemostaticAgent.xml", Value: 6f, Reduction: .05f, Ticks: 20000) })
        {
            XDocument thing = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs/ThingDefs/Medicine", values.File));
            Assert.AreEqual(values.Value, (float)thing.Descendants("MarketValue").Single());
            Assert.AreEqual(values.Reduction, (float)thing.Descendants("coagulationMultiplier").Single());
            Assert.AreEqual(values.Ticks, (int)thing.Descendants("disappearsAfterTicks").Single());
        }
    }

    [TestMethod]
    public void SpallingChangesItsDefaultWithoutRenamingThePersistedSetting()
    {
        string source = File.ReadAllText(Path.Combine(RuntimeDefinitionTests.ModRoot, "Source/MoreInjuries/MoreInjuries/MoreInjuriesSettings.cs"));
        Assert.Contains("[SettingsEntry<bool>(DefaultValue = false)]\n    internal partial ref bool EnableSpalling", source.Replace("\r\n", "\n"));
        string worker = File.ReadAllText(Path.Combine(RuntimeDefinitionTests.ModRoot, "Source/MoreInjuries/MoreInjuries/HealthConditions/SpallingInjury/SpallingWorker.cs"));
        Assert.Contains("IsEnabled => MoreInjuriesMod.Settings.EnableSpalling", worker);
        Assert.DoesNotContain("CombatExtendedLoaded &&", worker, "Saved enabled preferences must not be forcibly disabled by CE detection.");
    }
}
