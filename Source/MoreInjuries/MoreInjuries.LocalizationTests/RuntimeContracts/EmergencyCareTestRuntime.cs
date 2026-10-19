using MoreInjuries.AI.Jobs;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.HeavyBleeding;
using MoreInjuries.HealthConditions.HeavyBleeding.Overrides;
using MoreInjuries.Integrations.CombatExtended;
using MoreInjuries.LocalizationTests.RuntimeContracts;
using Verse;
using Verse.AI;

namespace HarmonyLib
{
    public static partial class AccessTools
    { public static T MethodDelegate<T>(System.Reflection.MethodInfo method) where T : Delegate => (T)method.CreateDelegate(typeof(T)); }
}
namespace CombatExtended
{
    public static class Controller { public static readonly Settings settings = new(); }
    public sealed class Settings { public float Radius = 12; public float MedicineSearchRadiusSquared => Radius * Radius; }
    public sealed class JobDriver_Stabilize;
    public static class CE_Utility
    {
        public static bool CanBeStabilized(Hediff wound) => wound is CEWoundFixture { Stabilization.Stabilized: false }
            && wound.BleedRate > 0f && !wound.Tended;
    }
}
namespace Verse
{
    public sealed class JobDef : Def { public Type driverClass = null!; }
    public partial class Thing { public bool Spawned; public object? ParentHolder; public float Value = 1; }
    public sealed partial class ThingDef { public int MedicineTier = 2; }
    public sealed class FixtureInventory : List<Thing>
    {
        public readonly Dictionary<ThingDef, Thing> Devices = [];
        public new bool Contains(Thing item) => base.Contains(item) || Devices.Values.Contains(item);
    }
    public sealed class Inventory { public readonly FixtureInventory innerContainer = []; }
    public sealed class CarryTracker { public Thing? CarriedThing; }
    public sealed partial class Pawn
    {
        public RimWorld.Pawn_GeneTracker? genes;
        public readonly List<KeyValuePair<BodyPartRecord, float>> LimbBleeding = [];
        public Inventory inventory = new(); public CarryTracker carryTracker = new();
        public Dictionary<ThingDef, Thing> Devices => inventory.innerContainer.Devices; public readonly Dictionary<ThingDef, Thing> RetrievedDevices = [];
        public bool DisabledDoctor, Reachable = true, Downed; public int TicksPerMoveCardinal = 13;
        public bool WorkTypeIsDisabled(object work) => DisabledDoctor;
        public bool CanReserve(Thing thing, int stackCount) => thing.Reservable;
    }
    public enum ThingRequestGroup { Medicine }
    public readonly struct IntVec3(int value)
    { private readonly int _value = value; public float DistanceTo(IntVec3 other) => Math.Abs(_value - other._value); public static implicit operator IntVec3(int value) => new(value); public static implicit operator int(IntVec3 value) => value._value; }
    public partial class Hediff
    {
        public bool IsCurrentlyLifeThreatening;
        public bool TestBleedingEligible;
        public bool TendableNow() => !Tended && TestBleedingEligible;
        public bool IsPermanent() => false;
        public bool IsTended() => Tended;
    }
    public partial class HediffDef { public InjuryProps? injuryProps; }
    public sealed class InjuryProps { public float bleedRate; }
    public static class MedicalStatTest
    { public static float GetStatValue(this Thing thing, object stat) => stat == RimWorld.StatDefOf.MarketValue ? thing.Value : thing is RimWorld.Medicine medicine ? medicine.Potency : 1f; }
    public sealed partial class HediffSet
    {
        public IEnumerable<Hediff> GetHediffsTendable() => hediffs.Where(wound => !wound.Tended);
        public bool TryGetHediff(HediffDef def, out Hediff? hediff) { hediff = GetFirstHediffOfDef(def); return hediff is not null; }
    }
    public sealed partial class HealthTracker
    {
        public bool HasHediffsNeedingTend() => hediffSet.hediffs.Any(hediff => hediff.TendableNow());
    }
}
namespace Verse.AI { public sealed partial class Job { public bool endAfterTendedOnce; public object[] Targets = []; public Target targetB => new(Targets.Length > 1 ? Targets[1] as Thing : null); } }
namespace RimWorld
{
    public sealed class GeneDef : Def;
    public sealed class Gene { public GeneDef def = new(); public bool Active = true; }
    public sealed partial class Pawn_GeneTracker
    {
        public readonly List<Gene> GenesListForReading = [];
        public bool HasActiveGene(GeneDef def) => GenesListForReading.Any(gene => gene.def == def && gene.Active);
    }
    public static class WorkTypeDefOf { public static readonly object Doctor = new(); }
    public static class StatDefOf { public static readonly object MedicalPotency = new(), MedicalTendSpeed = new(), MarketValue = new(); }
    public static class MedicalCareUtility
    { public static bool AllowsMedicine(this MedicalCareCategory care, ThingDef def) => care >= MedicalCareCategory.HerbalOrWorse && (care == MedicalCareCategory.Best || def.MedicineTier <= (int)care - 1); }
    public static class JobDefOf { public static readonly object TendPatient = new(); }
    public static class HealthAIUtility { public static Thing? FindBestMedicine(Pawn doctor, Pawn patient, bool onlyUseInventory) => null; }
}
namespace UnityEngine { public static partial class Mathf { public static float Sqrt(float value) => MathF.Sqrt(value); } }
namespace MoreInjuries { public sealed partial class TestSettings { public bool EnableAdrenaline; public float MinBleedRateForAutoTourniquet = 1f; } }
namespace MoreInjuries.Extensions
{
    public static class EmergencyTestExtensions
    {
        public static bool CanReachPatient(this Pawn doctor, Pawn patient) => doctor.Reachable;
        public static bool IsOnBodyPartOrChildren(this Hediff wound, BodyPartRecord part) => wound.Part == part;
    }
}
namespace MoreInjuries.Roslyn.Future.Extensions { }
namespace MoreInjuries.Defs.WellKnown
{
    public static partial class KnownHediffDefOf { public static readonly HediffDef TourniquetApplied = new(), Fracture = new(); }
    public static partial class KnownResearchProjectDefOf
    { public static readonly ResearchDef Cpr = new(), BasicAnatomy = new(), BasicFirstAid = new(), AdvancedFirstAid = new(); }
    public static partial class KnownThingDefOf
    { public static readonly ThingDef Tourniquet = new(), Bandage = new(), HemostaticAgent = new(), Epinephrine = new(), SalineBag = new(), BloodBag = new(); }
    public static partial class KnownJobDefOf
    { public static readonly JobDef Tourniquet = new(), Bandage = new(), Hemostat = new(), Epinephrine = new(), Saline = new(), Blood = new(), RemoveTourniquet = new();
      public static JobDef UseTourniquet => Tourniquet; public static JobDef UseBandage => Bandage; public static JobDef UseHemostaticAgent => Hemostat; public static JobDef UseEpinephrine => Epinephrine; }
    public static class KnownBodyPartDefOf { public static readonly BodyPartDef Neck = new(); }
}
namespace MoreInjuries.Things
{
    public static partial class MedicalDeviceHelper
    {
        internal static Thing? FindFeasibleInventoryDevice(Pawn doctor, ThingDef def)
            => FindMedicalDevice(doctor, doctor, def, fromInventoryOnly: true);
        internal static Thing? FindRetrievedDevice(Pawn doctor, Pawn patient, ThingDef def)
            => doctor.RetrievedDevices.TryGetValue(def, out Thing? item) && !item.Destroyed && !item.Forbidden && item.Reservable ? item : null;
        public static Thing? FindMedicalDevice(Pawn doctor, Pawn patient, ThingDef def, Predicate<Hediff>? predicate = null, bool fromInventoryOnly = false)
            => doctor.Devices.TryGetValue(def, out Thing? thing) && !thing.Destroyed && !thing.Forbidden && thing.Reservable
                && (predicate is null || patient.health.hediffSet.hediffs.Any(wound => predicate(wound))) ? thing : null;
        public static Thing? FindMedicalDevice(Pawn doctor, Pawn patient, ThingDef def, Func<Pawn, int> count, bool fromInventoryOnly = false)
            => count(patient) > 0 ? FindMedicalDevice(doctor, patient, def, fromInventoryOnly: fromInventoryOnly) : null;
    }
}
namespace MoreInjuries.AI.Jobs
{
    public sealed class EmergencyDescriptor(object definition, params object[] targets) : IJobDescriptor
    { public Job CreateJob() => JobMaker.MakeJob(definition, targets); public void StartJob() { } }
}
namespace MoreInjuries.HealthConditions.Drugs.Epinephrine
{
    public static class JobDriver_UseEpinephrine
    { public const float SAFETY_THRESHOLD = .25f; public static IJobDescriptor GetDispatcher(Pawn doctor, Pawn patient, Thing device) => new EmergencyDescriptor(KnownJobDefOf.Epinephrine, patient, device); }
}
namespace MoreInjuries.HealthConditions.HeavyBleeding
{ public static class JobDriver_HemostasisBase { public static bool JobCanTreat(Hediff wound) => wound.TestBleedingEligible && !wound.Tended; } }
namespace MoreInjuries.HealthConditions.HeavyBleeding.Bandages
{ public static class JobDriver_UseBandage { public static IJobDescriptor GetDispatcher(Pawn doctor, Pawn patient, Thing device, bool fromInventoryOnly) => new EmergencyDescriptor(KnownJobDefOf.Bandage, patient, device); } }
namespace MoreInjuries.HealthConditions.HeavyBleeding.HemostaticAgents
{ public static class JobDriver_UseHemostaticAgent { public static IJobDescriptor GetDispatcher(Pawn doctor, Pawn patient, Thing device, bool fromInventoryOnly) => new EmergencyDescriptor(KnownJobDefOf.Hemostat, patient, device); } }
namespace MoreInjuries.HealthConditions.HeavyBleeding.Tourniquets
{
    public sealed class BleedRateByLimbEnumerable : List<KeyValuePair<BodyPartRecord, float>>, IDisposable
    {
        public static BleedRateByLimbEnumerable EvaluateLimbs(Pawn patient)
        { BleedRateByLimbEnumerable result = new(); result.AddRange(patient.LimbBleeding); return result; }
        public void Dispose() { }
    }
    public static class JobDriver_TourniquetBase { public static bool PawnKnowsWhatTheyreDoing(Pawn doctor) => true; }
    public static class JobDriver_UseTourniquet { public static IJobDescriptor GetDispatcher(Pawn doctor, Pawn patient, Thing device, BodyPartRecord part) => new EmergencyDescriptor(KnownJobDefOf.Tourniquet); }
    public static class JobDriver_RemoveTourniquetSafely { public static IJobDescriptor GetDispatcher(Pawn doctor, Pawn patient, BodyPartRecord part) => new EmergencyDescriptor(KnownJobDefOf.RemoveTourniquet); }
}
namespace MoreInjuries.HealthConditions.HeavyBleeding.Transfusions
{
    public enum SalineTransfusionMode { Stabilize }
    public static class JobDriver_UseSalineBag
    {
        public static ThingDef JobDeviceDef => KnownThingDefOf.SalineBag;
        public static int JobGetMedicalDeviceCountToFullyHeal(Pawn patient, bool fullyHeal)
            => patient.health.hediffSet.GetFirstHediffOfDef(RimWorld.HediffDefOf.BloodLoss)?.Severity > BloodLossConstants.BLOOD_LOSS_THRESHOLD ? 1 : 0;
        public static IJobDescriptor GetDispatcher(Pawn doctor, Pawn patient, Thing thing, bool fromInventoryOnly, SalineTransfusionMode mode) => new EmergencyDescriptor(KnownJobDefOf.Saline);
    }
    public static class JobDriver_UseBloodBag
    {
        public static ThingDef JobDeviceDef => KnownThingDefOf.BloodBag;
        public static int JobGetMedicalDeviceCountToFullyHeal(Pawn patient, bool fullyHeal)
            => JobDriver_UseSalineBag.JobGetMedicalDeviceCountToFullyHeal(patient, fullyHeal) > 0
                || patient.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.Hemodilution)?.Severity > BloodLossConstants.BLOOD_LOSS_THRESHOLD ? 1 : 0;
        public static IJobDescriptor GetDispatcher(Pawn doctor, Pawn patient, Thing thing, bool fromInventoryOnly, bool fullyHeal) => new EmergencyDescriptor(KnownJobDefOf.Blood);
    }
}

namespace Verse
{
    public sealed class Pawn_InventoryTracker;
}
namespace Verse.AI
{
    public readonly struct Target(Verse.Thing? thing) { public Verse.Thing Thing => thing!; }
    public sealed class PathFinder
    {
        public int Calls; public float Multiplier = 13; public bool Found = true;
        public PawnPath FindPathNow(Verse.IntVec3 start, Verse.IntVec3 end, Verse.Pawn doctor, object? tuning = null, PathEndMode peMode = PathEndMode.ClosestTouch)
        { Calls++; return new() { Found = Found, TotalCost = start.DistanceTo(end) * Multiplier }; }
    }
    public sealed class PawnPath { public bool Found; public float TotalCost; public void ReleaseToPool() { } }
}
namespace MoreInjuries.HealthConditions.HeavyBleeding
{
    public sealed class HemostasisModExtension { public float CoagulationMultiplier; }
}
