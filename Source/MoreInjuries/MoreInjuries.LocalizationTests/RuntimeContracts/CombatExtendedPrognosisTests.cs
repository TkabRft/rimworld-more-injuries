using System.Reflection;
using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.VitalFailure;
using MoreInjuries.Integrations.CombatExtended;
using MoreInjuries.Patches;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass, DoNotParallelize]
public sealed class CombatExtendedPrognosisTests
{
    internal static void Initialize(bool active = true, bool unsupported = false, bool registered = true)
    {
        foreach (string name in new[] { "s_active", "s_available", "s_verified", "s_logged", "s_treatmentResolved" })
        { typeof(CombatExtendedBridge).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, false); }
        ((List<MethodInfo>)typeof(CombatExtendedBridge).GetField("s_getters", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Clear();
        Harmony.Patches.Clear(); Harmony.RegisteredCE = registered;
        AccessTools.UnsupportedCE = unsupported; ModsConfig.CombatExtendedActive = active;
        CombatExtendedBridge.Initialize();
    }

    [TestCleanup]
    public void Cleanup() => Initialize(false);

    private static CEWoundFixture Wound(Pawn pawn, float rate, float modifier, bool inside = false, bool missing = false, bool stabilize = true)
    {
        CEWoundFixture wound = missing ? new Hediff_MissingPart() : new CEWoundFixture();
        wound.pawn = pawn; wound.def = new() { label = "wound" }; wound.Severity = 1;
        wound.UntreatedRate = rate; wound.Part = new() { depth = inside ? BodyPartDepth.Inside : BodyPartDepth.Outside };
        wound.Stabilization = new() { parent = wound, InitialModifier = modifier };
        wound.comps.Add(wound.Stabilization);
        pawn.health.hediffSet.hediffs.Add(wound); pawn.health.hediffSet.NativeBleeding = true;
        if (stabilize) { wound.Stabilization.Stabilize(pawn, new()); }
        return wound;
    }

    private static PhysiologySnapshot Capture(Pawn pawn, int nextTick = 60)
    {
        PhysiologySnapshot source = new() { BleedRate = pawn.health.hediffSet.BleedRateTotal, NextBloodTick = nextTick,
            NextShockTick = 150, NextVitalTick = 250, ColdProtection = 1, ComaMultiplier = 1, Breathing = 1, Pumping = 1,
            Failure = new(int.MaxValue, null) };
        CombatExtendedBleedingCapture? capture = null;
        CombatExtendedBridge.Capture(pawn, ref source, ref capture);
        return source;
    }

    [TestMethod]
    public void ProjectionMatchesIterativeCeExternalInternalMissingAndNegativeCourses()
    {
        Initialize();
        foreach (float modifier in new[] { -.75f, 0f, .35f, .99f })
        foreach (bool inside in new[] { false, true })
        foreach (bool missing in new[] { false, true })
        {
            Pawn pawn = VitalFailureTests.Patient(); pawn.HealthScale = 2;
            CEWoundFixture wound = Wound(pawn, 3, modifier, inside, missing);
            PhysiologySnapshot source = Capture(pawn);
            Assert.AreEqual(1, source.BleedingCount);
            int hediffs = pawn.health.hediffSet.hediffs.Count, rolls = Rand.RangeCalls;
            for (int tick = 1; tick <= 12000; tick++)
            {
                pawn.Tick = tick;
                float adjustment = 0; wound.Stabilization!.CompPostTickInterval(ref adjustment, 1);
                Assert.AreEqual(pawn.health.hediffSet.BleedRateTotal, PrognosisBleeding.Total(in source, tick), .00001f,
                    $"modifier={modifier}, inside={inside}, missing={missing}, tick={tick}");
            }
            Assert.AreEqual(hediffs, pawn.health.hediffSet.hediffs.Count);
            Assert.AreEqual(rolls, Rand.RangeCalls);
        }
        TestPatch observer = Harmony.Patches.Values.SelectMany(info => info.Postfixes)
            .First(patch => patch.owner == "Th3Fr3d.MoreInjuries.CombatExtended" && patch.Patch.method.Name == nameof(CombatExtendedBridge.ObserveBleeding));
        Assert.Contains(CombatExtendedBridge.CE_HARMONY_OWNER, observer.Patch.before);
        Assert.AreEqual(Priority.First, observer.Patch.priority);
    }

    [TestMethod]
    public void MixedBleedingAndNativeSuppressionPreserveCurrentTotalAndIndependentWounds()
    {
        Initialize(); Pawn pawn = VitalFailureTests.Patient(); pawn.HealthScale = .5f;
        Wound(pawn, 2, -.2f); Wound(pawn, 1, .3f, inside: true);
        CEWoundFixture ordinary = Wound(pawn, 3, 1f);
        PhysiologySnapshot source = Capture(pawn);
        Assert.AreEqual(2, source.BleedingCount);
        Assert.AreEqual(pawn.health.hediffSet.BleedRateTotal, PrognosisBleeding.Total(in source, 0), .00001f);
        Assert.AreEqual((2f + 1f + ordinary.UntreatedRate) / pawn.HealthScale, PrognosisBleeding.Total(in source, 12000), .00001f);
        pawn.health.hediffSet.AggregateBleedFactor = .25f; source = Capture(pawn);
        Assert.AreEqual(3f, PrognosisBleeding.Total(in source, 12000), .00001f);
        pawn.health.hediffSet.AggregateBleedFactor = 0; source = Capture(pawn);
        Assert.AreEqual(0f, PrognosisBleeding.Total(in source, 12000));
        pawn.health.CanBleed = false; source = Capture(pawn);
        Assert.AreEqual(0, source.BleedingCount); Assert.AreEqual(0f, PrognosisBleeding.Total(in source, 12000));
    }

    [TestMethod]
    public void ReturningBleedingFeedsShockAndCerebralForecastWithoutAllocatingNumericWork()
    {
        Initialize(); Pawn pawn = VitalFailureTests.Patient(); Wound(pawn, 5f, -.2f);
        PhysiologySnapshot source = Capture(pawn);
        source.BloodLoss = .3f; source.VitalEnabled = true; source.CanHaveBrain = true; source.CanDevelopShock = true;
        PhysiologyEstimate estimate = PhysiologyPrognosis.Estimate(in source, VitalFailureDefOf.MI_VitalFailureConfig,
            HediffDefOf.BloodLoss, KnownHediffDefOf.BrainDamage_Hypoxia);
        Assert.IsTrue(estimate.Ticks < PhysiologyPrognosis.HORIZON);
        Assert.AreSame(KnownHediffDefOf.BrainDamage_Hypoxia, estimate.Cause);
        PhysiologySnapshot frozen = source; frozen.BleedingCount = 0;
        Assert.AreEqual(int.MaxValue, PhysiologyPrognosis.Estimate(in frozen, VitalFailureDefOf.MI_VitalFailureConfig,
            HediffDefOf.BloodLoss, KnownHediffDefOf.BrainDamage_Hypoxia).Ticks);
        float rate = 0;
        for (int tick = 0; tick < 12000; tick++) { rate += PrognosisBleeding.Total(in source, tick); }
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int tick = 0; tick < 12000; tick++) { rate += PrognosisBleeding.Total(in source, tick); }
        Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - allocated);
        Assert.IsTrue(rate > 0f);
    }

    [TestMethod]
    public void ZeroBleedingReturnsAndDeathEstimateMatchesDeterministicCeSequenceAcrossPawnPhases()
    {
        Initialize();
        foreach (int offset in new[] { 0, 7, 59 })
        {
            Pawn pawn = VitalFailureTests.Patient(offset); CEWoundFixture wound = Wound(pawn, 4f, -.2f);
            PhysiologySnapshot source = Capture(pawn, 60 - offset);
            Assert.AreEqual(0f, source.BleedRate); Assert.IsTrue(source.Relevant);
            source.BloodLoss = .4f; source.DirectBloodDeath = true;
            PhysiologyEstimate estimate = PhysiologyPrognosis.Estimate(in source, VitalFailureDefOf.MI_VitalFailureConfig,
                HediffDefOf.BloodLoss, KnownHediffDefOf.BrainDamage_Hypoxia);
            float blood = .4f; int death = int.MaxValue;
            for (int tick = 1; tick <= PhysiologyPrognosis.HORIZON; tick++)
            {
                pawn.Tick = tick; float adjustment = 0;
                wound.Stabilization!.CompPostTickInterval(ref adjustment, 1);
                if (!pawn.IsHashIntervalTick(60, 1)) { continue; }
                float rate = pawn.health.hediffSet.BleedRateTotal;
                blood = Math.Clamp(blood + (rate >= .1f ? rate * .001f : -.00033333333f), 0f, 1f);
                if (blood >= 1f) { death = tick; break; }
            }
            Assert.IsTrue(death < int.MaxValue);
            Assert.AreEqual(death, estimate.Ticks, 60);
            Assert.AreSame(HediffDefOf.BloodLoss, estimate.Cause);
            PhysiologySnapshot frozen = source; frozen.BleedingCount = 0;
            Assert.AreEqual(int.MaxValue, PhysiologyPrognosis.Estimate(in frozen, VitalFailureDefOf.MI_VitalFailureConfig,
                HediffDefOf.BloodLoss, KnownHediffDefOf.BrainDamage_Hypoxia).Ticks);
        }
    }

    [TestMethod]
    public void ApplicationDecayTendingHealingAndNewWoundsInvalidateVisibleCachedForecast()
    {
        Initialize(); Pawn pawn = VitalFailureTests.Patient(); Find.TickManager.TicksGame = pawn.Tick = 0;
        CEWoundFixture wound = Wound(pawn, 5, -.5f, stabilize: false);
        pawn.Vital!.TryGetPrognosis(out PhysiologySnapshot source, out _);
        Assert.AreEqual(5f, source.BleedRate);
        int reads = pawn.health.hediffSet.CapacityReads;
        pawn.Vital.TryGetPrognosis(out _, out _); Assert.AreEqual(reads, pawn.health.hediffSet.CapacityReads);
        wound.Stabilization!.Stabilize(pawn, new());
        pawn.Vital.TryGetPrognosis(out source, out _); Assert.AreEqual(0f, source.BleedRate);
        Assert.IsTrue(pawn.health.hediffSet.CapacityReads > reads);
        reads = pawn.health.hediffSet.CapacityReads;
        pawn.Tick = Find.TickManager.TicksGame = 60;
        float adjustment = 0; wound.Stabilization.CompPostTickInterval(ref adjustment, 1);
        pawn.Vital.TryGetPrognosis(out _, out _); Assert.IsTrue(pawn.health.hediffSet.CapacityReads > reads);
        wound.Tended = true; Patch_VitalFailure_Dirty.Postfix(pawn);
        pawn.Vital.TryGetPrognosis(out source, out _); Assert.AreEqual(0, source.BleedingCount); Assert.AreEqual(0f, source.BleedRate);
        wound.Tended = false; wound.Severity = 0; Patch_VitalFailure_Dirty.Postfix(pawn);
        pawn.Vital.TryGetPrognosis(out source, out _); Assert.AreEqual(0, source.BleedingCount);
        Wound(pawn, 2, .5f); pawn.Vital.TryGetPrognosis(out source, out _); Assert.AreEqual(1, source.BleedingCount);
        Assert.AreEqual(1f, source.BleedRate);
        Pawn closed = VitalFailureTests.Patient(); CEWoundFixture closedWound = Wound(closed, 2, -.2f);
        closed.Tick = 60; closedWound.Stabilization!.CompPostTickInterval(ref adjustment, 1);
        Assert.AreEqual(0, closed.health.hediffSet.CapacityReads);
        Assert.AreEqual(string.Empty, closed.Vital!.PrognosisTooltip);
    }

    [TestMethod]
    public void ReconstructedSaveStateProducesSameForecastWithoutSerializingAdapterData()
    {
        Initialize(); Pawn first = VitalFailureTests.Patient(); Wound(first, 3, -.3f, inside: true, missing: true);
        Pawn reloaded = VitalFailureTests.Patient(); Wound(reloaded, 3, -.3f, inside: true, missing: true);
        PhysiologySnapshot before = Capture(first), after = Capture(reloaded);
        for (int tick = 0; tick <= 12000; tick += 60)
        { Assert.AreEqual(PrognosisBleeding.Total(in before, tick), PrognosisBleeding.Total(in after, tick)); }
    }

    [TestMethod]
    public void CeAbsentUnsupportedLayoutAndMissingCeRegistrationUseCurrentBleedingFallback()
    {
        Initialize(false); Pawn pawn = VitalFailureTests.Patient(); pawn.health.hediffSet.BleedRateTotal = .7f;
        PhysiologySnapshot source = Capture(pawn);
        Assert.AreEqual(.7f, source.BleedRate); Assert.AreEqual(0, source.BleedingCount); Assert.IsFalse(source.UnmodeledRisk);
        int warnings = Log.Warnings;
        Initialize(unsupported: true); source = Capture(pawn); _ = Capture(pawn);
        Assert.AreEqual(.7f, source.BleedRate); Assert.IsTrue(source.UnmodeledRisk); Assert.AreEqual(warnings + 1, Log.Warnings);
        Initialize(registered: false); source = Capture(pawn);
        Assert.IsTrue(source.UnmodeledRisk); Assert.AreEqual(0, source.BleedingCount);
    }

    [TestMethod]
    public void FooterHidesNonfatalResidualBloodLossButRetainsFatalNonbleedingAndFutureBleeding()
    {
        Initialize(); Pawn pawn = VitalFailureTests.Patient(); Find.TickManager.TicksGame = pawn.Tick = 0;
        Hediff blood = VitalFailureTests.Add(pawn, HediffDefOf.BloodLoss, .2f);
        Open(pawn); Draw(pawn); Assert.AreEqual(0, Widgets.Draws);
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0; pawn.Vital!.Invalidate();
        Draw(pawn); Assert.AreEqual(3, Widgets.Draws); Assert.Contains("Held:", Widgets.LastLabel);
        Assert.DoesNotContain("Bleeding", Widgets.LastLabel);
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 1; pawn.Vital.Invalidate();
        blood.Severity = 1; Hediff shock = VitalFailureTests.Add(pawn, KnownHediffDefOf.HypovolemicShock, 1);
        Draw(pawn); Assert.AreEqual(3, Widgets.Draws); Assert.DoesNotContain("Bleeding", Widgets.LastLabel);
        blood.Severity = .2f; pawn.health.hediffSet.hediffs.Remove(shock); pawn.Vital.Invalidate();
        Wound(pawn, 6f, -.2f); Draw(pawn);
        Assert.AreEqual(3, Widgets.Draws); Assert.DoesNotContain("Bleeding", Widgets.LastLabel);
        Assert.Contains("stabilization is temporary", TooltipHandler.LastTip);
        Open(null); int reads = pawn.health.hediffSet.CapacityReads;
        Draw(pawn); Assert.AreEqual(0, Widgets.Draws); Assert.AreEqual(reads, pawn.health.hediffSet.CapacityReads);
    }

    private static void Open(Pawn? pawn)
    {
        Find.MainTabsRoot.OpenTab = pawn is null ? null : MainButtonDefOf.Inspect;
        ((MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow).OpenTabType = typeof(ITab_Pawn_Health);
        Find.Selector.SingleSelectedThing = pawn;
    }

    private static void Draw(Pawn pawn)
    { Widgets.Draws = 0; Patch_PhysiologyDeathEstimate.Postfix(new(0, 0, 300, 600), pawn, true); }
}
