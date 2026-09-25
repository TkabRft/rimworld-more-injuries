using MoreInjuries.Roslyn.SourceGen.XmlBinding.Attributes;

namespace MoreInjuries.HealthConditions.LungCollapse;

[XmlBindable]
public sealed partial class HediffCompProperties_LungCollapseResolution : HediffCompProperties<HediffComp_LungCollapseResolution>
{
    [XmlBinding<float>("surgicalThreshold", defaultValue: 0.45f)]
    public partial float SurgicalThreshold { get; }

    [XmlBinding<float>("severityPerDayTended", defaultValue: -0.15f)]
    public partial float SeverityPerDayTended { get; }

    [XmlBinding<float>("minTendQuality", defaultValue: 0.4f)]
    public partial float MinTendQuality { get; }
}
