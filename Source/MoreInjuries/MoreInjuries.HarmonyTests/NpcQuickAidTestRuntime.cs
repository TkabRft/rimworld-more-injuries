using MoreInjuries.AI.NpcCombatMedicine;
using Verse;
using Verse.AI;

namespace Verse
{
    public static class HealthUtility { public static int TicksUntilDeathDueToBloodLoss(Pawn pawn) => pawn.BleedingDeathTicks; }
    public sealed partial class HediffDef
    {
        public readonly List<object> Extensions = [];
        public T? GetModExtension<T>() where T : class => Extensions.OfType<T>().FirstOrDefault();
    }
    public sealed class SimpleCurve { public Func<float, float> EvaluateValue = value => value; public float Evaluate(float value) => EvaluateValue(value); }
    public readonly record struct ThingRequest(ThingDef Def) { public static ThingRequest ForDef(ThingDef def) => new(def); }
    public readonly record struct TraverseParms(Pawn Pawn) { public static TraverseParms For(Pawn pawn, Danger danger) => new(pawn); }
    public static class GenClosest
    {
        public static Thing? ClosestThingReachable(IntVec3 origin, Map map, ThingRequest request, PathEndMode mode,
            TraverseParms parms, float maxDistance, Func<Thing, bool> validator, int searchRegionsMin, int searchRegionsMax, bool forceAllowGlobalSearch)
            => parms.Pawn.Supplies.Concat(map.listerThings.Buildings).Where(thing => thing.Spawned
                && thing.def == request.Def && thing.Position.InHorDistOf(origin, maxDistance) && validator(thing))
                .OrderBy(thing => thing.Position.DistanceToSquared(origin)).FirstOrDefault();
    }
}
namespace MoreInjuries.HealthConditions.Secondary.Linked
{
    public sealed class LinkedSeverityProperties_ModExtension { public List<HediffCompHandler_LinkedSeverity>? ModifiedHediffs; }
    public class HediffCompHandler_LinkedSeverity
    {
        public HediffDef LinkedHediffDef = Vasodilation();
        private static HediffDef Vasodilation()
        {
            HediffDef def = new() { defName = "Vasodilation" };
            def.Extensions.Add(new MoreInjuries.HealthConditions.HeavyBleeding.Overrides.BleedRateModifiers.BleedRateModifier_ModExtension());
            return def;
        }
        public List<MoreInjuries.HealthConditions.Secondary.Handlers.Modifiers.SecondaryHediffModifier>? SeverityModifiers;
    }
}
namespace MoreInjuries.HealthConditions.HeavyBleeding.Overrides.BleedRateModifiers
{
    public sealed class BleedRateModifier_ModExtension { public object Modifier = new BleedRateModifier_Severity(); }
    public sealed class BleedRateModifier_Severity { public float Offset = 1f; }
}
namespace MoreInjuries.HealthConditions.Secondary.Handlers.Modifiers
{
    public class SecondaryHediffModifier;
    public class HediffModifier_SimpleCurve : SecondaryHediffModifier { public SimpleCurve SeverityCurve = new(); }
}
namespace MoreInjuries.AI.NpcCombatMedicine
{
    internal static class NpcMedicalTriage { internal static float Urgency(Pawn patient) => patient.EmergencyUrgency; }
    internal static partial class NpcExtraction
    {
        internal sealed class Plan
        {
            internal IntVec3 Destination;
            internal float Ticks, Exposure;
            internal NpcExtractionRisk Origin, Target;
            internal Func<Job> Create = () => new();
        }
        internal static Func<Pawn, Pawn, Plan?>? OnPreview;
        internal static Plan? Preview(Pawn actor, Pawn patient, out bool danger)
        { Plan? plan = OnPreview?.Invoke(actor, patient); danger = plan?.Origin.Hazard == true; return plan; }
    }
    internal static partial class NpcMedicalAi
    {
        internal static Action<Pawn, Pawn, Action<Func<Job?>, JobDef, Thing?, float, int, int, float>>? OnPreviewBleeding;
        internal static void PreviewQuickBleeding(Pawn actor, Pawn patient, Action<Func<Job?>, JobDef, Thing?, float, int, int, float> preview)
            => OnPreviewBleeding?.Invoke(actor, patient, preview);
    }
}
