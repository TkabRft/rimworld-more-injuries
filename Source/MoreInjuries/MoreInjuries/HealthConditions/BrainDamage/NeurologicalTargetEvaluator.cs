using MoreInjuries.HealthConditions.Secondary.Handlers;
using MoreInjuries.HealthConditions.Secondary.Handlers.TargetEvaluators;
using MoreInjuries.HealthConditions.Secondary.Handlers.Modifiers;
using MoreInjuries.HealthConditions.VitalFailure;
using RimWorld;
using Verse;

namespace MoreInjuries.HealthConditions.BrainDamage;

public sealed class NeurologicalTargetEvaluator : BodyPartHediffTargetEvaluator
{
    public override BodyPartRecord? GetTargetBodyPart(HediffComp comp, HediffCompHandler_SecondaryCondition handler)
    {
        if (comp.parent.Part is { } part && part.def.tags?.Contains(BodyPartTagDefOf.ConsciousnessSource) == true
            && !comp.Pawn.health.hediffSet.PartIsMissing(part) && comp.Pawn.health.hediffSet.GetPartHealth(part) > 0f)
        { return part; }
        return VitalAnatomy.For(comp.Pawn.RaceProps.body).SurvivingBrain(comp.Pawn.health.hediffSet);
    }
}

public sealed class HediffModifier_AllowPersonalityOutcome : SecondaryHediffModifier
{
    public override float GetModifier(Hediff hediff, IHediffCompHandler compHandler) => hediff.pawn.RaceProps.Humanlike
        && hediff.pawn.skills is not null && (!hediff.pawn.IsMutant || !hediff.pawn.mutant.Def.preventsMentalBreaks) ? Unchanged : Disallow;
}
