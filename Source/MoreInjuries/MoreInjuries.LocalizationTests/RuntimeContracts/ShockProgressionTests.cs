using MoreInjuries.HealthConditions.HypovolemicShock;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
[DoNotParallelize]
public sealed class ShockProgressionTests
{
    [TestInitialize]
    public void Reset()
    {
        MoreInjuriesMod.Settings.EnableHypovolemicShock = true;
        ModsConfig.BiotechActive = false;
    }

    [TestMethod]
    public void TargetIsContinuousAndDonationStaysBelowOnset()
    {
        Assert.AreEqual(0f, ShockPerfusionModel.TargetSeverity(0.449f));
        Assert.AreEqual(0f, ShockPerfusionModel.TargetSeverity(0.45f));
        Assert.AreEqual(0.5f, ShockPerfusionModel.TargetSeverity(0.6f), 0.00001f);
        Assert.AreEqual(0.85f, ShockPerfusionModel.TargetSeverity(0.8f), 0.00001f);
        Assert.AreEqual(1f, ShockPerfusionModel.TargetSeverity(1f), 0.00001f);
        float previous = 0f;
        for (int i = 0; i <= 10000; i++)
        {
            float target = ShockPerfusionModel.TargetSeverity(i / 10000f);
            Assert.IsTrue(target >= previous && target - previous < 0.00034f);
            previous = target;
        }
    }

    [TestMethod]
    public void ShockRisesGraduallyAndStopsAtDeficitTarget()
    {
        foreach (float bloodLoss in new[] { 0.5f, 0.6f, 0.8f, 0.9f, 1f })
        {
            HediffComp_Shock shock = CreateShock(0.01f, bloodLoss);
            float target = ShockPerfusionModel.TargetSeverity(bloodLoss);
            for (int i = 0; i < 200; i++)
            {
                float previous = shock.parent.Severity;
                Tick(shock);
                Assert.IsTrue(shock.parent.Severity >= previous);
                Assert.IsTrue(shock.parent.Severity - previous <= 0.060001f);
                Assert.IsTrue(shock.parent.Severity <= target + 0.00001f);
            }
            Assert.AreEqual(target, shock.parent.Severity, 0.00001f);
        }
    }

    [TestMethod]
    public void StoppingBleedingAloneLeavesDeficitButResuscitationPermitsRecovery()
    {
        HediffComp_Shock shock = CreateShock(0.7f, 0.9f);
        Tick(shock);
        Assert.IsTrue(shock.parent.Severity > 0.7f);
        shock.Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)!.Severity = 0.1f;
        for (int i = 0; i < 200; i++) { Tick(shock); }
        Assert.AreEqual(0f, shock.parent.Severity);
    }

    [TestMethod]
    public void TendingAndAdrenalineSupportButNeverReplaceMissingVolume()
    {
        HediffComp_Shock untreated = CreateShock(0.01f, 0.9f);
        HediffComp_Shock treated = CreateShock(0.01f, 0.9f);
        treated.parent.Tended = true;
        treated.CompTended(1f, 1f);
        Assert.IsTrue(treated.FixedNow);
        treated.Pawn.health.hediffSet.hediffs.Add(new Hediff
        {
            pawn = treated.Pawn, def = Defs.WellKnown.KnownHediffDefOf.AdrenalineRush, Severity = 1f
        });
        Tick(untreated);
        Tick(treated);
        Assert.IsTrue(treated.parent.Severity < untreated.parent.Severity);
        for (int i = 0; i < 200; i++) { Tick(treated); }
        Assert.IsTrue(treated.parent.Severity > 0.5f, "Support cannot erase a critical volume deficit.");
        treated.parent.Tended = false;
        Tick(treated);
        Assert.IsFalse(treated.FixedNow, "An old tend cannot permanently stabilize subsequent hemorrhage.");
    }

    [TestMethod]
    public void FeatureToggleDeathrestAndImmunityAreRespected()
    {
        HediffComp_Shock disabled = CreateShock(0.7f, 1f);
        MoreInjuriesMod.Settings.EnableHypovolemicShock = false;
        Tick(disabled);
        Assert.IsTrue(disabled.parent.Severity < 0.7f);
        MoreInjuriesMod.Settings.EnableHypovolemicShock = true;
        ModsConfig.BiotechActive = true;
        HediffComp_Shock deathresting = CreateShock(0.7f, 1f);
        deathresting.Pawn.health.hediffSet.hediffs.Add(new Hediff { def = HediffDefOf.Deathrest });
        Tick(deathresting);
        Assert.IsTrue(deathresting.parent.Severity < 0.7f);
        HediffComp_Shock immune = CreateShock(0.7f, 0.8f);
        immune.Pawn.OxygenImmune = true;
        immune.Pawn.Tick = 1;
        Tick(immune);
        Assert.DoesNotContain(immune.parent, immune.Pawn.health.hediffSet.hediffs);
    }

    [TestMethod]
    public void ExistingSevereShockNeverSnapsToNewTargetAndUsesPawnOffset()
    {
        HediffComp_Shock shock = CreateShock(0.9f, 0.6f);
        shock.Pawn.HashOffset = 59;
        shock.Pawn.Tick = 0;
        Tick(shock);
        Assert.AreEqual(0.9f, shock.parent.Severity);
        shock.Pawn.Tick = 91;
        Tick(shock);
        Assert.AreEqual(0.8925f, shock.parent.Severity, 0.00001f);
        for (int i = 0; i < 100; i++) { Tick(shock); }
        Assert.AreEqual(0.5f, shock.parent.Severity, 0.00001f);
    }

    private static void Tick(HediffComp_Shock shock)
    {
        float adjustment = 0;
        shock.CompPostTick(ref adjustment);
    }

    private static HediffComp_Shock CreateShock(float severity, float bloodLoss)
    {
        Pawn pawn = new();
        Hediff condition = new() { pawn = pawn, Severity = severity, def = new HediffDef() };
        pawn.health.hediffSet.hediffs.Add(condition);
        pawn.health.hediffSet.hediffs.Add(new Hediff { pawn = pawn, def = HediffDefOf.BloodLoss, Severity = bloodLoss });
        return new HediffComp_Shock { parent = condition, props = new HediffCompProperties_Shock() };
    }
}
