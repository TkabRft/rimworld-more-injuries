// UI/Verse stand-ins for enumerating the production drug info-card method.
// The severity setter deliberately models the native pawn access on a stage change.
using RimWorld;

namespace Verse
{
    public partial class HediffDef
    {
        public List<HediffStage>? stages;
        public object? chemicalNeed;
        public List<HediffCompProperties>? comps;
        public virtual IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest request)
        {
            yield return new StatDrawEntry(StatCategoryDefOf.Drug, "Base", "base value", "base entry", 1);
        }
    }

    public class HediffWithComps
    {
        protected float severityInt;
        public HediffDef def = null!;
        public Pawn? pawn;
        public float Severity
        {
            get => severityInt;
            set
            {
                int previousStage = Stage;
                severityInt = value;
                if (Stage != previousStage && pawn is null)
                {
                    throw new InvalidOperationException("Native Hediff.Severity dereferences pawn.health on stage changes.");
                }
            }
        }
        private int Stage => def.stages?.Count(stage => severityInt >= stage.minSeverity) ?? 0;
    }

    public sealed class HediffStage
    {
        public string? label;
        public float minSeverity;
        public float regeneration;
        public IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            yield return new StatDrawEntry(StatCategoryDefOf.Drug, "Stage effect", "effect", "effect", 0);
        }
    }

    public class HediffCompProperties { public Type compClass = null!; }
    public class HediffCompProperties_SeverityPerDay : HediffCompProperties
    {
        public float severityPerDay;
    }

    public sealed class ChemicalDef
    {
        public string LabelCap => "Test chemical";
    }

    public sealed class HediffCompProperties_DrugEffectFactor : HediffCompProperties
    {
        public ChemicalDef chemical = new();
        public List<float> SampledSeverities = [];
    }

    public sealed class HediffComp_DrugEffectFactor
    {
        public HediffWithComps parent = null!;
        public HediffCompProperties_DrugEffectFactor props = null!;
        public void CompModifyChemicalEffect(ChemicalDef chemical, ref float effect)
        {
            Assert.IsNull(parent.pawn, "A display preview must never attach a real pawn.");
            props.SampledSeverities.Add(parent.Severity);
            effect *= 1f - parent.Severity;
        }
    }

    public static class DrugInfoTextExtensions
    {
        public static bool NullOrEmpty(this string? value) => string.IsNullOrEmpty(value);
        public static string CapitalizeFirst(this string value) => value;
        public static string Translate(this string value, params object[] arguments) => value.StartsWith("MI_Prognosis", StringComparison.Ordinal)
            ? PrognosisTestText.Translate(value, arguments) : value + " " + string.Join(" ", arguments);
        public static string ToStringPercent(this float value) => value.ToString("P0", System.Globalization.CultureInfo.InvariantCulture);
    }
}

namespace RimWorld
{
    public readonly struct StatRequest;
    public sealed class StatDrawEntry(object category, string label, string value, string description, int priority)
    {
        public string LabelCap => label;
        public string ValueString => value;
    }
    public static class StatCategoryDefOf
    {
        public static readonly object Drug = new();
    }
}
