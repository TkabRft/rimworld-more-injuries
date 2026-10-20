using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.VitalFailure;
using MoreInjuries.Patches;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass, DoNotParallelize]
public sealed class PrognosisPresentationTests
{
    [TestMethod]
    [DataRow(0f, false)]
    [DataRow(.001f, false)]
    [DataRow(1f, false)]
    [DataRow(5.999f, false)]
    [DataRow(6f, true)]
    [DataRow(12f, true)]
    [DataRow(2f, true)] // A modified coma boundary still follows the stage, not a hard-coded number.
    public void DependencyRequiresACurrentLifeThreateningStage(float severity, bool lifeThreatening)
    {
        Hediff_ChemicalDependency dependency = new()
        {
            pawn = new(),
            def = new() { lethalSeverity = 12, maxSeverity = 12, stages = [new() { lifeThreatening = lifeThreatening }] },
            Severity = severity
        };
        Assert.AreEqual(lifeThreatening, PrognosisConditionPolicy.ShouldProject(dependency));
        dependency.def.stages = null;
        Assert.IsFalse(PrognosisConditionPolicy.ShouldProject(dependency));
        Assert.IsTrue(PrognosisConditionPolicy.ShouldProject(new HediffWithComps { def = dependency.def }),
            "Do not hide another condition just because its current stage is not life-threatening.");
    }

    [TestMethod]
    public void EarlyDependencyDoesNotHideAnUnrelatedMedicalEmergency()
    {
        Pawn pawn = VitalFailureTests.Patient();
        pawn.health.hediffSet.hediffs.Add(new Hediff_ChemicalDependency
        { pawn = pawn, def = new() { lethalSeverity = 12, stages = [new()] }, Severity = 1 });
        Open(pawn); Draw(pawn);
        Assert.AreEqual(0, Widgets.Draws);
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0;
        pawn.Vital!.Invalidate(); Draw(pawn);
        Assert.AreEqual(2, Widgets.Draws);
        Assert.Contains("Estimated death:", Widgets.LastLabel);
        Assert.DoesNotContain("Held:", Widgets.LastLabel);
        Assert.DoesNotContain("Untended:", Widgets.LastLabel);
        Assert.DoesNotContain("Bleeding stays controlled:", TooltipHandler.LastTip);
        Open(null);
    }

    [TestMethod]
    public void TooltipKeepsIdentityAndGetterAcrossAdvancingHealthUpdatesButChangesForAnotherPawn()
    {
        Pawn pawn = VitalFailureTests.Patient();
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0;
        Hediff brain = VitalFailureTests.Add(pawn, KnownHediffDefOf.BrainDamage_Hypoxia, .1f);
        TooltipHandler.LastSignal = default; TooltipHandler.IdentityChanges = 0;
        Find.TickManager.TicksGame = 0; Open(pawn); Draw(pawn);
        TipSignal original = TooltipHandler.LastSignal;
        string firstText = original.textGetter();
        for (int i = 1; i <= 5; i++)
        {
            Find.TickManager.TicksGame = i * 300;
            brain.Severity += .5f;
            pawn.Vital!.Invalidate(); Draw(pawn);
            Assert.AreEqual(original.uniqueId, TooltipHandler.LastSignal.uniqueId);
            Assert.AreSame(original.textGetter, TooltipHandler.LastSignal.textGetter);
            Assert.AreEqual(pawn.Vital.PrognosisTooltip, original.textGetter(), "The stable getter must show the updated forecast.");
        }
        Assert.AreNotEqual(firstText, original.textGetter());
        Assert.AreEqual(1, TooltipHandler.IdentityChanges, "Timer changes must not restart the tooltip's hover identity.");
        Pawn other = VitalFailureTests.Patient();
        other.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0;
        Open(other); Draw(other);
        Assert.AreNotEqual(original.uniqueId, TooltipHandler.LastSignal.uniqueId);
        Assert.AreEqual(2, TooltipHandler.IdentityChanges);
        Open(null);
    }

    private static void Open(Pawn? pawn)
    {
        Find.MainTabsRoot.OpenTab = pawn is null ? null : MainButtonDefOf.Inspect;
        ((MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow).OpenTabType = typeof(ITab_Pawn_Health);
        Find.Selector.SingleSelectedThing = pawn;
    }

    private static void Draw(Pawn pawn)
    {
        Widgets.Draws = 0;
        Patch_PhysiologyDeathEstimate.Postfix(new(0, 0, 400, 600), pawn, true);
    }
}
