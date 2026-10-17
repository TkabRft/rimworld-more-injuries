using MoreInjuries.HealthConditions;
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
            bool tracked = false, present = false;
            if (def.comps is not null)
            {
                foreach (CompProperties props in def.comps)
                {
                    tracked |= props.compClass == typeof(MoreInjuryComp);
                    present |= props.compClass == typeof(VitalFailureComp);
                }
            }
            if (!present && (tracked || ModsConfig.AnomalyActive && race.IsAnomalyEntity && race.bleedRateFactor > 0f))
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
