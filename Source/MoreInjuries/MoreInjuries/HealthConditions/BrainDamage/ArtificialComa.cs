using MoreInjuries.HealthConditions.VitalFailure;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace MoreInjuries.HealthConditions.BrainDamage;

public sealed class HediffCompProperties_ArtificialComa : HediffCompProperties
{
    public HediffCompProperties_ArtificialComa() => compClass = typeof(HediffComp_ArtificialComa);
}

public sealed class HediffComp_ArtificialComa : HediffComp
{
    public override void CompPostTick(ref float severityAdjustment)
    {
        if (Pawn.Dead || Pawn.Suspended || !Pawn.IsHashIntervalTick(250)) { return; }
        if (Pawn.health.hediffSet.HasHediff(NeurologicalDefOf.WakeUpHigh))
        {
            Pawn.health.RemoveHediff(parent);
            return;
        }
        if (parent.Severity >= 0.72f && Rand.MTBEventOccurs(0.17f, GenDate.TicksPerDay, 250f))
        {
            NeurologicalOutcomes.Apply(Pawn, VitalAnatomy.For(Pawn.RaceProps.body).SurvivingBrain(Pawn.health.hediffSet),
                NeurologicalDefOf.MI_NeurologicalConfig.ComaProfile, 0.05f);
        }
    }
}

public sealed class Recipe_InduceArtificialComa : Recipe_Surgery
{
    internal static bool CanReverse(Pawn pawn) => !pawn.IsMutant || MutantUtility.CanUseDrug(pawn, ThingDefOf.WakeUp);

    public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
    {
        BodyPartRecord? brain = VitalAnatomy.For(pawn.RaceProps.body).SurvivingBrain(pawn.health.hediffSet);
        if (brain is not null) { yield return brain; }
    }

    public override bool AvailableOnNow(Thing thing, BodyPartRecord? part = null) => thing is Pawn pawn
        && VitalPhysiology.Supports(pawn) && !pawn.Dead && !pawn.Suspended && VitalPhysiology.CanReceiveCare(pawn)
        && (pawn.playerSettings is null || (int)pawn.playerSettings.medCare >= (int)MedicalCareCategory.NormalOrWorse)
        && CanReverse(pawn) && !pawn.health.hediffSet.HasHediff(NeurologicalDefOf.MI_ArtificialComa)
        && !pawn.health.hediffSet.HasHediff(NeurologicalDefOf.WakeUpHigh) && base.AvailableOnNow(thing, part);

    public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
    {
        if (!AvailableOnNow(pawn, part) || CheckSurgeryFail(billDoer, pawn, ingredients, part, bill)) { return; }
        pawn.health.AddHediff(NeurologicalDefOf.MI_ArtificialComa);
    }
}

public sealed class Recipe_AwakenArtificialComa : Recipe_AdministerIngestible
{
    public override bool AvailableOnNow(Thing thing, BodyPartRecord? part = null) => thing is Pawn pawn
        && VitalPhysiology.CanReceiveCare(pawn) && Recipe_InduceArtificialComa.CanReverse(pawn)
        && pawn.health.hediffSet.HasHediff(NeurologicalDefOf.MI_ArtificialComa) && base.AvailableOnNow(thing, part);

    public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
    {
        if (!AvailableOnNow(pawn, part)) { return; }
        base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);
        if (pawn.health.hediffSet.HasHediff(NeurologicalDefOf.WakeUpHigh)
            && pawn.health.hediffSet.GetFirstHediffOfDef(NeurologicalDefOf.MI_ArtificialComa) is { } coma)
        {
            pawn.health.RemoveHediff(coma);
        }
    }
}
