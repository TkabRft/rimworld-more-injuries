using System.Globalization;
using System.Xml.Linq;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.VitalFailure;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class ExpandedPrognosisTests
{
    private static PhysiologySnapshot Stable()
    {
        _ = VitalFailureTests.Patient();
        return new()
        {
            Relevant = true, VitalEnabled = true, CanHaveBrain = true, Breathing = 1, Pumping = 1,
            ColdProtection = 1, ComaMultiplier = 1, NextBloodTick = 60, NextShockTick = 150, NextVitalTick = 250,
            Failure = new(int.MaxValue, null)
        };
    }

    private static PhysiologyEstimate Estimate(PhysiologySnapshot source) => PhysiologyPrognosis.Estimate(in source,
        VitalFailureDefOf.MI_VitalFailureConfig, HediffDefOf.BloodLoss, KnownHediffDefOf.BrainDamage_Hypoxia);

    private static void Timers(ref PhysiologySnapshot source)
    {
        source.ManageFailures = true;
        source.FailureRates = [8, 1, .2f, 1, .34f, .2f, 8];
        source.FailureDefs = [VitalFailureDefOf.MI_RespiratoryFailure, VitalFailureDefOf.MI_LiverFailure,
            VitalFailureDefOf.MI_RenalFailure, VitalFailureDefOf.MI_FiltrationFailure, VitalFailureDefOf.MI_MetabolicFailure,
            VitalFailureDefOf.MI_Coma, KnownHediffDefOf.CardiacArrest];
    }

    [TestMethod]
    public void ResolvedFailuresDisappearAndProspectiveFailuresCompeteWithCerebralInjury()
    {
        PhysiologySnapshot source = Stable(); Timers(ref source);
        source.Failures.Set(0, .99f);
        Assert.AreEqual(int.MaxValue, Estimate(source).Ticks);
        source.Breathing = 0;
        Assert.AreSame(VitalFailureDefOf.MI_RespiratoryFailure, Estimate(source).Cause);
        source.Failures.Set(0, 0f);
        Assert.AreSame(KnownHediffDefOf.BrainDamage_Hypoxia, Estimate(source).Cause);
        Assert.AreEqual(3750, Estimate(source).Ticks);
        source.CanHaveBrain = false;
        Assert.AreEqual(7500, Estimate(source).Ticks);
        source.Breathing = 1; source.CanHaveBrain = true; source.RenalFailed = true;
        Assert.AreEqual(300000, Estimate(source).Ticks, 250);
    }

    [TestMethod]
    public void ReturningCapacityCancelsFailureAndStopsProductionWithoutErasingDamage()
    {
        PhysiologySnapshot source = Stable(); Timers(ref source);
        source.RawCapacities = [1, 1, 1, 1, 1]; source.MinimumCapacities = [0, 0, 0, 0, 0]; source.ApplicableCapacities = 31;
        source.Trends = [new() { Severity = .52f, Maximum = 1, Rate = -1, ImmunityTicks = -1 }]; source.TrendCount = 1;
        source.CapacityTerms = [new() { Capacity = 0, Trend = 0, MinimumSeverity = .5f, NextSeverity = float.MaxValue, Factor = 1, Maximum = 0 }];
        source.CapacityTermCount = 1; source.Failures.Set(0, .1f);
        Assert.AreEqual(0f, PrognosisCapacity.Level(in source, 0, 0));
        Assert.AreEqual(1f, PrognosisCapacity.Level(in source, 0, 1500));
        Assert.AreEqual(int.MaxValue, Estimate(source).Ticks);
        source.ClinicalBrain = 7.9f;
        Assert.AreEqual(250, Estimate(source).Ticks);
    }

    [TestMethod]
    public void ImmunityAndTendingExpiryChangeDiseaseDeadlines()
    {
        PrognosisTrend trend = new() { Severity = .2f, Maximum = 1, Terminal = 1, Rate = .84f,
            TendedRate = -.53f * .8f, TendTicks = 10000, ImmunityTicks = 30000, ImmuneRate = -.7f, Interval = 200, NextTick = 200 };
        Assert.AreEqual(int.MaxValue, trend.UntilTerminal());
        Assert.IsTrue(trend.At(60000) < trend.At(30000));
        trend.Severity = .95f; trend.TendedRate = 0; trend.ImmunityTicks = 10000;
        Assert.AreEqual(3600, trend.UntilTerminal());
        trend.Severity = .8f; trend.TendedRate = -.53f; trend.TendTicks = 6000; trend.ImmunityTicks = -1;
        Assert.AreEqual(18200, trend.UntilTerminal(), 200);
    }

    [TestMethod]
    public void XmlLethalTrendsAndStageRateChangesUseActualThresholds()
    {
        XElement wet = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs/HediffDefs/Hediffs_Ischemia.xml"))
            .Root!.Elements().Single(node => node.Element("defName")?.Value == "GangreneWet");
        float rate = float.Parse(wet.Descendants("severityPerDay").Single().Value, CultureInfo.InvariantCulture);
        PrognosisTrend trend = new() { Severity = .5f, Maximum = 1, Terminal = float.Parse(wet.Element("lethalSeverity")!.Value, CultureInfo.InvariantCulture),
            StageRate = rate, ImmunityTicks = -1, Interval = 200, NextTick = 200 };
        Assert.AreEqual(40000, trend.UntilTerminal());
        trend.Cause = new() { stages = [new() { severityGainFactor = 1 }, new() { minSeverity = .5f, severityGainFactor = 2 }] };
        trend.Severity = 0; trend.StageRate = 1;
        Assert.AreEqual(45000, trend.UntilTerminal(), 200);
        Assert.AreEqual(1f, trend.At(45000), .001f);
    }

    [TestMethod]
    public void DilutionRecoveryCanPreventTheProjectedCerebralDeath()
    {
        PhysiologySnapshot source = Stable(); source.Dilution = .455f; source.ClinicalBrain = 7.99f;
        Assert.IsTrue(Estimate(source).Ticks < int.MaxValue);
        source.DilutionDecayPerDay = .333f;
        Assert.AreEqual(int.MaxValue, Estimate(source).Ticks);
    }

    [TestMethod]
    public void NativeExposureRatesAndTemperatureRecoveryAreBounded()
    {
        Assert.AreEqual(.75f, NativeExposureModel.Cold(1), .0001f);
        Assert.AreEqual(.375f, NativeExposureModel.Heat(1), .0001f);
        Assert.AreEqual(10f, NativeExposureModel.Vacuum(1, .5f));
        Assert.AreEqual(0f, NativeExposureModel.Vacuum(1, 1));
        Assert.AreEqual(0f, NativeExposureModel.Vacuum(.49f, 0));
        Assert.AreEqual(.1f, NativeExposureModel.Toxic(1, .5f, .5f), .0001f);
        PrognosisTrend cold = new() { Severity = .8f, Maximum = 1, TemperatureRecovery = true, NextTick = 60 };
        Assert.AreEqual(.785f, cold.At(60), .00001f);
        Assert.AreEqual(0f, cold.At(60000));
    }

    [TestMethod]
    public void StructuralLimitProtectionExpiryAndNativeCapacityDeathHaveDistinctOutcomes()
    {
        PhysiologySnapshot source = Stable(); source.Breathing = 0;
        int ordinary = Estimate(source).Ticks;
        source.CerebralStructuralLimit = 4;
        Assert.IsTrue(Estimate(source).Ticks < ordinary);
        source.CerebralStructuralLimit = 0; source.Protected = true; source.ProtectionTicks = 5000;
        Assert.AreEqual(5000, Estimate(source).Ticks);
        source.ProtectionTicks = 0; Assert.AreEqual(int.MaxValue, Estimate(source).Ticks);
        source.Protected = false; source.NativeComaProtection = true; Assert.AreEqual(int.MaxValue, Estimate(source).Ticks);
        source.NativeComaProtection = false; source.VitalEnabled = false; source.NativeDeathCapacityMask = 1;
        source.NativeCapacityThresholds = [0, 0, 0, 0, 0]; source.CapacityLabels = ["breathing", "pumping", "consciousness", "filtration", "metabolism"];
        PhysiologyEstimate result = Estimate(source);
        Assert.AreEqual(0, result.Ticks); Assert.AreEqual("breathing", result.CapacityLabel);
    }

    [TestMethod]
    public void NewNumericalForecastPathsAllocateNothingAfterWarmup()
    {
        PhysiologySnapshot source = Stable(); Timers(ref source); source.RenalFailed = true;
        source.Trends = [new() { Severity = .2f, Maximum = 1, Terminal = 1, Rate = 1, ImmunityTicks = 10000, ImmuneRate = -.5f }];
        source.TrendCount = 1;
        for (int i = 0; i < 100; i++) { _ = Estimate(source); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) { _ = Estimate(source); }
        Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [TestMethod]
    public void RecoveryDuringProtectionDoesNotLeaveAFakeFatalDeadline()
    {
        PhysiologySnapshot source = Stable(); source.Protected = true; source.ProtectionTicks = 10000;
        source.Trends = [new() { Severity = .95f, Maximum = 1, Terminal = 1, Rate = .84f,
            ImmunityTicks = 6000, ImmuneRate = -.7f, Interval = 200, NextTick = 200 }]; source.TrendCount = 1;
        Assert.AreEqual(int.MaxValue, Estimate(source).Ticks);
        source.Trends = null; source.TrendCount = 0; source.ClinicalBrain = 8.1f;
        source.BrainNaturalRecoveryPerDay = 8f; source.NextHealingTick = 600;
        Assert.AreEqual(int.MaxValue, Estimate(source).Ticks);
        source.BrainNaturalRecoveryPerDay = 0f;
        Assert.AreEqual(10000, Estimate(source).Ticks);
    }
}
