using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.Secondary;
using MoreInjuries.HealthConditions.Secondary.Handlers;
using MoreInjuries.HealthConditions.Secondary.Handlers.HediffMakers;
using MoreInjuries.HealthConditions.VitalFailure;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MoreInjuries.HealthConditions.BrainDamage;

internal static class NeurologicalOutcomes
{
    internal static NeurologicalProfileDef SourceProfile(Hediff source) => source.def.GetModExtension<NeurologicalSourceProperties>()?.Profile
        ?? source.TryGetComp<HediffComp_NeurologicalSource>()?.Profile ?? NeurologicalDefOf.MI_NeurologicalConfig.HypoxiaProfile;

    internal static bool Eligible(Pawn pawn, HediffDef def) => def.GetModExtension<NeurologicalConditionProperties>() is { } properties
        && (pawn.RaceProps.Humanlike || properties.AnimalEligible)
        && (!pawn.IsMutant || !pawn.mutant.Def.preventsMentalBreaks || properties.AnimalEligible);

    // Two bounded linear passes over a compiled, small profile. Zero weights really exclude entries.
    internal static WeightedHediffMakerDef? Choose(Pawn pawn, NeurologicalProfileDef profile)
    {
        float total = 0f;
        for (int i = 0; i < profile.Outcomes.Count; i++)
        {
            WeightedHediffMakerDef outcome = profile.Outcomes[i];
            if (outcome.Weight > 0f && Eligible(pawn, outcome.HediffDef)) { total += outcome.Weight; }
        }
        if (total <= 0f) { return null; }
        float roll = Rand.Range(0f, total);
        WeightedHediffMakerDef? last = null;
        for (int i = 0; i < profile.Outcomes.Count; i++)
        {
            WeightedHediffMakerDef outcome = profile.Outcomes[i];
            if (outcome.Weight <= 0f || !Eligible(pawn, outcome.HediffDef)) { continue; }
            last = outcome;
            roll -= outcome.Weight;
            if (roll <= 0f) { return outcome; }
        }
        return last;
    }

    internal static void Apply(Pawn pawn, BodyPartRecord? brain, NeurologicalProfileDef profile, float? severity = null)
    {
        if (pawn.Dead || pawn.Suspended || !MoreInjuriesMod.Settings.EnableNeuralDamage) { return; }
        WeightedHediffMakerDef? outcome = Choose(pawn, profile);
        if (outcome is null) { return; }
        Hediff? existing = pawn.health.hediffSet.hediffs.Find(h => h.def == outcome.HediffDef && h.Part == brain);
        float added = severity ?? outcome.GetInitialSeverity();
        if (existing is not null) { existing.Severity = Mathf.Min(1f, existing.Severity + added); }
        else
        {
            Hediff condition = HediffMaker.MakeHediff(outcome.HediffDef, pawn, brain);
            condition.Severity = added;
            pawn.health.AddHediff(condition);
        }
    }

    internal static void DirectInjury(Pawn pawn, Hediff injury, BodyPartRecord? part, DamageInfo? damage)
    {
        part ??= injury.Part;
        if (damage is not { } info || injury is not Hediff_Injury || injury.def == KnownHediffDefOf.BrainDamage_Hypoxia
            || part?.def.tags?.Contains(BodyPartTagDefOf.ConsciousnessSource) != true || !VitalPhysiology.Supports(pawn)
            || pawn.Dead || pawn.Suspended || !MoreInjuriesMod.Settings.EnableNeuralDamage) { return; }
        float chance = NeurologicalDefOf.MI_NeurologicalConfig.DirectBrainChanceFactor
            * Mathf.Clamp01(Mathf.Min(injury.Severity, info.Amount) / part.def.GetMaxHealth(pawn));
        if (Rand.Chance(chance)) { Apply(pawn, part, NeurologicalDefOf.MI_NeurologicalConfig.ForDamage(info.Def)); }
    }

    internal static void Transform(Pawn pawn)
    {
        if (!pawn.IsMutant) { return; }
        MutantDef mutant = pawn.mutant.Def;
        if (!mutant.removeChronicIllnesses && !mutant.removePermanentInjuries) { return; }
        for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
        {
            Hediff hediff = pawn.health.hediffSet.hediffs[i];
            if (hediff.def.GetModExtension<NeurologicalConditionProperties>()?.Regenerative == true)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }
    }

    internal static void Regenerate(Pawn pawn, IReadOnlyList<Hediff> conditions, ISet<BodyPartRecord> injuredParts, float regeneration)
    {
        for (int i = conditions.Count - 1; i >= 0; i--)
        {
            Hediff hediff = conditions[i];
            if (hediff.Part is null || !injuredParts.Contains(hediff.Part))
            {
                hediff.Severity = Mathf.Max(0f, hediff.Severity - regeneration / 400f * (250f / GenDate.TicksPerDay));
                if (hediff.Severity <= 0f) { pawn.health.RemoveHediff(hediff); }
            }
        }
    }

}

public sealed class HediffMakerProperties_NeurologicalProfile : HediffMakerProperties
{
    public override HediffMakerDef GetHediffMakerDef(HediffComp parentComp, HediffCompHandler_SecondaryCondition handler)
        => NeurologicalOutcomes.Choose(parentComp.Pawn, parentComp.parent.TryGetComp<HediffComp_NeurologicalSource>()?.Profile
            ?? NeurologicalDefOf.MI_NeurologicalConfig.HypoxiaProfile) ?? throw new System.InvalidOperationException("No eligible neurological outcome; use the guarded neurological handler.");
}

public sealed class HediffCompHandler_NeurologicalOutcome_Tick : HediffCompHandler_SecondaryCondition_Tick
{
    public override void Handle(HediffComp_SecondaryCondition comp)
    {
        if (VitalAnatomy.For(comp.Pawn.RaceProps.body).SurvivingBrain(comp.Pawn.health.hediffSet) is null) { return; }
        WeightedHediffMakerDef? outcome = NeurologicalOutcomes.Choose(comp.Pawn,
            comp.parent.TryGetComp<HediffComp_NeurologicalSource>()?.Profile ?? NeurologicalDefOf.MI_NeurologicalConfig.HypoxiaProfile);
        if (outcome is not null && !ShouldSkip(comp, outcome.HediffDef)) { TryApplyHediff(comp, outcome); }
    }
}

public sealed class HediffCompProperties_NeurologicalSource : HediffCompProperties
{
    public HediffCompProperties_NeurologicalSource() => compClass = typeof(HediffComp_NeurologicalSource);
}

public sealed class HediffComp_NeurologicalSource : HediffComp
{
    private NeurologicalProfileDef? _profile;
    private bool _concussionRolled;
    internal NeurologicalProfileDef Profile => _profile ?? (parent.def == KnownHediffDefOf.HemorrhagicStroke
        ? NeurologicalDefOf.MI_NeurologicalConfig.StrokeProfile : NeurologicalDefOf.MI_NeurologicalConfig.HypoxiaProfile);

    public override void CompExposeData()
    {
        Scribe_Defs.Look(ref _profile, "neurologicalSourceProfile");
        Scribe_Values.Look(ref _concussionRolled, "severeConcussionOutcomeRolled", false);
    }

    internal void Record(DamageDef? damage, bool directBrainHit)
    {
        _profile = NeurologicalDefOf.MI_NeurologicalConfig.ForDamage(damage);
        if (parent.def != KnownHediffDefOf.Concussion || _concussionRolled
            || parent.Severity < NeurologicalDefOf.MI_NeurologicalConfig.ConcussionThreshold) { return; }
        _concussionRolled = true;
        if (!directBrainHit && Rand.Chance(NeurologicalDefOf.MI_NeurologicalConfig.ConcussionChance))
        {
            NeurologicalOutcomes.Apply(Pawn, parent.Part, Profile);
        }
    }

    internal void SetProfile(NeurologicalProfileDef profile) => _profile = profile;
}
