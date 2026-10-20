using System.Collections.Generic;
using System.Linq;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;

namespace MoreInjuries.AI.NpcCombatMedicine;

// Explicit roles: a clinical reserve is not a performance-dose allowance.
internal static class NpcMedicalDrugs
{
    internal const string DET_PACKAGE = "det.battlemeds";
    internal static ThingDef? Optional(string name) => ModsConfig.IsActive(DET_PACKAGE)
        ? DefDatabase<ThingDef>.GetNamedSilentFail(name) : null;
    internal static ThingDef? Stim(TechLevel tech) => Optional(tech >= TechLevel.Ultra ? "StimpackUltratech" : "StimpackIndustrial");
    internal static bool Restorative(ThingDef def) => EffectName(def) is "HerbalPowderHediff" or "IndustrialStimpackHediff" or "GlitterStimpackHediff";
    internal static bool Booster(ThingDef def) => def == KnownThingDefOf.Morphine || def == ThingDefOf.Yayo
        || def == ThingDefOf.GoJuice || EffectName(def) == "DV_AdrenalineShotHediff";
    internal static bool Clinical(ThingDef def) => def == KnownThingDefOf.Morphine || Restorative(def);
    internal static IEnumerable<ThingDef> Restoratives => new[] { Optional("HerbalPowder"), Optional("StimpackIndustrial"), Optional("StimpackUltratech") }
        .OfType<ThingDef>();
    internal static IEnumerable<ThingDef> Analgesics => new[] { KnownThingDefOf.Morphine, ThingDefOf.Yayo, ThingDefOf.GoJuice }.Concat(Restoratives);
    internal static string? EffectName(ThingDef def)
    {
        string? effect = def.defName switch
        {
            "HerbalPowder" => "HerbalPowderHediff",
            "StimpackIndustrial" => "IndustrialStimpackHediff",
            "StimpackUltratech" => "GlitterStimpackHediff",
            "DV_AdrenalineShot" => "DV_AdrenalineShotHediff",
            _ => null
        };
        return effect is not null && Optional(def.defName) == def ? effect : null;
    }

    internal static bool GrantAllowed(ThingDef def)
    {
        MoreInjuriesSettings settings = MoreInjuriesMod.Settings;
        if (settings.NpcDrugSelection == 2 && (Clinical(def) || Booster(def)
            || def.ingestible is { drugCategory: not DrugCategory.None })) { return false; }
        if (Restorative(def)) { return true; }
        return def == KnownThingDefOf.Morphine ? settings.NpcPainCare : !Booster(def) || settings.NpcPerformanceDrugs;
    }

    internal static bool SafeOptionalBooster(Pawn pawn, ThingDef def) => EffectName(def) != "DV_AdrenalineShotHediff"
        || NpcPainCare.Willing(pawn, pawn, def)
            && NpcPainProjection.TryDose(pawn, def, out _, out _, NpcDrugPurpose.Performance);
}
