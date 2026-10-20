using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.AI.NpcCombatMedicine;
using Verse;
using Verse.AI;

namespace MoreInjuries.HarmonyTests
{
    [TestClass]
    public sealed class NpcMedicalReservationTests
    {
        [TestMethod]
        public void PatientReceivingCareCannotStartSelfTreatmentOrAcceptAnotherDoctor()
        {
            Map map = new();
            Pawn patient = new() { Map = map }, first = new() { Map = map }, second = new() { Map = map };
            Job existing = new();
            Assert.IsTrue(NpcMedicalReservations.PatientAvailable(patient, patient));
            Assert.IsTrue(map.reservationManager.Reserve(first, existing, patient));
            Assert.IsFalse(NpcMedicalReservations.PatientAvailable(patient, patient), "Self-treatment must not bypass another doctor's claim.");
            Assert.IsFalse(NpcMedicalReservations.PatientAvailable(second, patient));
            Assert.IsTrue(NpcMedicalReservations.PatientAvailable(first, patient));
            map.reservationManager.ReleaseClaimedBy(first, existing);
            Assert.IsTrue(NpcMedicalReservations.PatientAvailable(patient, patient));
        }

        [TestMethod]
        public void FailedTacticalPreflightReleasesOnlyProposedJobClaimsAndKeepsMedicineInInventory()
        {
            Map map = new();
            Pawn doctor = new() { Map = map }, other = new() { Map = map };
            Thing patient = new(), oldTarget = new(), medicine = new();
            Job current = new(), otherJob = new(), proposed = new();
            doctor.inventory.innerContainer.Items.Add(medicine);
            map.reservationManager.Reserve(doctor, current, oldTarget);
            map.reservationManager.Reserve(other, otherJob, medicine);
            proposed.ReservationAttempt = (pawn, errors) =>
            {
                Assert.IsFalse(errors, "Contested candidates must fail preflight quietly.");
                return map.reservationManager.Reserve(pawn, proposed, patient)
                    && map.reservationManager.Reserve(pawn, proposed, medicine);
            };
            Assert.IsFalse(NpcMedicalReservations.PrepareTacticalJob(doctor, proposed));
            Assert.IsTrue(map.reservationManager.Claims.Any(claim => claim.Job == current));
            Assert.IsTrue(map.reservationManager.Claims.Any(claim => claim.Job == otherJob));
            Assert.IsFalse(map.reservationManager.Claims.Any(claim => claim.Job == proposed));
            Assert.Contains(medicine, doctor.inventory.innerContainer.Items);
            Assert.IsNull(doctor.carryTracker.CarriedThing);
        }

        [TestMethod]
        public void SuccessfulTacticalPreflightCanBeRepeatedAtJobStart()
        {
            Pawn doctor = new(); Thing patient = new(); Job job = new();
            job.ReservationAttempt = (pawn, _) => pawn.Map.reservationManager.Reserve(pawn, job, patient);
            Assert.IsTrue(NpcMedicalReservations.PrepareTacticalJob(doctor, job));
            Assert.IsTrue(job.TryMakePreToilReservations(doctor, false));
            Assert.HasCount(1, doctor.Map.reservationManager.Claims);
        }

        [TestMethod]
        public void UnusedMedicineReturnsToInventoryWithoutDuplicatingOrTakingOtherCargo()
        {
            Pawn doctor = new();
            Thing medicine = new() { def = new() { IsMedicine = true }, stackCount = 3 };
            doctor.carryTracker.innerContainer.Items.Add(medicine);
            Job job = new() { targetB = new(medicine) };
            NpcMedicalReservations.RestockUnusedMedicine(doctor, job);
            Assert.IsNull(doctor.carryTracker.CarriedThing);
            Assert.HasCount(1, doctor.inventory.innerContainer.Items);
            Assert.AreSame(medicine, doctor.inventory.innerContainer.Items[0]);
            Assert.AreEqual(3, medicine.stackCount);
            Thing otherMedicine = new() { def = medicine.def };
            doctor.carryTracker.innerContainer.Items.Add(otherMedicine);
            NpcMedicalReservations.RestockUnusedMedicine(doctor, job);
            Assert.AreSame(otherMedicine, doctor.carryTracker.CarriedThing, "Only this job's selected medicine may be restocked.");
        }

        [TestMethod]
        public void RestockingRespectsRemainingCapacityAndLeavesOverflowForNormalCleanup()
        {
            Pawn doctor = new() { FitCount = 1 };
            Thing medicine = new() { def = new() { IsMedicine = true }, stackCount = 3 };
            doctor.carryTracker.innerContainer.Items.Add(medicine);
            Job job = new() { targetB = new(medicine) };
            NpcMedicalReservations.RestockUnusedMedicine(doctor, job);
            Assert.AreEqual(1, doctor.inventory.innerContainer.Items.Sum(item => item.stackCount));
            Assert.AreEqual(2, doctor.carryTracker.CarriedThing!.stackCount);
            doctor.FitCount = 0;
            NpcMedicalReservations.RestockUnusedMedicine(doctor, job);
            Assert.AreEqual(2, doctor.carryTracker.CarriedThing!.stackCount);
            Assert.AreEqual(3, doctor.InventoryUpdates);
        }

        [TestMethod]
        public void InventoryTendingTransfersExactlyOneDoseAndReturnsItOnFailure()
        {
            Pawn doctor = new(), supplier = new();
            Thing medicine = new() { def = new() { IsMedicine = true }, stackCount = 8, Spawned = false };
            supplier.inventory.innerContainer.Items.Add(medicine);
            Job job = new() { targetB = new(medicine) };
            Assert.IsTrue(NpcMedicalReservations.TakeMedicine(doctor, job, medicine, supplier.inventory.innerContainer));
            Assert.AreEqual(7, medicine.stackCount);
            Thing carried = doctor.carryTracker.CarriedThing!;
            Assert.AreEqual(1, carried.stackCount);
            Assert.AreSame(carried, job.targetB.Thing);
            Assert.IsFalse(carried.Spawned, "Inventory collection must never stage medicine on the ground.");
            Assert.IsTrue(NpcMedicalReservations.TakeMedicine(doctor, job, carried, supplier.inventory.innerContainer));
            Assert.AreEqual(7, medicine.stackCount, "An already-carried dose must not be collected again.");
            NpcMedicalReservations.RestockUnusedMedicine(doctor, job);
            Assert.IsNull(doctor.carryTracker.CarriedThing);
            Assert.AreEqual(8, supplier.inventory.innerContainer.Items.Sum(item => item.stackCount)
                + doctor.inventory.innerContainer.Items.Sum(item => item.stackCount));
        }

        [TestMethod]
        public void MissingInventoryMedicineOrUnrelatedCargoCannotBeRetargeted()
        {
            Pawn doctor = new();
            Thing medicine = new() { def = new() { IsMedicine = true }, stackCount = 5, Spawned = false };
            ThingOwner source = new();
            Job job = new() { targetB = new(medicine) };
            Assert.IsFalse(NpcMedicalReservations.TakeMedicine(doctor, job, medicine, source));
            source.Items.Add(medicine);
            Thing cargo = new(); doctor.carryTracker.innerContainer.Items.Add(cargo);
            Assert.IsFalse(NpcMedicalReservations.TakeMedicine(doctor, job, medicine, source));
            Assert.AreSame(cargo, doctor.carryTracker.CarriedThing);
            Assert.AreSame(medicine, job.targetB.Thing);
            Assert.AreEqual(5, medicine.stackCount);
            doctor.carryTracker.innerContainer.Items.Clear(); source.TransferLimit = 0;
            Assert.IsFalse(NpcMedicalReservations.TakeMedicine(doctor, job, medicine, source));
            Assert.AreEqual(5, medicine.stackCount);
        }

        [TestMethod]
        public void SelfTendingKeepsTheRestOfItsOwnStackInInventory()
        {
            Pawn doctor = new();
            Thing medicine = new() { def = new() { IsMedicine = true }, stackCount = 4, Spawned = false };
            doctor.inventory.innerContainer.Items.Add(medicine);
            Job job = new() { targetB = new(medicine) };
            Assert.IsTrue(NpcMedicalReservations.TakeMedicine(doctor, job, medicine, doctor.inventory.innerContainer));
            Assert.AreEqual(3, medicine.stackCount);
            Assert.AreSame(medicine, doctor.inventory.innerContainer.Items.Single());
            Assert.AreEqual(1, doctor.carryTracker.CarriedThing!.stackCount);
            Assert.IsFalse(doctor.carryTracker.CarriedThing.Spawned);
            Assert.AreEqual(1, doctor.InventoryUpdates);
        }
    }
}

// Main-thread game boundaries for the source-linked reservation/cleanup policy.
namespace Verse
{
    public partial class Thing
    {
        public ThingDef def = new();
        public int stackCount = 1;
        public bool Destroyed;
        public bool Spawned = true;
        public Action<Pawn>? IngestionEffect;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public float Ingested(Pawn ingester, float nutritionWanted)
        {
            stackCount--; Destroyed = stackCount <= 0;
            IngestionEffect?.Invoke(ingester);
            if (Destroyed)
            {
                if (ParentHolder is Pawn_InventoryTracker inventory) { inventory.innerContainer.Items.Remove(this); }
                if (ParentHolder is CarryTracker carry) { carry.innerContainer.Items.Remove(this); }
            }
            return nutritionWanted;
        }
    }
    public sealed partial class Pawn : Thing
    {
        public Map Map = new();
        public Pawn_InventoryTracker inventory = new();
        public CarryTracker carryTracker = new();
        public int FitCount = int.MaxValue, InventoryUpdates;
        public bool ManagedByMi = true, SafeDrug = true;
        public int Remembered;
        public JobTracker jobs = new();
        public Job CurJob { get => jobs.curJob; set => jobs.curJob = value; }
    }
    public sealed class Map { public ReservationManager reservationManager = new(); }
    public sealed class Pawn_InventoryTracker { public Pawn pawn = null!; public ThingOwner innerContainer = new(); }
    public sealed class CarryTracker
    {
        public ThingOwner innerContainer = new();
        public Thing? CarriedThing => innerContainer.Items.FirstOrDefault();
        public int MaxPickup = int.MaxValue;
        public Pawn Pawn = null!;
        public int AvailableStackSpace(ThingDef def) => MaxPickup;
        public bool TryDropCarriedThing(IntVec3 cell, ThingPlaceMode mode, out Thing result)
        {
            result = CarriedThing!;
            if (result is null || !DropAllowed) { return false; }
            innerContainer.Items.Remove(result); result.Spawned = true; result.ParentHolder = null;
            if (result is Pawn patient) { patient.Position = cell; } else { result.PositionHeld = cell; }
            return true;
        }
        public bool DropAllowed = true;
        public int TryStartCarry(Thing thing, int count)
        {
            if (MaxPickup <= 0 || ReferenceEquals(CarriedThing, thing)
                || CarriedThing is Thing carried && carried.def != thing.def) { return 0; }
            int moved = Math.Min(thing.stackCount, Math.Min(count, MaxPickup));
            if (CarriedThing is Thing existing) { existing.stackCount += moved; thing.stackCount -= moved; }
            else if (moved == thing.stackCount) { innerContainer.Items.Add(thing); thing.Spawned = false; thing.ParentHolder = this; }
            else { thing.stackCount -= moved; innerContainer.Items.Add(new() { def = thing.def, stackCount = moved, Spawned = false, ParentHolder = this }); }
            return moved;
        }
    }
    public sealed class ThingOwner : IEnumerable<Thing>
    {
        public object? Owner;
        public IEnumerator<Thing> GetEnumerator() => Items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public List<Thing> Items = [];
        public int TransferLimit = int.MaxValue;
        public bool Contains(Thing item) => Items.Contains(item);
        public int TryTransferToContainer(Thing item, ThingOwner destination, int count, out Thing transferred)
        {
            int moved = TryTransferToContainer(item, destination, count);
            transferred = moved > 0 ? destination.Items.Last() : null!;
            return moved;
        }
        public int TryTransferToContainer(Thing item, ThingOwner destination, int count)
        {
            if (!Items.Contains(item)) { return 0; }
            int moved = Math.Min(item.stackCount, Math.Min(count, TransferLimit));
            if (moved <= 0) { return 0; }
            if (moved == item.stackCount) { Items.Remove(item); item.ParentHolder = destination.Owner; destination.Items.Add(item); }
            else { item.stackCount -= moved; destination.Items.Add(new() { def = item.def, stackCount = moved, Spawned = false, ParentHolder = destination.Owner }); }
            return moved;
        }
    }
    public sealed class ReservationManager
    {
        public sealed class Claim(Pawn pawn, Job job, Thing target)
        { public Pawn Pawn = pawn; public Job Job = job; public Thing Target = target; }
        public List<Claim> Claims = [];
        public bool Reserve(Pawn pawn, Job job, Thing target)
        {
            if (Claims.Any(claim => claim.Target == target && claim.Pawn != pawn)) { return false; }
            if (!Claims.Any(claim => claim.Pawn == pawn && claim.Job == job && claim.Target == target))
            { Claims.Add(new(pawn, job, target)); }
            return true;
        }
        public void ReleaseClaimedBy(Pawn pawn, Job job) => Claims.RemoveAll(claim => claim.Pawn == pawn && claim.Job == job);
        public int CanReserveStack(Pawn pawn, Thing target, int maxPawns)
            => Claims.Any(claim => claim.Target == target && claim.Pawn != pawn) ? 0 : target.stackCount;
        public void Release(Thing target, Pawn pawn, Job job) => Claims.RemoveAll(claim => claim.Pawn == pawn && claim.Job == job && claim.Target == target);
    }
    public static class ReservationUtility
    {
        public static bool Reserve(this Pawn pawn, Thing target, Job job, int maxPawns = 1, int stackCount = -1, bool errorOnFailed = false)
            => stackCount != 0 && pawn.Map.reservationManager.Reserve(pawn, job, target);
        public static bool CanReserve(this Pawn pawn, Thing target, int maxPawns, int stackCount, bool ignoreOtherReservations = false)
        {
            Assert.IsFalse(ignoreOtherReservations);
            return !pawn.Map.reservationManager.Claims.Any(claim => claim.Target == target && claim.Pawn != pawn);
        }
    }
    public static partial class Find { public static TickManager TickManager = new(); }
    public sealed class TickManager { public int TicksGame = 1000; }
    public sealed class JobDef { public string defName = "fixture"; public Type? driverClass; }
}
namespace Verse.AI
{
    public readonly struct LocalTargetInfo
    {
        public Thing Thing { get; }
        public Pawn Pawn => (Pawn)Thing;
        public IntVec3 Cell { get; }
        public LocalTargetInfo(Thing thing) { Thing = thing; Cell = default; }
        public LocalTargetInfo(IntVec3 cell) { Thing = null!; Cell = cell; }
    }
    public sealed class QueuedJob { public Job job = new(); }
    public sealed class Job
    {
        public LocalTargetInfo targetA;
        public LocalTargetInfo targetB;
        public LocalTargetInfo targetC;
        public bool endAfterTendedOnce, playerForced;
        public JobDef def = new();
        public int count = 1;
        public int startTick;
        public int loadID;
        public LocalTargetInfo GetTarget(TargetIndex index) => index == TargetIndex.A ? targetA : index == TargetIndex.B ? targetB : targetC;
        public void SetTarget(TargetIndex index, Thing? thing)
        { if (index == TargetIndex.A) { targetA = new(thing!); } else if (index == TargetIndex.B) { targetB = new(thing!); } else { targetC = new(thing!); } }
        public Func<Pawn, bool, bool>? ReservationAttempt;
        public bool TryMakePreToilReservations(Pawn pawn, bool errorOnFailed) => ReservationAttempt?.Invoke(pawn, errorOnFailed) ?? true;
    }
    public enum TargetIndex { A, B, C }
    public enum ToilCompleteMode { Instant }
    public enum JobCondition { Succeeded, Incompletable, Ongoing, InterruptForced }
    public sealed class JobTracker : Pawn_JobTracker { }
    public class Pawn_JobTracker
    {
        public Pawn pawn = null!;
        public readonly List<QueuedJob> jobQueue = [];
        public Action? DuringStart;
        public bool KeptCarry, Resumed;
        public void StartJob(Job newJob, JobCondition lastJobEndCondition = JobCondition.Ongoing, bool resumeCurJobAfterwards = false, bool? keepCarryingThingOverride = null)
        {
            KeptCarry = keepCarryingThingOverride == true; Resumed = resumeCurJobAfterwards;
            DuringStart?.Invoke();
            if (resumeCurJobAfterwards) { jobQueue.Add(new() { job = curJob }); }
            if (!KeptCarry && pawn.carryTracker.CarriedThing is Thing item) { pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _); }
            curJob = newJob;
        }
        public Job curJob = new();
        public JobCondition? Ended;
        public void EndCurrentJob(JobCondition condition) => Ended = condition;
    }
    public sealed class Toil
    {
        public readonly List<Func<bool>> Failures = [];
        public Toil? JumpTarget;
        public Func<bool>? JumpCondition;
        public string? Tag;
        public Pawn actor = null!;
        public Action initAction = null!;
        public Func<object>? activeSkill;
        public readonly List<Action> FinishActions = [];
        public void AddPreInitAction(Action action) { Action previous = initAction; initAction = () => { action(); previous?.Invoke(); }; }
        public void AddFinishAction(Action action) => FinishActions.Add(action);
        public ToilCompleteMode defaultCompleteMode;
        public bool atomicWithPrevious;
    }
    public static class ToilMaker { public static Toil MakeToil(string name) => new(); }
}
namespace MoreInjuries.AI.NpcCombatMedicine
{
    internal static class NpcMedicalCompatibility
    {
        internal static void InventoryChanged(Pawn pawn) => pawn.InventoryUpdates++;
        internal static int Fit(Pawn pawn, Thing thing) => pawn.FitCount;
        internal static bool SafetyOwned(Pawn pawn) => pawn.NativeSafety;
    }
    internal sealed class NpcMedicalState { internal readonly NpcDrugBudget Drugs = new(); }
    internal static partial class NpcMedicalAi
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Pawn, NpcMedicalState> s_states = new();
        internal static NpcMedicalState State(Pawn pawn) => s_states.GetOrCreateValue(pawn);
        internal static bool ActorEligible(Pawn pawn) => pawn.ManagedByMi;
        internal static bool Managed(Pawn pawn) => pawn.ManagedByMi && MoreInjuriesMod.Settings.EnableNpcCombatMedicine;
        internal static bool SafePerformance(Pawn pawn, Thing thing) => pawn.SafeDrug;
    }
    internal static class NpcMedicalMemory { internal static void Remember(Pawn pawn) => pawn.Remembered++; }
}
namespace MoreInjuries
{
    internal sealed class MoreInjuriesSettings
    {
        internal bool EnableNpcCombatMedicine = true, NpcPerformanceDrugs = true;
        internal int NpcPerformanceMaximum = 1, NpcPerformanceInterval = 20000;
        internal bool NpcPainCare = true;
        internal float NpcPainThreshold = .5f;
    }
    internal static class MoreInjuriesMod { internal static MoreInjuriesSettings Settings = new(); }
}
namespace MoreInjuries.Defs.WellKnown
{
    internal static class KnownThingDefOf { internal static readonly ThingDef Epinephrine = new(); internal static ThingDef Morphine = new(); }
    internal static class KnownJobDefOf { internal static readonly JobDef MI_NpcExtract = new(), MI_NpcTend = new(), MI_NpcPerformanceDrug = new(), MI_NpcPainSelf = new(), MI_NpcPainAssist = new(); }
}
namespace MoreInjuries.Things
{ internal static class MedicalDeviceHelper { internal const int MAX_MEDICAL_DEVICE_RESERVATIONS = 10; } }
namespace UnityEngine
{
    public static partial class Mathf
    {
        public static int Min(int a, int b) => Math.Min(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
    }
}
