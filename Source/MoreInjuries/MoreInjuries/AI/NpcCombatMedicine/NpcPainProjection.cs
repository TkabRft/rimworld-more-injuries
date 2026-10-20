using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.Secondary.Linked;
using MoreInjuries.HealthConditions.Secondary.Handlers.Modifiers;
using MoreInjuries.HealthConditions.HeavyBleeding.Overrides.BleedRateModifiers;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal enum NpcDrugPurpose { Pain, Restorative, Performance }

internal readonly struct NpcPainDose(float pain, float physiologicalRisk, float overdoseRisk, float addictionRisk, float benefit = 0f,
    bool recovery = false, float function = 0f, float bleedingFactor = 1f)
{
    internal float Pain { get; } = pain;
    internal float Benefit { get; } = benefit;
    internal float PhysiologicalRisk { get; } = physiologicalRisk;
    internal float OverdoseRisk { get; } = overdoseRisk;
    internal float AddictionRisk { get; } = addictionRisk;
    internal bool Recovery { get; } = recovery;
    internal float Function { get; } = function;
    internal float BleedingFactor { get; } = bleedingFactor;
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

    internal static bool TryDose(Pawn patient, ThingDef drug, out NpcPainDose result, out string reason, NpcDrugPurpose purpose = NpcDrugPurpose.Pain)
    {
        result = default;
        reason = "unsupported native dose-projection API";
        if (s_divideByBodySize is not { IsStatic: false, FieldType: Type fieldType }
            || fieldType != typeof(bool) || s_canCauseOverdose is null) { return false; }
        reason = "unsupported drug outcomes";
        if (drug.ingestible?.outcomeDoers is not { Count: > 0 } outcomes) { return false; }
        string? effect = NpcMedicalDrugs.EffectName(drug);
        IngestionOutcomeDoer_GiveHediff? high = effect is null ? DrugStatsUtility.GetDrugHighGiver(drug)
            : outcomes.OfType<IngestionOutcomeDoer_GiveHediff>()
                .FirstOrDefault(giver => giver.hediffDef?.defName == effect);
        CompProperties_Drug? comp = DrugStatsUtility.GetDrugComp(drug);
        if (high?.hediffDef?.stages is not { Count: > 0 } || comp is null
            || !(patient.BodySize > 0f)) { return false; }
        // Recognized drugs use deterministic GiveHediff outcomes. Custom effects
        // require an explicit projection adapter.
        if (outcomes.OfType<IngestionOutcomeDoer_GiveHediff>().Count(giver => giver.hediffDef == high.hediffDef) != 1
            || outcomes.Any(outcome => outcome.GetType() != typeof(IngestionOutcomeDoer_GiveHediff)
            || outcome.chance != 1f || outcome is IngestionOutcomeDoer_GiveHediff giver
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
        if (!LinkedEffects(patient, projected, replacement, out float bleedingFactor))
        { reason = "unsupported immediate linked drug effects"; return false; }
        float beforePain = patient.health.hediffSet.PainTotal;
        float afterPain = projected.PainTotal;
        float Factor(HediffSet set, bool healing) => set.hediffs.Aggregate(1f, (factor, hediff) =>
        {
            float value = healing ? hediff.CurStage?.naturalHealingFactor ?? -1f : hediff.CurStage?.totalBleedFactor ?? -1f;
            return value >= 0f ? factor * value : factor;
        });
        float healingBenefit = patient.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(injury => injury.CanHealNaturally())
            ? Mathf.Max(0f, Factor(projected, true) - Factor(patient.health.hediffSet, true)) : 0f;
        float bleedingBenefit = patient.health.hediffSet.BleedRateTotal > 0f
            ? Mathf.Max(0f, Factor(patient.health.hediffSet, false) - Factor(projected, false)) : 0f;
        bool painBenefit = NpcPainPolicy.Effective(beforePain, afterPain);
        if (purpose == NpcDrugPurpose.Pain && !painBenefit
            || purpose == NpcDrugPurpose.Restorative && healingBenefit <= .000001f && bleedingBenefit <= .000001f)
        { reason = "existing effects sufficient or ineffective top-up"; return false; }

        float capacityBenefit = 0f;
        float physiologicalRisk = 0f;
        bool canAct = !patient.health.forceDowned;
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
            // Consciousness has no extra NPC safety floor. Use the resolved
            // native capability boundary, including when recovering from pain.
            bool capable = after >= capacity.minForCapable;
            if (capacity == PawnCapacityDefOf.Consciousness ? !capable
                : !NpcPainPolicy.SafeCapacity(before, after, capacity.minForCapable))
            { reason = "post-dose capacity failure: " + capacity.defName; return false; }
            if (capacity == PawnCapacityDefOf.Moving || capacity == PawnCapacityDefOf.Breathing
                || capacity == PawnCapacityDefOf.BloodPumping) { canAct &= capable; }
            physiologicalRisk += Mathf.Max(0f, before - after);
            capacityBenefit += Mathf.Max(0f, after - before);
        }

        float threshold = patient.GetStatValue(StatDefOf.PainShockThreshold);
        float function = (beforePain - afterPain) / Mathf.Max(.01f, threshold)
            + (capacityBenefit - physiologicalRisk) / 5f;
        bool recovery = patient.Downed && patient.health.InPainShock && afterPain < threshold && canAct;
        if (!patient.Downed && purpose != NpcDrugPurpose.Restorative && function <= .000001f)
        { reason = "existing booster sufficient or ineffective top-up"; return false; }

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
        result = new(afterPain, physiologicalRisk, overdoseRisk, addiction, bleedingBenefit * 10f + healingBenefit,
            recovery, function, bleedingFactor);
        reason = "acceptable";
        return true;
    }

    private static bool LinkedEffects(Pawn patient, HediffSet projected, Hediff high, out float bleedingFactor)
    {
        bleedingFactor = 1f;
        LinkedSeverityProperties_ModExtension? links = high.def.GetModExtension<LinkedSeverityProperties_ModExtension>();
        if (links?.ModifiedHediffs is not { Count: > 0 }) { return true; }
        foreach (HediffCompHandler_LinkedSeverity handler in links.ModifiedHediffs)
        {
            // This adapter covers MI morphine's deterministic vasodilation.
            // Other handlers may consult real health or execute arbitrary code.
            if (handler.GetType() != typeof(HediffCompHandler_LinkedSeverity)
                || handler.LinkedHediffDef.defName != "Vasodilation"
                || handler.SeverityModifiers?.Any(modifier => modifier.GetType() != typeof(HediffModifier_SimpleCurve)) == true
                || handler.LinkedHediffDef.GetModExtension<BleedRateModifier_ModExtension>()?.Modifier
                    is not BleedRateModifier_Severity { Offset: > 0f } bleedModifier)
            { return false; }
            float Contribution(Hediff source)
            {
                float severity = source.Severity;
                if (handler.SeverityModifiers is { } modifiers)
                { foreach (HediffModifier_SimpleCurve modifier in modifiers) { severity *= modifier.SeverityCurve.Evaluate(source.Severity); } }
                return severity;
            }
            float before = patient.health.hediffSet.hediffs.Where(source => source.def == high.def).Sum(Contribution);
            float existing = patient.health.hediffSet.GetFirstHediffOfDef(handler.LinkedHediffDef)?.Severity ?? 0f;
            float severity = Mathf.Max(0f, existing - before) + Contribution(high);
            if (float.IsNaN(severity) || float.IsInfinity(severity)) { return false; }
            projected.hediffs.RemoveAll(source => source.def == handler.LinkedHediffDef);
            projected.hediffs.Add(new ProjectedHigh(patient, handler.LinkedHediffDef, severity));
            bleedingFactor *= (bleedModifier.Offset + severity) / (bleedModifier.Offset + existing);
        }
        return true;
    }
}
