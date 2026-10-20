using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.Drugs;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.HarmonyTests;

[TestClass, DoNotParallelize]
public sealed class NpcDetDrugTests
{
    private const string OWNER = "MoreInjuries.test.DetDrugs";

    [TestInitialize]
    public void Initialize()
    {
        MoreInjuriesMod.Settings = new();
        ModsConfig.DetActive = true;
        ModsConfig.IdeologyActive = ModsConfig.BiotechActive = false;
        DefDatabase<ThingDef>.Definitions.Clear();
        Register("HerbalPowder", "HerbalPowderHediff", .95f, 1.2f);
        Register("StimpackIndustrial", "IndustrialStimpackHediff", .85f, 1.8f, .5f);
        Register("StimpackUltratech", "GlitterStimpackHediff", .5f, 3f, .30f);
        ThingDef mbh = Register("DV_AdrenalineShot", "DV_AdrenalineShotHediff", 1f, -1f);
        mbh.ingestible.outcomeDoers[0].hediffDef.stages[0].capMods = [
            new() { capacity = PawnCapacityDefOf.Moving, offset = .2f },
            new() { capacity = PawnCapacityDefOf.Breathing, offset = .3f },
            new() { capacity = PawnCapacityDefOf.BloodPumping, offset = .15f }];
    }

    [TestCleanup]
    public void Cleanup()
    {
        new Harmony(OWNER).UnpatchAll(OWNER);
        ModsConfig.DetActive = false;
        DefDatabase<ThingDef>.Definitions.Clear();
    }

    private static ThingDef Register(string name, string effect, float pain, float healing, float bleeding = -1f)
    {
        ThingDef def = new() { defName = name, ingestible = new() { drugCategory = DrugCategory.Medical,
            outcomeDoers = [new() { severity = 1f, hediffDef = new() { defName = effect,
                stages = [new() { painFactor = pain, naturalHealingFactor = healing, totalBleedFactor = bleeding }] } }] } };
        def.Drug.chemical = null;
        def.Drug.overdoseSeverityOffset = new(.08f, .14f);
        DefDatabase<ThingDef>.Definitions[name] = def;
        return def;
    }
    private static ThingDef Drug(string name) => DefDatabase<ThingDef>.GetNamedSilentFail(name)!;
    private static Pawn Wounded()
    {
        Pawn pawn = new() { RawPain = 0f };
        pawn.health.hediffSet.hediffs.Add(new Hediff_Injury { pawn = pawn, def = new(), Severity = 5f });
        return pawn;
    }
    private static void AddEffect(Pawn patient, ThingDef drug) => patient.health.hediffSet.hediffs.Add(new()
    { pawn = patient, def = drug.ingestible.outcomeDoers[0].hediffDef, Severity = 1f });
    private static Thing Pocket(Pawn pawn, string name, int count = 1)
    {
        Thing item = new() { def = Drug(name), stackCount = count, ParentHolder = pawn.inventory, Spawned = false };
        item.IngestionEffect = patient => AddEffect(patient, item.def);
        pawn.inventory.innerContainer.Items.Add(item);
        return item;
    }
    private static void Patch() => new Harmony(OWNER).CreateClassProcessor(typeof(NpcMedicalIngestionPatch)).Patch();

    [TestMethod]
    public void OptionalDefinitionsAreUnavailableWithoutThePackageAndRequireTheirExpectedOutcome()
    {
        Assert.HasCount(3, NpcMedicalDrugs.Restoratives.ToList());
        ModsConfig.DetActive = false;
        Assert.IsEmpty(NpcMedicalDrugs.Restoratives);
        Assert.IsFalse(NpcMedicalDrugs.Booster(Drug("DV_AdrenalineShot")));
        ModsConfig.DetActive = true;
        Drug("StimpackIndustrial").ingestible.outcomeDoers[0].hediffDef.defName = "ChangedByAnotherMod";
        Assert.IsFalse(NpcPainProjection.TryDose(Wounded(), Drug("StimpackIndustrial"), out _, out _, NpcDrugPurpose.Restorative));
    }

    [TestMethod]
    [DataRow("HerbalPowder")]
    [DataRow("StimpackIndustrial")]
    [DataRow("StimpackUltratech")]
    public void PainFreeWoundsReceiveHealingButEquivalentExistingEffectsRejectTopUps(string name)
    {
        Pawn patient = Wounded();
        Thing item = Pocket(patient, name);
        MoreInjuriesMod.Settings.NpcPainCare = MoreInjuriesMod.Settings.NpcPerformanceDrugs = false;
        Assert.IsTrue(NpcRestorativeCare.Evaluate(patient, patient, item, out NpcPainDose dose, out _));
        Assert.IsTrue(dose.Benefit > 0f);
        Assert.HasCount(1, patient.health.hediffSet.hediffs, "Projection must not add real effects.");
        AddEffect(patient, item.def);
        Assert.IsFalse(NpcRestorativeCare.Evaluate(patient, patient, item, out _, out _));
    }

    [TestMethod]
    public void HealingNeedsAHealableInjuryAndBleedingReliefNeedsActualBleeding()
    {
        Pawn patient = new() { RawPain = 0f };
        Thing powder = Pocket(patient, "HerbalPowder"), stim = Pocket(patient, "StimpackIndustrial");
        Assert.IsFalse(NpcRestorativeCare.Evaluate(patient, patient, powder, out _, out _));
        patient.health.hediffSet.hediffs.Add(new Hediff_Injury { pawn = patient, def = new(), Severity = 5f, Healable = false });
        Assert.IsFalse(NpcRestorativeCare.Evaluate(patient, patient, powder, out _, out _));
        patient.health.hediffSet.BleedRateTotal = .2f;
        Assert.IsTrue(NpcRestorativeCare.Evaluate(patient, patient, stim, out _, out _));
        Assert.IsFalse(NpcRestorativeCare.Evaluate(patient, patient, powder, out _, out _));
    }

    [TestMethod]
    public void DetAnalgesiaWorksWithoutWoundsButRestorativeUseDoesNot()
    {
        Pawn patient = new() { RawPain = .8f };
        Thing stim = Pocket(patient, "StimpackUltratech");
        Assert.IsTrue(NpcPainCare.Evaluate(patient, patient, stim, out NpcPainDose dose, out _));
        Assert.AreEqual(.4f, dose.Pain, .00001f);
        Assert.IsFalse(NpcRestorativeCare.Evaluate(patient, patient, stim, out _, out _));
    }

    [TestMethod]
    public void RestorativeUseRejectsOverdoseRandomOutcomesAndActorRefusals()
    {
        Pawn patient = Wounded();
        Thing stim = Pocket(patient, "StimpackIndustrial");
        patient.health.hediffSet.hediffs.Add(new() { pawn = patient, def = HediffDefOf.DrugOverdose, Severity = .4f });
        Assert.IsFalse(NpcRestorativeCare.Evaluate(patient, patient, stim, out _, out _));
        patient.health.hediffSet.hediffs.RemoveAt(1);
        stim.def.ingestible.outcomeDoers[0].chance = .5f;
        Assert.IsFalse(NpcRestorativeCare.Evaluate(patient, patient, stim, out _, out _));
        stim.def.ingestible.outcomeDoers[0].chance = 1f;
        stim.def.ingestible.outcomeDoers.Add(stim.def.ingestible.outcomeDoers[0]);
        Assert.IsFalse(NpcRestorativeCare.Evaluate(patient, patient, stim, out _, out _), "Duplicate effect outcomes must not be projected as a single dose.");
        stim.def.ingestible.outcomeDoers.RemoveAt(1);
        patient.Teetotaler = true;
        Assert.IsFalse(NpcRestorativeCare.Evaluate(patient, patient, stim, out _, out _));
    }

    [TestMethod]
    public void MbhNeedsAnEffectiveSafeCapacityGainAndIsNotAClinicalPainDrug()
    {
        Pawn pawn = new() { RawPain = 1f };
        Thing item = Pocket(pawn, "DV_AdrenalineShot");
        Assert.IsTrue(NpcMedicalDrugs.SafeOptionalBooster(pawn, item.def));
        Assert.IsFalse(NpcPainCare.Evaluate(pawn, pawn, item, out _, out _));
        Assert.IsFalse(NpcMedicalDrugs.Restorative(item.def));
        AddEffect(pawn, item.def);
        Assert.IsFalse(NpcMedicalDrugs.SafeOptionalBooster(pawn, item.def));
        pawn.health.hediffSet.hediffs.Clear();
        pawn.health.hediffSet.hediffs.Add(new() { pawn = pawn, def = HediffDefOf.DrugOverdose, Severity = .4f });
        Assert.IsFalse(NpcMedicalDrugs.SafeOptionalBooster(pawn, item.def));
    }

    [TestMethod]
    public void SeparateProvisioningControlsAlsoApplyToScavengedDrugs()
    {
        MoreInjuriesMod.Settings.NpcPerformanceDrugs = false;
        Assert.IsTrue(NpcMedicalDrugs.GrantAllowed(KnownThingDefOf.Morphine));
        Assert.IsTrue(NpcMedicalDrugs.GrantAllowed(Drug("StimpackIndustrial")));
        Assert.IsFalse(NpcMedicalDrugs.GrantAllowed(Drug("DV_AdrenalineShot")));
        MoreInjuriesMod.Settings.NpcPainCare = false;
        Assert.IsFalse(NpcMedicalDrugs.GrantAllowed(KnownThingDefOf.Morphine));
        Assert.IsTrue(NpcMedicalDrugs.GrantAllowed(Drug("HerbalPowder")));
        MoreInjuriesMod.Settings.NpcDrugSelection = 2;
        foreach (ThingDef drug in DefDatabase<ThingDef>.Definitions.Values.Append(KnownThingDefOf.Morphine))
        { Assert.IsFalse(NpcMedicalDrugs.GrantAllowed(drug)); }
    }

    [TestMethod]
    public void RestorativeSelfAndAssistJobsConsumeOneDoseWithoutChargingDrugBudgets()
    {
        Patch();
        Pawn patient = Wounded();
        patient.DoctorCapable = false;
        Thing dose = Pocket(patient, "HerbalPowder");
        patient.CurJob = NpcRestorativeCare.Select(patient, patient)!;
        JobDriver_NpcRestorativeSelf self = new() { pawn = patient };
        Assert.IsTrue(self.TryMakePreToilReservations(false));
        self.Run();
        Assert.IsTrue(dose.Destroyed);
        Assert.AreEqual(0, NpcMedicalAi.State(patient).Drugs.Doses);
        Assert.AreEqual(-1, NpcMedicalAi.State(patient).Drugs.LastClinical);

        Pawn doctor = new(); patient = Wounded(); patient.Downed = true;
        dose = Pocket(doctor, "StimpackIndustrial");
        doctor.CurJob = NpcRestorativeCare.Select(doctor, patient)!;
        JobDriver_NpcRestorativeAssist assist = new() { pawn = doctor };
        Assert.IsTrue(assist.TryMakePreToilReservations(false));
        assist.Run();
        Assert.IsTrue(dose.Destroyed);
        Assert.AreEqual(0, NpcMedicalAi.State(patient).Drugs.Doses);
        Assert.AreEqual(-1, NpcMedicalAi.State(patient).Drugs.LastClinical);
        Assert.IsNull(MedicalDrugAdministration.GetDoctor(dose));
        Assert.IsFalse(MedicalDrugAdministration.IsRestorative(dose));
    }

    [TestMethod]
    public void FinalValidationAndFailedRetrievalPreserveTheSelectedDose()
    {
        Patch();
        Pawn actor = Wounded();
        Thing dose = Pocket(actor, "StimpackIndustrial", 3);
        actor.CurJob = NpcRestorativeCare.Select(actor, actor)!;
        actor.inventory.innerContainer.TransferLimit = 0;
        new JobDriver_NpcRestorativeSelf { pawn = actor }.Run();
        Assert.AreEqual(3, dose.stackCount);
        Assert.IsNull(actor.carryTracker.CarriedThing);
        actor.CurJob = JobMaker.MakeJob(KnownJobDefOf.MI_NpcRestorativeSelf, dose);
        actor.health.hediffSet.hediffs.Clear();
        dose.Ingested(actor, 0f);
        Assert.AreEqual(3, dose.stackCount);
        Assert.AreEqual(0, actor.CurJob.count);
    }

    [TestMethod]
    public void RestorativeSelfResumeKeepsTheSameToilLayoutAndConsumesItsCarriedDose()
    {
        Patch();
        Pawn actor = Wounded();
        Thing dose = Pocket(actor, "HerbalPowder");
        actor.CurJob = NpcRestorativeCare.Select(actor, actor)!;
        int before = new JobDriver_NpcRestorativeSelf { pawn = actor }.BuildToils().Count;
        Assert.IsTrue(NpcMedicalReservations.TakeDrug(actor, actor.CurJob, TargetIndex.A, dose, actor.inventory.innerContainer));
        JobDriver_NpcRestorativeSelf resumed = new() { pawn = actor };
        Assert.AreEqual(before, resumed.BuildToils().Count);
        Assert.IsTrue(resumed.TryMakePreToilReservations(false));
        resumed.Run();
        Assert.IsTrue(dose.Destroyed);
        Assert.IsNull(actor.carryTracker.CarriedThing);
    }

    [TestMethod]
    public void ModuleToggleDuringChewingReturnsTheUnusedSelectedDose()
    {
        Patch();
        Pawn actor = Wounded();
        Thing dose = Pocket(actor, "HerbalPowder");
        actor.CurJob = NpcRestorativeCare.Select(actor, actor)!;
        JobDriver_NpcRestorativeSelf driver = new() { pawn = actor };
        driver.BeforeToil = (_, toil) =>
        {
            if (toil.Tag == "native chewing boundary") { MoreInjuriesMod.Settings.EnableNpcCombatMedicine = false; }
        };
        driver.Run();
        Assert.IsFalse(dose.Destroyed);
        Assert.IsNull(actor.carryTracker.CarriedThing);
        Assert.IsTrue(actor.inventory.innerContainer.Items.Contains(dose));
    }

    [TestMethod]
    public void MbhUsesPerformanceBudgetWhileManualAdministrationKeepsNativeOwnership()
    {
        Patch();
        Pawn actor = new();
        Thing dose = Pocket(actor, "DV_AdrenalineShot");
        actor.CurJob = JobMaker.MakeJob(KnownJobDefOf.MI_NpcPerformanceDrug, dose);
        dose.Ingested(actor, 0f);
        Assert.IsTrue(dose.Destroyed);
        Assert.AreEqual(1, NpcMedicalAi.State(actor).Drugs.Doses);
        Assert.AreEqual(-1, NpcMedicalAi.State(actor).Drugs.LastClinical);

        Pawn clinician = new();
        Thing manual = Pocket(clinician, "HerbalPowder");
        actor.CurJob = JobMaker.MakeJob(KnownJobDefOf.MI_NpcRestorativeSelf, new Thing());
        MoreInjuriesMod.Settings.EnableNpcCombatMedicine = false;
        MedicalDrugAdministration.Administer(clinician, actor, manual);
        Assert.IsTrue(manual.Destroyed, "Manual administration is not reclassified from the recipient's current NPC job.");
        Assert.AreEqual(1, NpcMedicalAi.State(actor).Drugs.Doses);
        Assert.IsNull(MedicalDrugAdministration.GetDoctor(manual));
    }
}
