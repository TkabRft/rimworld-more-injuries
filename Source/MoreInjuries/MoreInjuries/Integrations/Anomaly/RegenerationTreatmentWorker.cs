using MoreInjuries.Defs.WellKnown;
using MoreInjuries.Extensions;
using MoreInjuries.HealthConditions;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace MoreInjuries.Integrations.Anomaly;

internal sealed class RegenerationTreatmentWorker(MoreInjuryComp parent) : InjuryWorker(parent), ICompTickHandler
{
    private const float REFERENCE_REGENERATION = 100f;
    private const float FRACTURE_HEAL_DAYS = 2f;
    private const float NEURO_HEAL_DAYS = 4f;
    private const float VANILLA_FRACTURE_HEALING_SEVERITY_PER_DAY = 0.125f;
    private const int TICK_INTERVAL = 250;

    private static HediffDef[]? s_neuroWhitelist;

    public override bool IsEnabled => MoreInjuriesMod.Settings.AnomalyRegenerationTreatsStructuralDamage && ModLister.AnomalyInstalled;

    public void CompTick()
    {
        if (!Pawn.IsHashIntervalTick(interval: TICK_INTERVAL))
        {
            return;
        }
        float regeneration = SumRegeneration(Pawn);
        if (regeneration <= 0f)
        {
            return;
        }
        ConvertFractures(Pawn);
        float intervalFraction = TICK_INTERVAL / (float)GenDate.TicksPerDay;
        float fractureTargetPerDay = regeneration / (REFERENCE_REGENERATION * FRACTURE_HEAL_DAYS);
        float addedFracturePerDay = fractureTargetPerDay - VANILLA_FRACTURE_HEALING_SEVERITY_PER_DAY;
        if (addedFracturePerDay < 0f)
        {
            addedFracturePerDay = 0f;
        }
        float neuroPerDay = regeneration / (REFERENCE_REGENERATION * NEURO_HEAL_DAYS);
        ReduceStructuralLeftovers(Pawn, addedFractureLoss: addedFracturePerDay * intervalFraction, neuroLoss: neuroPerDay * intervalFraction);
    }

    private static float SumRegeneration(Pawn pawn)
    {
        float total = 0f;
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; ++i)
        {
            if (hediffs[i].CurStage is HediffStage { regeneration: > 0f } stage)
            {
                total += stage.regeneration;
            }
        }
        return total;
    }

    private static void ConvertFractures(Pawn pawn)
    {
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        List<Hediff> fractures = [];
        for (int i = 0; i < hediffs.Count; ++i)
        {
            Hediff hediff = hediffs[i];
            if (hediff.def == KnownHediffDefOf.Fracture)
            {
                fractures.Add(hediff);
            }
        }
        for (int i = 0; i < fractures.Count; ++i)
        {
            Hediff fracture = fractures[i];
            BodyPartRecord? part = fracture.Part;
            if (part is null || !pawn.health.hediffSet.TryGetFirstHediffMatchingPart(part, KnownHediffDefOf.FractureHealing, out _))
            {
                Hediff healingFracture = HediffMaker.MakeHediff(KnownHediffDefOf.FractureHealing, pawn, part);
                healingFracture.Severity = 1f;
                pawn.health.AddHediff(healingFracture);
            }
            pawn.health.RemoveHediff(fracture);
        }
    }

    private static void ReduceStructuralLeftovers(Pawn pawn, float addedFractureLoss, float neuroLoss)
    {
        Hediff[] hediffs = [.. pawn.health.hediffSet.hediffs];
        for (int i = 0; i < hediffs.Length; ++i)
        {
            Hediff hediff = hediffs[i];
            if (hediff.def == KnownHediffDefOf.FractureHealing)
            {
                ReduceSeverity(hediff, addedFractureLoss);
            }
            else if (IsNeuroCondition(hediff.def) && !PartHasInjury(pawn, hediff.Part))
            {
                ReduceSeverity(hediff, neuroLoss);
            }
        }
    }

    private static bool IsNeuroCondition(HediffDef def)
    {
        HediffDef[] whitelist = s_neuroWhitelist ??=
        [
            KnownHediffDefOf.SpinalCordParalysis,
            KnownHediffDefOf.BrainDamage_MotorDysfunction,
            KnownHediffDefOf.BrainDamage_LanguageAphasia,
            KnownHediffDefOf.BrainDamage_VisualAgnosia,
            KnownHediffDefOf.BrainDamage_FrontalLobe,
            KnownHediffDefOf.BrainDamage_Confusion,
        ];
        return Array.IndexOf(whitelist, def) >= 0;
    }

    private static bool PartHasInjury(Pawn pawn, BodyPartRecord? part)
    {
        if (part is null)
        {
            return false;
        }
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; ++i)
        {
            Hediff hediff = hediffs[i];
            if (hediff.Part == part && hediff is Hediff_Injury)
            {
                return true;
            }
        }
        return false;
    }

    private static void ReduceSeverity(Hediff hediff, float loss)
    {
        if (loss <= 0f)
        {
            return;
        }
        float severity = hediff.Severity - loss;
        if (severity < 0f)
        {
            severity = 0f;
        }
        hediff.Severity = severity;
    }
}
