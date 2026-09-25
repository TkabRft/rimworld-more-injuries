using RimWorld;
using System.Collections.Generic;
using Verse;

namespace MoreInjuries.HealthConditions.LungCollapse;

public sealed class HediffComp_LungCollapseResolution : HediffComp
{
    private HediffCompProperties_LungCollapseResolution Properties => (HediffCompProperties_LungCollapseResolution)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        if (!Pawn.IsHashIntervalTick(GenTicks.TickRareInterval))
        {
            return;
        }
        if (parent.Severity >= Properties.SurgicalThreshold)
        {
            return;
        }
        HediffComp_TendDuration? tend = parent.TryGetComp<HediffComp_TendDuration>();
        if (tend is not { IsTended: true } || tend.tendQuality < Properties.MinTendQuality)
        {
            return;
        }
        if (LungIsBleeding())
        {
            return;
        }
        float newSeverity = parent.Severity + (Properties.SeverityPerDayTended * tend.tendQuality * GenTicks.TickRareInterval / GenDate.TicksPerDay);
        if (newSeverity <= 0f)
        {
            Pawn.health.RemoveHediff(parent);
            return;
        }
        parent.Severity = newSeverity;
    }

    private bool LungIsBleeding()
    {
        BodyPartRecord? part = parent.Part;
        if (part is null)
        {
            return false;
        }
        List<Hediff> hediffs = Pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; ++i)
        {
            Hediff hediff = hediffs[i];
            if (hediff.Part == part && hediff.Bleeding)
            {
                return true;
            }
        }
        return false;
    }
}
