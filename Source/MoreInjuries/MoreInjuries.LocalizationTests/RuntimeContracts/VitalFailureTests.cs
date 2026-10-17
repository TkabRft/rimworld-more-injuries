using System.Globalization;
using System.Xml.Linq;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.VitalFailure;
using MoreInjuries.Patches;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class VitalFailureTests
{
    private static void InitializeDefs()
    {
        VitalFailureDefOf.MI_VitalFailureConfig = new();
        VitalFailureDefOf.Metabolism = new();
        XDocument xml = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs", "HediffDefs", "Hediffs_VitalFailure.xml"));
        HediffDef Definition(string name)
        {
            XElement node = xml.Root!.Elements().Single(def => def.Element("defName")?.Value == name);
            return new() { label = node.Element("label")!.Value, lethalSeverity = 1f,
                Extension = new VitalFailureProperties { Rate = float.Parse(node.Descendants("severityPerDay").Single().Value, CultureInfo.InvariantCulture) } };
        }
        VitalFailureDefOf.MI_RespiratoryFailure = Definition("MI_RespiratoryFailure");
        VitalFailureDefOf.MI_LiverFailure = Definition("MI_LiverFailure");
        VitalFailureDefOf.MI_RenalFailure = Definition("MI_RenalFailure");
        VitalFailureDefOf.MI_FiltrationFailure = Definition("MI_FiltrationFailure");
        VitalFailureDefOf.MI_MetabolicFailure = Definition("MI_MetabolicFailure");
        VitalFailureDefOf.MI_Coma = Definition("MI_Coma");
        MoreInjuriesMod.Settings.EnableDelayedVitalFailure = true;
        MoreInjuriesMod.Settings.EnableHypovolemicShock = true;
        MoreInjuriesMod.Settings.AnomalyEnableConditionsForShamblers = true;
        ModsConfig.BiotechActive = false;
        ModsConfig.AnomalyActive = false;
        Rand.RangeFraction = 0.5f;
        DefDatabase<PawnCapacityDef>.AllDefsListForReading.Clear();
    }

    private static Pawn Patient(int offset = 0)
    {
        InitializeDefs();
        Pawn pawn = new() { Tracked = true, HashOffset = offset };
        pawn.health.hediffSet.pawn = pawn;
        pawn.Vital = new() { parent = pawn };
        foreach (BodyPartTagDef tag in new[] { BodyPartTagDefOf.ConsciousnessSource, BodyPartTagDefOf.BloodPumpingSource,
            BodyPartTagDefOf.BloodFiltrationLiver, BodyPartTagDefOf.BloodFiltrationKidney, BodyPartTagDefOf.BloodFiltrationKidney })
        {
            pawn.RaceProps.body.AllParts.Add(new() { def = new() { tags = [tag] } });
        }
        return pawn;
    }

    private static void Tick(Pawn pawn, int tick)
    {
        pawn.Tick = tick;
        Find.TickManager.TicksGame = tick;
        pawn.Vital!.TickHealth(1);
    }

    private static Hediff Add(Pawn pawn, HediffDef def, float severity)
    {
        Hediff hediff = pawn.health.AddHediff(def);
        hediff.Severity = severity;
        pawn.Vital!.Invalidate();
        return hediff;
    }

    [TestMethod]
    public void CompleteFailureHasThreeHourWindowAcrossSeedsAndOffsets()
    {
        foreach (PawnCapacityDef route in new[] { PawnCapacityDefOf.BloodPumping, PawnCapacityDefOf.Breathing })
        {
            List<float> times = [];
            for (int seed = 0; seed < 32; seed++)
            {
                Pawn pawn = Patient(seed * 137);
                pawn.health.hediffSet.Capacities[route] = 0;
                Random random = new(seed);
                bool dead = false;
                for (int tick = 0; tick <= 11000; tick++)
                {
                    Rand.RangeFraction = (float)random.NextDouble();
                    Tick(pawn, tick);
                    if (pawn.health.hediffSet.hediffs.Any(h => h.CauseDeathNow()))
                    {
                        times.Add(tick / 2500f); dead = true; break;
                    }
                }
                Assert.IsTrue(dead);
                Assert.IsTrue(times[^1] >= 2.4 && times[^1] <= 3.85, times[^1].ToString());
            }
            Assert.AreEqual(3f, times.Average(), 0.15);
        }
    }

    [TestMethod]
    public void FullShockCannotSurviveThroughLuckyMissesAndSourcesUseMaximum()
    {
        Pawn pawn = Patient();
        Add(pawn, HediffDefOf.BloodLoss, 1f);
        Add(pawn, KnownHediffDefOf.HypovolemicShock, 1f);
        pawn.Vital!.Refresh();
        Assert.AreEqual(1f, pawn.Vital.Deprivation, 0.00001);
        Add(pawn, KnownHediffDefOf.Hemodilution, 1f);
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0;
        pawn.Vital.Invalidate(); pawn.Vital.Refresh();
        Assert.AreEqual(1f, pawn.Vital.Deprivation, 0.00001);
        for (int t = 0; t <= 7500; t++) { Tick(pawn, t); }
        Hediff brain = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.BrainDamage_Hypoxia)!;
        Assert.IsTrue(brain.Severity >= 8f);
        Assert.IsTrue(brain.CauseDeathNow());
        Assert.AreEqual(1, pawn.health.hediffSet.hediffs.Count(h => h.def == KnownHediffDefOf.BrainDamage_Hypoxia));
    }

    [TestMethod]
    public void TendingAdrenalineColdAndResuscitationAffectTheRightSources()
    {
        Pawn pawn = Patient();
        Hediff blood = Add(pawn, HediffDefOf.BloodLoss, 1f);
        Hediff shock = Add(pawn, KnownHediffDefOf.HypovolemicShock, 1f);
        shock.TestComps.Add(new HediffComp_TendDuration { parent = shock, tendQuality = .3f, tendTicksLeft = 1000 });
        Add(pawn, KnownHediffDefOf.AdrenalineRush, .75f);
        pawn.Vital!.Refresh();
        Assert.AreEqual(.85f / 1.375f, pawn.Vital.Deprivation, .00001);
        Hediff cold = Add(pawn, HediffDefOf.Hypothermia, 1f);
        pawn.Vital.Refresh();
        Assert.AreEqual(.85f / 1.375f * .2f, pawn.Vital.Deprivation, .00001);
        cold.Severity = 0;
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0;
        pawn.Vital.Invalidate(); pawn.Vital.Refresh();
        Assert.AreEqual(1f, pawn.Vital.Deprivation, .00001, "Tending/adrenaline cannot replace absent breathing.");
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 1;
        blood.Severity = .4f;
        pawn.Vital.Invalidate(); pawn.Vital.Refresh();
        Assert.AreEqual(0f, pawn.Vital.Deprivation);
        Assert.AreEqual(1f, shock.Severity, "Volume restoration acts before visible shock subsides.");
    }

    [TestMethod]
    public void ActiveHypoxiaCannotHealButRecoveryRetainsDamage()
    {
        Pawn pawn = Patient();
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = .2f;
        Tick(pawn, 0);
        Hediff_Injury brain = (Hediff_Injury)pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.BrainDamage_Hypoxia)!;
        float severity = brain.Severity;
        brain.Heal(10);
        Assert.AreEqual(severity, brain.Severity);
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 1;
        pawn.Vital!.Invalidate(); pawn.Vital.Refresh();
        Assert.AreEqual(severity, brain.Severity);
        brain.Heal(severity / 2);
        Assert.AreEqual(severity / 2, brain.Severity);
    }

    [TestMethod]
    public void PairedOrgansReplacementsAndUnknownAnatomyHaveCorrectFailureOwnership()
    {
        Pawn pawn = Patient();
        var parts = pawn.RaceProps.body.AllParts;
        parts[3].Missing = true;
        pawn.Vital!.Refresh();
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_RenalFailure));
        parts[4].Efficiency = 0;
        parts[2].Efficiency = 0;
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodFiltration] = 0;
        pawn.Vital.Invalidate(); pawn.Vital.Refresh();
        Assert.IsTrue(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_RenalFailure));
        Assert.IsTrue(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_LiverFailure));
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_FiltrationFailure));
        parts[4].Efficiency = 1; parts[2].Efficiency = 1;
        pawn.Vital.Invalidate(); pawn.Vital.Refresh();
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_RenalFailure));
        Assert.IsTrue(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_FiltrationFailure));
        Pawn alien = Patient(); alien.RaceProps.body = new();
        alien.Vital!.Refresh();
        Assert.IsFalse(alien.health.hediffSet.HasHediff(VitalFailureDefOf.MI_LiverFailure));
        Assert.IsFalse(alien.health.hediffSet.HasHediff(VitalFailureDefOf.MI_RenalFailure));
    }

    [TestMethod]
    public void EveryFailureUsesXmlRateAndResolvesOnRestoredFunction()
    {
        Pawn pawn = Patient();
        pawn.health.hediffSet.Capacities[VitalFailureDefOf.Metabolism] = 0;
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Consciousness] = 0;
        Tick(pawn, 0);
        Hediff metabolic = pawn.health.hediffSet.GetFirstHediffOfDef(VitalFailureDefOf.MI_MetabolicFailure)!;
        Hediff coma = pawn.health.hediffSet.GetFirstHediffOfDef(VitalFailureDefOf.MI_Coma)!;
        Assert.AreEqual(.001f + .34f / 240f, metabolic.Severity, .00001);
        Assert.AreEqual(.001f + .2f / 240f, coma.Severity, .00001);
        Assert.AreEqual(0f, pawn.Vital!.Deprivation);
        pawn.health.hediffSet.Capacities.Clear(); pawn.Vital.Invalidate(); pawn.Vital.Refresh();
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_Coma));
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_MetabolicFailure));
    }

    [TestMethod]
    public void RequiredCapacityGuardPreservesStructuralDeathAndOtherCauses()
    {
        Pawn pawn = Patient();
        foreach (PawnCapacityDef capacity in new[] { PawnCapacityDefOf.BloodPumping, PawnCapacityDefOf.Breathing,
            PawnCapacityDefOf.BloodFiltration, PawnCapacityDefOf.Consciousness, VitalFailureDefOf.Metabolism })
        {
            PawnCapacityDef? result = capacity;
            Patch_VitalFailure_RequiredCapacity.Postfix(pawn, ref result);
            Assert.IsNull(result);
        }
        PawnCapacityDef other = new(); PawnCapacityDef? unrelated = other;
        Patch_VitalFailure_RequiredCapacity.Postfix(pawn, ref unrelated);
        Assert.AreSame(other, unrelated);
        other.lethalFlesh = true;
        DefDatabase<PawnCapacityDef>.AllDefsListForReading.Add(other);
        pawn.health.capacities.Incapable.Add(other);
        PawnCapacityDef? second = PawnCapacityDefOf.BloodPumping;
        Patch_VitalFailure_RequiredCapacity.Postfix(pawn, ref second);
        Assert.AreSame(other, second, "A supported first failure must not mask an unrelated second failure.");
        pawn.RaceProps.body.AllParts[0].Missing = true;
        PawnCapacityDef? structural = PawnCapacityDefOf.Consciousness;
        Patch_VitalFailure_RequiredCapacity.Postfix(pawn, ref structural);
        Assert.IsNotNull(structural);
        Assert.IsTrue(Add(pawn, new HediffDef { lethalSeverity = 1 }, 1).CauseDeathNow());
    }

    [TestMethod]
    public void EligibilityImmunityDeathrestAndToggleGuardsWork()
    {
        for (int mode = 0; mode < 7; mode++)
        {
            Pawn pawn = Patient();
            pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0;
            if (mode == 0) { pawn.Tracked = false; }
            if (mode == 1) { pawn.RaceProps.IsFlesh = false; }
            if (mode == 2) { pawn.OxygenImmune = true; }
            if (mode == 3) { ModsConfig.BiotechActive = true; Add(pawn, HediffDefOf.Deathrest, 1); }
            if (mode == 4) { MoreInjuriesMod.Settings.EnableDelayedVitalFailure = false; }
            if (mode == 5) { pawn.IsShambler = true; MoreInjuriesMod.Settings.AnomalyEnableConditionsForShamblers = false; }
            if (mode == 6) { pawn.Tracked = false; pawn.RaceProps.IsAnomalyEntity = true; ModsConfig.AnomalyActive = true; }
            Tick(pawn, 0);
            Assert.AreEqual(mode == 6, pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_RespiratoryFailure));
            Assert.AreEqual(mode == 6, pawn.health.hediffSet.HasHediff(KnownHediffDefOf.BrainDamage_Hypoxia));
        }
    }

    [TestMethod]
    public void HeartAttackAndMechanicalArrestHaveDistinctResuscitationRules()
    {
        Pawn pawn = Patient();
        Hediff attack = Add(pawn, KnownHediffDefOf.HeartAttack, 1);
        pawn.Vital!.Refresh();
        Assert.IsFalse(attack.CauseDeathNow());
        Assert.IsTrue(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
        Assert.AreEqual(0, PawnCapacityDefOf.BloodPumping.Worker.CalculateCapacityLevel(pawn.health.hediffSet));
        Assert.IsTrue(pawn.Vital.CanRestoreCirculation());
        pawn.Vital.RestoreCirculation();
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.HeartAttack));
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 0;
        pawn.Vital.Invalidate(); pawn.Vital.Refresh();
        Assert.IsFalse(pawn.Vital.CanRestoreCirculation());
        pawn.Vital.RestoreCirculation();
        Assert.IsTrue(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 1;
        pawn.Vital.Invalidate(); pawn.Vital.Refresh();
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
    }

    [TestMethod]
    public void RecursiveStateChecksDoNotKillOrAdvanceAndHealthTicksDoNotDoubleRun()
    {
        Pawn pawn = Patient();
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 0;
        pawn.health.OnChanged = () =>
        {
            Patch_VitalFailure_Dirty.Postfix(pawn);
            Patch_VitalFailure_StateChange.Prefix(pawn);
            PawnCapacityDef? result = PawnCapacityDefOf.BloodPumping;
            Patch_VitalFailure_RequiredCapacity.Postfix(pawn, ref result);
            Assert.IsNull(result);
        };
        pawn.Vital!.Refresh();
        Hediff arrest = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.CardiacArrest)!;
        Assert.AreEqual(.001f, arrest.Severity);
        Tick(pawn, 0); float once = arrest.Severity;
        Patch_VitalFailure_HealthTick.Postfix(pawn, 1);
        Assert.AreEqual(once, arrest.Severity);
        int reads = pawn.health.hediffSet.CapacityReads;
        for (int i = 1; i < 250; i++) { Tick(pawn, i); }
        Assert.AreEqual(reads, pawn.health.hediffSet.CapacityReads);
    }

    [TestMethod]
    public void NumericHelpersAllocateZeroBytesAfterWarmup()
    {
        float sum = 0;
        for (int i = 0; i < 10000; i++) { sum += VitalFailureModel.Deprivation(.2f, .1f, .3f, .5f); }
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100000; i++)
        {
            sum += VitalFailureModel.Advance(.1f, 64f * VitalFailureModel.Deprivation(.2f, .1f, .3f, .5f), .9f, 1f / 240f);
        }
        Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - start);
        Assert.IsTrue(sum > 0);
    }

    [TestMethod]
    public void SaveReloadPreservesMechanicalOriginAndDoesNotReplayProgress()
    {
        Pawn pawn = Patient();
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 0;
        Tick(pawn, 0);
        Hediff arrest = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.CardiacArrest)!;
        var oldArrestComp = arrest.GetComp<HediffComp_VitalArrest>()!;
        var saved = Scribe.Save(pawn.Vital!.PostExposeData);
        var savedArrest = Scribe.Save(oldArrestComp.CompExposeData);
        Assert.IsTrue(savedArrest.ContainsKey("mechanicalFailure"));
        var newArrestComp = new HediffComp_VitalArrest { parent = arrest, props = oldArrestComp.props };
        Scribe.Load(savedArrest, newArrestComp.CompExposeData);
        arrest.TestComps.Remove(oldArrestComp); arrest.TestComps.Add(newArrestComp);
        Assert.IsTrue(newArrestComp.MechanicalFailure);
        pawn.Vital = new() { parent = pawn };
        Scribe.Load(saved, pawn.Vital.PostExposeData);
        float before = arrest.Severity;
        Tick(pawn, 0);
        Assert.AreEqual(before, arrest.Severity);
        Tick(pawn, 250);
        Assert.AreEqual(before + 8f / 240f, arrest.Severity, .00001);
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 1;
        pawn.Vital.Invalidate(); pawn.Vital.Refresh();
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
    }

    [TestMethod]
    public void ActualCprAndDefibrillatorMethodsRespectPumpFunctionAndPreserveBrainDamage()
    {
        Pawn doctor = new() { MedicalSkill = 20 };
        var cpr = new HealthConditions.CardiacArrest.JobDriver_PerformCpr();
        var defib = new HealthConditions.CardiacArrest.JobDriver_UseDefibrillator();
        Pawn pawn = Patient();
        Add(pawn, KnownHediffDefOf.HeartAttack, 1f);
        Hediff brain = Add(pawn, KnownHediffDefOf.BrainDamage_Hypoxia, 3f);
        pawn.Vital!.Refresh();
        Rand.ChanceHandler = _ => true;
        Assert.IsTrue(defib.TestApply(doctor, pawn));
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.HeartAttack));
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
        Assert.AreEqual(3f, brain.Severity);
        Hediff arrest = Add(pawn, KnownHediffDefOf.CardiacArrest, .2f);
        Rand.ChanceHandler = _ => false;
        Assert.IsTrue(defib.TestApply(doctor, pawn));
        Assert.AreEqual(.2f, arrest.Severity);
        Assert.IsTrue(cpr.TestApply(doctor, pawn));
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 0;
        pawn.Vital.Invalidate(); pawn.Vital.Refresh();
        arrest = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.CardiacArrest)!;
        Assert.IsFalse(HealthConditions.CardiacArrest.JobDriver_UseDefibrillator.JobCanTreat(arrest));
        Assert.IsFalse(defib.TestApply(doctor, pawn));
        cpr.TestApply(doctor, pawn);
        Assert.IsTrue(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
        Assert.AreEqual(3f, brain.Severity);
        Hediff choke = Add(pawn, KnownHediffDefOf.ChokingOnBlood, .5f);
        cpr.TestApply(doctor, pawn);
        Assert.IsFalse(pawn.health.hediffSet.hediffs.Contains(choke));
    }

    [TestMethod]
    public void ArtificialHeartBlocksSpontaneousArrestButNotActualPumpFailure()
    {
        Pawn pawn = Patient();
        var heart = RimWorld.BodyPartDefOf.Heart;
        heart.tags = [BodyPartTagDefOf.BloodPumpingSource];
        pawn.RaceProps.body.AllParts[1].def = heart;
        pawn.health.hediffSet.hediffs.Add(new Hediff_AddedPart { pawn = pawn, def = new(), Part = pawn.RaceProps.body.AllParts[1] });
        Add(pawn, KnownHediffDefOf.CardiacArrest, .01f);
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 0;
        pawn.Vital!.Invalidate(); pawn.Vital.Refresh();
        Assert.IsTrue(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
    }

    [TestMethod]
    public void NaturalBloodRecoveryDoesNotRescueUntreatedFullShockButBloodCan()
    {
        foreach (bool transfuse in new[] { false, true })
        {
            Pawn pawn = Patient();
            Hediff blood = Add(pawn, HediffDefOf.BloodLoss, 1);
            Hediff shock = Add(pawn, KnownHediffDefOf.HypovolemicShock, 1);
            var tend = new HediffComp_TendDuration { parent = shock, tendQuality = .3f, tendTicksLeft = 10000 };
            shock.TestComps.Add(tend);
            float maximum = 0;
            for (int t = 0; t <= 30000; t++)
            {
                if (t % 60 == 0) { blood.Severity = Math.Max(0f, blood.Severity - .00033333333f); }
                if (t == 5000 && transfuse) { blood.Severity = Math.Max(0, blood.Severity - .35f); }
                tend.tendTicksLeft = Math.Max(0, 10000 - t);
                if (t % 150 == 0)
                {
                    shock.Severity = HealthConditions.HypovolemicShock.ShockPerfusionModel.Advance(shock.Severity, blood.Severity, 0, tend.IsTended ? tend.tendQuality : 0, false);
                    pawn.Vital!.Invalidate();
                }
                Tick(pawn, t);
                maximum = Math.Max(maximum, pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.BrainDamage_Hypoxia)?.Severity ?? 0);
            }
            Assert.AreEqual(!transfuse, maximum >= 8, $"transfuse={transfuse}, peak={maximum}");
        }
    }

    [TestMethod]
    public void LoadingDoesNotCreateConditionsAndMultipleBrainsPreserveStructuralRules()
    {
        Pawn pawn = Patient();
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 0;
        Scribe.mode = LoadSaveMode.LoadingVars;
        try { pawn.Vital!.Refresh(); }
        finally { Scribe.mode = LoadSaveMode.Inactive; }
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
        pawn.RaceProps.body.AllParts.Add(new() { def = new() { tags = [BodyPartTagDefOf.ConsciousnessSource] } });
        pawn.RaceProps.body.AllParts[0].Missing = true;
        Assert.IsTrue(pawn.Vital!.MayDelay(PawnCapacityDefOf.Consciousness));
        pawn.RaceProps.body.AllParts[^1].Health = 0;
        Assert.IsFalse(pawn.Vital.MayDelay(PawnCapacityDefOf.Consciousness));
    }

    [TestMethod]
    public void OxygenImmunityDoesNotProtectAgainstLiverOrUnrelatedDisease()
    {
        Pawn pawn = Patient(); pawn.OxygenImmune = true;
        pawn.RaceProps.body.AllParts[2].Efficiency = 0;
        pawn.Vital!.Refresh();
        Hediff liver = pawn.health.hediffSet.GetFirstHediffOfDef(VitalFailureDefOf.MI_LiverFailure)!;
        liver.Severity = 1;
        Assert.IsTrue(liver.CauseDeathNow());
        Hediff brain = Add(pawn, KnownHediffDefOf.BrainDamage_Hypoxia, 8);
        Assert.IsFalse(brain.CauseDeathNow());
        Assert.IsTrue(Add(pawn, KnownHediffDefOf.HeartAttack, 1).CauseDeathNow(), "Unconverted disease retains its native death rule.");
    }

    [TestMethod]
    public void SystemicOwnershipKeepsFocalSourcesAndLegacyFallback()
    {
        Pawn pawn = Patient();
        foreach (HediffDef source in new[] { KnownHediffDefOf.HypovolemicShock, KnownHediffDefOf.CardiacArrest, KnownHediffDefOf.Hemodilution })
        {
            Assert.IsTrue(VitalFailureComp.OwnsSystemicCerebralSource(pawn, source, KnownHediffDefOf.BrainDamage_Hypoxia));
            Assert.IsFalse(VitalFailureComp.OwnsSystemicCerebralSource(pawn, source, KnownHediffDefOf.OrganHypoxia));
        }
        Assert.IsFalse(VitalFailureComp.OwnsSystemicCerebralSource(pawn, new HediffDef(), KnownHediffDefOf.BrainDamage_Hypoxia));
        MoreInjuriesMod.Settings.EnableDelayedVitalFailure = false;
        Assert.IsFalse(VitalFailureComp.OwnsSystemicCerebralSource(pawn, KnownHediffDefOf.CardiacArrest, KnownHediffDefOf.BrainDamage_Hypoxia));
        Hediff arrest = Add(pawn, KnownHediffDefOf.CardiacArrest, .2f);
        Assert.AreEqual(8f, arrest.GetComp<HediffComp_VitalArrest>()!.SeverityChangePerDay());
        MoreInjuriesMod.Settings.EnableDelayedVitalFailure = true;
        Assert.AreEqual(0f, arrest.GetComp<HediffComp_VitalArrest>()!.SeverityChangePerDay());
    }

    [TestMethod]
    public void FailureRatesAndTerminalSettingsMatchRuntimeXml()
    {
        Patient();
        foreach (var item in new[] { (VitalFailureDefOf.MI_RespiratoryFailure, 8f), (VitalFailureDefOf.MI_LiverFailure, 1f),
            (VitalFailureDefOf.MI_RenalFailure, .2f), (VitalFailureDefOf.MI_FiltrationFailure, 1f),
            (VitalFailureDefOf.MI_MetabolicFailure, .34f), (VitalFailureDefOf.MI_Coma, .2f) })
        {
            Assert.AreEqual(item.Item2, item.Item1.GetModExtension<VitalFailureProperties>()!.SeverityPerDay);
        }
        Assert.AreEqual(64f, VitalFailureDefOf.MI_VitalFailureConfig.CerebralSeverityPerDay);
        Assert.AreEqual(8f, VitalFailureDefOf.MI_VitalFailureConfig.TerminalCerebralSeverity);
    }
}
