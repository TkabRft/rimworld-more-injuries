using MoreInjuries.AI.Jobs.Outcomes.Conditions.Operators;
using MoreInjuries.Caching;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.Acidosis;
using MoreInjuries.HealthConditions.Hemodilution;
using MoreInjuries.HealthConditions.HypovolemicShock;
using System.Globalization;
using System.Xml.Linq;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class TransfusionAndAllocationTests
{
    private static float s_sink;

    [TestMethod]
    public void ProductionTimedCacheRefreshesCountsAfterBagApplicationAndExpiry()
    {
        int calculations = 0;
        Pawn patient = new();
        Hediff loss = new() { Severity = 0.8f, def = HediffDefOf.BloodLoss };
        Hediff dilution = new() { def = KnownHediffDefOf.Hemodilution };
        patient.health.hediffSet.hediffs.Add(loss);
        patient.health.hediffSet.hediffs.Add(dilution);
        var cache = new WeakTimedDataCache<Pawn, int, bool, TimedDataEntry<int>>(250, (pawn, fullyHeal) =>
        {
            calculations++;
            int needed = (int)Math.Ceiling(Math.Max(0f, loss.Severity - (fullyHeal ? 0f : 0.449f)) / 0.15f);
            return Math.Min(needed, HemodilutionEvaluator.CalculateMaximumSafeSalineTransfusions(
                dilution.Severity, loss.Severity, 0.449f, HemodilutionEvaluator.FluidVolumeFromBloodLossReduction(0.15f)));
        });
        Find.TickManager.TicksGame = 1;
        Assert.AreEqual(5, cache.GetData(patient, true));
        dilution.Severity = After(dilution.Severity, loss.Severity, 0.075f, 1f);
        loss.Severity -= 0.15f;
        Assert.AreEqual(5, cache.GetData(patient, true));
        Assert.AreEqual(1, calculations);
        // ApplyDevice explicitly removes this entry, rather than waiting for expiry.
        Assert.IsTrue(cache.RemoveData(patient));
        Assert.AreEqual(4, cache.GetData(patient, true));
        Assert.AreEqual(2, calculations);
        Find.TickManager.TicksGame = 252;
        Assert.AreEqual(4, cache.GetData(patient, true));
        Assert.AreEqual(3, calculations);
        Assert.IsTrue(cache.MarkDirty(patient));
        Assert.AreEqual(2, cache.GetData(patient, false, forceRefresh: true));
        Assert.AreEqual(4, calculations);
    }

    [TestMethod]
    public void RealOutcomeBridgeConvertsExistingXmlAmountsExactlyOnce()
    {
        XDocument blood = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs", "Internal", "OutcomeDoerTemplateDefs", "BloodBag_OutcomeDoerTemplateDef.xml"));
        XDocument fluids = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs", "ThingDefs", "Medicine", "Medicine_Transfusions.xml"));
        float bloodDose = Dose(blood);
        float salineDose = Dose(fluids);
        Assert.AreEqual(0.35f, bloodDose);
        Assert.AreEqual(0.15f, salineDose);
        Pawn patient = new();
        patient.health.hediffSet.hediffs.Add(new() { def = HediffDefOf.BloodLoss, Severity = 0.8f });
        Hediff dilution = new() { def = KnownHediffDefOf.Hemodilution };
        patient.health.hediffSet.hediffs.Add(dilution);
        var bridge = new DynamicProcedureCall_CalculateHemodilution();
        float saline = bridge.Evaluate(patient, patient, null, new State(salineDose, 1f));
        Assert.AreEqual(1f / 9f, saline, 0.000001f);
        dilution.Severity = 0.4f;
        float afterBlood = dilution.Severity + bridge.Evaluate(patient, patient, null, new State(bloodDose, -1f));
        Assert.AreEqual(0.3096774f, afterBlood, 0.000001f);
    }

    [TestMethod]
    public void SafeSalineCountsHandleFullVolumeOverflowAndRemainingState()
    {
        float volume = HemodilutionEvaluator.FluidVolumeFromBloodLossReduction(0.15f);
        Assert.AreEqual(5, HemodilutionEvaluator.CalculateMaximumSafeSalineTransfusions(0f, 0.8f, 0.449f, volume));
        Assert.AreEqual(0.45f, After(0f, 0.8f, 6 * volume, 1f), 0.000001f);
        float d = 0f, b = 0.8f;
        for (int i = 0; i < 5; i++)
        {
            Assert.AreEqual(5 - i, HemodilutionEvaluator.CalculateMaximumSafeSalineTransfusions(d, b, 0.449f, volume));
            d = After(d, b, volume, 1f);
            b = Math.Max(0f, b - 0.15f);
            Assert.IsTrue(d <= 0.449f);
        }
        Assert.AreEqual(0, HemodilutionEvaluator.CalculateMaximumSafeSalineTransfusions(d, b, 0.449f, volume));
        Assert.IsTrue(After(d, b, volume, 1f) > 0.449f);
    }

    [TestMethod]
    public void RoundedCountsAgreeWithOutcomesAcrossLossDilutionAndOverflowBoundaries()
    {
        foreach (float b in new[] { 0f, 0.05f, 0.2f, 0.449f, 0.8f, 1f })
        foreach (float d in new[] { 0f, 0.05f, 0.2f, 0.4f, 0.449f, 0.6f, 0.8f, 1f })
        foreach (float threshold in new[] { 0.05f, 0.449f, 0.8f })
        {
            int saline = HemodilutionEvaluator.CalculateMaximumSafeSalineTransfusions(d, b, threshold, 0.075f);
            Assert.IsTrue(saline >= 0);
            if (d <= threshold)
            {
                Assert.IsTrue(After(d, b, saline * 0.075f, 1f) <= threshold + 0.000001f);
            }
            Assert.IsTrue(After(d, b, (saline + 1) * 0.075f, 1f) > threshold - 0.000001f);
            int blood = HemodilutionEvaluator.CalculateMinimumRequiredBloodTransfusionsToTreatHemodilution(d, b, threshold, 0.175f);
            Assert.IsTrue(After(d, b, blood * 0.175f, -1f) <= threshold + 0.000001f);
            if (blood > 0) { Assert.IsTrue(After(d, b, (blood - 1) * 0.175f, -1f) > threshold - 0.000001f); }
        }
        Assert.AreEqual(3, HemodilutionEvaluator.CalculateMaxNeededBloodTransfusionsToTreatHemodilution(0f, 0.8f, 0.175f));
        Assert.AreEqual(3, HemodilutionEvaluator.CalculateMinimumRequiredBloodTransfusionsToTreatHemodilution(0.8f, 0.8f, 0.449f, 0.175f));
        Assert.AreEqual(0, HemodilutionEvaluator.CalculateMaximumSafeSalineTransfusions(0.8f, 0.8f, 0.449f, 0.075f));
    }

    [TestMethod]
    public void WarmNumericHelpersAllocateZeroManagedBytes()
    {
        s_sink = RunMath(10000);
        long before = GC.GetAllocatedBytesForCurrentThread();
        s_sink = RunMath(100000);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(0L, allocated);
        Assert.IsTrue(s_sink > 0f);
    }

    private static float RunMath(int iterations)
    {
        float sum = 0f;
        for (int i = 0; i < iterations; i++)
        {
            sum += ShockPerfusionModel.Advance(0.8f, 0.8f, 0.75f, 0.15f, false);
            sum += AcidosisProgressionModel.Advance(0.1f, 0.001f, 3f, 8f, 0.5f, 1f / 240f);
            sum += AcidosisProgressionModel.ShockProductionPerDay(4f, 0.3f, 0.75f, 0.015f);
            sum += HemodilutionEvaluator.CalculateHemodilutionDelta(0.2f, 0.8f, 0.075f, 1f);
            sum += HemodilutionEvaluator.CalculateMaximumSafeSalineTransfusions(0.2f, 0.8f, 0.449f, 0.075f);
            sum += HemodilutionEvaluator.CalculateMinimumRequiredBloodTransfusionsToTreatHemodilution(0.8f, 0.8f, 0.449f, 0.175f);
        }
        return sum;
    }

    private static float After(float d, float b, float amount, float factor)
        => d + HemodilutionEvaluator.CalculateHemodilutionDelta(d, b, amount, factor);

    private static float Dose(XDocument document) => float.Parse(document.Descendants("li")
        .Single(li => li.Element("symbol")?.Value == "added_volume").Element("value")!.Value, CultureInfo.InvariantCulture);

    private sealed class State(float dose, float factor) : IRuntimeState
    {
        public float ResolveRequired(string name) => name switch
        {
            DynamicProcedureCall_CalculateHemodilution.PARAMETER_ADDED_VOLUME => dose,
            DynamicProcedureCall_CalculateHemodilution.PARAMETER_DILUTION_FACTOR => factor,
            _ => throw new KeyNotFoundException(name)
        };
    }
}
