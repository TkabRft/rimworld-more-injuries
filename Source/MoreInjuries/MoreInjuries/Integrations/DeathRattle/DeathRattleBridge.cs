using HarmonyLib;
using MoreInjuries.HealthConditions.BrainDamage;
using MoreInjuries.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;

namespace MoreInjuries.Integrations.DeathRattle;

internal static class DeathRattleBridge
{
    internal const string PACKAGE_ID = "Troopersmith1.DeathRattle";
    internal const string HARMONY_OWNER = "cat2002.deathrattle";
    internal static readonly HashSet<PatchOperation> SuppressedOperations = [];
    internal static readonly HashSet<string> SourceIdentifiers = new(StringComparer.Ordinal);
    internal static bool Enforced { get; private set; }
    internal static Pawn? MigratingPawn { get; set; }
    internal static bool Active => ModsConfig.IsActive(PACKAGE_ID);
    internal static bool IsPackage(string? id) => string.Equals(id, PACKAGE_ID, StringComparison.OrdinalIgnoreCase)
        || string.Equals(id, PACKAGE_ID + "_steam", StringComparison.OrdinalIgnoreCase);
    internal static bool Owns(Def def) => IsPackage(def.modContentPack?.PackageIdPlayerFacing);

    internal static RecipeDef? Replacement(RecipeDef source) => !Owns(source) ? source : source.defName switch
    {
        "ArtificialComa" => NeurologicalDefOf.MI_InduceArtificialComa,
        "EPOE_SurgeryRemoveDRBrainDamage" => NeurologicalDefOf.MI_TreatDiffuseBrainDamage_Neurocure,
        _ => null
    };

    internal static void Enforce()
    {
        if (!Active) { return; }
        Enforced = false;
        try
        {
            Type? patches = AccessTools.TypeByName("DeathRattle.HarmonyPatches");
            Type? recipes = AccessTools.TypeByName("DeathRattle.PostDefFixer");
            if (patches is null || recipes is null) { throw new InvalidOperationException("Unrecognized Death Rattle runtime layout."); }
            // Defs are resolved now. Force these initializers before removing their contributions,
            // regardless of the order StaticConstructorOnStartup happened to visit the two mods.
            RuntimeHelpers.RunClassConstructor(patches.TypeHandle);
            RuntimeHelpers.RunClassConstructor(recipes.TypeHandle);
            Harmony harmony = new("Th3Fr3d.MoreInjuries.DeathRattleTransition");
            foreach (MethodBase method in Harmony.GetAllPatchedMethods().ToArray())
            {
                if (Harmony.GetPatchInfo(method)?.Owners.Contains(HARMONY_OWNER) == true)
                {
                    harmony.Unpatch(method, HarmonyPatchType.All, HARMONY_OWNER);
                }
            }
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                if (!IsPackage(mod.PackageIdPlayerFacing)) { continue; }
                foreach (Def def in mod.AllDefs) { SourceIdentifiers.Add(def.defName); }
            }
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.recipes is null) { continue; }
                for (int i = def.recipes.Count - 1; i >= 0; i--)
                {
                    RecipeDef old = def.recipes[i];
                    if (!Owns(old)) { continue; }
                    def.recipes.RemoveAt(i);
                    RecipeDef? replacement = Replacement(old);
                    if (replacement is not null && !def.recipes.Contains(replacement)) { def.recipes.Add(replacement); }
                }
            }
            Enforced = true;
        }
        catch (Exception exception)
        {
            Log.Error("More Injuries could not establish Death Rattle priority: " + exception);
        }
    }

    internal static string? DependencyBlocker()
    {
        foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
        {
            if (IsPackage(mod.PackageIdPlayerFacing)) { continue; }
            foreach (ModDependency dependency in mod.ModMetaData.Dependencies)
            {
                if (IsPackage(dependency.packageId)) { return mod.Name; }
            }
        }
        return null;
    }
}

[HarmonyPatch(typeof(ModContentPack), "get_Patches")]
internal static class Patch_DeathRattle_PatchOwnership
{
    internal static void Postfix(ModContentPack __instance, IEnumerable<PatchOperation> __result)
    {
        if (!DeathRattleBridge.IsPackage(__instance.PackageIdPlayerFacing)) { return; }
        foreach (PatchOperation operation in __result) { DeathRattleBridge.SuppressedOperations.Add(operation); }
    }
}

[HarmonyPatch(typeof(PatchOperation), nameof(PatchOperation.Apply))]
internal static class Patch_DeathRattle_SuppressXml
{
    internal static bool Prefix(PatchOperation __instance, ref bool __result, ref bool ___neverSucceeded)
    {
        if (!DeathRattleBridge.SuppressedOperations.Contains(__instance)) { return true; }
        ___neverSucceeded = false;
        __result = true;
        return false;
    }
}
