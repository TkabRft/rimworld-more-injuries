using System.Diagnostics.CodeAnalysis;

namespace MoreInjuries.HealthConditions.BrainDamage;

[SuppressMessage("Style", "IDE1006", Justification = "XML field names are public data contracts.")]
public sealed class NeurologicalMilestone
{
    private readonly float injury;
    private readonly float lastingSeverity;
    internal float Injury => injury;
    internal float LastingSeverity => lastingSeverity;
}
