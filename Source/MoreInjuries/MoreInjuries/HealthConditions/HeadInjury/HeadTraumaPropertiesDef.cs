using MoreInjuries.Roslyn.SourceGen.XmlBinding.Attributes;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace MoreInjuries.HealthConditions.HeadInjury;

[XmlBindable]
public sealed partial class HeadTraumaPropertiesDef : Def
{
    public const string DEF_NAME = "HeadTraumaProperties";

    private const float DEFAULT_DAMAGE_TYPE_PERCENT = 0.08f;
    private const float DEFAULT_PART_MULTIPLIER = 1f;

    private static readonly List<HeadTraumaPartMultiplier> s_emptyPartMultipliers = [];
    private static readonly List<HeadTraumaDamageTypePercent> s_emptyDamageTypePercents = [];
    private static readonly List<BodyPartDef> s_emptyHeadRoots = [];
    private static readonly List<BodyPartGroupDef> s_emptyHeadGroups = [];

    private static HeadTraumaPropertiesDef? s_named;
    private static bool s_resolved;

    private Dictionary<BodyPartDef, float> _partMultiplierByDef = [];
    private Dictionary<string, float> _damageTypePercentByName = [];
    private HashSet<BodyPartDef> _headRoots = [];
    private HashSet<BodyPartGroupDef> _headGroups = [];

    [XmlBinding("partMultipliers", DefaultValueFrom = nameof(s_emptyPartMultipliers))]
    public partial IReadOnlyList<HeadTraumaPartMultiplier> PartMultipliers { get; }

    [XmlBinding("damageTypePercents", DefaultValueFrom = nameof(s_emptyDamageTypePercents))]
    public partial IReadOnlyList<HeadTraumaDamageTypePercent> DamageTypePercents { get; }

    [XmlBinding("headRoots", DefaultValueFrom = nameof(s_emptyHeadRoots))]
    public partial IReadOnlyList<BodyPartDef> HeadRoots { get; }

    [XmlBinding("headGroups", DefaultValueFrom = nameof(s_emptyHeadGroups))]
    public partial IReadOnlyList<BodyPartGroupDef> HeadGroups { get; }

    [XmlBinding("defaultDamageTypePercent", DefaultValueFrom = nameof(DEFAULT_DAMAGE_TYPE_PERCENT))]
    public partial float DefaultDamageTypePercent { get; }

    [XmlBinding("defaultPartMultiplier", DefaultValueFrom = nameof(DEFAULT_PART_MULTIPLIER))]
    public partial float DefaultPartMultiplier { get; }

    internal static HeadTraumaPropertiesDef? Named
    {
        get
        {
            if (!s_resolved)
            {
                s_resolved = true;
                s_named = DefDatabase<HeadTraumaPropertiesDef>.GetNamed(DEF_NAME, errorOnFail: false);
                if (s_named is null)
                {
                    Logger.ConfigError($"Required def '{DEF_NAME}' was not found. Head trauma outcomes are disabled.");
                }
            }
            return s_named;
        }
    }

    public override void ResolveReferences()
    {
        base.ResolveReferences();

        Dictionary<BodyPartDef, float> partMultiplierByDef = [];
        foreach (HeadTraumaPartMultiplier entry in PartMultipliers)
        {
            if (entry.BodyPart is not null)
            {
                partMultiplierByDef[entry.BodyPart] = entry.Multiplier;
            }
        }
        _partMultiplierByDef = partMultiplierByDef;

        Dictionary<string, float> damageTypePercentByName = [];
        foreach (HeadTraumaDamageTypePercent entry in DamageTypePercents)
        {
            string damageDefName = entry.DamageDefName;
            if (!string.IsNullOrEmpty(damageDefName))
            {
                damageTypePercentByName[damageDefName] = entry.Percent;
            }
        }
        _damageTypePercentByName = damageTypePercentByName;

        HashSet<BodyPartDef> headRoots = [];
        foreach (BodyPartDef root in HeadRoots)
        {
            if (root is not null)
            {
                headRoots.Add(root);
            }
        }
        _headRoots = headRoots;

        HashSet<BodyPartGroupDef> headGroups = [];
        foreach (BodyPartGroupDef group in HeadGroups)
        {
            if (group is not null)
            {
                headGroups.Add(group);
            }
        }
        _headGroups = headGroups;
    }

    internal bool IsHeadPart(BodyPartRecord part)
    {
        if (_partMultiplierByDef.ContainsKey(part.def))
        {
            return true;
        }
        for (BodyPartRecord? current = part; current is not null; current = current.parent)
        {
            if (_headRoots.Contains(current.def))
            {
                return true;
            }
            List<BodyPartGroupDef>? groups = current.groups;
            if (groups is null)
            {
                continue;
            }
            foreach (BodyPartGroupDef group in groups)
            {
                if (_headGroups.Contains(group))
                {
                    return true;
                }
            }
        }
        return false;
    }

    internal float GetPartMultiplier(BodyPartRecord part)
    {
        if (_partMultiplierByDef.TryGetValue(part.def, out float multiplier))
        {
            return multiplier;
        }
        return DefaultPartMultiplier;
    }

    internal float GetDamageTypePercent(DamageDef? damageDef)
    {
        if (damageDef?.defName is string defName
            && _damageTypePercentByName.TryGetValue(defName, out float percent))
        {
            return percent;
        }
        return DefaultDamageTypePercent;
    }
}
