using MoreInjuries.AI;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.HeavyBleeding;
using MoreInjuries.HealthConditions.VitalFailure;
using MoreInjuries.Integrations.CombatExtended;
using MoreInjuries.Patches;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass, DoNotParallelize]
public sealed class WeightedEmergencyTests
{
    [TestCleanup]
    public void Cleanup() { CombatExtendedPrognosisTests.Initialize(false); GenClosest.Device = null; }

    private static (Pawn Doctor, Pawn Patient, Hediff Wound) Pair(float rate)
    {
        CombatExtendedPrognosisTests.Initialize(false);
        Pawn doctor = VitalFailureTests.Patient(), patient = VitalFailureTests.Patient();
        doctor.Map = doctor.MapHeld = patient.Map = patient.MapHeld = new(); patient.playerSettings = new(); patient.health.hediffSet.NativeBleeding = true;
        Hediff wound = VitalFailureTests.Add(patient, new(), 1f);
        wound.TestBleedingEligible = true; wound.TestBleedRate = rate;
        KnownResearchProjectDefOf.BasicAnatomy.IsFinished = KnownResearchProjectDefOf.AdvancedFirstAid.IsFinished = true;
        KnownResearchProjectDefOf.Cpr.IsFinished = KnownResearchProjectDefOf.EmergencyMedicine.IsFinished = true;
        MoreInjuriesMod.Settings.EnableAdrenaline = false;
        return (doctor, patient, wound);
    }

    private static Thing Supply(Pawn doctor, ThingDef def, float value, int? distance = null)
    {
        Thing thing = new() { def = def, Value = value, Map = doctor.Map, MapHeld = doctor.Map, PositionHeld = distance ?? 0 };
        if (distance is null) { doctor.Devices[def] = thing; }
        else { doctor.RetrievedDevices[def] = thing; }
        return thing;
    }

    [TestMethod]
    public void NearbyHemostaticBeatsPocketBandageButDistantHemostaticDoesNot()
    {
        var (doctor, patient, _) = Pair(.6f);
        Supply(doctor, KnownThingDefOf.Bandage, 2);
        Thing hemostatic = Supply(doctor, KnownThingDefOf.HemostaticAgent, 6, 5);
        foreach (bool firstAid in new[] { false, true })
        {
            Assert.AreSame(KnownJobDefOf.Hemostat, EmergencyCare.CreateJob(doctor, patient, firstAid)!.def);
            hemostatic.PositionHeld = 80;
            Assert.AreSame(KnownJobDefOf.Bandage, EmergencyCare.CreateJob(doctor, patient, firstAid)!.def);
            hemostatic.PositionHeld = 5;
        }
        Assert.AreEqual(string.Empty, patient.Vital!.PrognosisTooltip);
    }

    [TestMethod]
    public void MinorWoundsConserveHemostatUnlessBandageIsImpracticalAndHeavyWoundsComeFirst()
    {
        var (doctor, patient, wound) = Pair(.05f);
        Supply(doctor, KnownThingDefOf.HemostaticAgent, 6);
        Supply(doctor, KnownThingDefOf.Bandage, 2);
        Assert.AreSame(KnownJobDefOf.Bandage, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        Hediff heavy = VitalFailureTests.Add(patient, new(), 1); heavy.TestBleedingEligible = true; heavy.TestBleedRate = .8f;
        Assert.AreSame(KnownJobDefOf.Hemostat, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        heavy.Tended = true; heavy.TestBleedRate = 0;
        Assert.AreSame(KnownJobDefOf.Bandage, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        doctor.Devices.Remove(KnownThingDefOf.Bandage);
        Thing distant = Supply(doctor, KnownThingDefOf.Bandage, 2, 80);
        Assert.AreSame(KnownJobDefOf.Hemostat, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        distant.Forbidden = true;
        Assert.AreSame(KnownJobDefOf.Hemostat, EmergencyCare.CreateJob(doctor, patient, false)!.def);
    }

    [TestMethod]
    public void QuickBleedingExceptionRequiresAnInterveningCompletedResuscitationAttempt()
    {
        var (doctor, patient, _) = Pair(.8f);
        Supply(doctor, KnownThingDefOf.HemostaticAgent, 6);
        VitalFailureTests.Add(patient, KnownHediffDefOf.CardiacArrest, .1f);
        Assert.AreSame(KnownJobDefOf.Hemostat, EmergencyCare.CreateJob(doctor, patient, true)!.def);
        EmergencyCare.NotifyTreatmentCompleted(patient, KnownJobDefOf.UseHemostaticAgent);
        Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, true)!.def);
        EmergencyCare.NotifyTreatmentCompleted(patient, KnownJobDefOf.PerformCpr);
        Assert.AreSame(KnownJobDefOf.Hemostat, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        doctor.health.capacities.Levels[PawnCapacityDefOf.Manipulation] = .1f;
        Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, false)!.def,
            "Slow manipulation makes the application exceed the 120-tick exception.");
    }

    [TestMethod]
    public void CriticalDeviceRetrievalChecksActualPathCostAndOtherwiseUsesCpr()
    {
        var (doctor, patient, _) = Pair(.05f);
        VitalFailureTests.Add(patient, KnownHediffDefOf.CardiacArrest, .1f);
        Thing defibrillator = Supply(doctor, KnownThingDefOf.Defibrillator, 100, 4);
        Assert.AreSame(KnownJobDefOf.UseDefibrillator, EmergencyCare.CreateJob(doctor, patient, true)!.def);
        Assert.IsTrue(doctor.Map!.pathFinder.Calls > 0);
        doctor.Map.pathFinder.Multiplier = 30;
        Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, true)!.def);
        doctor.Map.pathFinder.Multiplier = 13; defibrillator.PositionHeld = 50;
        Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, true)!.def);
        defibrillator.PositionHeld = 4; defibrillator.Reservable = false;
        Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, true)!.def);
    }

    [TestMethod]
    public void CriticalAirwayAndSevereVolumeLossOutrankMinorBleeding()
    {
        var (doctor, patient, _) = Pair(.05f);
        Supply(doctor, KnownThingDefOf.Bandage, 2);
        Hediff choking = VitalFailureTests.Add(patient, KnownHediffDefOf.ChokingOnBlood, .8f);
        patient.health.capacities.Levels[PawnCapacityDefOf.Breathing] = .2f;
        Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        patient.health.hediffSet.hediffs.Remove(choking);
        VitalFailureTests.Add(patient, HediffDefOf.BloodLoss, 1);
        Supply(doctor, KnownThingDefOf.SalineBag, 2); Supply(doctor, KnownThingDefOf.BloodBag, 20);
        Assert.AreSame(KnownJobDefOf.Saline, EmergencyCare.CreateJob(doctor, patient, true)!.def);
        patient.playerSettings!.medCare = MedicalCareCategory.NoMeds;
        Assert.AreSame(KnownJobDefOf.Bandage, EmergencyCare.CreateJob(doctor, patient, true)!.def);
    }

    [TestMethod]
    public void SharedHemostasisClockAndProjectionChangeOnlyAtStagesAndReconstructRemainingTime()
    {
        foreach ((int total, float initial) in new[] { (20000, .05f), (30000, .2f) })
        {
            TimedHemostasisClock clock = new(); clock.Reset(total, total, 1000);
            PrognosisBleeding profile = new(3, 1, false, false, 60, false, timedBase: initial, remaining: total, duration: total);
            int updates = 0, remaining = total;
            for (int tick = 0; tick <= total; tick++)
            {
                if (clock.Advance(1000 + tick, out int next)) { remaining = next; updates++; }
                Assert.AreEqual(3 * TimedHemostasis.Multiplier(initial, total, remaining), profile.At(tick), .000001f);
                Assert.AreEqual(total - tick, clock.RemainingAt(1000 + tick));
            }
            Assert.AreEqual(total / 250, updates);
            Assert.AreEqual(3f, profile.At(total));
            clock.Reset(total, total, 1000);
            int saved = clock.RemainingAt(1987);
            TimedHemostasisClock restored = new(); restored.Reset(total, saved, 90000);
            Assert.IsFalse(restored.Advance(90012, out _));
            Assert.IsTrue(restored.Advance(90013, out int after));
            Assert.AreEqual(total - 1000, after, "Reload preserves the original stage phase without replaying offline time.");
            Assert.AreEqual(TimedHemostasis.Multiplier(initial, total, saved), profile.At(987) / 3f, .000001f);
        }
    }

    [TestMethod]
    public void NativeIntervalJumpsAndDuplicateLegacyCallbacksDoNotReplayTimerUpdates()
    {
        TimedHemostasisClock clock = new(); clock.Reset(20000, 20000, 100);
        Assert.IsTrue(clock.Advance(1100, out int remaining)); Assert.AreEqual(19000, remaining);
        Assert.IsFalse(clock.Advance(1100, out _));
        Assert.IsTrue(clock.Advance(25000, out remaining)); Assert.AreEqual(0, remaining);
        Assert.IsFalse(clock.Advance(25001, out _));
        clock.Reset(30000, 12345, 50000); // An old save partway through a continuous-decay interval.
        Assert.IsFalse(clock.Advance(50094, out _));
        Assert.IsTrue(clock.Advance(50095, out remaining)); Assert.AreEqual(12250, remaining);
    }

    [TestMethod]
    public void BothScenariosReachChokingAndCapacityTermsWithNoNumericAllocations()
    {
        PrognosisTrend trend = new() { Severity = .6f, Maximum = 1f, Interval = 200, NextTick = 200,
            Choking = new() { Active = true, Interval = 750, NextTick = 750, SourcePresent = true, CoughCapable = true,
                PositiveFactor = 1, NegativeFactor = 1, Source = new(3, -.5f, false, false, 60,
                    timedBase: .05f, remaining: 20000, duration: 20000) } };
        Assert.AreEqual(0f, trend.At(20000, holdControl: true));
        Assert.IsTrue(trend.At(20000) > 0f);
        PhysiologySnapshot source = new() { RawCapacities = [1f], MinimumCapacities = [0f], ApplicableCapacities = 1,
            Trends = [trend], TrendCount = 1, CapacityTerms = [new() { Capacity = 0, Trend = 0,
                MinimumSeverity = .5f, NextSeverity = float.MaxValue, Factor = 0f, Maximum = float.MaxValue }], CapacityTermCount = 1 };
        Assert.AreEqual(0f, PrognosisCapacity.Level(in source, 0, 20000));
        source.HoldBleedingControl = true;
        Assert.AreEqual(1f, PrognosisCapacity.Level(in source, 0, 20000));
        for (int i = 0; i < 100; i++) { _ = PrognosisCapacity.Level(in source, 0, 20000); }
        long bytes = GC.GetAllocatedBytesForCurrentThread(); _ = PrognosisCapacity.Level(in source, 0, 20000);
        Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - bytes);
    }

    [TestMethod]
    public void NativeBleedingSuppressionPreventsAutomaticConsumableUse()
    {
        var (doctor, patient, _) = Pair(1f);
        Supply(doctor, KnownThingDefOf.HemostaticAgent, 6);
        patient.health.CanBleed = false;
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, true));
        Assert.AreEqual(string.Empty, patient.Vital!.PrognosisTooltip);
    }

    [TestMethod]
    public void HeldControlAndUntendedForecastDivergeFromOneZeroBleedingSnapshotWithoutMutation()
    {
        CombatExtendedPrognosisTests.Initialize(); Pawn patient = VitalFailureTests.Patient();
        CEWoundFixture wound = Stabilized(patient, 6f);
        int rolls = Rand.RangeCalls, count = patient.health.hediffSet.hediffs.Count;
        Assert.IsTrue(patient.Vital!.TryGetPrognoses(out var source, out var held, out var untended));
        Assert.AreEqual(0f, source.BleedRate); Assert.IsTrue(source.TemporaryBleedingRisk);
        Assert.AreEqual(int.MaxValue, held.Ticks); Assert.IsTrue(untended.Ticks < 60000);
        Assert.Contains("Current control", patient.Vital.PrognosisTooltip);
        Assert.Contains("Without tending", patient.Vital.PrognosisTooltip);
        int reads = patient.health.hediffSet.CapacityReads;
        patient.Vital.TryGetPrognoses(out _, out var cachedHeld, out var cachedUntended);
        Assert.AreEqual(reads, patient.health.hediffSet.CapacityReads);
        Assert.AreEqual(held.Ticks, cachedHeld.Ticks); Assert.AreEqual(untended.Ticks, cachedUntended.Ticks);
        Assert.AreEqual(count, patient.health.hediffSet.hediffs.Count); Assert.AreEqual(rolls, Rand.RangeCalls);
        wound.Tended = true; patient.Vital.InvalidateForecast();
        patient.Vital.TryGetPrognoses(out source, out held, out untended);
        Assert.IsFalse(source.TemporaryBleedingRisk); Assert.AreEqual(int.MaxValue, untended.Ticks);
    }

    private static CEWoundFixture Stabilized(Pawn patient, float rate, bool shadow = false)
    {
        CEWoundFixture wound = shadow ? new ShadowWound() : new CEWoundFixture();
        wound.pawn = patient; wound.def = new() { injuryProps = new() { bleedRate = 1 } }; wound.Part = new(); wound.Severity = 1;
        wound.UntreatedRate = rate; wound.Stabilization = new() { parent = wound, InitialModifier = -.5f };
        wound.comps.Add(wound.Stabilization); patient.health.hediffSet.hediffs.Add(wound);
        patient.health.hediffSet.NativeBleeding = true; wound.Stabilization.Stabilize(patient, new());
        return wound;
    }
    private sealed class ShadowWound : CEWoundFixture { public override float BleedRate => 0f; }

    [TestMethod]
    public void MissingSuppressedBaselineAndNonfatalTemporaryControlRemainVisibleAndExplicit()
    {
        foreach (bool shadow in new[] { false, true })
        {
            CombatExtendedPrognosisTests.Initialize(); Pawn patient = VitalFailureTests.Patient();
            Stabilized(patient, .001f, shadow);
            patient.Vital!.TryGetPrognoses(out var source, out _, out _);
            Assert.IsTrue(source.TemporaryBleedingRisk); Assert.AreEqual(shadow, source.BleedingUncertain);
            Find.MainTabsRoot.OpenTab = MainButtonDefOf.Inspect;
            ((MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow).OpenTabType = typeof(ITab_Pawn_Health);
            Find.Selector.SingleSelectedThing = patient; Widgets.Draws = 0;
            Patch_PhysiologyDeathEstimate.Postfix(new(0, 0, 300, 600), patient, true);
            Assert.AreEqual(3, Widgets.Draws); Assert.Contains("Untended:", Widgets.LastLabel);
            Assert.DoesNotContain("Bleeding", Widgets.LastLabel);
            Assert.Contains(shadow ? "uncertain" : ">24h", Widgets.LastLabel);
            Assert.Contains("Untended wounds remain", TooltipHandler.LastTip);
            Find.MainTabsRoot.OpenTab = null;
            int reads = patient.health.hediffSet.CapacityReads; Widgets.Draws = 0;
            Patch_PhysiologyDeathEstimate.Postfix(new(0, 0, 300, 600), patient, true);
            Assert.AreEqual(0, Widgets.Draws); Assert.AreEqual(reads, patient.health.hediffSet.CapacityReads);
        }
    }
}
