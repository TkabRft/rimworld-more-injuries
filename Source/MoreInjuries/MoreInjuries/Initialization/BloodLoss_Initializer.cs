using MoreInjuries.HealthConditions.HeavyBleeding;
using RimWorld;
using Verse;

namespace MoreInjuries.Initialization;

[StaticConstructorOnStartup]
public static class BloodLoss_Initializer
{
    static BloodLoss_Initializer()
    {
        // The periodic handlers decide direct blood-loss death. A later XML patch must not
        // turn the severity cap itself into a lethal threshold before those handlers run.
        HediffDef bloodLoss = HediffDefOf.BloodLoss;
        bloodLoss.lethalSeverity = BloodLossPolicy.NonlethalCapThreshold(bloodLoss.maxSeverity, bloodLoss.lethalSeverity);
    }
}
