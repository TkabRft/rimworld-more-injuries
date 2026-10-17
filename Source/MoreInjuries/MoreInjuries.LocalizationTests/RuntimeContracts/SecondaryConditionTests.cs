using MoreInjuries.HealthConditions.Secondary;
using MoreInjuries.HealthConditions.Secondary.Handlers;
using System.Runtime.CompilerServices;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
[DoNotParallelize]
public sealed class SecondaryConditionTests
{
    private static readonly int[] s_intervals = [250, 60, 60, 60];
    [TestMethod]
    public void MixedIntervalsPreserveBaselineDispatchAndRandomSequence()
    {
        foreach (int offset in new[] { 0, 1, 9, 59, 249, 391, 7919 })
        {
            foreach (bool curveEnabled in new[] { false, true })
            {
                CollectionAssert.AreEqual(RunBaseline(offset, curveEnabled), RunProduction(offset, curveEnabled, zeroInitialized: false), $"Offset {offset}; curve {curveEnabled}");
            }
        }
    }

    [TestMethod]
    public void ZeroInitializedPropertiesMustNotSilentlyDisableEveryHandler()
    {
        // XML materializers can populate an object without running its field initializers.
        // A default-zero int cache must not mean 'no handlers' before computing the schedule.
        CollectionAssert.AreEqual(RunBaseline(391, curveEnabled: true), RunProduction(391, curveEnabled: true, zeroInitialized: true));
    }

    [TestMethod]
    public void EmptyScheduleDoesNotEvaluateChance()
    {
        HediffCompProperties_SecondaryCondition properties = new();
        HediffComp_SecondaryCondition comp = new() { props = properties, parent = new Hediff { pawn = new Pawn() } };
        Rand.ChanceHandler = _ => throw new AssertFailedException("Empty schedule consumed random state");
        float adjustment = 0;
        comp.CompPostTick(ref adjustment);
    }

    private static string[] RunBaseline(int offset, bool curveEnabled)
    {
        // The dispatch loop from 077997d, before adding the wake gate.
        Random random = new(12345);
        List<string> events = [];
        for (int tick = 0; tick < 15000; tick++)
        {
            bool processed = false;
            for (int index = 0; index < s_intervals.Length; index++)
            {
                int interval = s_intervals[index];
                if ((tick + offset) % interval != 0) { continue; }
                if (!processed && curveEnabled && random.NextDouble() >= 0.7) { break; }
                events.Add($"{tick}:{index}:{interval}:{random.NextDouble():R}");
                processed = true;
            }
        }
        return [.. events];
    }

    private static string[] RunProduction(int offset, bool curveEnabled, bool zeroInitialized)
    {
        Random random = new(12345);
        List<string> events = [];
        Pawn pawn = new() { HashOffset = offset };
        HediffCompProperties_SecondaryCondition properties = zeroInitialized
            ? (HediffCompProperties_SecondaryCondition)RuntimeHelpers.GetUninitializedObject(typeof(HediffCompProperties_SecondaryCondition))
            : new();
        properties.TestHandlers = [.. s_intervals.Select((interval, index) => new RecordingHandler(interval, () => events.Add($"{pawn.Tick}:{index}:{interval}:{random.NextDouble():R}")))];
        properties.TestSeverityCurve = curveEnabled ? new SimpleCurve(_ => 0.7f) : null;
        Rand.ChanceHandler = chance => random.NextDouble() < chance;
        HediffComp_SecondaryCondition comp = new() { props = properties, parent = new Hediff { pawn = pawn } };
        float adjustment = 0;
        for (pawn.Tick = 0; pawn.Tick < 15000; pawn.Tick++) { comp.CompPostTick(ref adjustment); }
        return [.. events];
    }

    private sealed class RecordingHandler(int interval, Action record) : IHediffComp_SecondaryCondition_TickHandler
    {
        public int TickInterval => interval;
        public void Handle(HediffComp_SecondaryCondition comp) => record();
    }
}
