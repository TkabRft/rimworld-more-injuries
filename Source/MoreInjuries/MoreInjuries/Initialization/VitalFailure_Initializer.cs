using MoreInjuries.HealthConditions;
using MoreInjuries.HealthConditions.BrainDamage;
using RimWorld;
using MoreInjuries.HealthConditions.VitalFailure;
using System.Collections.Generic;
using Verse;

namespace MoreInjuries.Initialization;

[StaticConstructorOnStartup]
internal static class VitalFailure_Initializer
{
    static VitalFailure_Initializer()
    {
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (def.race is not { IsFlesh: true } race)
            {
                continue;
            }
            bool present = false;
            if (def.comps is not null)
            {
                foreach (CompProperties props in def.comps)
                {
                    present |= props.compClass == typeof(VitalFailureComp);
                }
            }
            if (VitalAnatomy.For(race.body).Brains.Length > 0)
            {
                def.recipes ??= new List<RecipeDef>();
                foreach (RecipeDef recipe in new[] { NeurologicalDefOf.MI_InduceArtificialComa, NeurologicalDefOf.MI_AwakenArtificialComa,
                    DefDatabase<RecipeDef>.GetNamed("TreatBrainDamage") })
                {
                    if (!def.recipes.Contains(recipe)) { def.recipes.Add(recipe); }
                }
            }
            if (!present)
            {
                def.comps ??= new List<CompProperties>();
                def.comps.Add(new VitalFailureCompProperties());
            }
        }
        if (ModsConfig.IsActive("Troopersmith1.DeathRattle"))
        {
            Log.Error("MI_VitalFailure_DeathRattleConflict".Translate());
        }
    }
}
