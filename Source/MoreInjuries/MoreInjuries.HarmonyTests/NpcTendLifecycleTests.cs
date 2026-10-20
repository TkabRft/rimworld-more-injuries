using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.AI.NpcCombatMedicine;
using Verse;
using Verse.AI;

namespace MoreInjuries.HarmonyTests
{
    [TestClass]
    public sealed class NpcTendLifecycleTests
    {
        [TestMethod]
        public void TwoFloorStacksCannotChangeTheResumedApplicationLayout()
        {
            Pawn doctor = new(), patient = new();
            Thing first = new() { def = new() { IsMedicine = true } }, second = new() { def = first.def };
            foreach (Thing medicine in new[] { first, second })
            {
                doctor.CurJob = new() { targetA = new(patient), targetB = new(medicine) };
                int beforePickup = new JobDriver_NpcTend { pawn = doctor }.BuildToils().Count;
                doctor.carryTracker.innerContainer.Items.Add(medicine);
                medicine.ParentHolder = doctor.carryTracker;
                medicine.Spawned = false;
                int afterPickup = new JobDriver_NpcTend { pawn = doctor }.BuildToils().Count;
                Assert.AreEqual(beforePickup, afterPickup, "An interruption/save after collecting either floor stack must retain the application's toil indices.");
                NpcMedicalReservations.RestockUnusedMedicine(doctor, doctor.CurJob);
            }
        }

        [TestMethod]
        public void ResumeAfterPickupUsesTheSameSelectedDoseForFloorAndInventoryRoutes()
        {
            foreach (bool inventory in new[] { false, true })
            {
                Pawn doctor = new(), patient = new();
                Thing medicine = new() { def = new() { IsMedicine = true }, stackCount = 4 };
                if (inventory) { doctor.inventory.innerContainer.Items.Add(medicine); medicine.ParentHolder = doctor.inventory; }
                doctor.CurJob = new() { def = MoreInjuries.Defs.WellKnown.KnownJobDefOf.MI_NpcTend,
                    targetA = new(patient), targetB = new(medicine), endAfterTendedOnce = true, startTick = Find.TickManager.TicksGame };
                List<Toil> original = new JobDriver_NpcTend { pawn = doctor }.BuildToils();
                int readyIndex = original.IndexOf(original[0].JumpTarget!);
                if (inventory) { Assert.IsTrue(NpcMedicalReservations.TakeMedicine(doctor, doctor.CurJob, medicine, doctor.inventory.innerContainer)); }
                else
                {
                    Assert.AreEqual(1, doctor.carryTracker.TryStartCarry(medicine, 1));
                    doctor.CurJob.SetTarget(TargetIndex.B, doctor.carryTracker.CarriedThing);
                }
                JobDriver_NpcTend resumed = new() { pawn = doctor, ResumeIndex = readyIndex };
                resumed.Run();
                Assert.IsFalse(patient.health.NeedsTend);
                Assert.AreEqual(3, medicine.stackCount);
                Assert.IsNull(doctor.carryTracker.CarriedThing);
            }
        }
    }
}

// Native tending/wait boundaries; production retrieval and lifecycle are linked.
namespace RimWorld
{
    public class JobDriver_TendPatient : JobDriver;
    public static class SoundDefOf { public static readonly object Interact_Tend = new(); }
    public static class SkillDefOf { public static readonly object Medicine = new(); }
    public static partial class JobDefOf { public static readonly JobDef Wait = new(), Wait_MaintainPosture = new(); }
    public static class Toils_Tend
    {
        public static int TendLimit = int.MaxValue;
        public static Toil FinalizeTend(Pawn patient)
        {
            Toil toil = new();
            toil.initAction = () =>
            {
                foreach (Hediff hediff in patient.health.hediffSet.hediffs.Where(wound => wound.TendableNow()).Take(TendLimit)) { hediff.Tended = true; }
                patient.health.NeedsTend = patient.health.hediffSet.hediffs.Any(wound => wound.TendableNow());
                Thing? medicine = toil.actor.carryTracker.CarriedThing;
                if (medicine is null) { return; }
                medicine.stackCount--;
                if (medicine.stackCount == 0) { medicine.Destroyed = true; toil.actor.carryTracker.innerContainer.Items.Remove(medicine); }
            };
            return toil;
        }
    }
}

namespace Verse
{
    public sealed class DamageDef
    {
        public bool isRanged, isExplosive, Violent = true;
        public bool ExternalViolenceFor(Pawn pawn) => Violent;
    }
    public readonly struct DamageInfo(DamageDef def) { public DamageDef Def => def; }
    public static class TendFixtureExtensions
    {
        public static bool Reserve(this Pawn pawn, LocalTargetInfo target, Job job, bool errorOnFailed = false) => pawn.Reserve(target.Thing, job, errorOnFailed: errorOnFailed);
    }
}
namespace RimWorld
{
    public static class CareFixtureExtensions { public static bool AllowsMedicine(this MedicalCareCategory care, ThingDef def) => care != MedicalCareCategory.NoMeds && care != MedicalCareCategory.NoCare; }
}
namespace MoreInjuries.AI
{
    internal static class MedicalTiming { internal static bool CanWork(Pawn pawn) => pawn.DoctorCapable; internal static int Duration(Pawn pawn, int ticks) => ticks; }
}
