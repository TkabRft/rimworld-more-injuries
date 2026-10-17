using MoreInjuries.HealthConditions.Drugs;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class DrugInformationCardTests
{
    [TestMethod]
    public void ToleranceCardCanEnumerateAllSamplesRepeatedlyWithoutAPawn()
    {
        HediffCompProperties_DrugEffectFactor tolerance = new();
        MedicalDrugHediffDef def = new()
        {
            stages = [new() { minSeverity = 0f }, new() { minSeverity = 0.5f }, new() { minSeverity = 0.8f }],
            comps = [new HediffCompProperties_SeverityPerDay { severityPerDay = -0.05f }, tolerance]
        };
        for (int render = 0; render < 3; render++)
        {
            StatDrawEntry[] entries = def.SpecialDisplayStats(default).ToArray();
            Assert.AreEqual(6, entries.Length, "Base, three stages, recovery, tolerance samples");
            Assert.AreEqual("Base", entries[0].LabelCap);
            Assert.IsTrue(entries.Last().ValueString.Contains("MI_DrugInfo_ToleranceSample", StringComparison.Ordinal));
        }
        CollectionAssert.AreEqual(new[] { 0.1f, 0.5f, 1f, 0.1f, 0.5f, 1f, 0.1f, 0.5f, 1f }, tolerance.SampledSeverities);
    }

    [TestMethod]
    public void NativeSeveritySetterFixtureReproducesThePawnlessStageChangeFailure()
    {
        HediffWithComps sample = new()
        {
            def = new() { stages = [new() { minSeverity = 0f }, new() { minSeverity = 0.5f }] }
        };
        sample.Severity = 0.1f;
        Assert.ThrowsExactly<InvalidOperationException>(() => sample.Severity = 0.5f);
    }
}
