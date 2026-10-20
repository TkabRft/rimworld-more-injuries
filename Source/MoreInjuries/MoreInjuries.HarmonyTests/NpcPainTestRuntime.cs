// Headless boundaries only. Production assessment/projection/selection and the
// real Harmony ingestion detour are linked into this assembly. Native worker
// formulas, ingestion RNG and game travel still require live acceptance.
using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;

namespace Verse
{
    public sealed partial class ThingDef
    {
        public string defName = "fixture";
        public float BaseMarketValue = 1f;
        public IngestibleProperties ingestible = new();
        public CompProperties_Drug Drug = new();
    }
    public sealed class IngestibleProperties
    { public List<IngestionOutcomeDoer_GiveHediff> outcomeDoers = []; public DrugCategory drugCategory = DrugCategory.Hard; }
    public partial class Thing
    {
        public object? ParentHolder;
        public bool IngestibleNow = true, Forbidden;
        private IntVec3 _positionHeld;
        public IntVec3 PositionHeld { get => ParentHolder is CarryTracker carry ? carry.Pawn.Position
            : ParentHolder is Pawn_InventoryTracker inventory ? inventory.pawn.Position
            : this is Pawn pawn ? pawn.Position : _positionHeld; set => _positionHeld = value; }
        public Map Map = new();
        public IntVec3 Position => PositionHeld;
        public string ThingID = "fixture";
        public bool IsForbidden(Pawn pawn) => Forbidden;
    }
    public readonly struct IntVec3
    {
        public IntVec3(float value) { Value = value; x = (int)value; z = 0; }
        public IntVec3(int x, int y, int z) { Value = x; this.x = x; this.z = z; }
        public readonly int x, z;
        public static IntVec3 Invalid => new(-10000, 0, -10000);
        public bool IsValid => x != -10000;
        public bool InBounds(Map map) => x >= 0 && z >= 0 && x < map.Size.x && z < map.Size.z;
        public float DistanceToSquared(IntVec3 other) => (Value - other.Value) * (Value - other.Value) + (z - other.z) * (z - other.z);
        public bool InHorDistOf(IntVec3 other, float radius) => DistanceToSquared(other) <= radius * radius;
        public float Value { get; }
        public override string ToString() => Value + "," + z;
        public static bool operator ==(IntVec3 a, IntVec3 b) => a.Value == b.Value && a.z == b.z;
        public static bool operator !=(IntVec3 a, IntVec3 b) => !(a == b);
        public override bool Equals(object? obj) => obj is IntVec3 other && this == other;
        public override int GetHashCode() => Value.GetHashCode() ^ z;
    }
    public enum ThingPlaceMode { Near, Direct }
    public sealed partial class Pawn
    {
        public Map MapHeld => ParentHolder is CarryTracker carry ? carry.Pawn.Map : Map;
        public Health health;
        public Pawn()
        { health = new(this); inventory.pawn = this; inventory.innerContainer.Owner = inventory; carryTracker.innerContainer.Owner = carryTracker; carryTracker.Pawn = this; jobs.pawn = this; }
        public float BodySize = 1f, RawPain = .5f, PainFactor = 1f, PainThreshold = .8f, ChemicalEffect = 1f;
        public bool IsColonistPlayerControlled, NativeSafety;
        public bool DestinationValid = true, RouteValid = true, CarrierCapable = true;
        public int TicksPerMoveCardinal = 60;
        public int BleedingDeathTicks = int.MaxValue;
        public float EmergencyUrgency;
        public JobDef CurJobDef => CurJob.def;
        public bool Downed, Dead, IsMutant, IsPrisoner, Teetotaler, AllowDrug = true, DoctorCapable = true, Hostile;
        public int CareRelation;
        public new IntVec3 Position;
        private static readonly object s_faction = new();
        public object Faction = s_faction;
        public bool Reachable = true;
        public Pawn_MindState mindState = new();
        public PawnPathFollower pather = new();
        public bool Exited;
        public void ExitMap(bool allowed, Rot4 rotation) { Exited = true; Spawned = false; }
        public bool CanReach(Thing thing, PathEndMode end, Danger danger) => Reachable;
        public RaceProperties RaceProps = new();
        public MutantTracker mutant = new();
        public PlayerSettings? playerSettings;
        public Pawn_GeneTracker? genes;
        public Pawn_DrugPolicyTracker? drugs;
        public bool IsTeetotaler() => Teetotaler;
        public bool HostileTo(Pawn other) => other != this && (Hostile || other.Hostile);
        public float GetStatValue(object stat) => PainThreshold;
        public bool CanReachImmediate(Thing target, PathEndMode end) => target is Pawn other
            ? other.Position.DistanceToSquared(Position) <= 2.25f : target.PositionHeld.DistanceToSquared(Position) <= 2.25f;
        public readonly HashSet<HistoryEventDef> RefusedEvents = [];
        public readonly List<Thing> Supplies = [];
    }
    public sealed class RaceProperties { public bool Humanlike = true, IsFlesh = true; }
    public sealed class MutantTracker { public MutantDef Def = new(); }
    public sealed class MutantDef { public bool entitledToMedicalCare = true; }
    public sealed class PlayerSettings { public MedicalCareCategory medCare = MedicalCareCategory.Best; }
    public sealed class Health
    {
        public Health(Pawn pawn) { hediffSet = new(pawn); capacities = new(hediffSet); }
        public HediffSet hediffSet;
        public CapacityTracker capacities;
        public bool forceDowned;
        public bool NeedsTend = true;
        public bool HasHediffsNeedingTend() => NeedsTend;
        public bool InPainShock => hediffSet.PainTotal >= hediffSet.pawn.PainThreshold;
    }
    public class Hediff
    {
        public string GetUniqueLoadID() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this).ToString();
        public bool Tended;
        public bool IsTended() => Tended;
        public bool TendableNow() => !Tended;
        public Pawn pawn = null!;
        public HediffDef def = null!;
        protected float severityInt;
        public float Severity { get => severityInt; set => severityInt = value; }
        public HediffStage? CurStage => def.stages.LastOrDefault(stage => stage.minSeverity <= Severity);
    }
    public sealed class Hediff_Injury : Hediff { public bool Healable = true; public bool CanHealNaturally() => Healable && Severity > 0f; }
    public static class DefDatabase<T> where T : class
    {
        public static readonly Dictionary<string, T> Definitions = [];
        public static T? GetNamedSilentFail(string name) => Definitions.TryGetValue(name, out T? value) ? value : null;
    }
    public sealed partial class HediffDef
    { public string defName = "fixture high"; public List<HediffStage> stages = []; public float initialSeverity = .01f, minSeverity, maxSeverity = 1f, lethalSeverity = -1f; }
    public sealed class HediffStage
    { public bool lifeThreatening; public float minSeverity, painFactor = 1f, painOffset, naturalHealingFactor = -1f, totalBleedFactor = -1f; public List<PawnCapacityModifier> capMods = []; }
    public sealed class PawnCapacityModifier
    { public PawnCapacityDef capacity = null!; public float offset, setMax = float.MaxValue; }
    public sealed class HediffSet(Pawn pawn)
    {
        public Pawn pawn = pawn;
        public List<Hediff> hediffs = [];
        public float BleedRateTotal;
        public float PainTotal => Math.Min(1f, Math.Max(0f, (pawn.RawPain + hediffs.Sum(hediff => hediff.CurStage?.painOffset ?? 0f))
            * hediffs.Aggregate(pawn.PainFactor, (factor, hediff) => factor * (hediff.CurStage?.painFactor ?? 1f))));
        public Hediff? GetFirstHediffOfDef(HediffDef def) => hediffs.FirstOrDefault(hediff => hediff.def == def);
    }
    public sealed class PawnCapacityDef(string name, float minimum)
    { public string defName = name; public float minForCapable = minimum; }
    public sealed class CapacityTracker(HediffSet set)
    {
        public readonly Dictionary<PawnCapacityDef, float> Base = [];
        public float GetLevel(PawnCapacityDef capacity) => PawnCapacityUtility.CalculateCapacityLevel(set, capacity);
        public bool CapableOf(PawnCapacityDef capacity) => GetLevel(capacity) > capacity.minForCapable;
    }
    public static class PawnCapacityUtility
    {
        public static float CalculateCapacityLevel(HediffSet set, PawnCapacityDef capacity)
        {
            float value = set.pawn.health.capacities.Base.TryGetValue(capacity, out float baseline) ? baseline : 1f;
            float max = float.MaxValue;
            foreach (PawnCapacityModifier modifier in set.hediffs.SelectMany(hediff => hediff.CurStage?.capMods ?? []))
            { if (modifier.capacity == capacity) { value += modifier.offset; max = Math.Min(max, modifier.setMax); } }
            return Math.Max(0f, Math.Min(max, value));
        }
    }
    public sealed class GeneDef { public ChemicalDef? chemical; public float overdoseChanceFactor = 1f; }
    public sealed class Gene { public bool Active = true; public GeneDef def = new(); }
    public static class ModsConfig { public static bool BiotechActive, IdeologyActive, DetActive; public static bool IsActive(string package) => DetActive && package == "det.battlemeds"; }
    public static class Prefs { public static bool DevMode; }
    public enum Danger { Some, Deadly }
    public static partial class Log { public static readonly List<string> Messages = []; public static void Message(string text) => Messages.Add(text); }
    public readonly struct NamedArgument(object value) { public object Value => value; }
    public static class NamedArgumentExtensions { public static NamedArgument Named(this Pawn pawn, string key) => new(pawn); }
    public static partial class Find { public static readonly HistoryEventsManager HistoryEventsManager = new(); }
}
namespace Verse.AI
{
    public enum PathEndMode { Touch, ClosestTouch, OnCell }
    public static class JobMaker
    {
        public static Job MakeJob(JobDef def) => new() { def = def };
        public static Job MakeJob(JobDef def, Thing a, IntVec3 b) => new() { def = def, targetA = new(a), targetB = new(b) };
        public static Job MakeJob(JobDef def, Thing a, Thing? b = null) => new() { def = def, targetA = new(a), targetB = new(b!) };
    }
    public abstract class JobDriver
    {
        public Pawn pawn = null!;
        public Job job => pawn.CurJob;
        public readonly List<Func<bool>> Failures = [];
        private readonly List<Action<JobCondition>> _finish = [];
        public virtual bool TryMakePreToilReservations(bool errorOnFailed) => true;
        protected virtual IEnumerable<Toil> MakeNewToils() => [];
        protected void AddEndCondition(Func<JobCondition> condition) { }
        protected void AddFinishAction(Action<JobCondition> action) => _finish.Add(action);
        protected void EndJobWith(JobCondition condition) => pawn.jobs.EndCurrentJob(condition);
        protected void ReadyForNextToil() { }
        public int ResumeIndex, StepLimit = 40;
        public Action<int, Toil>? BeforeToil;
        public List<Toil> BuildToils() => MakeNewToils().ToList();
        public void Run()
        {
            Job attemptJob = job;
            try
            {
                List<Toil> toils = BuildToils();
                foreach (Toil toil in toils) { toil.actor = pawn; }
                for (int index = ResumeIndex, steps = 0; index < toils.Count && steps < StepLimit; steps++)
                {
                    Toil toil = toils[index];
                    BeforeToil?.Invoke(index, toil);
                    if (pawn.jobs.Ended is not null || pawn.CurJob != attemptJob) { break; }
                    if (Failures.Any(fail => fail()) || toil.Failures.Any(fail => fail())) { EndJobWith(JobCondition.Incompletable); break; }
                    toil.initAction?.Invoke();
                    foreach (Action finish in toil.FinishActions) { finish(); }
                    if (pawn.jobs.Ended is not null) { break; }
                    index = toil.JumpTarget is not null && toil.JumpCondition!() ? toils.IndexOf(toil.JumpTarget) : index + 1;
                }
            }
            finally
            {
                foreach (Action<JobCondition> finish in _finish) { finish(pawn.jobs.Ended ?? JobCondition.Succeeded); }
                pawn.Map.reservationManager.ReleaseClaimedBy(pawn, attemptJob);
            }
        }
    }
    public static class PainToilExtensions
    {
        public static void FailOn(this JobDriver driver, Func<bool> condition) => driver.Failures.Add(condition);
        public static Toil FailOn(this Toil toil, Func<bool> condition) { toil.Failures.Add(condition); return toil; }
        public static void FailOnDespawnedNullOrForbidden(this JobDriver driver, TargetIndex target) => driver.FailOn(() => driver.job.GetTarget(target).Thing is not { Spawned: true, Destroyed: false, Forbidden: false });
        public static Toil WithProgressBarToilDelay(this Toil toil, TargetIndex target) => toil;
        public static Toil PlaySustainerOrSound(this Toil toil, object sound) => toil;
        public static Toil FailOnCannotTouch(this Toil toil, TargetIndex target, PathEndMode end) => toil.FailOn(() => !toil.actor.CanReachImmediate(toil.actor.CurJob.GetTarget(target).Thing, end));
        public static Toil FailOnDespawnedNullOrForbidden(this Toil toil, TargetIndex target)
            => toil.FailOn(() => toil.actor.CurJob.GetTarget(target).Thing is not { Spawned: true, Destroyed: false, Forbidden: false });
    }
    public static class Toils_General { public static Toil Wait(int ticks) => new(); public static Toil Do(Action action) => new() { initAction = action }; }
    public static class Toils_Jump
    {
        public static Toil Jump(Toil target) => JumpIf(target, () => true);
        public static Toil JumpIf(Toil target, Func<bool> condition) => new() { JumpTarget = target, JumpCondition = condition };
    }
    public static class Toils_Goto
    {
        public static Toil GotoThing(TargetIndex target, PathEndMode end)
        { Toil toil = new(); toil.initAction = () => toil.actor.Position = toil.actor.CurJob.GetTarget(target).Thing.PositionHeld; return toil; }
        public static Toil GotoCell(TargetIndex target, PathEndMode end)
        { Toil toil = new(); toil.initAction = () => toil.actor.Position = toil.actor.CurJob.GetTarget(target).Cell; return toil; }
    }
    public static class Toils_Haul
    {
        public static Toil StartCarryThing(TargetIndex target)
        {
            Toil toil = new();
            toil.initAction = () =>
            {
                Pawn actor = toil.actor;
                if (actor.carryTracker.TryStartCarry(actor.CurJob.GetTarget(target).Thing, actor.CurJob.count) != 1)
                { actor.jobs.EndCurrentJob(JobCondition.Incompletable); return; }
                actor.CurJob.SetTarget(target, actor.carryTracker.CarriedThing);
                actor.CurJob.count--;
            };
            return toil;
        }
    }
}
namespace RimWorld
{
    public enum TechLevel { Neolithic, Medieval, Industrial, Spacer, Ultra }
    public class JobDriver_Ingest : JobDriver;
    public static class Toils_Ingest
    {
        public static Toil ChewIngestible(Pawn actor, float multiplier, TargetIndex source, TargetIndex table) => new() { Tag = "native chewing boundary" };
        public static Toil FinalizeIngest(Pawn actor, TargetIndex source)
            => Toils_General.Do(() =>
            {
                Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(1, actor.CurJob.count);
                actor.CurJob.GetTarget(source).Thing.Ingested(actor, 0f);
            });
    }
    public enum MedicalCareCategory { NoCare, NoMeds, HerbalOrWorse, NormalOrWorse, Best }
    public enum DrugCategory { None, Medical, Social, Hard }
    public static class StatDefOf { public static readonly object PainShockThreshold = new(), MedicalPotency = new(); }
    public static class PawnCapacityDefOf
    {
        public static readonly PawnCapacityDef Consciousness = new("Consciousness", .3f), Breathing = new("Breathing", 0f),
            BloodPumping = new("BloodPumping", 0f), Moving = new("Moving", .15f), Manipulation = new("Manipulation", .1f), Sight = new("Sight", .1f);
    }
    public sealed class IngestionOutcomeDoer_GiveHediff
    {
        public float chance = 1f;
        public HediffDef hediffDef = new(); public float severity = .5f;
        public ChemicalDef? toleranceChemical;
        // Reflection reads this XML-initialized private field in production.
        private bool divideByBodySize;
        public bool multiplyByGeneToleranceFactors;
        public void SetBodySizeDivision(bool divide) => divideByBodySize = divide;
    }
    public sealed class ChemicalDef { public HediffDef toleranceHediff = new(); }
    public sealed class CompProperties_Drug
    {
        public ChemicalDef? chemical = new(); public bool CanCauseOverdose = true;
        public FloatRange overdoseSeverityOffset = new(.18f, .35f); public float largeOverdoseChance = .01f, addictiveness = .1f;
    }
    public readonly struct FloatRange(float min, float max) { public readonly float max = max; }
    public sealed class Pawn_GeneTracker { public readonly List<Gene> GenesListForReading = []; public float AddictionFactor = 1f; public float AddictionChanceFactor(ChemicalDef? chemical) => AddictionFactor; }
    public sealed class Pawn_DrugPolicyTracker
    {
        public bool OverdosePossible = true;
        private bool CanCauseOverdose(ThingDef drug) => OverdosePossible;
    }
    public static class DrugStatsUtility
    {
        public static IngestionOutcomeDoer_GiveHediff? GetDrugHighGiver(ThingDef def) => def.ingestible.outcomeDoers.FirstOrDefault();
        public static CompProperties_Drug GetDrugComp(ThingDef def) => def.Drug;
        public static float GetAddictivenessAtTolerance(ThingDef def, float tolerance) => def.Drug.addictiveness;
    }
    public static class AddictionUtility
    {
        public static void ModifyChemicalEffectForToleranceAndBodySize(Pawn pawn, ChemicalDef? chemical, ref float effect,
            bool applyGeneToleranceFactor, bool divideByBodySize = true)
        { effect *= pawn.ChemicalEffect; if (divideByBodySize) { effect /= pawn.BodySize; } }
        public static Hediff? FindToleranceHediff(Pawn pawn, ChemicalDef chemical) => pawn.health.hediffSet.GetFirstHediffOfDef(chemical.toleranceHediff);
    }
    public static class HediffDefOf { public static readonly HediffDef DrugOverdose = new() { defName = "DrugOverdose" }; }
    public static class ThingDefOf { public static ThingDef Yayo = new(), GoJuice = new(); }
    public static class PawnUtility { public static void ForceWait(Pawn pawn, int ticks, bool maintainPosture) { } public static bool CanTakeDrug(Pawn pawn, ThingDef drug) => pawn.AllowDrug; }
    public sealed class HistoryEventDef;
    public static class HistoryEventArgsNames { public const string Doer = "Doer"; }
    public readonly struct HistoryEvent(HistoryEventDef def, NamedArgument doer)
    { public HistoryEventDef Def => def; public Pawn Actor => (Pawn)doer.Value; }
    public sealed class HistoryEventsManager
    { public readonly List<HistoryEvent> Events = []; public void RecordEvent(HistoryEvent historyEvent) => Events.Add(historyEvent); }
    public static class HistoryEventDefOf
    {
        public static readonly HistoryEventDef IngestedDrug = new(), AdministeredDrug = new(), IngestedHardDrug = new(),
            AdministeredHardDrug = new(), IngestedRecreationalDrug = new(), AdministeredRecreationalDrug = new();
    }
    public static class IdeoUtility { public static bool DoerWillingToDo(HistoryEventDef def, Pawn actor) => !actor.RefusedEvents.Contains(def); }
}
namespace MoreInjuries.HealthConditions.Drugs
{
    public abstract class JobDriver_UseMedicalDrug : JobDriver
    {
        protected Thing? DeviceUsed => job.targetB.Thing;
        protected Pawn Patient => job.targetA.Pawn;
        protected abstract ThingDef DeviceDef { get; }
        protected virtual bool RequiresTreatment(Pawn patient) => true;
        protected abstract bool ApplyDevice(Pawn doctor, Pawn patient, Thing? device);
        // Travel/waiting are native/shared boundaries; exercise the real final
        // application implementation without pretending to simulate game pathing.
        protected override IEnumerable<Toil> MakeNewToils()
        { yield return Toils_General.Do(() => ApplyDevice(pawn, Patient, DeviceUsed)); }
    }
}
namespace MoreInjuries.AI.NpcCombatMedicine
{
    internal static class NpcMedicalGeneration
    {
        internal static bool IsPerformance(ThingDef def) => def == KnownThingDefOf.Morphine || def == ThingDefOf.Yayo || def == ThingDefOf.GoJuice;
        internal static bool CanDoctor(Pawn actor) => actor.DoctorCapable;
    }
    internal static partial class NpcMedicalAi
    {
        internal static float Urgency(Pawn patient) => Math.Max(patient.EmergencyUrgency, patient.health.NeedsTend ? 50f : NpcPainCare.Priority(patient));
        internal static int Relation(Pawn actor, Pawn patient) => actor.HostileTo(patient) ? -1 : patient.CareRelation;
        internal static float Severity(Pawn patient, string name) => patient.health.hediffSet.hediffs.FirstOrDefault(hediff => hediff.def.defName == name)?.Severity ?? 0f;
        internal static Thing? FindSupply(Pawn actor, Pawn patient, ThingDef def) => NpcMedicalSupplies.Find(actor, patient, def);
    }
}
namespace UnityEngine
{
    public static partial class Mathf
    {
        public static float Abs(float value) => Math.Abs(value);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
        public static float Clamp01(float value) => Clamp(value, 0, 1);
    }
}
