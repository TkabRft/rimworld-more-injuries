using MoreInjuries.HealthConditions.CardiacArrest;
using MoreInjuries.HealthConditions.Choking;
using MoreInjuries.HealthConditions.VitalFailure;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass, DoNotParallelize]
public sealed class VitalFailureMenuTests
{
    [TestInitialize]
    public void Initialize()
    {
        CardiacArrestWorker.Calls = ChokingWorker.Calls = 0;
        MoreInjuriesMod.Settings.EnableChoking = true;
    }

    [TestMethod]
    public void CorpseRightClickReturnsNoLivingCareOptionsOrInvalidCast()
    {
        Pawn dead = VitalFailureTests.Patient(); dead.Dead = true;
        VitalFailureComp corpseComp = new() { parent = new Corpse { InnerPawn = dead } };
        Assert.HasCount(0, corpseComp.CompFloatMenuOptions(new Pawn()).ToList());
        Assert.AreEqual(0, CardiacArrestWorker.Calls + ChokingWorker.Calls);
    }

    [TestMethod]
    public void NonPawnOrUninitializedOwnerDoesNotEnterMedicalMenuWorkers()
    {
        foreach (object? parent in new object?[] { new object(), null })
        {
            VitalFailureComp comp = new() { parent = parent! };
            Assert.HasCount(0, comp.CompFloatMenuOptions(new Pawn()).ToList());
        }
        Assert.AreEqual(0, CardiacArrestWorker.Calls + ChokingWorker.Calls);
    }

    [TestMethod]
    public void DeadPawnDisabledModuleAndExistingInjuryPipelineKeepTheirExclusions()
    {
        foreach (int exclusion in Enumerable.Range(0, 3))
        {
            Pawn pawn = VitalFailureTests.Patient(); pawn.Tracked = false;
            if (exclusion == 0) { pawn.Dead = true; }
            if (exclusion == 1) { MoreInjuriesMod.Settings.EnableDelayedVitalFailure = false; }
            if (exclusion == 2) { pawn.Tracked = true; }
            Assert.HasCount(0, pawn.Vital!.CompFloatMenuOptions(new Pawn()).ToList());
        }
        Assert.AreEqual(0, CardiacArrestWorker.Calls + ChokingWorker.Calls);
    }

    [TestMethod]
    public void LivingFallbackPatientsStillReachEmergencyWorkersAndChokingToggle()
    {
        foreach (bool choking in new[] { true, false })
        {
            Pawn patient = VitalFailureTests.Patient(); patient.Tracked = false;
            MoreInjuriesMod.Settings.EnableChoking = choking;
            List<FloatMenuOption> options = patient.Vital!.CompFloatMenuOptions(new Pawn()).ToList();
            Assert.HasCount(choking ? 2 : 1, options);
        }
        Assert.AreEqual(2, CardiacArrestWorker.Calls); Assert.AreEqual(1, ChokingWorker.Calls);
    }

    [TestMethod]
    public void NonhumanFallbackRequiresSameFactionAndMedicalPermission()
    {
        Pawn patient = VitalFailureTests.Patient(); patient.Tracked = false; patient.RaceProps.Humanlike = false;
        Pawn doctor = new() { Faction = new() }; patient.Faction = new();
        Assert.HasCount(0, patient.Vital!.CompFloatMenuOptions(doctor).ToList());
        patient.Faction = doctor.Faction;
        Assert.HasCount(2, patient.Vital.CompFloatMenuOptions(doctor).ToList());
        patient.playerSettings = new() { medCare = RimWorld.MedicalCareCategory.NoCare };
        Assert.HasCount(0, patient.Vital.CompFloatMenuOptions(doctor).ToList());
    }
}
