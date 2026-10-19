using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using Verse;

namespace MoreInjuries.AI.NpcCombatMedicine;

[HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
internal static class NpcMedicalIngestionPatch
{
    private readonly struct DoseState(bool ran, bool performance)
    {
        internal bool Ran { get; } = ran;
        internal bool Performance { get; } = performance;
    }

    private static bool Prefix(Thing __instance, Pawn ingester, ref float __result, out DoseState __state)
    {
        __state = new(true, false);
        if (!NpcMedicalAi.Managed(ingester) || ingester.CurJob?.def != KnownJobDefOf.MI_NpcPerformanceDrug) { return true; }
        __state = new(true, true);
        MoreInjuriesSettings settings = MoreInjuriesMod.Settings;
        if (settings.NpcPerformanceDrugs && NpcMedicalAi.SafePerformance(ingester, __instance)
            && NpcMedicalAi.State(ingester).Drugs.PerformanceAllowed(Find.TickManager.TicksGame,
                settings.NpcPerformanceMaximum, settings.NpcPerformanceInterval)) { return true; }
        // Native ingestion retries while a requested count remains. Rejecting consumption
        // without completing that request can leave a pawn repeatedly trying the same dose.
        ingester.CurJob.count = 0;
        __state = default;
        __result = 0f;
        return false;
    }

    private static void Postfix(Thing __instance, Pawn ingester, DoseState __state)
    {
        if (!__state.Ran || !MoreInjuriesMod.Settings.EnableNpcCombatMedicine) { return; }
        int now = Find.TickManager.TicksGame;
        if (__instance.def == KnownThingDefOf.Epinephrine)
        { NpcMedicalAi.State(ingester).Drugs.ClinicalTaken(now); NpcMedicalMemory.Remember(ingester); }
        else if (__state.Performance)
        { NpcMedicalAi.State(ingester).Drugs.PerformanceTaken(now); NpcMedicalMemory.Remember(ingester); }
    }
}
