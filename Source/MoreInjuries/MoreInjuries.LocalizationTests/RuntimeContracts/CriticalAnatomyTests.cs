using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.Secondary;
using MoreInjuries.HealthConditions.VitalFailure;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class CriticalAnatomyTests
{
    [TestMethod]
    public void MissingHeartCannotAcquireArrestButStillCausesCerebralHypoxia()
    {
        foreach (bool delayed in new[] { false, true })
        {
            Pawn pawn = VitalFailureTests.Patient();
            MoreInjuriesMod.Settings.EnableDelayedVitalFailure = delayed;
            pawn.RaceProps.body.AllParts[1].Missing = true;
            pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 0f;
            VitalFailureTests.Add(pawn, KnownHediffDefOf.CardiacArrest, .2f);
            Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
            if (!delayed) { continue; }
            VitalFailureTests.Tick(pawn, 0);
            Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
            Assert.AreEqual(1f, pawn.Vital!.Deprivation, .00001);
            Assert.IsTrue(pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.BrainDamage_Hypoxia)!.Severity > 0f);
            Assert.IsFalse(pawn.Vital.CanRestoreCirculation());
        }
    }

    [TestMethod]
    public void HeartLossClearsBothMechanicalAndSpontaneousArrestWithoutErasingBrainInjury()
    {
        foreach (bool mechanical in new[] { false, true })
        {
            Pawn pawn = VitalFailureTests.Patient();
            Hediff brain = VitalFailureTests.Add(pawn, KnownHediffDefOf.BrainDamage_Hypoxia, 2f);
            Hediff arrest = VitalFailureTests.Add(pawn, KnownHediffDefOf.CardiacArrest, .4f);
            arrest.GetComp<HediffComp_VitalArrest>()!.MechanicalFailure = mechanical;
            pawn.RaceProps.body.AllParts[1].Missing = true;
            pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 0f;
            pawn.Vital!.Refresh();
            Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
            Assert.AreEqual(2f, brain.Severity);
            Assert.AreEqual(1f, pawn.Vital.Deprivation, .00001);
        }
    }

    [TestMethod]
    public void PresentNonfunctionalAndAdditionalPumpsStillPermitArrest()
    {
        Pawn pawn = VitalFailureTests.Patient();
        BodyPartRecord missing = pawn.RaceProps.body.AllParts[1];
        missing.Missing = true;
        BodyPartRecord extra = new() { def = new() { tags = [BodyPartTagDefOf.BloodPumpingSource] }, Efficiency = 0f };
        pawn.RaceProps.body.AllParts.Add(extra);
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 0f;
        pawn.Vital!.Refresh();
        Assert.IsTrue(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
        extra.Missing = true;
        pawn.Vital.Invalidate();
        pawn.Vital.Refresh();
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
        extra.Missing = false;
        extra.Efficiency = 1f;
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 1f;
        pawn.Vital.Invalidate();
        pawn.Vital.Refresh();
        Assert.AreEqual(0f, pawn.Vital.Deprivation);
        Assert.IsTrue(pawn.Vital.CanRestoreCirculation());
    }

    [TestMethod]
    public void ForecastDoesNotInventArrestForMissingPumpOrMutateStaleArrest()
    {
        Pawn pawn = VitalFailureTests.Patient();
        Hediff stale = VitalFailureTests.Add(pawn, KnownHediffDefOf.CardiacArrest, .9f);
        pawn.RaceProps.body.AllParts[1].Missing = true;
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 0f;
        Find.MainTabsRoot.OpenTab = MainButtonDefOf.Inspect;
        ((MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow).OpenTabType = typeof(ITab_Pawn_Health);
        Find.Selector.SingleSelectedThing = pawn;
        try
        {
            pawn.Vital!.TryGetPrognosis(out PhysiologySnapshot snapshot, out PhysiologyEstimate estimate);
            Assert.IsTrue(snapshot.PumpAbsent);
            Assert.IsFalse(snapshot.Arrest);
            Assert.IsFalse(snapshot.RestartableHeart);
            Assert.AreEqual(0f, snapshot.Pumping);
            Assert.AreSame(KnownHediffDefOf.BrainDamage_Hypoxia, estimate.Cause);
            PrognosisFailures failures = snapshot.Failures;
            failures.Advance(in snapshot, 250);
            Assert.AreEqual(0f, failures.Get(6));
            Assert.IsTrue(pawn.health.hediffSet.hediffs.Contains(stale));
            Assert.AreEqual(.9f, stale.Severity);
        }
        finally { Find.MainTabsRoot.OpenTab = null; Find.Selector.SingleSelectedThing = null; }
    }

    [TestMethod]
    public void MissingOrgansStillCauseSystemicFailuresButAbsentSpeciesGroupsDoNot()
    {
        Pawn pawn = VitalFailureTests.Patient();
        foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts.Skip(2)) { part.Missing = true; }
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0f;
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodFiltration] = 0f;
        pawn.Vital!.Refresh();
        Assert.IsTrue(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_RespiratoryFailure));
        Assert.IsTrue(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_LiverFailure));
        Assert.IsTrue(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_RenalFailure));
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_FiltrationFailure));
        Pawn alien = VitalFailureTests.Patient();
        alien.RaceProps.body.AllParts.RemoveRange(1, 4);
        alien.Vital!.Refresh();
        Assert.IsFalse(alien.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
        Assert.IsFalse(alien.health.hediffSet.HasHediff(VitalFailureDefOf.MI_LiverFailure));
        Assert.IsFalse(alien.health.hediffSet.HasHediff(VitalFailureDefOf.MI_RenalFailure));
    }

    [TestMethod]
    public void FailedOrganLookupCannotBecomeWholeBodyCondition()
    {
        Pawn pawn = VitalFailureTests.Patient();
        HediffSet set = pawn.health.hediffSet;
        BodyPartRecord organ = pawn.RaceProps.body.AllParts[1];
        Assert.IsTrue(SecondaryConditionTarget.IsValid(set, organ, allowWholeBody: false));
        organ.Missing = true;
        Assert.IsFalse(SecondaryConditionTarget.IsValid(set, organ, allowWholeBody: false));
        Assert.IsFalse(SecondaryConditionTarget.IsValid(set, organ, allowWholeBody: true));
        Assert.IsFalse(SecondaryConditionTarget.IsValid(set, null, allowWholeBody: false));
        Assert.IsTrue(SecondaryConditionTarget.IsValid(set, null, allowWholeBody: true));
    }
}
