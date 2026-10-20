using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.HarmonyTests;

[TestClass, DoNotParallelize]
public sealed class NpcMedicalDecisionTests
{
    [TestInitialize]
    public void Reset() => new NpcPainCareTests().Initialize();

    private static Thing Pocket(Pawn actor)
    {
        Thing dose = new() { def = ThingDefOf.Yayo, Spawned = false, ParentHolder = actor.inventory };
        actor.inventory.innerContainer.Items.Add(dose); return dose;
    }
    private static Job? Care(Pawn actor, Pawn patient) => NpcPainCare.Select(actor, patient,
        allowed: (action, dose) => NpcMedicalAttempts.Allowed(actor, patient, action, dose));

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void RealPainSelectionWinsBeforeAnyOffMapDecisionAndAssistedJobConsumesDose(int mode)
    {
        MoreInjuriesMod.Settings.NpcMedicalAiMode = mode;
        Pawn actor = new(), patient = new() { Map = actor.Map, Downed = true, RawPain = .9f };
        Thing dose = Pocket(actor);
        Job? selected = NpcMedicalDecision.Choose(actor, mode, [patient], _ => (null, false), _ => null,
            other => Care(actor, other), _ => throw new AssertFailedException("Evacuation preempted safe care"),
            () => throw new AssertFailedException("Withdrawal preempted safe care"));
        Assert.AreSame(KnownJobDefOf.MI_NpcPainAssist, selected!.def);
        actor.CurJob = selected;
        new JobDriver_NpcPainAssist { pawn = actor }.Run();
        Assert.IsTrue(dose.Destroyed);
        Assert.IsTrue(patient.Downed, "MI must not force a patient to stand; native health owns recovery.");
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void LowerModesNeverCallWithdrawalOrEvacuationWhenCareIsUnavailable(int mode)
    {
        Pawn actor = new(), patient = new() { Downed = true, RawPain = .9f };
        Assert.IsNull(NpcMedicalDecision.Choose(actor, mode, [patient], _ => (null, false), _ => null,
            other => Care(actor, other), _ => throw new AssertFailedException("Mode acquired evacuation"),
            () => throw new AssertFailedException("Mode acquired withdrawal")));
    }

    [TestMethod]
    public void MedicalOnlyNeverCallsTacticalExtractionAndTacticalModesRetainUnsafeExtractionPriority()
    {
        Pawn actor = new(), patient = new() { Downed = true, RawPain = .9f };
        Pocket(actor);
        Job extraction = new() { def = KnownJobDefOf.MI_NpcExtract };
        Assert.AreSame(KnownJobDefOf.MI_NpcPainAssist,
            NpcMedicalDecision.Choose(actor, 0, [patient], _ => throw new AssertFailedException("Mode acquired extraction"),
                _ => null, other => Care(actor, other), _ => null, () => null)!.def);
        foreach (int mode in new[] { 1, 2 })
        {
            Assert.AreSame(extraction, NpcMedicalDecision.Choose(actor, mode, [patient], _ => (extraction, true),
                _ => null, _ => throw new AssertFailedException("Unsafe ordinary treatment selected"), _ => null, () => null));
        }
    }

    [TestMethod]
    public void EntireCarePassPrecedesEvacuationAndUnsafeCareDoesNotBypassEmergencyGate()
    {
        Pawn actor = new(), first = new() { Downed = true }, second = new() { Downed = true, RawPain = .9f };
        Pocket(actor);
        Assert.AreSame(KnownJobDefOf.MI_NpcPainAssist,
            NpcMedicalDecision.Choose(actor, 2, [first, second], other => (null, other == first), _ => null,
                other => Care(actor, other), _ => throw new AssertFailedException("Skipped feasible care on second patient"), () => null)!.def);
    }

    [TestMethod]
    public void MixedCollapseMissingSuppliesAndIncapableHelpersDoNotInventRecovery()
    {
        foreach (int rejection in new[] { 0, 1, 2 })
        {
            Pawn actor = new(), patient = new() { Downed = true, RawPain = rejection == 0 ? .1f : .9f };
            if (rejection != 1) { Pocket(actor); }
            if (rejection == 2) { actor.DoctorCapable = false; }
            Job evacuation = new() { def = KnownJobDefOf.MI_NpcEvacuate };
            Assert.AreSame(evacuation, NpcMedicalDecision.Choose(actor, 2, [patient], _ => (null, false), _ => null,
                other => Care(actor, other), _ => evacuation, () => null));
            Assert.IsTrue(patient.Downed);
        }
    }

    [TestMethod]
    public void UnavailableEvacuationDoesNotDisableIndividualWithdrawalUnderFire()
    {
        Pawn actor = new(), patient = new() { Downed = true };
        Job withdrawal = new() { def = KnownJobDefOf.MI_NpcWithdraw };
        Assert.AreSame(withdrawal, NpcMedicalDecision.Choose(actor, 2, [patient], _ => (null, true), _ => null,
            _ => null, _ => null, () => withdrawal));
    }

    [TestMethod]
    public void DecisionDiagnosticsAreTransitionOnlyAndIncludeMode()
    {
        MoreInjuriesMod.Settings.EnableLogging = true;
        Pawn actor = new(), patient = new(); Prefs.DevMode = true;
        for (int i = 0; i < 10; i++) { NpcMedicalDecision.Report(actor, patient, 0, "care", "unavailable dose"); }
        Assert.HasCount(1, Log.Messages);
        NpcMedicalDecision.Report(actor, patient, 2, "care", "unavailable dose");
        Assert.HasCount(2, Log.Messages); StringAssert.Contains(Log.Messages[1], "mode=2");
        Prefs.DevMode = false;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ActiveClinicalJobsYieldToKnownExplosiveWithoutConsumingDose(bool self)
    {
        Pawn actor = new(), patient = self ? actor : new() { Downed = true, RawPain = .9f, Map = actor.Map };
        if (self) { actor.RawPain = .9f; }
        Thing dose = Pocket(actor);
        actor.CurJob = Care(actor, patient)!;
        Assert.IsNotNull(actor.CurJob);
        actor.mindState.knownExploder = new() { Map = actor.Map, PositionHeld = actor.Position };
        JobDriver driver = self ? new JobDriver_NpcPainSelf { pawn = actor } : new JobDriver_NpcPainAssist { pawn = actor };
        driver.Run();
        Assert.IsFalse(dose.Destroyed); Assert.AreEqual(1, dose.stackCount);
        Assert.AreEqual(JobCondition.Incompletable, actor.jobs.Ended);
    }

    [TestMethod]
    public void NativeMovementWrapperRespectsModeAndGrenadesButKeepsForcedWorkAndWithdrawalAvailable()
    {
        foreach (int mode in new[] { 0, 1, 2 })
        {
            MoreInjuriesMod.Settings.NpcMedicalAiMode = mode;
            Pawn actor = new(); actor.CurJob = new() { def = KnownJobDefOf.MI_NpcWithdraw, targetA = new(new IntVec3(5)) };
            new JobDriver_NpcMove { pawn = actor }.Run();
            Assert.AreEqual(mode == 2, actor.Position == new IntVec3(5));
        }
        Pawn escaping = new() { NativeSafety = true };
        escaping.CurJob = new() { def = KnownJobDefOf.MI_NpcWithdraw, targetA = new(new IntVec3(5)) };
        new JobDriver_NpcMove { pawn = escaping }.Run(); Assert.AreEqual(new IntVec3(5), escaping.Position);
        Pawn warned = new(); warned.CurJob = new() { def = KnownJobDefOf.MI_NpcReposition, targetA = new(new IntVec3(5)) };
        warned.mindState.knownExploder = new() { Map = warned.Map, PositionHeld = warned.Position };
        new JobDriver_NpcMove { pawn = warned }.Run(); Assert.AreEqual(new IntVec3(0), warned.Position);
        warned.jobs.Ended = null; warned.CurJob.playerForced = true;
        new JobDriver_NpcMove { pawn = warned }.Run(); Assert.AreEqual(new IntVec3(5), warned.Position);
    }

    [TestMethod]
    public void LocalExtractionCannotStartNewPickupInMedicalOnlyMode()
    {
        MoreInjuriesMod.Settings.NpcMedicalAiMode = 0;
        Pawn carrier = new(), patient = new() { Downed = true, Map = carrier.Map };
        carrier.CurJob = new() { def = KnownJobDefOf.MI_NpcExtract, targetA = new(patient), targetB = new(new IntVec3(5)), targetC = new(patient.Position) };
        Assert.IsFalse(new JobDriver_NpcExtract { pawn = carrier }.TryMakePreToilReservations(false));
        Assert.IsTrue(patient.Spawned); Assert.IsNull(carrier.carryTracker.CarriedThing);
    }
}
