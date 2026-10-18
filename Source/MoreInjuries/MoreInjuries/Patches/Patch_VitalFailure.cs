using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.Extensions;
using MoreInjuries.HealthConditions.VitalFailure;
using MoreInjuries.HealthConditions.BrainDamage;
using RimWorld;
using Verse;

namespace MoreInjuries.Patches;

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.HealthTickInterval))]
internal static class Patch_VitalFailure_HealthTick
{
    internal static void Postfix(Pawn ___pawn, int delta) => VitalFailureComp.For(___pawn)?.TickHealth(delta);
}

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.CheckForStateChange))]
internal static class Patch_VitalFailure_StateChange
{
    internal static void Prefix(Pawn ___pawn)
    {
        if (MoreInjuries.Integrations.DeathRattle.DeathRattleBridge.MigratingPawn != ___pawn)
        { VitalFailureComp.For(___pawn)?.Refresh(); }
    }
}

[HarmonyPatch(typeof(PawnCapacitiesHandler), nameof(PawnCapacitiesHandler.Notify_CapacityLevelsDirty))]
internal static class Patch_VitalFailure_Dirty
{
    internal static void Postfix(Pawn ___pawn) => VitalFailureComp.For(___pawn)?.Invalidate();
}

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDeadFromRequiredCapacity))]
internal static class Patch_VitalFailure_RequiredCapacity
{
    internal static void Postfix(Pawn ___pawn, ref PawnCapacityDef? __result)
    {
        if (__result is not null && VitalFailureComp.For(___pawn)?.MayDelay(__result) == true)
        {
            __result = null;
            // The native method returns only the first failed capacity. Do not hide a second,
            // unsupported lethal capacity supplied by another mod.
            foreach (PawnCapacityDef capacity in DefDatabase<PawnCapacityDef>.AllDefsListForReading)
            {
                if (capacity.lethalFlesh && VitalFailureComp.For(___pawn)?.MayDelay(capacity) != true
                    && !___pawn.health.capacities.CapableOf(capacity))
                {
                    __result = capacity;
                    break;
                }
            }
        }
    }
}

[HarmonyPatch(typeof(PawnCapacityWorker_BloodPumping), nameof(PawnCapacityWorker_BloodPumping.CalculateCapacityLevel))]
internal static class Patch_VitalFailure_Pumping
{
    internal static void Postfix(HediffSet diffSet, ref float __result)
    {
        if (VitalFailureComp.ForcesPumping(diffSet.pawn))
        {
            __result = 0f;
        }
    }
}

[HarmonyPatch(typeof(Hediff), nameof(Hediff.CauseDeathNow))]
internal static class Patch_VitalFailure_HediffDeath
{
    internal static void Postfix(Hediff __instance, ref bool __result)
    {
        Pawn pawn = __instance.pawn;
        if (pawn is null || !VitalFailureComp.OwnsOxygenProgression(pawn))
        {
            return;
        }
        if (VitalFailureComp.IsTerminalHeartAttack(__instance)
            && pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest))
        {
            __result = false;
        }
        else if (__instance.def == KnownHediffDefOf.BrainDamage_Hypoxia
            && !pawn.HasOxygenDeficiencyImmunity()
            && !(ModsConfig.BiotechActive && pawn.health.hediffSet.HasHediff(HediffDefOf.Deathrest))
            && !pawn.health.hediffSet.HasPreventsDeath
            && CerebralInjury.ClinicalSeverity(__instance) >= VitalFailureDefOf.MI_VitalFailureConfig.TerminalCerebralSeverity)
        {
            __result = true;
        }
        else if (__instance.def == KnownHediffDefOf.CardiacArrest && !pawn.HasOxygenDeficiencyImmunity()
            && !(ModsConfig.BiotechActive && pawn.health.hediffSet.HasHediff(HediffDefOf.Deathrest))
            && !pawn.health.hediffSet.HasPreventsDeath && __instance.Severity >= 1f)
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(Hediff_Injury), nameof(Hediff_Injury.Heal))]
internal static class Patch_VitalFailure_Healing
{
    internal static bool Prefix(Hediff_Injury __instance, float amount)
    {
        if (amount <= 0f || !VitalFailureComp.OwnsOxygenProgression(__instance.pawn)
            || (__instance.def != KnownHediffDefOf.BrainDamage_Hypoxia && __instance.def != KnownHediffDefOf.OrganHypoxia))
        {
            return true;
        }
        VitalFailureComp? comp = VitalFailureComp.For(__instance.pawn);
        comp?.Refresh();
        return comp is null || comp.Deprivation <= 0f;
    }
}

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_Resurrected))]
internal static class Patch_VitalFailure_Resurrection
{
    internal static void Postfix(Pawn ___pawn) => VitalFailureComp.For(___pawn)?.Resume();
}

[HarmonyPatch(typeof(Pawn_MutantTracker), nameof(Pawn_MutantTracker.Turn))]
internal static class Patch_VitalFailure_MutantTurn
{
    internal static void Postfix(Pawn ___pawn)
    {
        NeurologicalOutcomes.Transform(___pawn);
        VitalFailureComp.For(___pawn)?.Resume();
    }
}

[HarmonyPatch(typeof(Pawn_MutantTracker), nameof(Pawn_MutantTracker.Revert))]
internal static class Patch_VitalFailure_MutantRevert
{
    internal static void Postfix(Pawn ___pawn) => VitalFailureComp.For(___pawn)?.Resume();
}

[HarmonyPatch(typeof(Pawn_GeneTracker), "Notify_GenesChanged")]
internal static class Patch_VitalFailure_Genes
{
    internal static void Postfix(Pawn ___pawn) => VitalFailureComp.For(___pawn)?.Invalidate();
}
