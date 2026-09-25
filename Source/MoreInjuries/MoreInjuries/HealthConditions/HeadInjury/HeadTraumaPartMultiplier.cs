using MoreInjuries.Roslyn.SourceGen.XmlBinding.Attributes;
using Verse;

namespace MoreInjuries.HealthConditions.HeadInjury;

[XmlBindable]
public sealed partial class HeadTraumaPartMultiplier
{
    [XmlBinding("bodyPart")]
    public partial BodyPartDef BodyPart { get; }

    [XmlBinding("multiplier")]
    public partial float Multiplier { get; }
}
