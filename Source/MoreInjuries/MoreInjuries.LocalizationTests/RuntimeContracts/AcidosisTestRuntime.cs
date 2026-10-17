using System.Globalization;
using System.Xml.Linq;

namespace MoreInjuries.LocalizationTests.RuntimeContracts
{
    internal static class RuntimeDefCurves
    {
        internal static readonly XElement Acidosis = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot,
            "Defs", "HediffDefs", "Hediffs_Acidosis.xml")).Descendants("li")
            .Single(node => node.Attribute("Class")?.Value == "MoreInjuries.HealthConditions.Acidosis.HediffCompProperties_Acidosis");

        internal static Verse.SimpleCurve Curve(XElement node)
        {
            (float X, float Y)[] points = node.Element("points")!.Elements("li").Select(point =>
            {
                string[] values = point.Value.Split(',');
                return (float.Parse(values[0], CultureInfo.InvariantCulture), float.Parse(values[1], CultureInfo.InvariantCulture));
            }).ToArray();
            return new(x =>
            {
                if (x <= points[0].X) { return points[0].Y; }
                for (int i = 1; i < points.Length; i++)
                {
                    if (x <= points[i].X)
                    {
                        var a = points[i - 1]; var b = points[i];
                        return a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X);
                    }
                }
                return points[^1].Y;
            });
        }
    }
}

namespace MoreInjuries.HealthConditions.Acidosis
{
    public partial class HediffCompProperties_Acidosis
    {
        private static readonly Verse.SimpleCurve s_curve = LocalizationTests.RuntimeContracts.RuntimeDefCurves.Curve(
            LocalizationTests.RuntimeContracts.RuntimeDefCurves.Acidosis.Element("shockAcidosisPerDayCurve")!);
        private static float Field(string name) => float.Parse(LocalizationTests.RuntimeContracts.RuntimeDefCurves.Acidosis.Element(name)!.Value, CultureInfo.InvariantCulture);
        public partial float HistoryRetentionPeriodHours => Field("historyRetentionPeriodHours");
        public partial int TickInterval => (int)Field("tickInterval");
        public partial float RecoveryPerDay => Field("recoveryPerDay");
        public partial float InitializationSeverityThreshold => Field("initializationSeverityThreshold");
        public partial float RecoveryPerDayWhileShocked => Field("recoveryPerDayWhileShocked");
        public partial Verse.SimpleCurve? ShockAcidosisPerDayCurve => s_curve;
    }
}

namespace MoreInjuries.HealthConditions.Secondary.Handlers.Modifiers
{
    public partial class HediffModifier_TendQuality
    {
        public SecondaryHediffModifier TestAtFullQuality = null!;
        public partial SecondaryHediffModifier AtFullQuality => TestAtFullQuality;
    }
}

namespace MoreInjuries.HealthConditions.Secondary
{
    public sealed class HediffComp_CausedBy : Verse.HediffComp
    {
        public readonly HashSet<string> Causes = [];
        public void AddCause(Verse.Hediff hediff) => Causes.Add(hediff.Label);
        public void AddCause(string label, string? labelBase = null) => Causes.Add(label);
    }
}

namespace MoreInjuries.HealthConditions.Secondary.Linked
{
    public sealed class LinkedSeverityProperties_ModExtension
    {
        public Dictionary<Verse.HediffDef, HediffCompHandler_LinkedSeverity> LinkedSeverityHandlers = [];
    }

    public sealed class HediffCompHandler_LinkedSeverity(Func<Verse.Hediff, float> evaluate)
    {
        public float Evaluate(Verse.Hediff hediff) => evaluate(hediff);
    }
}

namespace MoreInjuries.Extensions
{
    public static class AcidosisExtensions
    {
        public static T? GetCachedModExtension<T>(this Verse.HediffDef def) where T : class => def.Extension as T;
    }
}

namespace MoreInjuries.Debug
{
    public static class DebugAssert
    {
        public static void IsNotNull([System.Diagnostics.CodeAnalysis.NotNull] object? value)
        {
            if (value is null) { throw new InvalidOperationException(); }
        }
    }
}

namespace MoreInjuries.Roslyn.Future.ThrowHelpers
{
    public static class Throw
    {
        public static class ArgumentOutOfRangeException
        {
            public static void IfNegativeOrZero(int value) { if (value <= 0) { throw new System.ArgumentOutOfRangeException(); } }
            public static void IfNegative(int value) { if (value < 0) { throw new System.ArgumentOutOfRangeException(); } }
            public static void IfZero(int value) { if (value == 0) { throw new System.ArgumentOutOfRangeException(); } }
        }
        public static class InvalidOperationException
        {
            public static void IfNull(object owner, [System.Diagnostics.CodeAnalysis.NotNull] object? value)
            {
                if (value is null) { throw new System.InvalidOperationException(); }
            }
        }
        public static class ArgumentNullException
        {
            public static void IfNull([System.Diagnostics.CodeAnalysis.NotNull] object? value)
            {
                if (value is null) { throw new System.ArgumentNullException(); }
            }
        }
    }
}

namespace MoreInjuries.AI.Jobs.Outcomes.Conditions.Operators
{
    public interface IRuntimeState { float ResolveRequired(string name); }
    public abstract class FloatOperator
    {
        public abstract float Evaluate(Verse.Pawn doctor, Verse.Pawn patient, Verse.Thing? device, IRuntimeState? runtimeState);
    }
}

namespace MoreInjuries.AI.Jobs.Outcomes.Conditions.Operators.Nullary
{
    public sealed class FloatOperator_HediffSeverity(Verse.HediffDef def) : Operators.FloatOperator
    {
        public override float Evaluate(Verse.Pawn doctor, Verse.Pawn patient, Verse.Thing? device, Operators.IRuntimeState? runtimeState)
            => patient.health.hediffSet.GetFirstHediffOfDef(def)?.Severity ?? 0f;
    }
}
