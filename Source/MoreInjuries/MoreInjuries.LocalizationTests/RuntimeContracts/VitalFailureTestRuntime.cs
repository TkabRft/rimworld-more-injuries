using System.Globalization;
using System.Xml.Linq;
using MoreInjuries.HealthConditions.VitalFailure;
using MoreInjuries.Patches;
using MoreInjuries.Defs.WellKnown;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch(Type type, string method) : Attribute;
}

namespace Verse
{
    public class Def { public string defName = ""; public virtual void ResolveReferences() { } public virtual IEnumerable<string> ConfigErrors() => []; }
    public class DefModExtension;
    public class CompProperties { public Type compClass = null!; }
    public class ThingComp
    {
        public object parent = null!;
        public virtual void PostExposeData() { }
    }
    public partial class HediffDef : Def
    {
        public float lethalSeverity = -1f;
        public float maxSeverity = float.MaxValue;
        public T? GetModExtension<T>() where T : class => Extension as T;
    }
    public partial class Hediff
    {
        public int CurStageIndex => def == KnownHediffDefOf.CardiacArrest && Severity >= .65f ? 1 : 0;
        public BodyPartRecord? Part;
        public virtual bool CauseDeathNow()
        {
            bool result = def.lethalSeverity > 0f && Severity >= def.lethalSeverity;
            Patch_VitalFailure_HediffDeath.Postfix(this, ref result);
            return result;
        }
    }
    public class Hediff_Injury : Hediff
    {
        public void Heal(float amount)
        {
            if (Patch_VitalFailure_Healing.Prefix(this, amount)) { Severity = Math.Max(0, Severity - amount); }
        }
    }
    public class HediffComp_SeverityPerDay : HediffComp
    {
        public virtual float SeverityChangePerDay() => ((HediffCompProperties_SeverityPerDay)props).severityPerDay;
    }
    public sealed partial class Pawn
    {
        public bool IsShambler;
        public VitalFailureComp? Vital;
        public T? TryGetComp<T>() where T : class => Vital as T;
        public bool IsHashIntervalTick(int interval, int delta) => (Tick + HashOffset) % interval < delta;
    }
    public sealed partial class RaceProperties { public BodyDef body = new(); }
    public sealed class BodyDef { public List<BodyPartRecord> AllParts = []; }
    public sealed class BodyPartTagDef;
    public sealed class BodyPartDef { public List<BodyPartTagDef> tags = []; public float MaxHealth = 10; public float GetMaxHealth(Pawn pawn) => MaxHealth; }
    public sealed class BodyPartRecord
    {
        public BodyPartDef def = new();
        public bool Missing;
        public float Efficiency = 1f;
        public float Health = 10f;
    }
    public sealed partial class HediffSet
    {
        public Pawn pawn = null!;
        public bool HasPreventsDeath;
        public void DirtyCache() { }
        public readonly Dictionary<PawnCapacityDef, float> Capacities = [];
        public int CapacityReads;
        public bool PartIsMissing(BodyPartRecord part) => part.Missing;
        public float GetPartHealth(BodyPartRecord part) => part.Health;
    }
    public static class PawnCapacityUtility
    {
        public static float CalculateCapacityLevel(HediffSet set, PawnCapacityDef capacity)
        {
            float value = capacity.Worker.CalculateCapacityLevel(set), factor = 1f, maximum = float.MaxValue;
            foreach (Hediff hediff in set.hediffs)
            {
                HediffStage? stage = hediff.def.stages?.LastOrDefault(stage => stage.minSeverity <= hediff.Severity);
                if (stage?.capMods is not { } modifiers) { continue; }
                foreach (PawnCapacityModifier modifier in modifiers)
                {
                    if (modifier.capacity != capacity) { continue; }
                    value += modifier.offset; factor *= modifier.postFactor; maximum = Math.Min(maximum, modifier.setMax);
                }
            }
            return Math.Max(0f, Math.Min(maximum, value * factor));
        }
        public static float CalculatePartEfficiency(HediffSet set, BodyPartRecord part) => part.Missing ? 0f : part.Efficiency;
    }
    public sealed class PawnCapacityDef
    {
        public bool lethalFlesh;
        public PawnCapacityWorker Worker;
        public PawnCapacityDef() { Worker = new(this); }
    }
    public class PawnCapacityWorker(PawnCapacityDef def)
    {
        public bool Applicable = true;
        public virtual bool CanHaveCapacity(BodyDef body) => Applicable;
        public virtual float CalculateCapacityLevel(HediffSet set)
        {
            set.CapacityReads++;
            float result = set.Capacities.GetValueOrDefault(def, 1f);
            if (def == RimWorld.PawnCapacityDefOf.BloodPumping) { Patch_VitalFailure_Pumping.Postfix(set, ref result); }
            return result;
        }
    }
    public sealed class PawnCapacityWorker_BloodPumping : PawnCapacityWorker
    {
        public PawnCapacityWorker_BloodPumping() : base(RimWorld.PawnCapacityDefOf.BloodPumping) { }
    }
    public sealed partial class HealthTracker
    {
        public PawnCapacitiesHandler capacities = new();
        public Action? OnChanged;
        public Hediff AddHediff(HediffDef def, BodyPartRecord? part = null)
        {
            Hediff hediff = def == KnownHediffDefOf.BrainDamage_Hypoxia || def == KnownHediffDefOf.OrganHypoxia
                ? new Hediff_Injury() : new Hediff();
            hediff.pawn = hediffSet.pawn; hediff.def = def; hediff.Part = part; hediff.Severity = 0.001f;
            if (def == KnownHediffDefOf.CardiacArrest)
            {
                hediff.TestComps.Add(new HediffComp_VitalArrest { parent = hediff, props = new HediffCompProperties_VitalArrest { severityPerDay = 8f } });
            }
            hediffSet.hediffs.Add(hediff);
            if (def == KnownHediffDefOf.CardiacArrest)
            {
                var comp = new MoreInjuries.HealthConditions.CardiacArrest.HediffComp_CardiacArrest { parent = hediff };
                hediff.TestComps.Add(comp);
                comp.CompPostPostAdd(null);
            }
            OnChanged?.Invoke();
            return hediff;
        }
    }
    public sealed class Pawn_HealthTracker
    {
        public void Notify_Resurrected() { }
        public void CheckForStateChange() { }
        public PawnCapacityDef? ShouldBeDeadFromRequiredCapacity() => null;
        public void HealthTickInterval(int delta) { }
    }
    public sealed class PawnCapacitiesHandler
    {
        public readonly HashSet<PawnCapacityDef> Incapable = [];
        public void Notify_CapacityLevelsDirty() { }
        public bool CapableOf(PawnCapacityDef def) => !Incapable.Contains(def);
    }
    public static class DefDatabase<T>
    {
        public static readonly List<T> AllDefsListForReading = [];
        public static T? GetNamedSilentFail(string name) => AllDefsListForReading.OfType<Def>().FirstOrDefault(d => d.defName == name) is T value ? value : default;
    }
}

namespace RimWorld
{
    public static class PawnCapacityDefOf
    {
        public static readonly Verse.PawnCapacityDef Consciousness = new();
        public static readonly Verse.PawnCapacityDef Breathing = new();
        public static readonly Verse.PawnCapacityDef BloodPumping = new();
        public static readonly Verse.PawnCapacityDef BloodFiltration = new();
    }
    public static class BodyPartTagDefOf
    {
        public static readonly Verse.BodyPartTagDef ConsciousnessSource = new();
        public static readonly Verse.BodyPartTagDef BloodPumpingSource = new();
        public static readonly Verse.BodyPartTagDef BloodFiltrationLiver = new();
        public static readonly Verse.BodyPartTagDef BloodFiltrationKidney = new();
    }
}

namespace MoreInjuries.HealthConditions.VitalFailure
{
    public partial class VitalFailureConfigDef
    {
        private static readonly XElement s_xml = XDocument.Load(Path.Combine(LocalizationTests.RuntimeContracts.RuntimeDefinitionTests.ModRoot,
            "Defs", "Internal", "VitalFailureConfig.xml")).Root!.Elements().Single();
        private static readonly Verse.SimpleCurve s_ShockDeprivationCurve = LocalizationTests.RuntimeContracts.RuntimeDefCurves.Curve(s_xml.Element("shockDeprivationCurve")!);
        public partial Verse.SimpleCurve ShockDeprivationCurve => s_ShockDeprivationCurve;
        private static readonly Verse.SimpleCurve s_DilutionDeprivationCurve = LocalizationTests.RuntimeContracts.RuntimeDefCurves.Curve(s_xml.Element("dilutionDeprivationCurve")!);
        public partial Verse.SimpleCurve DilutionDeprivationCurve => s_DilutionDeprivationCurve;
        private static readonly Verse.SimpleCurve s_HypothermiaProtectionCurve = LocalizationTests.RuntimeContracts.RuntimeDefCurves.Curve(s_xml.Element("hypothermiaProtectionCurve")!);
        public partial Verse.SimpleCurve HypothermiaProtectionCurve => s_HypothermiaProtectionCurve;
        public partial float CerebralSeverityPerDay => float.Parse(s_xml.Element("cerebralSeverityPerDay")!.Value, CultureInfo.InvariantCulture);
        public partial float TerminalCerebralSeverity => float.Parse(s_xml.Element("terminalCerebralSeverity")!.Value, CultureInfo.InvariantCulture);
    }
    public partial class VitalFailureProperties
    {
        internal float Rate;
        public partial float SeverityPerDay => Rate;
    }
}
