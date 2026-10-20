// Native boundaries for production-linked neurological and migration behavior.
using System.Reflection;
using MoreInjuries.HealthConditions.BrainDamage;
using MoreInjuries.HealthConditions.Secondary.Handlers.HediffMakers;
using MoreInjuries.HealthConditions.VitalFailure;

namespace HarmonyLib
{
    public static partial class AccessTools
    {
        public static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!;
        public delegate ref F FieldRef<T, F>(T instance);
        public static FieldRef<T, F> FieldRefAccess<T, F>(string name) => (FieldRef<T, F>)(object)(FieldRef<Verse.HediffComp_TendDuration, int>)TendTicks;
        private static ref int TendTicks(Verse.HediffComp_TendDuration tend) => ref tend.tendTicksLeft;
    }
}
namespace Verse
{
    public static class Scribe_BodyParts { public static void Look(ref BodyPartRecord value, string key) => Scribe.Value(ref value, key, null!); }
    public class DamageDef : Def;
    public class RecipeDef : Def { public ThingFilter fixedIngredientFilter = new(); }
    public sealed class ThingFilter
    {
        private readonly HashSet<ThingDef> _allowed = [];
        public IEnumerable<ThingDef> AllowedThingDefs => _allowed;
        public bool Allows(ThingDef thing) => _allowed.Contains(thing);
        public void SetAllow(ThingDef thing, bool allowed) { if (allowed) { _allowed.Add(thing); } else { _allowed.Remove(thing); } }
        public void SetDisallowAll() => _allowed.Clear();
    }
    public sealed partial class RaceProperties { public bool Humanlike = true; }
    public sealed partial class Pawn
    {
        public bool Suspended;
        public bool IsMutant;
        public RimWorld.Pawn_MutantTracker mutant = new();
        public PlayerSettings? playerSettings;
        public string LabelShort => Name;
        public RimWorld.BillStack? BillStack;
        public Verse.AI.Job? CurJob;
        public int HashOffsetTicks() => Tick + HashOffset;
    }
    public sealed partial class HediffSet
    {
        private float _bleedRateTotal;
        public bool NativeBleeding;
        public float AggregateBleedFactor = 1f;
        public float BleedRateTotal
        {
            get => NativeBleeding ? pawn.health.CanBleed ? hediffs.Sum(hediff => hediff.BleedRate) * AggregateBleedFactor / pawn.HealthScale : 0f : _bleedRateTotal;
            set => _bleedRateTotal = value;
        }
    }
    public sealed class PlayerSettings { public RimWorld.MedicalCareCategory medCare = RimWorld.MedicalCareCategory.Best; }
    public sealed partial class JobTracker { public int Ended; public void EndCurrentJob(Verse.AI.JobCondition condition) => Ended++; }
    public partial class Hediff
    {
        public int ageTicks;
        public int loadID;
        public HediffStage? CurStage => def.stages?.FirstOrDefault();
        public string GetUniqueLoadID() => "Hediff_" + loadID;
    }
    public static class HediffMaker
    {
        public static Hediff MakeHediff(HediffDef def, Pawn pawn, BodyPartRecord? part = null) => new() { def = def, pawn = pawn, Part = part };
    }
    public sealed partial class HealthTracker
    {
        public HediffDef? ThrowOnAdd;
        public void AddHediff(Hediff hediff)
        {
            if (hediff.def == ThrowOnAdd) { throw new InvalidOperationException("Controlled native add failure"); }
            hediffSet.hediffs.Add(hediff);
            if (hediff.def == MoreInjuries.Defs.WellKnown.KnownHediffDefOf.CardiacArrest)
            { hediff.TestComps.Add(new HediffComp_VitalArrest { parent = hediff, props = new HediffCompProperties_VitalArrest { severityPerDay = 8 } }); }
        }
    }
    public static class Scribe_Defs { public static void Look<T>(ref T? value, string label) where T : class => Scribe.Value(ref value, label, null); }
    public class Game;
    public class GameComponent
    {
        public virtual void ExposeData() { }
        public virtual void LoadedGame() { }
    }
    public static partial class Find { public static LetterStack LetterStack = new(); }
    public sealed class LetterStack { public List<string> Texts = []; public void ReceiveLetter(string title, string text, object type) => Texts.Add(text); }
    public static class Log
    {
        public static int Warnings;
        public static readonly List<string> Messages = [];
        public static void Message(string message) => Messages.Add(message);
        public static void Error(string error) { }
        public static void Warning(string warning) => Warnings++;
    }

}
namespace Verse.AI { public sealed partial class Job { public RimWorld.Bill? bill; } }
namespace RimWorld
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefOfAttribute : Attribute;
    [AttributeUsage(AttributeTargets.Field)] public sealed class MayRequireAttribute(string package) : Attribute;
    public static class DefOfHelper { public static void EnsureInitializedInCtor(Type type) { } }
    public enum MedicalCareCategory { NoCare, NoMeds, HerbalOrWorse, NormalOrWorse, Best }
    public sealed class MutantDef
    {
        public bool breathesAir = true, entitledToMedicalCare = true, preventsMentalBreaks, canUseDrugs = true;
        public bool removeChronicIllnesses, removePermanentInjuries;
        public List<Verse.ThingDef>? drugWhitelist;
    }
    public static class MutantUtility
    {
        public static bool CanUseDrug(Verse.Pawn pawn, Verse.ThingDef drug) => pawn.mutant.Def.canUseDrugs
            && (pawn.mutant.Def.drugWhitelist is null || pawn.mutant.Def.drugWhitelist.Contains(drug));
    }
    public sealed partial class Pawn_GeneTracker { public void Notify_GenesChanged() { } }
    public sealed class Pawn_MutantTracker { public MutantDef Def = new(); public void Turn() { } public void Revert() { } }
    public class Recipe_Surgery
    {
        public virtual IEnumerable<Verse.BodyPartRecord> GetPartsToApplyOn(Verse.Pawn pawn, Verse.RecipeDef recipe) => [];
        public virtual bool AvailableOnNow(Verse.Thing thing, Verse.BodyPartRecord? part = null) => true;
        public virtual void ApplyOnPawn(Verse.Pawn pawn, Verse.BodyPartRecord part, Verse.Pawn billDoer, List<Verse.Thing> ingredients, Bill bill) { }
        protected bool CheckSurgeryFail(Verse.Pawn surgeon, Verse.Pawn patient, List<Verse.Thing> ingredients, Verse.BodyPartRecord part, Bill bill) => false;
    }
    public class Recipe_AdministerIngestible : Recipe_Surgery
    {
        public override void ApplyOnPawn(Verse.Pawn pawn, Verse.BodyPartRecord part, Verse.Pawn billDoer, List<Verse.Thing> ingredients, Bill bill)
        {
            foreach (Verse.Thing thing in ingredients) { thing.Ingested(pawn, 0); }
        }
    }
    public static class ThingDefOf { public static readonly Verse.ThingDef WakeUp = new(); }
    public static class LetterDefOf { public static object NeutralEvent = new(), NegativeEvent = new(); }
    public sealed class Bill { public Verse.RecipeDef recipe = new(); public Verse.ThingFilter ingredientFilter = new(); }
    public sealed class BillStack { public List<Bill> Bills = []; }
}
namespace MoreInjuries.HealthConditions.Secondary.Handlers.HediffMakers
{
    public partial class HediffMakerDef
    {
        internal Verse.HediffDef Target = null!;
        internal float Minimum = .01f, Maximum = .25f;
        public partial Verse.HediffDef HediffDef => Target;
        public partial float MinSeverity => Minimum;
        public partial float MaxSeverity => Maximum;
        public partial bool AllowDuplicate => false;
        public partial bool AllowMultiple => true;
    }
    public partial class WeightedHediffMakerDef { internal float TestWeight = 1; public partial float Weight => TestWeight; }
}
namespace MoreInjuries.HealthConditions.Secondary.Handlers
{
    public class HediffCompHandler_SecondaryCondition
    {
        public virtual void Handle(HediffComp_SecondaryCondition comp) { }
        protected bool ShouldSkip(HediffComp_SecondaryCondition comp, Verse.HediffDef def) => false;
        protected void TryApplyHediff(HediffComp_SecondaryCondition comp, HediffMakerDef maker) { }
    }
    public class HediffCompHandler_SecondaryCondition_Tick : HediffCompHandler_SecondaryCondition;
}
namespace MoreInjuries.Integrations.DeathRattle
{
    internal static class DeathRattleBridge
    {
        internal static bool Active;
        internal static bool Enforced = true;
        internal static Verse.Pawn? MigratingPawn;
        internal static readonly HashSet<Verse.Def> Owned = [];
        internal static string? Blocker;
        internal static readonly Dictionary<Verse.RecipeDef, Verse.RecipeDef?> Recipes = [];
        internal static bool Owns(Verse.Def def) => Owned.Contains(def);
        internal static void Enforce() { }
        internal static string? DependencyBlocker() => Blocker;
        internal static Verse.RecipeDef? Replacement(Verse.RecipeDef def) => Recipes.GetValueOrDefault(def);
    }
}
