using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

// Retains the native severityPerDay save data and legacy progression when the module is disabled.
public sealed class HediffComp_VitalArrest : HediffComp_SeverityPerDay
{
    internal bool MechanicalFailure;

    public override float SeverityChangePerDay() => VitalFailureComp.OwnsOxygenProgression(Pawn) ? 0f : base.SeverityChangePerDay();

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref MechanicalFailure, "mechanicalFailure", false);
    }
}

public sealed class HediffCompProperties_VitalArrest : HediffCompProperties_SeverityPerDay
{
    public HediffCompProperties_VitalArrest() => compClass = typeof(HediffComp_VitalArrest);
}
