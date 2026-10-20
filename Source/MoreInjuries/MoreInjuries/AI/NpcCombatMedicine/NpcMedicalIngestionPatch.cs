using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.Drugs;
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
        Pawn? clinician = MedicalDrugAdministration.GetDoctor(__instance);
        bool restorative = MedicalDrugAdministration.IsRestorative(__instance)
            || clinician is null && ingester.CurJob?.def == KnownJobDefOf.MI_NpcRestorativeSelf && ingester.CurJob.targetA.Thing == __instance;
        bool clinicalSelf = clinician is null && ingester.CurJob is { } selectedJob
            && (selectedJob.def == KnownJobDefOf.MI_NpcPainSelf || selectedJob.def == KnownJobDefOf.MI_NpcRestorativeSelf)
            && selectedJob.targetA.Thing == __instance;
        if (clinicalSelf || restorative || MedicalDrugAdministration.IsClinicalPain(__instance))
        {
            Pawn actor = clinician ?? ingester;
            if (restorative ? NpcRestorativeCare.FinalCheck(actor, ingester, __instance) : NpcPainCare.FinalCheck(actor, ingester, __instance)) { return true; }
            if (clinician is null && ingester.CurJob is { } painJob) { painJob.count = 0; }
            __state = default;
            __result = 0f;
            return false;
        }
        if (clinician is not null || !NpcMedicalAi.Managed(ingester)
            || ingester.CurJob?.def != KnownJobDefOf.MI_NpcPerformanceDrug) { return true; }
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
