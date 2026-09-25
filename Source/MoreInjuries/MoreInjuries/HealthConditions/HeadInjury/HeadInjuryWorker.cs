using MoreInjuries.Defs.WellKnown;
using UnityEngine;
using Verse;

namespace MoreInjuries.HealthConditions.HeadInjury;

public sealed class HeadInjuryWorker(MoreInjuryComp parent) : InjuryWorker(parent), IPostPostApplyDamageHandler
{
    private const float REFERENCE_HIT_POINTS = 10f;
    private const float MINIMUM_SEVERITY = 0.01f;

    public override bool IsEnabled =>
        (MoreInjuriesMod.Settings.EnableConcussion || MoreInjuriesMod.Settings.EnableHemorrhagicStroke)
        && HeadTraumaPropertiesDef.Named is not null;

    public void PostPostApplyDamage(ref readonly DamageInfo dinfo)
    {
        HeadTraumaPropertiesDef? properties = HeadTraumaPropertiesDef.Named;
        if (properties is null)
        {
            return;
        }
        BodyPartRecord? hitPart = dinfo.HitPart;
        if (hitPart is null || !properties.IsHeadPart(hitPart))
        {
            return;
        }
        float receivedDamage = dinfo.Amount;
        if (receivedDamage <= 0f)
        {
            return;
        }
        Pawn patient = Pawn;
        BodyPartRecord? brain = patient.health.hediffSet.GetBrain();
        if (brain is null)
        {
            return;
        }
        float healthScaling = 1f;
        if (hitPart.def.hitPoints > 0)
        {
            healthScaling = REFERENCE_HIT_POINTS / hitPart.def.hitPoints;
        }
        float trauma = receivedDamage
            * properties.GetDamageTypePercent(dinfo.Def)
            * properties.GetPartMultiplier(hitPart)
            * healthScaling;
        MoreInjuriesSettings settings = MoreInjuriesMod.Settings;
        if (settings.EnableConcussion)
        {
            TryApplyOutcome(
                patient,
                brain,
                KnownHediffDefOf.Concussion,
                trauma,
                settings.ConcussionThreshold,
                settings.ConcussionChance);
        }
        if (settings.EnableHemorrhagicStroke)
        {
            TryApplyOutcome(
                patient,
                brain,
                KnownHediffDefOf.HemorrhagicStroke,
                trauma,
                settings.HemorrhagicStrokeThreshold,
                settings.HemorrhagicStrokeChance);
        }
    }

    private static void TryApplyOutcome(
        Pawn patient,
        BodyPartRecord brain,
        HediffDef hediffDef,
        float trauma,
        float threshold,
        float chance)
    {
        float added = Mathf.Clamp01(trauma / threshold) * chance;
        if (added < MINIMUM_SEVERITY)
        {
            return;
        }
        if (!patient.health.hediffSet.TryGetHediff(hediffDef, out Hediff? hediff))
        {
            hediff = HediffMaker.MakeHediff(hediffDef, patient);
            patient.health.AddHediff(hediff, brain);
        }
        hediff.Severity = Mathf.Min(1f, hediff.Severity + added);
    }
}
