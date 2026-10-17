using MoreInjuries.HealthConditions.HeavyBleeding;
using Verse;

namespace MoreInjuries.HealthConditions.Secondary.Handlers.Modifiers;

public sealed class HediffModifier_RequireUnsupportedTrauma : SecondaryHediffModifier
{
    public override float GetModifier(Hediff hediff, IHediffCompHandler compHandler)
        => TraumaEligibility.Supports(hediff.pawn) ? Disallow : Unchanged;
}
