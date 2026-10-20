using HarmonyLib;
using Verse;

namespace MoreInjuries.Patches;

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDeadFromLethalDamageThreshold))]
internal static class Patch_LethalDamageThreshold
{
    internal static bool Prefix(ref bool __result)
    {
        if (!MoreInjuriesMod.Settings.PreventDeathByLethalDamageThreshold)
        {
            return true;
        }
        __result = false;
        return false;
    }
}
