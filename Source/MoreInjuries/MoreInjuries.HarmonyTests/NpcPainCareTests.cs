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
public sealed class NpcPainCareTests
{
    private const string OWNER = "MoreInjuries.test.ClinicalPain";

    [TestInitialize]
    public void Initialize()
    {
        MoreInjuriesMod.Settings = new();
        ModsConfig.BiotechActive = ModsConfig.IdeologyActive = false;
        Prefs.DevMode = false;
        Log.Messages.Clear();
        KnownThingDefOf.Morphine = Drug("Morphine", .35f,
            Stage(0, .6f, -.1f), Stage(.1f, .4f, -.2f), Stage(.2f, .2f, -.3f), Stage(.4f, .1f, -.4f));
        ThingDefOf.Yayo = Drug("Yayo", .75f, Stage(0, .5f));
        ThingDefOf.GoJuice = Drug("GoJuice", .5f, Stage(0, .1f));
        Find.TickManager.TicksGame = 1000;
        Find.HistoryEventsManager.Events.Clear();
    }

    [TestCleanup]
    public void Cleanup() => new Harmony(OWNER).UnpatchAll(OWNER);

    private static HediffStage Stage(float severity, float factor, float sedation = 0f)
        => new() { minSeverity = severity, painFactor = factor,
            capMods = sedation == 0f ? [] : [new() { capacity = PawnCapacityDefOf.Consciousness, offset = sedation }] };

    private static ThingDef Drug(string name, float gain, params HediffStage[] stages)
        => new() { defName = name, ingestible = new() { outcomeDoers = [new() { severity = gain,
            hediffDef = new() { defName = name + "High", stages = stages.ToList() } }] } };

    private static Thing Pocket(Pawn pawn, ThingDef definition, int count = 1)
    {
        Thing item = new() { def = definition, ParentHolder = pawn.inventory, Spawned = false, stackCount = count };
        pawn.inventory.innerContainer.Items.Add(item);
        return item;
    }
    private static Hediff High(Pawn patient, ThingDef drug, float severity)
    {
        Hediff high = new() { pawn = patient, def = drug.ingestible.outcomeDoers[0].hediffDef, Severity = severity };
        patient.health.hediffSet.hediffs.Add(high);
        return high;
    }
    private static void Condition(Pawn patient, string name, float severity)
        => patient.health.hediffSet.hediffs.Add(new() { pawn = patient, def = new() { defName = name }, Severity = severity });
    private static bool Dose(Pawn patient, ThingDef drug, out NpcPainDose projection)
        => NpcPainProjection.TryDose(patient, drug, out projection, out _);

    [TestMethod]
    public void PreventiveTriggerUsesIndividualThresholdAndRespectsConfiguredFraction()
    {
        Pawn patient = new() { RawPain = .4f };
        Assert.AreEqual(15f, NpcPainCare.Priority(patient));
        patient.PainThreshold = 1f;
        Assert.AreEqual(0f, NpcPainCare.Priority(patient));
        MoreInjuriesMod.Settings.NpcPainThreshold = .9f;
        patient.RawPain = .87f;
        Assert.AreEqual(0f, NpcPainCare.Priority(patient));
        patient.RawPain = .9f;
        Assert.AreEqual(55f, NpcPainCare.Priority(patient));
        MoreInjuriesMod.Settings.NpcPainThreshold = 0f;
        patient.RawPain = .001f;
        Assert.AreEqual(15f, NpcPainCare.Priority(patient));
        patient.PainFactor = 0f;
        Assert.AreEqual(0f, NpcPainCare.Priority(patient));
    }

    [TestMethod]
    public void PainOnlyMixedAndNonPainDowningHaveDifferentEligibility()
    {
        Pawn patient = new() { Downed = true, RawPain = .9f };
        Assert.AreEqual(65f, NpcPainCare.Priority(patient));
        Assert.AreEqual(NpcPainCollapse.Pain, NpcPainCare.Assess(patient).Collapse);
        patient.health.capacities.Base[PawnCapacityDefOf.Moving] = .1f;
        Assert.AreEqual(65f, NpcPainCare.Priority(patient), "Mixed causes still warrant pain assistance, without a promise of recovery.");
        Assert.AreEqual(NpcPainCollapse.Mixed, NpcPainCare.Assess(patient).Collapse);
        Assert.IsTrue(Dose(patient, ThingDefOf.Yayo, out _));
        patient.RawPain = .5f;
        Assert.AreEqual(0f, NpcPainCare.Priority(patient), "Another cause of collapse alone is not an analgesic indication.");
        Assert.AreEqual(NpcPainCollapse.Other, NpcPainCare.Assess(patient).Collapse);
    }

    [TestMethod]
    public void LowSeverityUnchangedStagesRejectDurationOnlyTopUpsForBothVanillaDrugs()
    {
        foreach (ThingDef drug in new[] { ThingDefOf.Yayo, ThingDefOf.GoJuice })
        {
            Pawn patient = new() { RawPain = 1f };
            High(patient, drug, .001f);
            Assert.IsFalse(Dose(patient, drug, out _));
        }
    }

    [TestMethod]
    public void DifferentHighsStackMultiplicativelyWhileSameHighTopUpDoesNot()
    {
        Pawn patient = new() { RawPain = 1f };
        High(patient, ThingDefOf.Yayo, .1f);
        Assert.AreEqual(.5f, patient.health.hediffSet.PainTotal, .0001f);
        Assert.IsTrue(Dose(patient, ThingDefOf.GoJuice, out NpcPainDose dose));
        Assert.AreEqual(.05f, dose.Pain, .0001f);
        Assert.AreEqual(.5f, patient.health.hediffSet.PainTotal, "Projection must leave real health intact.");
        Assert.HasCount(1, patient.health.hediffSet.hediffs);
    }

    [TestMethod]
    public void MorphineAllowsBeneficialStageChangeAndRejectsBuildupOrDurationOnlyTopUp()
    {
        Pawn patient = new();
        High(patient, KnownThingDefOf.Morphine, .01f);
        Assert.IsTrue(Dose(patient, KnownThingDefOf.Morphine, out NpcPainDose dose));
        Assert.AreEqual(.1f, dose.Pain, .0001f);
        patient.health.hediffSet.hediffs[0].Severity = .06f;
        Assert.IsFalse(Dose(patient, KnownThingDefOf.Morphine, out _));
        patient.health.hediffSet.hediffs[0].Severity = .25f;
        patient.ChemicalEffect = .1f;
        Assert.IsFalse(Dose(patient, KnownThingDefOf.Morphine, out _));
    }

    [TestMethod]
    public void BodySizeToleranceAndCapacityEffectsAffectOneDoseProjection()
    {
        Pawn patient = new() { BodySize = .5f };
        Assert.IsFalse(Dose(patient, KnownThingDefOf.Morphine, out _));
        patient.BodySize = 2f;
        Assert.IsTrue(Dose(patient, KnownThingDefOf.Morphine, out NpcPainDose dose));
        Assert.AreEqual(.2f, dose.Pain, .0001f);
        patient.ChemicalEffect = 0f;
        Assert.IsFalse(Dose(patient, KnownThingDefOf.Morphine, out _));
        patient.BodySize = patient.ChemicalEffect = 1f;
        patient.health.capacities.Base[PawnCapacityDefOf.Consciousness] = .55f;
        Assert.IsFalse(Dose(patient, KnownThingDefOf.Morphine, out _), "Analgesia must not substitute sedation collapse for pain shock.");
        patient.health.capacities.Base[PawnCapacityDefOf.Consciousness] = 1f;
        patient.health.capacities.Base[PawnCapacityDefOf.Moving] = .2f;
        Assert.IsFalse(Dose(patient, KnownThingDefOf.Morphine, out _), "Bound indirect incapacity when a worker reads the real consciousness tracker.");
    }

    [TestMethod]
    public void ProjectedPostDoseSedationIsReadFromResolvedStages()
    {
        Pawn patient = new();
        ThingDefOf.Yayo.ingestible.outcomeDoers[0].hediffDef.stages[0].capMods.Add(new()
            { capacity = PawnCapacityDefOf.Breathing, setMax = 0f });
        Assert.IsFalse(Dose(patient, ThingDefOf.Yayo, out _));
    }

    [TestMethod]
    public void OverdoseResistanceImmunityAndDependencyDoNotEraseOtherEffects()
    {
        Pawn patient = new() { genes = new(), drugs = new() };
        ModsConfig.BiotechActive = true;
        CompProperties_Drug comp = ThingDefOf.Yayo.Drug;
        patient.genes.GenesListForReading.Add(new() { def = new() { chemical = comp.chemical, overdoseChanceFactor = .5f } });
        Assert.IsTrue(Dose(patient, ThingDefOf.Yayo, out NpcPainDose resistant));
        Assert.AreEqual(.355f, resistant.OverdoseRisk, .0001f);
        patient.health.hediffSet.hediffs.Add(new() { def = HediffDefOf.DrugOverdose, Severity = .2f });
        Assert.IsFalse(Dose(patient, ThingDefOf.Yayo, out _));
        patient.drugs.OverdosePossible = false; // Native helper's immunity/dependency result.
        Assert.IsTrue(Dose(patient, ThingDefOf.Yayo, out NpcPainDose immune));
        Assert.AreEqual(0f, immune.OverdoseRisk);
        ThingDefOf.Yayo.ingestible.outcomeDoers[0].hediffDef.stages[0].capMods.Add(new()
            { capacity = PawnCapacityDefOf.Consciousness, setMax = .1f });
        Assert.IsFalse(Dose(patient, ThingDefOf.Yayo, out _));
    }

    [TestMethod]
    [DataRow("BloodLoss", .2f)]
    [DataRow("HypovolemicShock", .01f)]
    [DataRow("KetamineBuildup", .01f)]
    [DataRow("ChloroformBuildup", .01f)]
    [DataRow("AlcoholHigh", .01f)]
    public void MorphineConservativeSafeguardsRemain(string condition, float severity)
    {
        Pawn patient = new();
        Condition(patient, condition, severity);
        Assert.IsFalse(NpcPainCare.Evaluate(patient, patient, Pocket(patient, KnownThingDefOf.Morphine), out _, out _));
    }

    [TestMethod]
    public void DoctoringIsRequiredOnlyForHelpingAndRecipientAversionDoesNotBecomeActorAversion()
    {
        Pawn actor = new() { DoctorCapable = false }, patient = new() { Teetotaler = true, AllowDrug = false, Downed = true, RawPain = .9f };
        Thing dose = Pocket(actor, ThingDefOf.Yayo);
        Assert.IsTrue(NpcPainCare.Evaluate(actor, actor, dose, out _, out _));
        Assert.IsFalse(NpcPainCare.Evaluate(actor, patient, dose, out _, out _));
        actor.DoctorCapable = true;
        Assert.IsTrue(NpcPainCare.Evaluate(actor, patient, dose, out _, out _));
        actor.Teetotaler = true;
        Assert.IsFalse(NpcPainCare.Evaluate(actor, patient, dose, out _, out _));
        Assert.IsFalse(NpcPainCare.Evaluate(actor, actor, dose, out _, out _));
    }

    [TestMethod]
    public void IdeologyChecksUseDifferentIngestionAndAdministrationEvents()
    {
        ModsConfig.IdeologyActive = true;
        Pawn actor = new(), patient = new() { Downed = true, RawPain = .9f };
        Thing dose = Pocket(actor, ThingDefOf.Yayo);
        actor.RefusedEvents.Add(HistoryEventDefOf.IngestedHardDrug);
        Assert.IsFalse(NpcPainCare.Evaluate(actor, actor, dose, out _, out _));
        Assert.IsTrue(NpcPainCare.Evaluate(actor, patient, dose, out _, out _));
        actor.RefusedEvents.Add(HistoryEventDefOf.AdministeredDrug);
        Assert.IsFalse(NpcPainCare.Evaluate(actor, patient, dose, out _, out _));
    }

    [TestMethod]
    public void CareRestrictionsPrisonerExceptionHostilityAndModuleToggleAreRevalidated()
    {
        Pawn actor = new() { playerSettings = new() { medCare = MedicalCareCategory.NoMeds } };
        Thing dose = Pocket(actor, ThingDefOf.Yayo);
        Assert.IsFalse(NpcPainCare.FinalCheck(actor, actor, dose));
        actor.IsPrisoner = true;
        Assert.IsTrue(NpcPainCare.FinalCheck(actor, actor, dose));
        Pawn patient = new() { Downed = true, RawPain = .9f, Hostile = true };
        Assert.IsFalse(NpcPainCare.FinalCheck(actor, patient, dose));
        patient.Hostile = false;
        patient.Position = new(20);
        Assert.IsFalse(NpcPainCare.FinalCheck(actor, patient, dose));
        patient.Position = new(0);
        patient.CareRelation = -1;
        Assert.IsFalse(NpcPainCare.FinalCheck(actor, patient, dose));
        MoreInjuriesMod.Settings.NpcPainCare = false;
        Assert.IsFalse(NpcPainCare.FinalCheck(actor, actor, dose));
    }

    [TestMethod]
    public void RankingPrefersLowerRiskThenEffortAndCostRatherThanStrongerAnalgesia()
    {
        Pawn actor = new() { RawPain = .9f };
        Thing yayo = Pocket(actor, ThingDefOf.Yayo), go = Pocket(actor, ThingDefOf.GoJuice);
        yayo.def.BaseMarketValue = 21f; go.def.BaseMarketValue = 53f;
        Assert.AreSame(yayo, NpcPainCare.Select(actor, actor)!.targetA.Thing);
        go.def.Drug.largeOverdoseChance = .001f;
        Assert.AreSame(go, NpcPainCare.Select(actor, actor)!.targetA.Thing);
        actor.inventory.innerContainer.Items.Clear();
        yayo.ParentHolder = null; yayo.Spawned = true; yayo.PositionHeld = new(10);
        actor.Supplies.Add(yayo);
        Assert.IsNull(NpcPainCare.Select(actor, actor, pocketOnly: true));
        Assert.AreSame(yayo, NpcPainCare.Select(actor, actor)!.targetA.Thing);
        yayo.PositionHeld = new(13);
        Assert.IsNull(NpcPainCare.Select(actor, actor));
    }

    [TestMethod]
    public void ClinicalSelfAndAssistedConsumptionBypassNeitherSafetyNorNativeOutcomesButLeaveBudgetsAlone()
    {
        new Harmony(OWNER).CreateClassProcessor(typeof(NpcMedicalIngestionPatch)).Patch();
        Pawn patient = new();
        NpcMedicalAi.State(patient).Drugs.PerformanceTaken(999);
        Thing self = Pocket(patient, ThingDefOf.Yayo);
        patient.CurJob = new() { def = KnownJobDefOf.MI_NpcPainSelf, count = 1, targetA = new(self) };
        int outcomes = 0;
        self.IngestionEffect = _ => outcomes++;
        self.Ingested(patient, 0f);
        Assert.IsTrue(self.Destroyed);
        Assert.AreEqual(1, outcomes);
        Assert.AreEqual(1, NpcMedicalAi.State(patient).Drugs.Doses);
        Assert.AreEqual(-1, NpcMedicalAi.State(patient).Drugs.LastClinical);
        Pawn helper = new();
        Thing assisted = Pocket(helper, ThingDefOf.Yayo);
        patient.CurJob = new(); patient.Downed = true; patient.RawPain = .9f; patient.Teetotaler = true;
        MedicalDrugAdministration.Administer(helper, patient, assisted, clinicalPain: true);
        Assert.IsTrue(assisted.Destroyed);
        Assert.IsNull(MedicalDrugAdministration.GetDoctor(assisted));
        Assert.AreEqual(-1, NpcMedicalAi.State(patient).Drugs.LastClinical);
        new Thing { def = KnownThingDefOf.Epinephrine }.Ingested(patient, 0f);
        Assert.AreEqual(1000, NpcMedicalAi.State(patient).Drugs.LastClinical);
    }

    [TestMethod]
    public void RejectedConsumptionLeavesDrugIntactEndsNativeRetryAndCanReturnSelectedDose()
    {
        new Harmony(OWNER).CreateClassProcessor(typeof(NpcMedicalIngestionPatch)).Patch();
        Pawn actor = new();
        Thing stack = Pocket(actor, ThingDefOf.Yayo, 4);
        Job job = actor.CurJob = new() { def = KnownJobDefOf.MI_NpcPainSelf, targetA = new(stack) };
        Assert.IsTrue(NpcMedicalReservations.TakeDrug(actor, job, TargetIndex.A, stack, actor.inventory.innerContainer));
        Thing selected = actor.carryTracker.CarriedThing!;
        Assert.AreEqual(3, stack.stackCount);
        Assert.AreEqual(1, selected.stackCount);
        actor.RawPain = 0f;
        selected.Ingested(actor, 0f);
        Assert.IsFalse(selected.Destroyed);
        Assert.AreEqual(1, selected.stackCount);
        Assert.AreEqual(0, job.count);
        NpcMedicalReservations.RestockUnusedDrug(actor, job, TargetIndex.A);
        Assert.IsNull(actor.carryTracker.CarriedThing);
        Assert.AreEqual(4, actor.inventory.innerContainer.Items.Sum(item => item.stackCount));
    }

    [TestMethod]
    public void FailedDrugTransferDoesNotDropSplitOrReplaceItsSelectedTarget()
    {
        Pawn actor = new();
        Thing stack = Pocket(actor, ThingDefOf.Yayo, 3);
        Job job = new() { targetB = new(stack) };
        actor.inventory.innerContainer.TransferLimit = 0;
        Assert.IsFalse(NpcMedicalReservations.TakeDrug(actor, job, TargetIndex.B, stack, actor.inventory.innerContainer));
        Assert.AreSame(stack, job.targetB.Thing);
        Assert.AreEqual(3, stack.stackCount);
        Assert.IsNull(actor.carryTracker.CarriedThing);
    }

    [TestMethod]
    public void SelfJobUsesNativeIngestionForExactlyOnePocketDoseWithoutDoctoring()
    {
        new Harmony(OWNER).CreateClassProcessor(typeof(NpcMedicalIngestionPatch)).Patch();
        Pawn actor = new() { DoctorCapable = false };
        Thing stack = Pocket(actor, ThingDefOf.Yayo, 4);
        actor.CurJob = NpcPainCare.Select(actor, actor)!;
        JobDriver_NpcPainSelf driver = new() { pawn = actor };
        Assert.IsTrue(driver.TryMakePreToilReservations(false));
        driver.Run();
        Assert.AreEqual(3, stack.stackCount);
        Assert.IsNull(actor.carryTracker.CarriedThing);
        Assert.AreEqual(0, NpcMedicalAi.State(actor).Drugs.Doses);
    }

    [TestMethod]
    public void AssistedJobConsumesOneUnitAndRecordsActingDoctorEventsExactlyOnce()
    {
        new Harmony(OWNER).CreateClassProcessor(typeof(NpcMedicalIngestionPatch)).Patch();
        ModsConfig.IdeologyActive = true;
        Pawn actor = new(), patient = new() { Downed = true, RawPain = .9f, Teetotaler = true };
        Thing stack = Pocket(actor, ThingDefOf.Yayo, 4);
        actor.CurJob = NpcPainCare.Select(actor, patient)!;
        new JobDriver_NpcPainAssist { pawn = actor }.Run();
        Assert.AreEqual(3, stack.stackCount);
        Assert.IsNull(actor.carryTracker.CarriedThing);
        Assert.HasCount(2, Find.HistoryEventsManager.Events);
        Assert.IsTrue(Find.HistoryEventsManager.Events.All(historyEvent => historyEvent.Actor == actor));
        Assert.AreEqual(-1, NpcMedicalAi.State(patient).Drugs.LastClinical);
    }

    [TestMethod]
    public void AssistedJobRejectionKeepsItsSelectedDoseAndProducesNoAdministrationEvents()
    {
        new Harmony(OWNER).CreateClassProcessor(typeof(NpcMedicalIngestionPatch)).Patch();
        ModsConfig.IdeologyActive = true;
        Pawn actor = new(), patient = new() { Downed = true, RawPain = .9f };
        Thing stack = Pocket(actor, ThingDefOf.Yayo, 4);
        actor.CurJob = NpcPainCare.Select(actor, patient)!;
        // The indication disappeared while the doctor was approaching.
        patient.RawPain = 0f;
        new JobDriver_NpcPainAssist { pawn = actor }.Run();
        Assert.AreEqual(4, stack.stackCount);
        Assert.IsNull(actor.carryTracker.CarriedThing);
        Assert.IsEmpty(Find.HistoryEventsManager.Events);
    }

    [TestMethod]
    public void SupplyAccessChangesAndUnrelatedCargoDoNotStartPainJobs()
    {
        Pawn actor = new(), supplier = new();
        Thing stack = Pocket(supplier, ThingDefOf.Yayo, 3);
        Assert.IsTrue(NpcPainCare.SourceAllowed(actor, actor, stack));
        supplier.Faction = new object();
        Assert.IsFalse(NpcPainCare.SourceAllowed(actor, actor, stack));
        supplier.Faction = actor.Faction; supplier.Hostile = true;
        Assert.IsFalse(NpcPainCare.SourceAllowed(actor, actor, stack));
        supplier.Hostile = false; supplier.Position = new(13);
        Assert.IsFalse(NpcPainCare.SourceAllowed(actor, actor, stack));
        Pocket(actor, ThingDefOf.Yayo);
        actor.carryTracker.innerContainer.Items.Add(new());
        Assert.IsNull(NpcPainCare.Select(actor, actor));
    }

    [TestMethod]
    public void SelfJobCanTakeOneDoseFromPermittedNearbyInventoryAndRetainsTheRest()
    {
        new Harmony(OWNER).CreateClassProcessor(typeof(NpcMedicalIngestionPatch)).Patch();
        Pawn actor = new(), supplier = new();
        Thing stack = Pocket(supplier, ThingDefOf.Yayo, 3);
        actor.CurJob = JobMaker.MakeJob(KnownJobDefOf.MI_NpcPainSelf, stack);
        new JobDriver_NpcPainSelf { pawn = actor }.Run();
        Assert.AreEqual(2, stack.stackCount);
        Assert.IsNull(actor.carryTracker.CarriedThing);
    }

    [TestMethod]
    public void AnotherPatientReservationBlocksClinicalSelfJobBeforeAnyDoseTransfer()
    {
        Pawn actor = new(), other = new();
        Thing stack = Pocket(actor, ThingDefOf.Yayo, 3);
        actor.Map.reservationManager.Reserve(other, new(), actor);
        actor.CurJob = NpcPainCare.Select(actor, actor)!;
        Assert.IsFalse(new JobDriver_NpcPainSelf { pawn = actor }.TryMakePreToilReservations(false));
        Assert.AreEqual(3, stack.stackCount);
        Assert.IsNull(actor.carryTracker.CarriedThing);
    }

    [TestMethod]
    public void GroundDoseRequiresCapacityAndReachabilityAndNativeIngestionStillRequestsOne()
    {
        new Harmony(OWNER).CreateClassProcessor(typeof(NpcMedicalIngestionPatch)).Patch();
        Pawn actor = new() { FitCount = 0 };
        Thing item = new() { def = ThingDefOf.Yayo, PositionHeld = new(1), stackCount = 3 };
        actor.Supplies.Add(item);
        Assert.IsNull(NpcPainCare.Select(actor, actor));
        actor.FitCount = 1; actor.Reachable = false;
        Assert.IsNull(NpcPainCare.Select(actor, actor));
        actor.Reachable = true;
        actor.CurJob = NpcPainCare.Select(actor, actor)!;
        new JobDriver_NpcPainSelf { pawn = actor }.Run();
        Assert.AreEqual(2, item.stackCount);
        Assert.IsNull(actor.carryTracker.CarriedThing);
    }

    [TestMethod]
    public void DiagnosticsLogReasonChangesPerActorAndPatientRatherThanEveryEvaluation()
    {
        Prefs.DevMode = true;
        Pawn actor = new(), a = new() { Downed = true, RawPain = .9f }, b = new() { Downed = true, RawPain = .9f };
        Pocket(actor, ThingDefOf.Yayo);
        NpcPainCare.Select(actor, a); NpcPainCare.Select(actor, b);
        NpcPainCare.Select(actor, a); NpcPainCare.Select(actor, b);
        Assert.HasCount(2, Log.Messages);
        actor.Teetotaler = true;
        NpcPainCare.Select(actor, a); NpcPainCare.Select(actor, a);
        Assert.HasCount(3, Log.Messages);
    }

    [TestMethod]
    public void ClinicalIntentRestoresAcrossNestedAdministrationAndExceptions()
    {
        Pawn doctor = new(), patient = new(), innerDoctor = new();
        Thing outer = Pocket(doctor, ThingDefOf.Yayo), inner = Pocket(innerDoctor, ThingDefOf.Yayo);
        inner.IngestionEffect = _ =>
        {
            Assert.IsFalse(MedicalDrugAdministration.IsClinicalPain(inner));
            Assert.IsNull(MedicalDrugAdministration.GetDoctor(outer));
        };
        outer.IngestionEffect = _ =>
        {
            Assert.IsTrue(MedicalDrugAdministration.IsClinicalPain(outer));
            MedicalDrugAdministration.Administer(innerDoctor, patient, inner);
            Assert.IsTrue(MedicalDrugAdministration.IsClinicalPain(outer));
            Assert.AreSame(doctor, MedicalDrugAdministration.GetDoctor(outer));
            throw new InvalidOperationException("outcome fixture");
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => MedicalDrugAdministration.Administer(doctor, patient, outer, clinicalPain: true));
        Assert.IsNull(MedicalDrugAdministration.GetDoctor(outer));
        Assert.IsFalse(MedicalDrugAdministration.IsClinicalPain(outer));
    }

    [TestMethod]
    public void ManualAdministrationToAnNpcTakingClinicalOrPerformanceDrugsKeepsItsNativePath()
    {
        new Harmony(OWNER).CreateClassProcessor(typeof(NpcMedicalIngestionPatch)).Patch();
        MoreInjuriesMod.Settings.NpcPainCare = MoreInjuriesMod.Settings.NpcPerformanceDrugs = false;
        Pawn playerDoctor = new() { ManagedByMi = false }, patient = new() { RawPain = 0f };
        foreach (JobDef current in new[] { KnownJobDefOf.MI_NpcPainSelf, KnownJobDefOf.MI_NpcPerformanceDrug })
        {
            Thing unrelated = Pocket(patient, ThingDefOf.Yayo), manual = Pocket(playerDoctor, ThingDefOf.Yayo);
            patient.CurJob = new() { def = current, targetA = new(unrelated) };
            MedicalDrugAdministration.Administer(playerDoctor, patient, manual);
            Assert.IsTrue(manual.Destroyed);
            Assert.IsFalse(unrelated.Destroyed);
            Assert.AreEqual(0, NpcMedicalAi.State(patient).Drugs.Doses);
        }
    }

    [TestMethod]
    public void SelfJobHasAStableToilGraphAndCanResumeWithItsAlreadyCarriedDose()
    {
        new Harmony(OWNER).CreateClassProcessor(typeof(NpcMedicalIngestionPatch)).Patch();
        Pawn actor = new();
        Thing stack = Pocket(actor, ThingDefOf.Yayo, 3);
        actor.CurJob = NpcPainCare.Select(actor, actor)!;
        List<Toil> before = new JobDriver_NpcPainSelf { pawn = actor }.BuildToils();
        int chewingIndex = before.FindIndex(toil => toil.Tag == "native chewing boundary");
        Assert.IsTrue(NpcMedicalReservations.TakeDrug(actor, actor.CurJob, TargetIndex.A, stack, actor.inventory.innerContainer));
        List<Toil> after = new JobDriver_NpcPainSelf { pawn = actor }.BuildToils();
        Assert.AreEqual(before.Count, after.Count);
        Assert.AreEqual(chewingIndex, after.FindIndex(toil => toil.Tag == "native chewing boundary"));
        JobDriver_NpcPainSelf resumed = new() { pawn = actor, ResumeIndex = chewingIndex };
        Assert.IsTrue(resumed.TryMakePreToilReservations(false));
        resumed.Run();
        Assert.AreEqual(2, stack.stackCount);
        Assert.IsNull(actor.carryTracker.CarriedThing);
    }

    [TestMethod]
    public void MissingSelectedDoseFailsQuietlyBeforeRetrievalOrConsumption()
    {
        Pawn actor = new();
        actor.CurJob = new() { def = KnownJobDefOf.MI_NpcPainSelf };
        Assert.IsFalse(NpcPainCare.SourceAllowed(actor, actor, null));
        JobDriver_NpcPainSelf driver = new() { pawn = actor };
        Assert.IsFalse(driver.TryMakePreToilReservations(false));
        driver.Run();
        Assert.AreEqual(JobCondition.Incompletable, actor.jobs.Ended);
        Assert.IsNull(actor.carryTracker.CarriedThing);
    }
}
