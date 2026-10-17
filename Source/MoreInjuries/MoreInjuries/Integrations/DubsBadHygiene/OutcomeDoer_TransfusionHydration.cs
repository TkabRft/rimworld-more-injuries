#if MOD_BAD_HYGIENE
using DubsBadHygiene;
#endif
using MoreInjuries.AI.Jobs.Outcomes;
using MoreInjuries.HealthConditions.HeavyBleeding.Transfusions;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace MoreInjuries.Integrations.DubsBadHygiene;

public sealed class OutcomeDoer_TransfusionHydration : JobOutcomeDoer_NeedBase
{
#if !MOD_BAD_HYGIENE
    private static readonly Dictionary<Type, MethodInfo> s_drinkMethods = [];
#endif

    protected override bool DoOutcome(Pawn doctor, Pawn patient, Thing? device, Need need)
    {
        if (!ModsConfig.IsActive("Dubwise.DubsBadHygiene"))
        {
            return true;
        }
#if MOD_BAD_HYGIENE
        if (need is not Need_Thirst thirst)
        {
            Logger.Error($"Unexpected need type {need.GetType()}. Expected {typeof(Need_Thirst)}");
            return false;
        }
#endif
        if (device?.def.GetModExtension<TransfusionProperties_ModExtension>() is not { } transfusionProps)
        {
            Logger.ConfigError($"Ran hydration outcome doer with unsupported device '{device?.def.defName ?? "null"}' (missing mod extension)");
            return true;
        }
        float hydration = 2f * transfusionProps.BloodLossSeverityReduction;
#if MOD_BAD_HYGIENE
        thirst.Drink(hydration);
#else
        // Invoke DBH's own Drink(float), preserving its clamp and lastGainTick bookkeeping.
        Type needType = need.GetType();
        if (!s_drinkMethods.TryGetValue(needType, out MethodInfo drink))
        {
            drink = needType.GetMethod("Drink", [typeof(float)]);
            if (drink is null || need.def.defName != "DBHThirst")
            {
                Logger.ConfigError($"Unsupported Bad Hygiene thirst type '{needType.FullName}': expected DBHThirst with Drink(float).");
                return false;
            }
            s_drinkMethods.Add(needType, drink);
        }
        drink.Invoke(need, [hydration]);
#endif
        return true;
    }
}
