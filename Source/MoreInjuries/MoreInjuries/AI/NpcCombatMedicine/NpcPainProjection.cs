using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal readonly struct NpcPainDose(float pain, float physiologicalRisk, float overdoseRisk, float addictionRisk)
{
    internal float Pain { get; } = pain;
    internal float PhysiologicalRisk { get; } = physiologicalRisk;
    internal float OverdoseRisk { get; } = overdoseRisk;
    internal float AddictionRisk { get; } = addictionRisk;
}

internal static class NpcPainProjection
{
    private static readonly FieldInfo? s_divideByBodySize = AccessTools.Field(typeof(IngestionOutcomeDoer_GiveHediff), "divideByBodySize");
    private static readonly Func<Pawn_DrugPolicyTracker, ThingDef, bool>? s_canCauseOverdose = BindOverdoseEligibility();

    private static Func<Pawn_DrugPolicyTracker, ThingDef, bool>? BindOverdoseEligibility()
    {
        MethodInfo? method = AccessTools.Method(typeof(Pawn_DrugPolicyTracker), "CanCauseOverdose", [typeof(ThingDef)]);
        return method is { IsStatic: false } && method.ReturnType == typeof(bool)
            ? AccessTools.MethodDelegate<Func<Pawn_DrugPolicyTracker, ThingDef, bool>>(method) : null;
    }

    // Never instantiate a real drug hediff: PostMake/comps/Severity callbacks can
    // change health or consume RNG. Only the resolved numeric stage is projected.
    private sealed class ProjectedHigh : Hediff
    {
        internal ProjectedHigh(Pawn patient, HediffDef definition, float severity)
        { pawn = patient; def = definition; severityInt = severity; }
    }

    internal static bool TryDose(Pawn patient, ThingDef drug, out NpcPainDose result, out string reason)
    {
        result = default;
        reason = "unsupported native dose-projection API";
        if (s_divideByBodySize is not { IsStatic: false, FieldType: Type fieldType }
            || fieldType != typeof(bool) || s_canCauseOverdose is null) { return false; }
        reason = "unsupported drug outcomes";
        IngestionOutcomeDoer_GiveHediff? high = DrugStatsUtility.GetDrugHighGiver(drug);
        CompProperties_Drug? comp = DrugStatsUtility.GetDrugComp(drug);
        if (high?.hediffDef?.stages is not { Count: > 0 } || comp is null
            || !(patient.BodySize > 0f)) { return false; }
        // The three supported drugs use GiveHediff outcomes. A mod replacing the
        // effects with an arbitrary outcome requires an explicit projection adapter.
        if (drug.ingestible.outcomeDoers.Any(outcome => outcome.GetType() != typeof(IngestionOutcomeDoer_GiveHediff)
            || outcome.chance < 1f || outcome is IngestionOutcomeDoer_GiveHediff giver
                && giver.hediffDef != high.hediffDef && giver.hediffDef != comp.chemical?.toleranceHediff)) { return false; }
        float gain = high.severity > 0f ? high.severity : high.hediffDef.initialSeverity;
        if ((bool)s_divideByBodySize.GetValue(high)) { gain /= patient.BodySize; }
        AddictionUtility.ModifyChemicalEffectForToleranceAndBodySize(patient, high.toleranceChemical,
            ref gain, high.multiplyByGeneToleranceFactors);
        if (!(gain > 0f) || float.IsInfinity(gain)) { reason = "no effective dose"; return false; }
        Hediff? existing = patient.health.hediffSet.GetFirstHediffOfDef(high.hediffDef);
        float severity = Mathf.Clamp((existing?.Severity ?? 0f) + gain, high.hediffDef.minSeverity, high.hediffDef.maxSeverity);
        if (drug == KnownThingDefOf.Morphine && severity >= .4f)
        { reason = "unsafe cumulative morphine buildup"; return false; }
        ProjectedHigh replacement = new(patient, high.hediffDef, severity);
        if (replacement.CurStage is { lifeThreatening: true }
            || high.hediffDef.lethalSeverity > 0f && severity >= high.hediffDef.lethalSeverity)
        { reason = "dangerous high stage"; return false; }
        HediffSet projected = new(patient);
        projected.hediffs.AddRange(patient.health.hediffSet.hediffs.Where(hediff => hediff != existing));
        projected.hediffs.Add(replacement);
        float beforePain = patient.health.hediffSet.PainTotal;
        float afterPain = projected.PainTotal;
        if (!NpcPainPolicy.Effective(beforePain, afterPain)) { reason = "existing analgesia sufficient or ineffective top-up"; return false; }

        float physiologicalRisk = 0f;
        float beforeConsciousness = patient.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
        float afterConsciousness = PawnCapacityUtility.CalculateCapacityLevel(projected, PawnCapacityDefOf.Consciousness);
        foreach (PawnCapacityDef capacity in new[] { PawnCapacityDefOf.Consciousness, PawnCapacityDefOf.Breathing,
            PawnCapacityDefOf.BloodPumping, PawnCapacityDefOf.Moving, PawnCapacityDefOf.Manipulation })
        {
            float before = patient.health.capacities.GetLevel(capacity);
            float baseline = PawnCapacityUtility.CalculateCapacityLevel(patient.health.hediffSet, capacity);
            float after = PawnCapacityUtility.CalculateCapacityLevel(projected, capacity);
            // Some workers consult pawn.health.capacities for dependencies rather
            // than the supplied set. Bound their result by the loss of consciousness;
            // never credit a speculative capacity gain or mutate the real tracker.
            if (capacity != PawnCapacityDefOf.Consciousness && beforeConsciousness > 0f && afterConsciousness < beforeConsciousness)
            { after *= afterConsciousness / beforeConsciousness; }
            // Detect workers/patches that cannot reproduce the current capacity.
            if (Mathf.Abs(baseline - before) > .02f || float.IsNaN(after))
            { reason = "unsupported capacity projection"; return false; }
            if (!NpcPainPolicy.SafeCapacity(before, after, capacity.minForCapable))
            { reason = "post-dose capacity failure: " + capacity.defName; return false; }
            physiologicalRisk += Mathf.Max(0f, before - after);
        }

        float overdoseFactor = 1f;
        if (ModsConfig.BiotechActive && patient.genes is { } genes)
        {
            foreach (Gene gene in genes.GenesListForReading)
            { if (gene.Active && gene.def.chemical == comp.chemical) { overdoseFactor *= gene.def.overdoseChanceFactor; } }
        }
        // The native policy helper distinguishes overdose immunity/dependency from
        // resistance. Resistance reduces random risk, not every other drug effect.
        bool canOverdose = comp.CanCauseOverdose && overdoseFactor > 0f
            && (patient.drugs is null || s_canCauseOverdose(patient.drugs, drug));
        float ordinaryMaximum = canOverdose ? Mathf.Max(0f, comp.overdoseSeverityOffset.max) / patient.BodySize : 0f;
        float currentOverdose = patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.DrugOverdose)?.Severity ?? 0f;
        if (!NpcPainPolicy.SafeOverdose(currentOverdose, ordinaryMaximum))
        { reason = "dangerous cumulative overdose"; return false; }
        // Worst ordinary severity plus the gene-modified rare large-overdose risk.
        // Planning never rolls either outcome; real ingestion retains native RNG.
        float overdoseRisk = ordinaryMaximum + (canOverdose ? comp.largeOverdoseChance * overdoseFactor : 0f);
        float tolerance = comp.chemical is null ? 0f : AddictionUtility.FindToleranceHediff(patient, comp.chemical)?.Severity ?? 0f;
        float addiction = DrugStatsUtility.GetAddictivenessAtTolerance(drug, tolerance)
            * (comp.chemical is not null ? patient.genes?.AddictionChanceFactor(comp.chemical) ?? 1f : 1f);
        result = new(afterPain, physiologicalRisk, overdoseRisk, addiction);
        reason = "acceptable";
        return true;
    }
}
