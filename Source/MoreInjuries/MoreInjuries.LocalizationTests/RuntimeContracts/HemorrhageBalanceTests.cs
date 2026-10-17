using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.Acidosis;
using MoreInjuries.HealthConditions.HypovolemicShock;
using MoreInjuries.HealthConditions.Secondary;
using MoreInjuries.HealthConditions.Secondary.Handlers;
using MoreInjuries.HealthConditions.Secondary.Handlers.Modifiers;
using MoreInjuries.HealthConditions.Secondary.Linked;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
[DoNotParallelize]
public sealed class HemorrhageBalanceTests
{
    [TestInitialize]
    public void Reset()
    {
        MoreInjuriesMod.Settings.EnableHypovolemicShock = true;
        MoreInjuriesMod.Settings.EnableAdvancedTraumaSimulation = true;
        MoreInjuriesMod.Settings.HypoxiaAcidosisConversionFactor = 0.015f;
        ModsConfig.BiotechActive = ModsConfig.AnomalyActive = false;
    }

    [TestMethod]
    public void PoorTendingReachesSevereCoagulopathyWindowAndGoodCareBuysTime()
    {
        foreach (int offset in new[] { 0, 59, 249, 7919 })
        {
            foreach ((float quality, float expected) in new[] { (0.15f, 5.3f), (0.3f, 5.9f), (0.8f, 9f) })
            {
                Result result = Simulate(quality, offset: offset);
                Assert.IsNotNull(result.SeriousAtHour);
                Assert.AreEqual(expected, result.SeriousAtHour.Value, 0.2f, $"quality {quality}, offset {offset}");
            }
        }
        Assert.IsNull(Simulate(1f).SeriousAtHour);
    }

    [TestMethod]
    public void BloodAndForcedSalineAtTwoHoursStopProductionBeforeSeriousAcidosis()
    {
        foreach (float reduction in new[] { 0.35f, 0.15f })
        {
            Result rescued = Simulate(0.15f, fluidReduction: reduction);
            Assert.IsNull(rescued.SeriousAtHour);
            Assert.IsTrue(rescued.Peak < 0.2f);
            Assert.AreEqual(0f, rescued.FinalAcidosis);
        }
        Result adrenaline = Simulate(0.15f, giveAdrenaline: true);
        Assert.IsNotNull(adrenaline.SeriousAtHour);
        Assert.AreEqual(6.1f, adrenaline.SeriousAtHour.Value, 0.2f);
    }

    [TestMethod]
    public void ModerateLossDoesNotCreateAcidosisAndFullLossDeterioratesFaster()
    {
        Assert.AreEqual(0f, Simulate(0.15f, bloodLoss: 0.6f, initialShock: 0.5f).Peak);
        Assert.IsTrue(Simulate(0.15f, bloodLoss: 1f, initialShock: 1f).SeriousAtHour < 2f);
    }

    [TestMethod]
    public void NativeQualityExpiryRetendingAndCompatibilityFlagAreIndependent()
    {
        Pawn pawn = CreatePawn(0.8f, 0.8f, 0.15f);
        Hediff shock = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.HypovolemicShock)!;
        var modifier = new HediffModifier_TendQuality { TestAtFullQuality = new ConstantModifier(0.5f) };
        Assert.AreEqual(0.925f, modifier.GetModifier(shock, null!), 0.00001f);
        var tend = shock.TryGetComp<HediffComp_TendDuration>()!;
        tend.tendQuality = 0.3f;
        Assert.AreEqual(0.85f, modifier.GetModifier(shock, null!), 0.00001f);
        tend.tendTicksLeft = 0;
        Assert.AreEqual(1f, modifier.GetModifier(shock, null!));
        tend.tendTicksLeft = 10000;
        tend.tendQuality = 2f;
        Assert.AreEqual(0.5f, modifier.GetModifier(shock, null!));
        tend.tendQuality = 0f;
        Assert.AreEqual(1f, modifier.GetModifier(shock, null!));
        var comp = shock.GetComp<HediffComp_Shock>()!;
        comp.FixedNow = true;
        float previous = shock.Severity;
        float adjustment = 0f;
        comp.CompPostTick(ref adjustment);
        Assert.AreEqual(ShockPerfusionModel.Advance(previous, 0.8f, 0f, 0f, false), shock.Severity);
    }

    [TestMethod]
    public void CreationAndOngoingProductionRespectGuardsWithoutDuplicatesOrFreeTick()
    {
        for (int guard = 0; guard < 7; guard++)
        {
            Reset();
            Pawn pawn = CreatePawn(0.8f, 0.8f, 0.15f);
            if (guard == 0) { pawn.OxygenImmune = true; }
            if (guard == 1) { MoreInjuriesMod.Settings.EnableHypovolemicShock = false; }
            if (guard == 2) { MoreInjuriesMod.Settings.EnableAdvancedTraumaSimulation = false; }
            if (guard == 3) { MoreInjuriesMod.Settings.HypoxiaAcidosisConversionFactor = 0f; }
            if (guard == 4) { pawn.Tracked = false; }
            if (guard == 5)
            {
                ModsConfig.BiotechActive = true;
                pawn.health.hediffSet.hediffs.Add(new() { pawn = pawn, def = HediffDefOf.Deathrest });
            }
            if (guard == 6)
            {
                pawn.Tracked = false; pawn.RaceProps.IsAnomalyEntity = true; ModsConfig.AnomalyActive = true;
            }
            Tick(pawn, tickAcidosis: false);
            Hediff? acid = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.Acidosis);
            if (guard != 6) { Assert.IsNull(acid, $"guard {guard}"); continue; }
            Assert.IsNotNull(acid);
            Assert.AreEqual(0.001f, acid.Severity, "Initialization must retain the condition without a free production tick.");
            Tick(pawn, tickAcidosis: false);
            Assert.AreEqual(1, pawn.health.hediffSet.hediffs.Count(h => h.def == KnownHediffDefOf.Acidosis));
            acid.GetComp<HediffComp_Acidosis>()!.Tick(false);
            Assert.IsTrue(acid.Severity > 0.001f);
            Assert.Contains("hypovolemic shock", acid.GetComp<HediffComp_CausedBy>()!.Causes);
            float previous = acid.Severity;
            MoreInjuriesMod.Settings.EnableAdvancedTraumaSimulation = false;
            acid.GetComp<HediffComp_Acidosis>()!.Tick(false);
            Assert.IsTrue(acid.Severity < previous || !pawn.health.hediffSet.hediffs.Contains(acid));
        }
    }

    [TestMethod]
    public void AdrenalineDoesNotLowerTargetAndTransfusionStopsSystemicProductionImmediately()
    {
        Assert.AreEqual(0.85f, ShockPerfusionModel.Advance(0.85f, 0.8f, 1f, 1f, false), 0.00001f);
        Pawn pawn = CreatePawn(0.8f, 0.8f, 0.15f);
        Tick(pawn);
        Hediff acid = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.Acidosis)!;
        acid.Severity = 0.2f;
        pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)!.Severity = 0.45f;
        acid.GetComp<HediffComp_Acidosis>()!.Tick(false);
        Assert.AreEqual(0.2f - 8f / 240f, acid.Severity, 0.00001f);
    }

    [TestMethod]
    public void ControllersRetain150And250TickGatesAndNativeTreatmentAcrossLoad()
    {
        Pawn pawn = CreatePawn(0.8f, 0.8f, 0.3f);
        Hediff shock = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.HypovolemicShock)!;
        HediffComp_Shock comp = shock.GetComp<HediffComp_Shock>()!;
        comp.FixedNow = true;
        var saved = Scribe.Save(comp.CompExposeData);
        Assert.IsTrue(saved.Keys.SequenceEqual(new[] { "fixedNow" }));
        var restored = new HediffComp_Shock { parent = shock, props = comp.props };
        Scribe.Load(saved, restored.CompExposeData);
        Assert.IsTrue(restored.FixedNow);
        var tend = shock.GetComp<HediffComp_TendDuration>()!;
        var nativeTreatment = Scribe.Save(() =>
        {
            Scribe_Values.Look(ref tend.tendQuality, "tendQuality");
            Scribe_Values.Look(ref tend.tendTicksLeft, "tendTicksLeft");
        });
        tend.tendQuality = 0f; tend.tendTicksLeft = 0;
        Scribe.Load(nativeTreatment, () =>
        {
            Scribe_Values.Look(ref tend.tendQuality, "tendQuality");
            Scribe_Values.Look(ref tend.tendTicksLeft, "tendTicksLeft");
        });
        Assert.AreEqual(0.3f, ShockTreatment.Quality(shock));
        pawn.Tick = 149;
        Tick(pawn);
        Assert.AreEqual(0.8f, shock.Severity);
        Assert.IsNull(pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.Acidosis));
        pawn.Tick = 150;
        Tick(pawn);
        Hediff acid = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.Acidosis)!;
        Assert.AreEqual(0.001f, acid.Severity);
        pawn.Tick = 249;
        Tick(pawn);
        Assert.AreEqual(0.001f, acid.Severity);
        pawn.Tick = 250;
        Tick(pawn);
        Assert.IsTrue(acid.Severity > 0.001f);
    }

    [TestMethod]
    public void ExistingAcidosisStopsShockProductionForEveryCompatibilityGuard()
    {
        for (int guard = 0; guard < 6; guard++)
        {
            Reset();
            Pawn pawn = CreatePawn(0.8f, 0.8f, 0.15f);
            Tick(pawn);
            Hediff acid = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.Acidosis)!;
            acid.Severity = 0.2f;
            if (guard == 0) { pawn.OxygenImmune = true; }
            if (guard == 1) { MoreInjuriesMod.Settings.EnableHypovolemicShock = false; }
            if (guard == 2) { MoreInjuriesMod.Settings.EnableAdvancedTraumaSimulation = false; }
            if (guard == 3) { MoreInjuriesMod.Settings.HypoxiaAcidosisConversionFactor = 0f; }
            if (guard == 4) { pawn.Tracked = false; }
            if (guard == 5)
            {
                ModsConfig.BiotechActive = true;
                pawn.health.hediffSet.hediffs.Add(new() { pawn = pawn, def = HediffDefOf.Deathrest });
            }
            acid.GetComp<HediffComp_Acidosis>()!.Tick(false);
            Assert.AreEqual(0.2f - 8f / 240f, acid.Severity, 0.00001f, $"guard {guard}");
        }
    }

    [TestMethod]
    public void LegacyInjuryHistoryAndSaveLoadContinueWithoutNewState()
    {
        Pawn pawn = CreatePawn(0.8f, 0.8f, 0.3f);
        var extension = new LinkedSeverityProperties_ModExtension();
        extension.LinkedSeverityHandlers[KnownHediffDefOf.Acidosis] = new(hediff => hediff.Severity * 0.015f);
        var injury = new Hediff { pawn = pawn, def = new() { Extension = extension, Label = "hypoxia" }, Severity = 4f };
        pawn.health.hediffSet.hediffs.Add(injury);
        Tick(pawn);
        Hediff acid = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.Acidosis)!;
        var comp = acid.GetComp<HediffComp_Acidosis>()!;
        var saved = Scribe.Save(comp.CompExposeData);
        Assert.IsTrue(saved.Keys.Order().SequenceEqual(new[] { "previousSeverity", "severityHistory" }));
        var restored = new HediffComp_Acidosis { parent = acid, props = comp.props };
        Scribe.Load(saved, restored.CompExposeData);
        injury.Severity += 4f;
        float before = acid.Severity;
        comp.Tick(false);
        float continuous = acid.Severity;
        acid.Severity = before;
        restored.Tick(false);
        Assert.AreEqual(continuous, acid.Severity, 0.000001f);
        Assert.AreEqual(0.206f, AcidosisProgressionModel.Advance(0.2f, 0.006f, 0f, 8f, 0.5f, 1f / 240f), 0.000001f);
        Assert.AreEqual(0.1666667f, AcidosisProgressionModel.Advance(0.2f, 0f, 0f, 8f, 0.5f, 1f / 240f), 0.000001f);
        // max, not sum: 0.04 injury growth beats 0.02 systemic production.
        Assert.AreEqual(0.2379167f, AcidosisProgressionModel.Advance(0.2f, 0.04f, 4.8f, 8f, 0.5f, 1f / 240f), 0.000001f);
    }

    private static Pawn CreatePawn(float bloodLoss, float shockSeverity, float quality)
    {
        Pawn pawn = new() { Tracked = true };
        pawn.health.hediffSet.hediffs.Add(new() { pawn = pawn, def = HediffDefOf.BloodLoss, Severity = bloodLoss });
        Hediff shock = new() { pawn = pawn, def = KnownHediffDefOf.HypovolemicShock, Severity = shockSeverity };
        shock.TestComps.Add(new HediffComp_Shock { parent = shock, props = new HediffCompProperties_Shock() });
        shock.TestComps.Add(new HediffComp_TendDuration { parent = shock, tendQuality = quality, tendTicksLeft = 10000 });
        pawn.health.hediffSet.hediffs.Add(shock);
        return pawn;
    }

    private static void Tick(Pawn pawn, bool tickAcidosis = true)
    {
        float adjustment = 0f;
        pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.HypovolemicShock)?.GetComp<HediffComp_Shock>()?.CompPostTick(ref adjustment);
        if (tickAcidosis)
        {
            pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.Acidosis)?.GetComp<HediffComp_Acidosis>()?.CompPostTick(ref adjustment);
        }
    }

    private static Result Simulate(float quality, int offset = 0, float bloodLoss = 0.8f, float initialShock = 0.8f,
        float fluidReduction = 0f, bool giveAdrenaline = false)
    {
        Pawn pawn = CreatePawn(bloodLoss, initialShock, quality);
        pawn.HashOffset = offset;
        Hediff loss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)!;
        Hediff shock = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.HypovolemicShock)!;
        HediffComp_TendDuration tend = shock.GetComp<HediffComp_TendDuration>()!;
        float? serious = null; float peak = 0f;
        for (int tick = 1; tick <= 30000; tick++)
        {
            pawn.Tick = tick;
            if (pawn.IsHashIntervalTick(60)) { loss.Severity = Math.Max(0f, loss.Severity - 0.00033333333f); }
            if (tick == 5000)
            {
                loss.Severity = Math.Max(0f, loss.Severity - fluidReduction);
                if (giveAdrenaline) { pawn.health.hediffSet.hediffs.Add(new() { pawn = pawn, def = KnownHediffDefOf.AdrenalineRush, Severity = 0.75f }); }
            }
            if (pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.AdrenalineRush) is { } adrenaline)
            {
                adrenaline.Severity = Math.Max(0f, adrenaline.Severity - 6f / 60000f);
            }
            tend.tendTicksLeft = Math.Max(0, 10000 - tick);
            Tick(pawn);
            float severity = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.Acidosis)?.Severity ?? 0f;
            peak = Math.Max(peak, severity);
            if (severity >= 0.4f && serious is null) { serious = tick / 2500f; }
        }
        return new(serious, peak, pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.Acidosis)?.Severity ?? 0f);
    }

    private sealed record Result(float? SeriousAtHour, float Peak, float FinalAcidosis);
    private sealed class ConstantModifier(float factor) : SecondaryHediffModifier
    {
        public override float GetModifier(Hediff hediff, IHediffCompHandler handler) => factor;
    }
}
