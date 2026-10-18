// These stand-ins let the actual production scheduler run without loading Unity or game implementation assemblies.
// They model only the API used by the two linked source files; they are not an in-game integration test.
using MoreInjuries.HealthConditions.Secondary.Handlers;

namespace Verse
{
    public class HediffCompProperties<T> where T : HediffComp;

    public class HediffComp
    {
        public object props = null!;
        public Hediff parent = null!;
        public Pawn Pawn => parent.pawn;
        public virtual void CompPostPostAdd(DamageInfo? damage) { }
        public virtual void CompPostTick(ref float severityAdjustment) { }
        public virtual void CompTended(float quality, float maxQuality, int batchPosition = 0) { }
        public virtual void CompExposeData() { }
    }

    public partial class Hediff
    {
        public Pawn pawn = null!;
        public float Severity;
    }

    public sealed partial class Pawn : Thing
    {
        public bool Dead;
        public int Tick;
        public int HashOffset;
        public bool IsHashIntervalTick(int interval) => (Tick + HashOffset) % interval == 0;
    }

    public readonly struct DamageInfo
    {
        public DamageDef Def { get; init; }
        public float Amount { get; init; }
    }

    public sealed class SimpleCurve(Func<float, float> evaluate)
    {
        public float Evaluate(float severity) => evaluate(severity);
    }

    public static class Rand
    {
        public static Func<float, bool> ChanceHandler = null!;
        public static bool Chance(float chance) => ChanceHandler?.Invoke(chance) ?? chance > 0.5f;
        public static bool MTBEventOccurs(float mtb, float units, float interval) => Chance(1f - MathF.Exp(-interval / (mtb * units)));
        public static float RangeFraction = 0.5f;
        public static float Range(float minimum, float maximum) => minimum + (maximum - minimum) * RangeFraction;
    }
}

namespace MoreInjuries.Roslyn.SourceGen.XmlBinding.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class XmlBindableAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class XmlBindingAttribute<T>(string name, T defaultValue = default!) : Attribute;

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class XmlBindingAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
        public string? DefaultValueFrom { get; set; }
    }
}

namespace MoreInjuries.HealthConditions.Secondary.Handlers
{
    public interface IHediffCompHandler;
    public interface IHediffCompHandler<T>
    {
        void Handle(T comp);
    }

    public interface IHediffComp_SecondaryCondition_TickHandler : IHediffCompHandler<HediffComp_SecondaryCondition>
    {
        int TickInterval { get; }
    }

    public interface IHediffComp_SecondaryCondition_PostMakeHandler : IHediffCompHandler<HediffComp_SecondaryCondition>;
}

namespace MoreInjuries.HealthConditions.Secondary.Handlers.Modifiers
{
    public abstract class SecondaryHediffModifier
    {
        protected static float Unchanged => 1f;
        protected static float Disallow => 0f;
        public abstract float GetModifier(Verse.Hediff hediff, IHediffCompHandler compHandler);
    }
}

namespace MoreInjuries.HealthConditions.Secondary
{
    public partial class HediffCompProperties_SecondaryCondition
    {
        // Stand in for XML-bound properties emitted by the existing source generator.
        public IReadOnlyList<IHediffCompHandler<HediffComp_SecondaryCondition>> TestHandlers = [];
        public Verse.SimpleCurve? TestSeverityCurve;
        public partial IReadOnlyList<IHediffCompHandler<HediffComp_SecondaryCondition>> Handlers => TestHandlers;
        public partial Verse.SimpleCurve? SeverityCurve => TestSeverityCurve;
    }
}
