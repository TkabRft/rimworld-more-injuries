using HarmonyLib;
using MoreInjuries.HealthConditions.BrainDamage;
using Verse;

namespace MoreInjuries.Patches;

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
    new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
internal static class Patch_NeurologicalInjury
{
    internal static void Postfix(Pawn ___pawn, Hediff hediff, BodyPartRecord part, DamageInfo? dinfo)
        => NeurologicalOutcomes.DirectInjury(___pawn, hediff, part, dinfo);
}
