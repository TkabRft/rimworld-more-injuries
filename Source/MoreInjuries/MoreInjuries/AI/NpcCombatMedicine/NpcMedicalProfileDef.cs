using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using RimWorld;
using Verse;

namespace MoreInjuries.AI.NpcCombatMedicine;

[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "XML field names are a public data interface.")]
public sealed class NpcMedicalProfileDef : Def
{
    private readonly TechLevel techLevel = TechLevel.Neolithic;
    private readonly FloatRange equippedChance = default;
    private readonly FloatRange medicChance = default;
    private readonly FloatRange performanceChance = default;
    private readonly IntRange performanceCount = IntRange.One;
    private readonly int medicalSkillFloor = 4;
    private readonly List<NpcMedicalItem> personal = [];
    private readonly List<NpcMedicalItem> medic = [];
    private readonly List<ThingDef> performanceDrugs = [];
    private readonly List<float> performanceWeights = [];

    public TechLevel TechLevel => techLevel;
    public FloatRange EquippedChance => equippedChance;
    public FloatRange MedicChance => medicChance;
    public FloatRange PerformanceChance => performanceChance;
    public IntRange PerformanceCount => performanceCount;
    public int MedicalSkillFloor => medicalSkillFloor;
    public IReadOnlyList<NpcMedicalItem> Personal => personal;
    public IReadOnlyList<NpcMedicalItem> Medic => medic;
    public IReadOnlyList<ThingDef> PerformanceDrugs => performanceDrugs;
    public IReadOnlyList<float> PerformanceWeights => performanceWeights;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors()) { yield return error; }
        if (performanceDrugs.Count != performanceWeights.Count) { yield return "NPC medical drug weights must match their definitions."; }
        if (performanceCount.min < 0 || performanceCount.max < performanceCount.min) { yield return "Invalid NPC performance reserve quantity."; }
        foreach (float weight in performanceWeights) { if (weight < 0f) { yield return "NPC medical drug weights must be nonnegative."; } }
        foreach (NpcMedicalItem item in personal) { foreach (string error in item.Errors()) { yield return error; } }
        foreach (NpcMedicalItem item in medic) { foreach (string error in item.Errors()) { yield return error; } }
    }
}

[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "XML field names are a public data interface.")]
public sealed class NpcMedicalProfileExtension : DefModExtension
{
    private readonly NpcMedicalProfileDef? profile = null;
    public NpcMedicalProfileDef? Profile => profile;
}

[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "XML field names are a public data interface.")]
public sealed class NpcMedicalItem
{
    private readonly ThingDef? thingDef = null;
    private readonly IntRange count = IntRange.One;
    private readonly float chance = 1f;
    private readonly bool fluid = false;
    private readonly bool unscaled = false;
    private readonly bool reusable = false;
    private readonly int choiceGroup = 0;
    public ThingDef? ThingDef => thingDef;
    public IntRange Count => count;
    public float Chance => chance;
    public bool Fluid => fluid;
    public bool Unscaled => unscaled || reusable;
    public bool Reusable => reusable;
    public int ChoiceGroup => choiceGroup;
    internal IEnumerable<string> Errors()
    {
        if (!fluid && thingDef is null) { yield return "NPC medical item requires a thingDef or fluid=true."; }
        if (count.min < 0 || count.max < count.min || chance < 0f || chance > 1f)
        { yield return "Invalid NPC medical item quantity/chance."; }
    }
}
