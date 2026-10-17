using MoreInjuries.Extensions;
using MoreInjuries.HealthConditions.Secondary.Handlers;
using MoreInjuries.HealthConditions.Secondary.Handlers.Modifiers;
using Verse;

namespace MoreInjuries.HealthConditions.HypovolemicShock;

public sealed class HediffModifier_HypovolemicShockOnset : SecondaryHediffModifier
{
    public override float GetModifier(Hediff hediff, IHediffCompHandler compHandler)
        => ShockPerfusionModel.TargetSeverity(hediff.Severity) > 0f && !hediff.pawn.HasOxygenDeficiencyImmunity()
            ? Unchanged : Disallow;
}
