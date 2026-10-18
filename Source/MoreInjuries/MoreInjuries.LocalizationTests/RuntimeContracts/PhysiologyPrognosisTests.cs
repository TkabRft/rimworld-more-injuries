using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.BrainDamage;
using MoreInjuries.HealthConditions.HypovolemicShock;
using MoreInjuries.HealthConditions.VitalFailure;
using MoreInjuries.Patches;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class PhysiologyPrognosisTests
{
    private static PhysiologySnapshot Baseline()
    {
        _ = VitalFailureTests.Patient();
        return new()
        {
            Relevant = true, VitalEnabled = true, CanHaveBrain = true, CanDevelopShock = true, ShockPresent = true,
            BloodLoss = 1, BleedRate = .1f, Shock = 1, Breathing = 1, Pumping = 1,
            ColdProtection = 1, ComaMultiplier = 1,
            NextBloodTick = 60, NextShockTick = 150, NextVitalTick = 250, Failure = new(int.MaxValue, null)
        };
    }

    private static PhysiologyEstimate Forecast(PhysiologySnapshot input) => PhysiologyPrognosis.Estimate(in input,
        VitalFailureDefOf.MI_VitalFailureConfig, HediffDefOf.BloodLoss, KnownHediffDefOf.BrainDamage_Hypoxia);

    private static float ProductionTime(float initialShock, float quality, bool natural, bool coma, int offset)
    {
        Pawn pawn = VitalFailureTests.Patient(offset); Rand.RangeFraction = .5f;
        Hediff blood = VitalFailureTests.Add(pawn, HediffDefOf.BloodLoss, 1);
        Hediff shock = VitalFailureTests.Add(pawn, KnownHediffDefOf.HypovolemicShock, initialShock);
        HediffComp_TendDuration tend = new() { parent = shock, tendQuality = quality, tendTicksLeft = 10000 };
        shock.TestComps.Add(tend);
        HediffComp_Shock shockComp = new() { parent = shock, props = new HediffCompProperties_Shock() };
        if (coma) { VitalFailureTests.Add(pawn, NeurologicalDefOf.MI_ArtificialComa, .001f); }
        for (int tick = 1; tick <= 60000; tick++)
        {
            pawn.Tick = tick; tend.tendTicksLeft = Math.Max(0, 10000 - tick);
            if (natural && pawn.IsHashIntervalTick(60)) { blood.Severity = Math.Max(0, blood.Severity - .00033333333f); }
            float adjustment = 0; shockComp.CompPostTick(ref adjustment);
            pawn.Vital!.Invalidate(); VitalFailureTests.Tick(pawn, tick);
            if (pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.BrainDamage_Hypoxia)?.CauseDeathNow() == true)
            { return tick / 2500f; }
        }
        Assert.Fail("No terminal cerebral injury within the controlled horizon."); return 0;
    }

    [TestMethod]
    public void ProductionControllersAndForecastMatchControlledBalanceTargets()
    {
        foreach ((float shock, float quality, bool natural, bool coma, float expected) in new[]
        {
            (1f, 0f, false, false, 1.5f), (0f, 0f, false, false, 2.4f), (0f, 0f, true, false, 2.6f),
            (1f, .3f, false, false, 1.8f), (1f, 0f, false, true, 3f)
        })
        {
            PhysiologySnapshot input = Baseline(); input.Shock = shock; input.TendQuality = quality; input.TendTicks = 10000;
            input.BleedRate = natural ? 0f : .1f; input.ComaMultiplier = coma ? .5f : 1f;
            float forecast = Forecast(input).Ticks / 2500f;
            Assert.AreEqual(expected, forecast, .15f);
            foreach (int offset in new[] { 0, 37, 249 })
            { Assert.AreEqual(forecast, ProductionTime(shock, quality, natural, coma, offset), .15f); }
        }
    }

    [TestMethod]
    public void CombinedSourcesUseMaximumAndTreatmentChangesTheUnderlyingTrend()
    {
        PhysiologySnapshot input = Baseline(); float initial = Forecast(input).Ticks;
        input.Breathing = 0; input.Pumping = 0; input.Dilution = 1;
        Assert.AreEqual(initial, Forecast(input).Ticks);
        input.ColdProtection = .45f; Assert.IsTrue(Forecast(input).Ticks > initial * 2);
        input = Baseline(); input.ClinicalBrain = 4;
        int before = Forecast(input).Ticks;
        input.BloodLoss = .2f; input.BleedRate = 0;
        Assert.AreEqual(int.MaxValue, Forecast(input).Ticks, "Restored volume stops production before displayed shock clears.");
        input.BleedRate = 20; Assert.IsTrue(Forecast(input).Ticks < int.MaxValue, "Resumed bleeding can recreate a fatal trend.");
        input = Baseline(); input.ClinicalBrain = 4; input.Adrenaline = .75f; input.AdrenalineDecayPerDay = 6;
        Assert.IsTrue(Forecast(input).Ticks > before);
    }

    [TestMethod]
    public void QualityExpiryAndAdrenalineDecayAreProjectedRatherThanFrozen()
    {
        PhysiologySnapshot input = Baseline(); input.TendQuality = 1; input.TendTicks = 10000;
        int supported = Forecast(input).Ticks;
        input.TendTicks = 500; Assert.IsTrue(Forecast(input).Ticks < supported);
        input = Baseline(); input.Adrenaline = .75f;
        int frozen = Forecast(input).Ticks;
        input.AdrenalineDecayPerDay = 6; Assert.IsTrue(Forecast(input).Ticks < frozen);
    }

    [TestMethod]
    public void EarliestFailureAndDirectBloodLossPolicyDetermineTheDisplayedCause()
    {
        PhysiologySnapshot input = Baseline(); input.DirectBloodDeath = true;
        PhysiologyEstimate result = Forecast(input); Assert.AreEqual(0, result.Ticks); Assert.AreSame(HediffDefOf.BloodLoss, result.Cause);
        input.DirectBloodDeath = false; input.Failure = new(500, VitalFailureDefOf.MI_LiverFailure);
        result = Forecast(input); Assert.AreEqual(500, result.Ticks); Assert.AreSame(VitalFailureDefOf.MI_LiverFailure, result.Cause);
        input.OxygenProtected = true; input.Failure = new(120000, VitalFailureDefOf.MI_RenalFailure);
        Assert.AreEqual(120000, Forecast(input).Ticks, "Long existing organ timers are not truncated to the cerebral horizon.");
        input.Protected = true; Assert.AreEqual(int.MaxValue, Forecast(input).Ticks);
    }

    [TestMethod]
    public void ReadOnlySnapshotAndCacheNeverAddFailuresConsumeRngOrReplayHealth()
    {
        Pawn pawn = VitalFailureTests.Patient(); Find.TickManager.TicksGame = pawn.Tick = 0;
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0;
        int count = pawn.health.hediffSet.hediffs.Count, reads = pawn.health.hediffSet.CapacityReads, rolls = Rand.RangeCalls;
        Assert.IsTrue(pawn.Vital!.TryGetPrognosis(out _, out PhysiologyEstimate first));
        Assert.AreEqual(count, pawn.health.hediffSet.hediffs.Count);
        Assert.AreEqual(rolls, Rand.RangeCalls);
        Assert.IsTrue(first.Ticks < int.MaxValue);
        int cachedReads = pawn.health.hediffSet.CapacityReads; Assert.IsTrue(cachedReads > reads);
        Find.TickManager.TicksGame = pawn.Tick = 100;
        pawn.Vital.TryGetPrognosis(out _, out PhysiologyEstimate second);
        Assert.AreEqual(first.Ticks - 100, second.Ticks); Assert.AreEqual(cachedReads, pawn.health.hediffSet.CapacityReads);
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 1; pawn.Vital.Invalidate();
        pawn.Vital.TryGetPrognosis(out _, out second); Assert.AreEqual(int.MaxValue, second.Ticks);
        Assert.AreEqual(count, pawn.health.hediffSet.hediffs.Count); Assert.AreEqual(rolls, Rand.RangeCalls);
    }

    [TestMethod]
    public void ProtectedNonAirBreathingExcludedAndAnimalBloodLossFollowExistingRules()
    {
        Pawn pawn = VitalFailureTests.Patient(); Find.TickManager.TicksGame = pawn.Tick = 0;
        pawn.IsMutant = true; pawn.mutant.Def.breathesAir = false;
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0;
        Assert.IsFalse(pawn.Vital!.TryGetPrognosis(out _, out _));
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 0; pawn.Vital.Invalidate();
        Assert.IsTrue(pawn.Vital.TryGetPrognosis(out _, out var output)); Assert.IsTrue(output.Ticks < int.MaxValue);
        pawn.health.hediffSet.HasPreventsDeath = true; pawn.Vital.Invalidate();
        pawn.Vital.TryGetPrognosis(out _, out output); Assert.AreEqual(int.MaxValue, output.Ticks);
        pawn.Suspended = true; pawn.Vital.Invalidate(); Assert.IsFalse(pawn.Vital.TryGetPrognosis(out _, out _));
        pawn = VitalFailureTests.Patient(); pawn.Tracked = false; MoreInjuriesMod.Settings.PreventDeathByBloodLoss = true;
        VitalFailureTests.Add(pawn, HediffDefOf.BloodLoss, 1); pawn.Vital!.TryGetPrognosis(out _, out output);
        Assert.AreEqual(0, output.Ticks); Assert.AreSame(HediffDefOf.BloodLoss, output.Cause);
    }

    [TestMethod]
    public void UiToggleAndReplacementUiGuardRestoreTheOriginalWithoutReadingHealth()
    {
        Pawn pawn = VitalFailureTests.Patient(); VitalFailureTests.Add(pawn, HediffDefOf.BloodLoss, 1);
        Widgets.Draws = 0; int reads = pawn.health.hediffSet.CapacityReads;
        MoreInjuriesMod.Settings.EnablePhysiologyDeathEstimate = false;
        Patch_PhysiologyDeathEstimate.Postfix(new(0, 0, 300, 600), pawn, true);
        MoreInjuriesMod.Settings.EnablePhysiologyDeathEstimate = true;
        Patch_PhysiologyDeathEstimate.Postfix(new(0, 0, 300, 600), pawn, false);
        Current.ProgramState = ProgramState.Entry;
        Patch_PhysiologyDeathEstimate.Postfix(new(0, 0, 300, 600), pawn, true);
        Current.ProgramState = ProgramState.Playing;
        Assert.AreEqual(0, Widgets.Draws); Assert.AreEqual(reads, pawn.health.hediffSet.CapacityReads);
        GUI.color = new(7); Patch_PhysiologyDeathEstimate.Postfix(new(0, 0, 300, 600), pawn, true);
        Assert.AreEqual(2, Widgets.Draws); Assert.AreEqual(new Color(7), GUI.color);
        string source = File.ReadAllText(Path.Combine(RuntimeDefinitionTests.ModRoot, "Source/MoreInjuries/MoreInjuries/Patches/Patch_PhysiologyDeathEstimate.cs"));
        Assert.DoesNotContain("TicksUntilDeathDueToBloodLoss", source);
    }

    [TestMethod]
    public void WarmNumericForecastAndMilestoneConsumptionAllocateNothing()
    {
        PhysiologySnapshot input = Baseline(); input.Shock = 0;
        NeurologicalEpisode episode = new() { ClinicalInjury = 2 };
        var milestones = NeurologicalDefOf.MI_NeurologicalConfig.InjuryMilestones;
        for (int i = 0; i < 100; i++) { _ = Forecast(input); episode.Consume(milestones, 1, true); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        int sum = 0;
        for (int i = 0; i < 1000; i++) { sum += Forecast(input).Ticks; episode.Consume(milestones, 1, true); }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(0L, allocated); Assert.IsTrue(sum > 0);
    }
}
