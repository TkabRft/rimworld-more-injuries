using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace MoreInjuries.HealthConditions.Drugs;

/// <summary>Expose the actual drug stages and recovery comps on linked condition information cards.</summary>
public sealed class MedicalDrugHediffDef : HediffDef
{
    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        foreach (StatDrawEntry entry in base.SpecialDisplayStats(req))
        {
            yield return entry;
        }
        if (stages is { Count: > 0 })
        {
            for (int i = 0; i < stages.Count; i++)
            {
                HediffStage stage = stages[i];
                string stageLabel = stage.label.NullOrEmpty() ? "MI_DrugInfo_Satisfied".Translate().ToString() : stage.label.CapitalizeFirst();
                string label = chemicalNeed is not null
                    ? "MI_DrugInfo_State".Translate(stageLabel).ToString()
                    : "MI_DrugInfo_Stage".Translate(stageLabel, stage.minSeverity.ToStringPercent()).ToString();
                string effects = string.Join("\n", stage.SpecialDisplayStats().Select(static entry => $"{entry.LabelCap}: {entry.ValueString}"));
                if (effects.NullOrEmpty())
                {
                    effects = "MI_DrugInfo_NoStageEffects".Translate();
                }
                yield return new StatDrawEntry(StatCategoryDefOf.Drug, label, effects, effects, 4000 - i);
            }
        }
        if (comps is null)
        {
            yield break;
        }
        foreach (HediffCompProperties comp in comps)
        {
            if (comp is HediffCompProperties_SeverityPerDay { severityPerDay: < 0 } recovery)
            {
                yield return new StatDrawEntry(StatCategoryDefOf.Drug,
                    "MI_DrugInfo_Recovery".Translate(),
                    "MI_DrugInfo_PerDay".Translate((-recovery.severityPerDay).ToStringPercent()),
                    "MI_DrugInfo_RecoveryExplanation".Translate(), 4100);
            }
            else if (comp is HediffCompProperties_DrugEffectFactor tolerance)
            {
                // Ask the game's own tolerance comp for its effect, rather than duplicating its formula.
                // This condition is never attached to a pawn and no ingestion or health action is run.
                TolerancePreviewHediff sample = new() { def = this };
                HediffComp_DrugEffectFactor effectComp = new() { parent = sample, props = tolerance };
                List<string> samples = [];
                foreach (float severity in new[] { 0.1f, 0.5f, 1f })
                {
                    sample.SetPreviewSeverity(severity);
                    float effect = 1f;
                    effectComp.CompModifyChemicalEffect(tolerance.chemical, ref effect);
                    samples.Add("MI_DrugInfo_ToleranceSample".Translate(severity.ToStringPercent(), effect.ToStringPercent()));
                }
                string effects = string.Join("\n", samples);
                yield return new StatDrawEntry(StatCategoryDefOf.Drug,
                    "MI_DrugInfo_Tolerance".Translate(), effects,
                    "MI_DrugInfo_ToleranceExplanation".Translate(tolerance.chemical.LabelCap) + "\n\n" + effects, 4200);
            }
        }
    }

    private sealed class TolerancePreviewHediff : HediffWithComps
    {
        // The native Severity setter notifies pawn health when stages change.
        // A detached display sample has no pawn; only set its numeric backing field.
        public void SetPreviewSeverity(float severity) => severityInt = severity;
    }
}
