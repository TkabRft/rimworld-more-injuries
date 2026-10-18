using MoreInjuries.HealthConditions.Secondary.Handlers.HediffMakers;
using RimWorld;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Verse;

namespace MoreInjuries.HealthConditions.BrainDamage;

[SuppressMessage("Style", "IDE1006", Justification = "XML field names are public data contracts.")]
public sealed class NeurologicalProfileDef : Def
{
    private readonly List<WeightedHediffMakerDef> outcomes = [];
    internal IReadOnlyList<WeightedHediffMakerDef> Outcomes => outcomes;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors()) { yield return error; }
        if (outcomes.Count == 0) { yield return "A neurological profile requires outcomes."; }
        foreach (WeightedHediffMakerDef outcome in outcomes)
        {
            if (outcome.Weight < 0f) { yield return "Neurological outcome weights cannot be negative."; }
        }
    }
}

[SuppressMessage("Style", "IDE1006", Justification = "XML field names are public data contracts.")]
public sealed class NeurologicalConditionProperties : DefModExtension
{
    private readonly bool animalEligible = false;
    private readonly bool regenerative = true;
    internal bool AnimalEligible => animalEligible;
    internal bool Regenerative => regenerative;
}

[SuppressMessage("Style", "IDE1006", Justification = "XML field names are public data contracts.")]
public sealed class NeurologicalSourceProperties : DefModExtension
{
    private readonly NeurologicalProfileDef profile = null!;
    internal NeurologicalProfileDef Profile => profile;
}

[SuppressMessage("Style", "IDE1006", Justification = "XML field names are public data contracts.")]
public sealed class NeurologicalDamageMapping
{
    private readonly string damageDefName = "";
    private readonly NeurologicalProfileDef profile = null!;
    internal string DamageDefName => damageDefName;
    internal NeurologicalProfileDef Profile => profile;
}

[SuppressMessage("Style", "IDE1006", Justification = "XML field names are public data contracts.")]
public sealed class NeurologicalConfigDef : Def
{
    private readonly List<NeurologicalDamageMapping> damageProfiles = [];
    private readonly NeurologicalProfileDef defaultProfile = null!;
    private readonly NeurologicalProfileDef hypoxiaProfile = null!;
    private readonly NeurologicalProfileDef strokeProfile = null!;
    private readonly NeurologicalProfileDef comaProfile = null!;
    private readonly float directBrainChanceFactor = 0.25f;
    private readonly float concussionThreshold = 0.7f;
    private readonly float concussionChance = 0.05f;
    private readonly float comaHypoxiaMultiplier = 0.5f;
    private Dictionary<string, NeurologicalProfileDef> _damageProfiles = [];
    internal NeurologicalProfileDef DefaultProfile => defaultProfile;
    internal NeurologicalProfileDef HypoxiaProfile => hypoxiaProfile;
    internal NeurologicalProfileDef StrokeProfile => strokeProfile;
    internal NeurologicalProfileDef ComaProfile => comaProfile;
    internal float DirectBrainChanceFactor => directBrainChanceFactor;
    internal float ConcussionThreshold => concussionThreshold;
    internal float ConcussionChance => concussionChance;
    internal float ComaHypoxiaMultiplier => comaHypoxiaMultiplier;

    internal NeurologicalProfileDef ForDamage(DamageDef? damage) => damage is not null
        && _damageProfiles.TryGetValue(damage.defName, out NeurologicalProfileDef profile) ? profile : defaultProfile;

    public override void ResolveReferences()
    {
        base.ResolveReferences();
        foreach (NeurologicalDamageMapping mapping in damageProfiles)
        {
            _damageProfiles[mapping.DamageDefName] = mapping.Profile;
        }
    }
}

[DefOf]
public static class NeurologicalDefOf
{
    public static NeurologicalConfigDef MI_NeurologicalConfig = null!;
    public static HediffDef WakeUpHigh = null!;
    public static HediffDef MI_DiffuseBrainDamage = null!;
    public static HediffDef MI_ArtificialComa = null!;
    public static RecipeDef MI_InduceArtificialComa = null!;
    public static RecipeDef MI_AwakenArtificialComa = null!;
    [MayRequire("vat.epoeforked")]
    public static RecipeDef MI_TreatDiffuseBrainDamage_Neurocure = null!;
    static NeurologicalDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(NeurologicalDefOf));
}
