using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

internal static class PrognosisConditionPolicy
{
    // A maintained dependency is not an active death warning. Start projecting its
    // progression only once its current stage is life-threatening (chemical coma).
    internal static bool ShouldProject(Hediff condition) => condition is not Hediff_ChemicalDependency
        || condition.CurStage?.lifeThreatening == true;
}
