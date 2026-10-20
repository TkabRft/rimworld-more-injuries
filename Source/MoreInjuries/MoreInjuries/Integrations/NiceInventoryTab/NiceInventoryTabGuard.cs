using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace MoreInjuries.Integrations.NiceInventoryTab;

internal static class NiceInventoryTabGuard
{
    internal static void Install(Harmony harmony)
    {
        try
        {
            Type? type = AccessTools.TypeByName("NITIntegrationCE.MakeItemSlot_Patch");
            if (type?.Assembly.GetName().Name != "NITIntegrationCE")
            {
                throw new MissingMemberException("NITIntegrationCE.MakeItemSlot_Patch");
            }
            MethodInfo? target = type.GetMethod("IsGrenade", BindingFlags.DeclaredOnly | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.Static, null, [typeof(ThingDef)], null);
            if (target?.ReturnType != typeof(bool))
            {
                throw new MissingMethodException(type.FullName, "IsGrenade(ThingDef)");
            }
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(NiceInventoryTabGuard), nameof(Prefix)));
        }
        catch (Exception exception)
        {
            Logger.Warning("More Injuries: Nice Inventory Tab CE null-tag guard unavailable; retaining its original behavior. " + exception.Message);
        }
    }

    internal static bool Prefix([HarmonyArgument(0)] ThingDef? def, ref bool __result)
    {
        if (def is not null && def.weaponTags is not null)
        {
            return true;
        }
        // An absent tag list cannot contain CE_AI_Grenade. Do not rewrite the definition.
        __result = false;
        return false;
    }
}
