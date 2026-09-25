using MoreInjuries.Extensions;
using RimWorld;
using System.Linq;
using Verse;

namespace MoreInjuries.HealthConditions.CardiacArrest;

public class HediffComp_CardiacArrest : HediffComp
{
    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        Pawn pawn = parent.pawn;

        if (pawn.HasOxygenDeficiencyImmunity() || HasArtificialHeart(pawn))
        {
            pawn.health.RemoveHediff(parent);
            return;
        }

        if (ModLister.BiotechInstalled && pawn.health.hediffSet.HasHediff(HediffDefOf.Deathrest))
        {
            pawn.health.RemoveHediff(parent);
            return;
        }

        base.CompPostPostAdd(dinfo);
    }

    private static bool HasArtificialHeart(Pawn pawn)
    {
        return pawn.health.hediffSet.hediffs.Any(hediff =>
            hediff.Part?.def == BodyPartDefOf.Heart &&
            hediff is Hediff_AddedPart);
    }
}
