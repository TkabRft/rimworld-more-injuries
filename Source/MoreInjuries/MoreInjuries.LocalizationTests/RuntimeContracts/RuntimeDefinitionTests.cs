using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Xml.Linq;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class RuntimeDefinitionTests
{
    [TestMethod]
    public void VitalFailureHookTargetsMatchPinnedApi()
    {
        string assetsPath = Path.Combine(ModRoot, "Source", "MoreInjuries", "artifacts", "obj", "MoreInjuries", "project.assets.json");
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
        string reference = assets.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(folder => Path.Combine(folder.Name, "krafs.rimworld.ref", "1.6.4633", "ref", "net472", "Assembly-CSharp.dll"))
            .First(File.Exists);
        using PEReader reader = new(File.OpenRead(reference));
        MetadataReader metadata = reader.GetMetadataReader();
        var expected = new Dictionary<string, string[]>
        {
            ["Verse.Pawn_HealthTracker"] = ["CheckForStateChange", "ShouldBeDeadFromRequiredCapacity", "HealthTickInterval", "Notify_Resurrected", "AddHediff"],
            ["RimWorld.Pawn_MutantTracker"] = ["Turn", "Revert"],
            ["RimWorld.Pawn_GeneTracker"] = ["Notify_GenesChanged"],
            ["Verse.ModContentPack"] = ["get_Patches"],
            ["Verse.PatchOperation"] = ["Apply"],
            ["Verse.ScribeSaver"] = ["InitSaving", "FinalizeSaving"],
            ["Verse.PawnCapacitiesHandler"] = ["Notify_CapacityLevelsDirty"],
            ["Verse.PawnCapacityUtility"] = ["CalculateCapacityLevel"],
            ["Verse.GenClosest"] = ["ClosestThingReachable"],
            ["Verse.HediffComp_Immunizable"] = ["SeverityChangePerDay"],
            ["Verse.ImmunityRecord"] = ["ImmunityChangePerTick"],
            ["Verse.Hediff"] = ["CauseDeathNow"],
            ["Verse.Hediff_Injury"] = ["Heal"],
            ["RimWorld.PawnCapacityWorker_BloodPumping"] = ["CalculateCapacityLevel"]
        };
        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            string name = metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);
            if (!expected.Remove(name, out string[]? methods)) { continue; }
            HashSet<string> present = type.GetMethods().Select(method => metadata.GetString(metadata.GetMethodDefinition(method).Name)).ToHashSet();
            foreach (string method in methods) { Assert.Contains(method, present, name); }
            if (name == "Verse.HediffComp_Immunizable")
            {
                Assert.IsTrue(type.GetFields().Any(field => metadata.GetString(metadata.GetFieldDefinition(field).Name)
                    == "severityPerDayNotImmuneRandomFactor"), "Forecast reads the persisted roll without calling Rand.");
            }
            if (name is "Verse.Pawn_HealthTracker" or "Verse.PawnCapacitiesHandler")
            {
                Assert.IsTrue(type.GetFields().Any(field => metadata.GetString(metadata.GetFieldDefinition(field).Name) == "pawn"), name);
            }
        }
        Assert.IsEmpty(expected);
    }

    internal static string ModRoot
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "About", "About.xml")) && Directory.Exists(Path.Combine(directory.FullName, "Defs")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new InvalidOperationException("Cannot locate the mod root from the test assembly.");
        }
    }

    [TestMethod]
    public void AddictionFieldsExistInPinnedRimWorldReference()
    {
        // Use the production project's resolved cache, including when tests run under another account.
        string assetsPath = Path.Combine(ModRoot, "Source", "MoreInjuries", "artifacts", "obj", "MoreInjuries", "project.assets.json");
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
        string reference = assets.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(folder => Path.Combine(folder.Name, "krafs.rimworld.ref", "1.6.4633", "ref", "net472", "Assembly-CSharp.dll"))
            .First(File.Exists);
        using FileStream stream = File.OpenRead(reference);
        using PEReader reader = new(stream);
        MetadataReader metadata = reader.GetMetadataReader();
        HashSet<string> fields = [];
        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            if (metadata.GetString(type.Namespace) != "Verse" || metadata.GetString(type.Name) is not ("Def" or "HediffDef")) { continue; }
            foreach (FieldDefinitionHandle field in type.GetFields())
            {
                fields.Add(metadata.GetString(metadata.GetFieldDefinition(field).Name));
            }
        }
        Assert.Contains("chemicalNeed", fields);
        foreach (string file in new[] { "Hediffs_Morphine.xml", "Hediffs_Anesthetics.xml" })
        {
            XDocument document = XDocument.Load(Path.Combine(ModRoot, "Defs", "HediffDefs", file));
            foreach (XElement addiction in document.Root!.Elements("HediffDef").Where(def => def.Element("chemicalNeed") is not null))
            {
                foreach (XElement field in addiction.Elements())
                {
                    Assert.Contains(field.Name.LocalName, fields, $"{addiction.Element("defName")!.Value}: unknown HediffDef field {field.Name}");
                }
                string need = addiction.Element("chemicalNeed")!.Value;
                Assert.IsTrue(document.Root.Elements("NeedDef").Any(def => def.Element("defName")?.Value == need));
            }
        }
    }

    [TestMethod]
    public void RuntimeXmlClassesExistInReleaseAssembly()
    {
        string assembly = Path.Combine(ModRoot, "Source", "MoreInjuries", "artifacts", "bin", "MoreInjuries", "release", "MoreInjuries.dll");
        using FileStream stream = File.OpenRead(assembly);
        using PEReader reader = new(stream);
        MetadataReader metadata = reader.GetMetadataReader();
        HashSet<string> types = [];
        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            types.Add(metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name));
        }
        foreach (string file in new[] { "Defs", "Patches" }.SelectMany(folder => Directory.EnumerateFiles(Path.Combine(ModRoot, folder), "*.xml", SearchOption.AllDirectories)))
        {
            foreach (XAttribute attribute in XDocument.Load(file).Descendants().Attributes("Class"))
            {
                if (attribute.Value.StartsWith("MoreInjuries.", StringComparison.Ordinal))
                {
                    Assert.Contains(attribute.Value, types, $"{Path.GetFileName(file)} references a type missing from Release: {attribute.Value}");
                }
            }
        }
        XDocument transfusions = XDocument.Load(Path.Combine(ModRoot, "Defs", "ThingDefs", "Medicine", "Medicine_Transfusions.xml"));
        Assert.IsTrue(transfusions.Root!.Elements("ThingDef").Any(def => def.Element("defName")?.Value == "SalineBag"));
    }

    [TestMethod]
    public void GeneratedChemicalGeneTexturesHaveAlphaChannels()
    {
        foreach (string chemical in new[] { "MI_Morphine_Chemical", "MI_Ketamine_Chemical" })
        {
            foreach (string template in new[] { "ChemicalDependency", "AddictionResistant", "AddictionImmune" })
            {
                string texture = Path.Combine(ModRoot, "Textures", "UI", "Icons", "Genes", "Chemicals", chemical, template + ".png");
                byte[] bytes = File.ReadAllBytes(texture);
                CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8).ToArray(), texture);
                Assert.AreEqual(6, bytes[25], $"Expected RGBA PNG: {texture}");
            }
        }
    }
}
