using MoreInjuries.Roslyn.SourceGen.XmlBinding.Attributes;
using RimWorld;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

[XmlBindable]
public sealed partial class VitalFailureConfigDef : Def
{
    [XmlBinding("shockDeprivationCurve")]
    public partial SimpleCurve ShockDeprivationCurve { get; }

    [XmlBinding("dilutionDeprivationCurve")]
    public partial SimpleCurve DilutionDeprivationCurve { get; }

    [XmlBinding("hypothermiaProtectionCurve")]
    public partial SimpleCurve HypothermiaProtectionCurve { get; }

    [XmlBinding<float>("cerebralSeverityPerDay", defaultValue: 128f)]
    public partial float CerebralSeverityPerDay { get; }

    [XmlBinding<float>("terminalCerebralSeverity", defaultValue: 8f)]
    public partial float TerminalCerebralSeverity { get; }
}

[XmlBindable]
public sealed partial class VitalFailureProperties : DefModExtension
{
    [XmlBinding("severityPerDay")]
    public partial float SeverityPerDay { get; }
}

[DefOf]
public static class VitalFailureDefOf
{
    public static VitalFailureConfigDef MI_VitalFailureConfig = null!;
    public static PawnCapacityDef Metabolism = null!;
    public static HediffDef MI_RespiratoryFailure = null!;
    public static HediffDef MI_LiverFailure = null!;
    public static HediffDef MI_RenalFailure = null!;
    public static HediffDef MI_FiltrationFailure = null!;
    public static HediffDef MI_MetabolicFailure = null!;
    public static HediffDef MI_Coma = null!;

    static VitalFailureDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(VitalFailureDefOf));
}

public sealed class VitalFailureCompProperties : CompProperties
{
    public VitalFailureCompProperties() => compClass = typeof(VitalFailureComp);
}
