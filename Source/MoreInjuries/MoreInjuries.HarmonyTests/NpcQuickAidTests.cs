using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.Secondary.Linked;
using MoreInjuries.HealthConditions.Secondary.Handlers.Modifiers;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.HarmonyTests;

[TestClass, DoNotParallelize]
public sealed class NpcQuickAidTests
{
    [TestInitialize]
    public void Initialize()
    {
        new NpcPainCareTests().Initialize();
        NpcExtraction.OnPreview = null; NpcMedicalAi.OnPreviewBleeding = null; NpcMedicalAi.OnHandoff = null;
    }

    private static (Pawn Actor, Pawn Patient) Pair()
    {
        Pawn actor = new() { Position = new(20) }, patient = new() { Position = new(21), Downed = true, RawPain = .9f, Map = actor.Map };
        actor.Map.mapPawns.AllPawnsSpawned.AddRange([actor, patient]);
        patient.health.capacities.Base[PawnCapacityDefOf.Consciousness] = .7f;
        return (actor, patient);
    }
    private static Thing Supply(Pawn holder, ThingDef def)
    {
        Thing dose = new() { def = def, Spawned = false, ParentHolder = holder.inventory };
        holder.inventory.innerContainer.Items.Add(dose); return dose;
    }
    private static Job? Choose(Pawn actor, Pawn patient, int mode = 2)
        => NpcMedicalDecision.Choose(actor, mode, [patient], _ => throw new AssertFailedException("Bypassed quick-aid comparison"),
            _ => null, _ => throw new AssertFailedException("Bypassed scoped safety"), _ => null, () => null,
            other => NpcQuickAid.Decide(actor, other, mode));

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void SeventyPercentConsciousnessCanRecoverAndProductionAssistConsumesOneDose(int mode)
    {
        (Pawn actor, Pawn patient) = Pair(); Thing dose = Supply(actor, KnownThingDefOf.Morphine);
        Assert.IsTrue(NpcPainCare.Evaluate(actor, patient, dose, out NpcPainDose projection, out string reason), reason);
        Assert.IsTrue(projection.Recovery);
        actor.CurJob = Choose(actor, patient, mode)!;
        Assert.AreSame(KnownJobDefOf.MI_NpcPainAssist, actor.CurJob.def);
        new JobDriver_NpcPainAssist { pawn = actor }.Run();
        Assert.IsTrue(dose.Destroyed); Assert.IsTrue(patient.Downed, "Native health owns standing.");
    }

    [TestMethod]
    public void NativeConsciousnessBoundaryAndCustomDefinitionsHaveNoExtraFloor()
    {
        (Pawn actor, Pawn patient) = Pair(); Thing dose = Supply(actor, KnownThingDefOf.Morphine);
        float minimum = PawnCapacityDefOf.Consciousness.minForCapable;
        try
        {
            patient.health.capacities.Base[PawnCapacityDefOf.Consciousness] = .6f;
            Assert.IsTrue(NpcPainCare.Evaluate(actor, patient, dose, out _, out _));
            patient.health.capacities.Base[PawnCapacityDefOf.Consciousness] = .599f;
            Assert.IsFalse(NpcPainCare.Evaluate(actor, patient, dose, out _, out _));
            PawnCapacityDefOf.Consciousness.minForCapable = .2f;
            patient.health.capacities.Base[PawnCapacityDefOf.Consciousness] = .51f;
            Assert.IsTrue(NpcPainCare.Evaluate(actor, patient, dose, out _, out _));
        }
        finally { PawnCapacityDefOf.Consciousness.minForCapable = minimum; }
    }

    [TestMethod]
    public void ActivePawnRejectsNetFunctionalLossButDownedPawnCanAcceptIt()
    {
        (Pawn actor, Pawn patient) = Pair();
        HediffStage stage = ThingDefOf.Yayo.ingestible.outcomeDoers[0].hediffDef.stages[0];
        stage.painFactor = .9f;
        foreach (PawnCapacityDef capacity in new[] { PawnCapacityDefOf.Consciousness, PawnCapacityDefOf.Moving, PawnCapacityDefOf.Manipulation })
        { stage.capMods.Add(new() { capacity = capacity, offset = -.3f }); }
        Thing dose = Supply(actor, ThingDefOf.Yayo);
        Assert.IsTrue(NpcPainCare.Evaluate(actor, patient, dose, out NpcPainDose projection, out _));
        Assert.IsFalse(projection.Recovery, "Pain remains above its threshold.");
        patient.Downed = false;
        Assert.IsFalse(NpcPainCare.Evaluate(actor, patient, dose, out _, out _));
    }

    [TestMethod]
    public void MixedCollapseNeverReceivesRecoveryBonusAndRecoveryOutranksSedation()
    {
        (Pawn actor, Pawn patient) = Pair();
        Supply(actor, KnownThingDefOf.Morphine); Thing stimulant = Supply(actor, ThingDefOf.GoJuice);
        Job selected = NpcPainCare.Select(actor, patient)!;
        Assert.AreSame(stimulant, selected.targetB.Thing);
        patient.health.forceDowned = true;
        Assert.IsTrue(NpcPainCare.Evaluate(actor, patient, stimulant, out NpcPainDose projection, out _));
        Assert.IsFalse(projection.Recovery);
    }

    [TestMethod]
    public void LinkedVasodilationUsesResolvedCurveWithoutChangingRealHealth()
    {
        (Pawn actor, Pawn patient) = Pair(); Thing dose = Supply(actor, KnownThingDefOf.Morphine);
        HediffDef high = dose.def.ingestible.outcomeDoers[0].hediffDef;
        high.Extensions.Add(new LinkedSeverityProperties_ModExtension { ModifiedHediffs = [new()
            { SeverityModifiers = [new HediffModifier_SimpleCurve { SeverityCurve = new() { EvaluateValue = _ => 2f } }] }] });
        Assert.IsTrue(NpcPainCare.Evaluate(actor, patient, dose, out NpcPainDose projection, out string reason), reason);
        Assert.AreEqual(1.7f, projection.BleedingFactor, .0001f);
        Assert.HasCount(0, patient.health.hediffSet.hediffs);
        high.Extensions.Clear();
        high.Extensions.Add(new LinkedSeverityProperties_ModExtension { ModifiedHediffs = [new()
            { SeverityModifiers = [new SecondaryHediffModifier()] }] });
        Assert.IsFalse(NpcPainCare.Evaluate(actor, patient, dose, out _, out reason));
        StringAssert.Contains(reason, "unsupported immediate linked");
    }

    [TestMethod]
    public void PartialCoverIsWeightedAndDangerousRetrievalDefersWithoutAllocatingAJob()
    {
        (Pawn actor, Pawn patient) = Pair(); Supply(actor, ThingDefOf.GoJuice);
        actor.Map.Risks[patient.Position] = new(.2f, 0, 0, 0);
        Assert.IsNotNull(Choose(actor, patient));
        actor.Map.Risks[patient.Position] = new(2f, 1f, 0, 0);
        Assert.IsNull(Choose(actor, patient));
        actor.Map.Risks.Clear(); actor.Map.Risks[actor.Position] = new(0, 0, 2f, 0);
        Assert.IsNull(Choose(actor, patient));
    }

    [TestMethod]
    public void ExtractionAndQuickAidCompeteBeforeCommitAndOnlyTacticalModesCanExtract()
    {
        foreach (int mode in new[] { 0, 1, 2 })
        {
            (Pawn actor, Pawn patient) = Pair(); int created = 0;
            NpcExtraction.OnPreview = (_, _) => new() { Origin = new(1f, 0, 0, 0), Ticks = 10,
                Create = () => { created++; return new() { def = KnownJobDefOf.MI_NpcExtract }; } };
            Assert.AreEqual(mode > 0, Choose(actor, patient, mode) is not null);
            Assert.AreEqual(mode > 0 ? 1 : 0, created);
        }
        (Pawn helper, Pawn casualty) = Pair(); Supply(helper, ThingDefOf.GoJuice);
        NpcExtraction.OnPreview = (_, _) => new() { Origin = new(.2f, 0, 0, 0), Ticks = 100,
            Create = () => throw new AssertFailedException("Created losing extraction") };
        Assert.AreSame(KnownJobDefOf.MI_NpcPainAssist, Choose(helper, casualty)!.def);
    }

    [TestMethod]
    public void SharedRoundAllowsOnlyOnePainDoseAndOneBleedingApplication()
    {
        (Pawn actor, Pawn patient) = Pair(); Supply(actor, ThingDefOf.GoJuice);
        actor.CurJob = Choose(actor, patient)!;
        NpcQuickAid.Applied(actor, patient, actor.CurJob, true);
        NpcMedicalAi.OnPreviewBleeding = (_, _, preview) => preview(() => new() {
            def = KnownJobDefOf.UseBandage, targetA = new(patient), endAfterTendedOnce = true }, KnownJobDefOf.UseBandage, null, .05f, 80, 0, 0);
        Pawn other = new() { Map = actor.Map, Position = actor.Position };
        other.CurJob = Choose(other, patient)!;
        Assert.AreSame(KnownJobDefOf.UseBandage, other.CurJob.def);
        NpcQuickAid.Applied(other, patient, other.CurJob, true);
        Assert.IsNull(Choose(actor, patient)); Assert.IsNull(Choose(other, patient));
    }

    [TestMethod]
    public void BudgetInterruptionSettingAndFreshLoadChecksPreserveUnusedSupplies()
    {
        (Pawn actor, Pawn patient) = Pair(); Thing dose = Supply(actor, ThingDefOf.GoJuice);
        actor.CurJob = Choose(actor, patient)!;
        Find.TickManager.TicksGame += 601;
        Assert.IsFalse(NpcQuickAid.Continue(actor, patient, actor.CurJob, final: true));
        Assert.IsFalse(dose.Destroyed);
        Assert.IsNull(Choose(actor, patient));
        (actor, patient) = Pair(); dose = Supply(actor, ThingDefOf.GoJuice);
        actor.CurJob = Choose(actor, patient)!;
        MoreInjuriesMod.Settings.EnableNpcCombatMedicine = false;
        Assert.IsFalse(NpcPainCare.FinalCheck(actor, patient, dose));
    }

    [TestMethod]
    public void RetainedDroppedAlliedAndOriginalPositionSuppliesUseRealResolver()
    {
        (Pawn actor, Pawn patient) = Pair(); Thing retained = Supply(patient, ThingDefOf.GoJuice);
        Assert.AreSame(retained, NpcMedicalAi.FindSupply(actor, patient, retained.def));
        patient.inventory.innerContainer.Items.Clear(); retained.ParentHolder = null; retained.Spawned = true; retained.PositionHeld = new(5);
        actor.Map.listerThings.Buildings.Add(retained);
        Assert.IsNull(NpcMedicalAi.FindSupply(actor, patient, retained.def));
        NpcQuickAid.Delivered(actor, patient, new(5));
        Assert.AreSame(retained, NpcMedicalAi.FindSupply(actor, patient, retained.def));
        retained.Forbidden = true; Assert.IsNull(NpcMedicalAi.FindSupply(actor, patient, retained.def));
        retained.Forbidden = false; actor.Reachable = false; Assert.IsNull(NpcMedicalAi.FindSupply(actor, patient, retained.def));
    }

    [TestMethod]
    public void NonDoctorCarrierOffersFollowupAndCompetingReservationOrCloseEnemyPreventsAid()
    {
        (Pawn actor, Pawn patient) = Pair(); actor.DoctorCapable = false;
        NpcQuickAid.Delivered(actor, patient, new(5));
        Supply(patient, ThingDefOf.GoJuice);
        Assert.IsNull(Choose(actor, patient));
        Pawn helper = new() { Map = actor.Map, Position = actor.Position };
        Assert.IsNotNull(Choose(helper, patient));
        helper.Map.mapPawns.AllPawnsSpawned.Add(new() { Map = helper.Map, Position = new(25), Hostile = true });
        Assert.IsNull(Choose(helper, patient));
    }

    [TestMethod]
    public void NewDangerIsCheckedAtNextCellAndAgainBeforeConsumption()
    {
        (Pawn actor, Pawn patient) = Pair(); Thing dose = Supply(actor, ThingDefOf.GoJuice);
        actor.CurJob = Choose(actor, patient)!;
        Assert.IsTrue(NpcQuickAid.Continue(actor, patient, actor.CurJob));
        actor.pather.MovingNow = true; actor.pather.nextCell = new(22);
        actor.Map.Risks[new(22)] = new(0, 0, 2f, 0);
        Assert.IsFalse(NpcQuickAid.Continue(actor, patient, actor.CurJob));
        actor.pather.MovingNow = false; actor.Map.Risks.Clear();
        patient.mindState.knownExploder = new() { Map = actor.Map, PositionHeld = patient.Position };
        Assert.IsFalse(NpcPainCare.FinalCheck(actor, patient, dose));
        patient.mindState.knownExploder.Map = new();
        Assert.IsTrue(NpcPainCare.FinalCheck(actor, patient, dose));
        actor.Map.Risks[patient.Position] = new(8f, 0, 0, 0);
        Assert.IsFalse(NpcPainCare.FinalCheck(actor, patient, dose), "New exposure must re-evaluate weighted benefit too.");
        Assert.IsFalse(dose.Destroyed);
    }

    [TestMethod]
    public void FullRetrievalPathIsAssessedAndUnknownRiskCarriesAWeightedPenalty()
    {
        (Pawn actor, Pawn patient) = Pair(); Thing dose = Supply(patient, ThingDefOf.GoJuice);
        actor.Map.pathFinder.Find = (start, end, _) => new() { TotalCost = 100, NodesReversed = [end, new(40), start] };
        actor.Map.Risks[new(40)] = new(0, 0, 2f, 0);
        Assert.IsNull(Choose(actor, patient));
        actor.Map.Risks.Clear();
        Assert.IsNotNull(Choose(actor, patient));
        Assert.IsGreaterThan(NpcQuickAid.Hazard(new(0, 0, 0, 0)), NpcQuickAid.Hazard(new(0, 0, 0, 0, false)));
    }

    [TestMethod]
    public void CriticalStabilizationBypassesRoundLimitAndReservationsAreNeverOverridden()
    {
        (Pawn actor, Pawn patient) = Pair(); Supply(actor, ThingDefOf.GoJuice);
        Pawn doctor = new(); actor.Map.reservationManager.Reserve(doctor, new(), patient);
        Assert.IsNull(Choose(actor, patient));
        patient.EmergencyUrgency = 100;
        Job emergency = new() { def = new() { defName = "critical-stabilization" } };
        Assert.AreSame(emergency, NpcMedicalDecision.Choose(actor, 0, [patient], _ => (null, false), _ => null,
            _ => emergency, _ => throw new AssertFailedException(), () => throw new AssertFailedException(),
            other => NpcQuickAid.Decide(actor, other, 0)));
    }

    [TestMethod]
    public void InProgressApplicationUsesRemainingTimeAndDuplicateCompletionDoesNotRestartRound()
    {
        (Pawn actor, Pawn patient) = Pair(); Supply(actor, ThingDefOf.GoJuice);
        actor.CurJob = Choose(actor, patient)!;
        Find.TickManager.TicksGame += 550;
        NpcQuickAid.Applying(actor.CurJob, 40);
        Find.TickManager.TicksGame += 30;
        Assert.IsTrue(NpcQuickAid.Continue(actor, patient, actor.CurJob));
        NpcQuickAid.Applied(actor, patient, actor.CurJob, true);
        NpcQuickAid.Applied(actor, patient, actor.CurJob, true);
        Assert.IsNull(Choose(actor, patient));
        Job loaded = JobMaker.MakeJob(actor.CurJob.def, patient, actor.CurJob.targetB.Thing);
        Assert.IsFalse(NpcQuickAid.HasGrant(new() { CurJob = loaded }, patient));
    }

    [TestMethod]
    public void FeasibleImmediateBleedingControlPrecedesRecoveryWhenBleedingDeadlineIsCritical()
    {
        (Pawn actor, Pawn patient) = Pair(); Supply(actor, ThingDefOf.GoJuice);
        patient.BleedingDeathTicks = 500;
        NpcMedicalAi.OnPreviewBleeding = (_, _, preview) => preview(() => new() {
            def = KnownJobDefOf.UseBandage, targetA = new(patient), endAfterTendedOnce = true }, KnownJobDefOf.UseBandage, null, .05f, 80, 0, 0);
        Assert.AreSame(KnownJobDefOf.UseBandage, Choose(actor, patient)!.def);
    }

    [TestMethod]
    public void DelayedAttemptCompletionCannotRemoveGrantOnRecycledJob()
    {
        (Pawn actor, Pawn patient) = Pair(); Supply(actor, ThingDefOf.GoJuice);
        Job pooled = Choose(actor, patient)!; actor.CurJob = pooled;
        NpcMedicalAttempts.Attempt old = NpcMedicalAttempts.Begin(actor, pooled, patient, pooled.targetB.Thing)!;
        actor.inventory.innerContainer.Items.Clear();
        pooled.loadID++; pooled.def = KnownJobDefOf.UseBandage; pooled.targetB = default;
        NpcMedicalAi.OnPreviewBleeding = (_, _, preview) => preview(() => pooled,
            KnownJobDefOf.UseBandage, null, .05f, 80, 0, 0);
        Assert.AreSame(pooled, Choose(actor, patient));
        NpcMedicalAttempts.Attempt replacement = NpcMedicalAttempts.Begin(actor, pooled, patient, pooled.targetB.Thing)!;
        Assert.IsTrue(old.Completed);
        NpcMedicalAttempts.Finish(old, JobCondition.InterruptOptional);
        Assert.IsFalse(replacement.Completed);
        Assert.IsTrue(NpcQuickAid.HasGrant(actor, patient));
        NpcMedicalAttempts.Finish(replacement, JobCondition.Incompletable);
        Assert.IsFalse(NpcQuickAid.HasGrant(actor, patient));
    }
}
