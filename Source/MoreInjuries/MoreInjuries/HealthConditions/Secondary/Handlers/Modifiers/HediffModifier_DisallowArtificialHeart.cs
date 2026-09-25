using RimWorld;
using System.Linq;
using Verse;

namespace MoreInjuries.HealthConditions.Secondary.Handlers.Modifiers;

public sealed class HediffModifier_DisallowArtificialHeart : SecondaryHediffModifier
{
    public override float GetModifier(Hediff hediff, IHediffCompHandler compHandler)
    {
        if (hediff.pawn.health.hediffSet.hediffs.Any(candidate =>
            candidate.Part?.def == BodyPartDefOf.Heart &&
            candidate is Hediff_AddedPart))
        {
            return Disallow;
        }
        return Unchanged;
    }
}
