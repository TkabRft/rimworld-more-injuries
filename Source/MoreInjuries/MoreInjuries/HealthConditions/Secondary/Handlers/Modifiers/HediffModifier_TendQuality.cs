using MoreInjuries.HealthConditions.HypovolemicShock;
using MoreInjuries.Roslyn.SourceGen.XmlBinding.Attributes;
using Verse;

namespace MoreInjuries.HealthConditions.Secondary.Handlers.Modifiers;

[XmlBindable]
public sealed partial class HediffModifier_TendQuality : SecondaryHediffModifier
{
    [XmlBinding("atFullQuality")]
    public partial SecondaryHediffModifier AtFullQuality { get; }

    public override float GetModifier(Hediff hediff, IHediffCompHandler compHandler)
        => ShockPerfusionModel.ChanceMultiplier(ShockTreatment.Quality(hediff), AtFullQuality.GetModifier(hediff, compHandler));
}
