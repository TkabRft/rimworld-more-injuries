using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MoreInjuries.Integrations.CombatExtended;

internal static class CombatExtendedBleedingHooks
{
    internal static bool Normalize(Harmony harmony, MethodInfo cePostfix, IEnumerable<MethodInfo> nativeGetters,
        IEnumerable<MethodInfo> miGetters)
    {
        // Validate before changing anything; retain every other owner's patches.
        foreach (MethodInfo getter in nativeGetters)
        {
            if (Harmony.GetPatchInfo(getter)?.Postfixes.Any(patch => patch.owner == CombatExtendedBridge.CE_HARMONY_OWNER
                && patch.PatchMethod == cePostfix) != true) { return false; }
        }
        foreach (MethodInfo getter in miGetters)
        {
            if (Harmony.GetPatchInfo(getter)?.Postfixes.Any(patch => patch.owner == CombatExtendedBridge.CE_HARMONY_OWNER
                && patch.PatchMethod == cePostfix) == true) { harmony.Unpatch(getter, cePostfix); }
        }
        return true;
    }
}
