using HarmonyLib;
using MoreInjuries.HealthConditions;
using MoreInjuries.Initialization;
using Verse;

namespace MoreInjuries.Patches;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExposeData))]
public static class Patch_Pawn_ExposeData
{
    internal static void Postfix(Pawn __instance)
    {
        if (Scribe.mode is LoadSaveMode.PostLoadInit
            && __instance.TryGetComp(out MoreInjuryComp comp)
            && comp.MissingJobParameterCache)
        {
            comp.MissingJobParameterCache = false;
            FixMisplacedBionicsModExtension.FixPawn(__instance);
        }
    }
}
