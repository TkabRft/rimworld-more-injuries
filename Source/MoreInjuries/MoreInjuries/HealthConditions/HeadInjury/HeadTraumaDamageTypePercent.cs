using MoreInjuries.Roslyn.SourceGen.XmlBinding.Attributes;

namespace MoreInjuries.HealthConditions.HeadInjury;

[XmlBindable]
public sealed partial class HeadTraumaDamageTypePercent
{
    [XmlBinding("damageDefName")]
    public partial string DamageDefName { get; }

    [XmlBinding("percent")]
    public partial float Percent { get; }
}
