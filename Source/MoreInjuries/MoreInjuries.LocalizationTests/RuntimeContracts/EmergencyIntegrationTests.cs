using System.Xml.Linq;
using MoreInjuries.AI;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.HeavyBleeding.Overrides;
using MoreInjuries.HealthConditions.VitalFailure;
using MoreInjuries.Integrations.CombatExtended;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass, DoNotParallelize]
public sealed class EmergencyIntegrationTests
{
    [TestCleanup]
    public void Cleanup()
    {
        CombatExtendedPrognosisTests.Initialize(false);
        MoreInjuriesMod.Settings.EnableAdrenaline = false;
        KnownResearchProjectDefOf.Cpr.IsFinished = true;
        KnownResearchProjectDefOf.BasicAnatomy.IsFinished = true;
        KnownResearchProjectDefOf.AdvancedFirstAid.IsFinished = true;
        GenClosest.Device = null;
    }

    private static (Pawn Doctor, Pawn Patient) Pair(bool ce = false)
    {
        CombatExtendedPrognosisTests.Initialize(ce);
        Pawn doctor = VitalFailureTests.Patient(), patient = VitalFailureTests.Patient();
        patient.playerSettings = new(); patient.health.hediffSet.NativeBleeding = true;
        DefDatabase<JobDef>.AllDefsListForReading.Clear();
        DefDatabase<JobDef>.AllDefsListForReading.Add(new() { defName = "Stabilize", driverClass = typeof(CombatExtended.JobDriver_Stabilize) });
        return (doctor, patient);
    }

    private static CEWoundFixture CeWound(Pawn patient)
    {
        CEWoundFixture wound = new() { pawn = patient, def = new(), Part = new(), UntreatedRate = 3, Severity = 1 };
        wound.Stabilization = new() { parent = wound, InitialModifier = -.5f }; wound.comps.Add(wound.Stabilization);
        patient.health.hediffSet.hediffs.Add(wound); patient.health.hediffSet.NativeBleeding = true;
        return wound;
    }

    [TestMethod]
    public void SharedSelectorAllowsOneQuickMajorBleedAndPrioritizesResuscitationOverBandages()
    {
        var (doctor, patient) = Pair();
        Hediff wound = VitalFailureTests.Add(patient, new(), 1); wound.TestBleedingEligible = true; wound.TestBleedRate = 1f;
        Hediff arrest = VitalFailureTests.Add(patient, KnownHediffDefOf.CardiacArrest, .1f);
        VitalFailureTests.Add(patient, KnownHediffDefOf.ChokingOnBlood, .8f);
        doctor.Devices[KnownThingDefOf.Bandage] = new(); doctor.Devices[KnownThingDefOf.HemostaticAgent] = new();
        foreach (bool firstAid in new[] { false, true })
        {
            Assert.AreSame(KnownJobDefOf.Hemostat, EmergencyCare.CreateJob(doctor, patient, firstAid)!.def);
            doctor.Devices.Remove(KnownThingDefOf.HemostaticAgent);
            Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, firstAid)!.def);
            doctor.Devices.Remove(KnownThingDefOf.Bandage);
            Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, firstAid)!.def);
            arrest.Severity = .8f; // Late arrest must still precede the airway-only branch.
            Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, firstAid)!.def);
            doctor.Devices[KnownThingDefOf.Bandage] = new(); doctor.Devices[KnownThingDefOf.HemostaticAgent] = new();
        }
        patient.health.hediffSet.hediffs.Remove(arrest); doctor.Devices.Clear();
        Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        Assert.AreEqual(string.Empty, patient.Vital!.PrognosisTooltip, "Work selection must not forecast.");
    }

    [TestMethod]
    public void CeNativeJobUsesAllowedMedicineFromDoctorPatientOrCarriedAndKeepsOneMedicineCount()
    {
        var (doctor, patient) = Pair(true); CeWound(patient);
        Medicine herbal = new() { def = new() { MedicineTier = 1 }, Potency = .6f };
        Medicine industrial = new() { def = new() { MedicineTier = 2 }, Potency = 1f };
        Medicine carried = new() { def = new() { MedicineTier = 3 }, Potency = 1.6f };
        doctor.inventory.innerContainer.Add(herbal); patient.inventory.innerContainer.Add(industrial); doctor.carryTracker.CarriedThing = carried;
        var job = EmergencyCare.CreateJob(doctor, patient, true)!;
        Assert.AreEqual("Stabilize", ((JobDef)job.def).defName); Assert.AreEqual(1, job.count); Assert.AreSame(carried, job.Targets[1]);
        patient.playerSettings!.medCare = MedicalCareCategory.HerbalOrWorse;
        job = EmergencyCare.CreateJob(doctor, patient, true)!; Assert.AreSame(herbal, job.Targets[1]);
        herbal.Forbidden = true; Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false));
        herbal.Forbidden = false; herbal.Reservable = false; Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false));
        herbal.Reservable = true; patient.playerSettings.medCare = MedicalCareCategory.NoMeds;
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false));
        patient.playerSettings.medCare = MedicalCareCategory.NoCare; Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false));
    }

    [TestMethod]
    public void NpcCeStabilizationCannotFallBackToUnboundedOrUnselectedMedicine()
    {
        var (doctor, patient) = Pair(true); CeWound(patient);
        Medicine nearby = new() { def = new() { MedicineTier = 2 }, Potency = 1f };
        Medicine unselected = new() { def = new() { MedicineTier = 3 }, Potency = 1.6f };
        doctor.inventory.innerContainer.Add(unselected);
        patient.inventory.innerContainer.Add(nearby);
        GenClosest.Device = unselected;
        EmergencyCare.NpcContext context = new() { Find = _ => null, Medicine = () => null };
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false, context));
        context.Medicine = () => nearby;
        var job = EmergencyCare.CreateJob(doctor, patient, false, context)!;
        Assert.IsNotNull(job);
        Assert.AreSame(nearby, job.Targets[1]);
        patient.playerSettings!.medCare = MedicalCareCategory.NoCare;
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false, context));
        context.IgnoreCare = true; // Only supplied by the NPC caller for a prisoner's own care.
        Assert.IsNotNull(EmergencyCare.CreateJob(doctor, patient, false, context));
    }

    [TestMethod]
    public void NpcShockEpinephrineIsClinicalAndHonorsItsSeparateSafetyGate()
    {
        var (doctor, patient) = Pair();
        MoreInjuriesMod.Settings.EnableAdrenaline = true;
        VitalFailureTests.Add(patient, KnownHediffDefOf.HypovolemicShock, .6f);
        Thing injector = new(); doctor.Devices[KnownThingDefOf.Epinephrine] = injector;
        EmergencyCare.NpcContext context = new() { Emergency = true, Find = def => def == KnownThingDefOf.Epinephrine ? injector : null };
        Assert.AreSame(KnownJobDefOf.UseEpinephrine, EmergencyCare.CreateJob(doctor, patient, false, context)!.def);
        context.ClinicalAllowed = false;
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false, context));
        context.ClinicalAllowed = true;
        VitalFailureTests.Add(patient, KnownHediffDefOf.AdrenalineRush, .3f);
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false, context));
    }

    [TestMethod]
    public void CeFallbackDoesNotBlockResuscitationOrRetryAlreadyStabilizedWounds()
    {
        var (doctor, patient) = Pair(true); CEWoundFixture wound = CeWound(patient);
        VitalFailureTests.Add(patient, KnownHediffDefOf.CardiacArrest, .1f);
        Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, true)!.def);
        doctor.inventory.innerContainer.Add(new Medicine());
        Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, true)!.def);
        wound.Stabilization!.Stabilize(doctor, new());
        Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, true)!.def);
        doctor.Reachable = false; Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false));
    }

    [TestMethod]
    public void ResearchAndDoctorCapacityLimitOnlyFeasibleTreatments()
    {
        var (doctor, patient) = Pair();
        Hediff wound = VitalFailureTests.Add(patient, new(), 1); wound.TestBleedingEligible = true; wound.TestBleedRate = 1f;
        VitalFailureTests.Add(patient, KnownHediffDefOf.CardiacArrest, .1f);
        doctor.Devices[KnownThingDefOf.HemostaticAgent] = new(); doctor.Devices[KnownThingDefOf.Bandage] = new();
        KnownResearchProjectDefOf.AdvancedFirstAid.IsFinished = false;
        Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        KnownResearchProjectDefOf.BasicAnatomy.IsFinished = false;
        Assert.AreSame(KnownJobDefOf.PerformCpr, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        KnownResearchProjectDefOf.Cpr.IsFinished = false; Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false));
        KnownResearchProjectDefOf.Cpr.IsFinished = true;
        doctor.health.capacities.Incapable.Add(PawnCapacityDefOf.Manipulation);
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, true));
    }

    [TestMethod]
    public void TemporaryZeroBleedingCannotTriggerRemovalOverAnUntreatedTourniquetWound()
    {
        var (doctor, patient) = Pair();
        BodyPartRecord limb = new();
        StatefulWound wound = new() { pawn = patient, def = new() { injuryProps = new() { bleedRate = 1f } }, Part = limb, Severity = 1 };
        patient.health.hediffSet.hediffs.Add(wound); patient.health.hediffSet.NativeBleeding = true;
        VitalFailureTests.Add(patient, KnownHediffDefOf.TourniquetApplied, .1f).Part = limb;
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, true));
        wound.Tended = true;
        Assert.AreSame(KnownJobDefOf.RemoveTourniquet, EmergencyCare.CreateJob(doctor, patient, true)!.def);
    }

    [TestMethod]
    public void CeBatchIsNotStartedSolelyToStackOnMiControlledWounds()
    {
        var (doctor, patient) = Pair(true);
        StatefulWound controlled = new() { pawn = patient, def = new(), Part = new(), UntreatedRate = 3, Severity = 1 };
        controlled.Stabilization = new() { parent = controlled }; controlled.comps.Add(controlled.Stabilization);
        patient.health.hediffSet.hediffs.Add(controlled); patient.health.hediffSet.NativeBleeding = true;
        doctor.inventory.innerContainer.Add(new Medicine());
        Assert.IsNull(CombatExtendedBridge.TryCreateStabilizationJob(doctor, patient));
        CeWound(patient);
        var job = CombatExtendedBridge.TryCreateStabilizationJob(doctor, patient)!;
        Assert.AreEqual("Stabilize", ((JobDef)job.def).defName);
        Assert.AreEqual(1, job.count, "A remaining eligible wound enables CE's ordinary one-medicine batch.");
    }

    [TestMethod]
    public void TemporaryHemostasisAndCeProfilesRetainWoundUnitsAndExpireIndependently()
    {
        var (_, patient) = Pair(); patient.HealthScale = 2f;
        StatefulWound wound = new() { pawn = patient, def = new(), UntreatedRate = 4, Severity = 1 };
        patient.health.hediffSet.hediffs.Add(wound); patient.health.hediffSet.NativeBleeding = true;
        PhysiologySnapshot source = new() { BleedRate = patient.health.hediffSet.BleedRateTotal, NextBloodTick = 60 };
        CombatExtendedBleedingCapture? capture = null;
        CombatExtendedBridge.Capture(patient, ref source, ref capture);
        Assert.AreEqual(.4f, source.BleedRate, .00001f);
        Assert.AreEqual(1.2f, PrognosisBleeding.Total(in source, 15000), .00001f);
        Assert.AreEqual(2f, PrognosisBleeding.Total(in source, 30000), .00001f);
        Assert.AreEqual(4f, capture!.For(wound).At(30000), .00001f, "Choking needs wound units, not total/HealthScale.");
        Assert.IsFalse(source.CeStabilization);
        PrognosisBleeding combined = new(4f, -.5f, false, false, 60, constant: .5f, timedBase: .2f, remaining: 30000, duration: 30000);
        Assert.AreEqual(0f, combined.At(0)); Assert.IsTrue(combined.At(6000) > 0f); Assert.AreEqual(2f, combined.At(30000));
    }

    [TestMethod]
    public void ChokingCourseRespondsToReturningBleedingCoughingTendingAndSourceRemoval()
    {
        PrognosisTrend trend = new() { Severity = .6f, Maximum = 1f, Interval = 200, NextTick = 200,
            Choking = new() { Active = true, Interval = 750, NextTick = 750, SourcePresent = true, CoughCapable = true,
                PositiveFactor = 1, NegativeFactor = 1, Source = new(3, -.5f, false, false, 60) } };
        Assert.IsTrue(trend.At(1500) < trend.Severity, "A conscious patient can cough while bleeding is completely suppressed.");
        Assert.IsTrue(trend.At(9000) > trend.At(3000), "Bleeding return must worsen the airway forecast.");
        trend.Choking.SourcePresent = false;
        Assert.AreEqual(0f, trend.At(20000));
        trend.Choking.SourcePresent = true; trend.Choking.Tended = true;
        trend.Choking.Source = new(0, 1, false, false, 60, false);
        Assert.AreEqual(0f, trend.At(20000));
        trend.Choking.CoughCapable = false;
        Assert.IsTrue(trend.At(1500) > trend.Severity);
        float severity = trend.Severity;
        for (int tick = 750; tick <= 9000; tick += 750)
        { severity = Math.Clamp(severity + PrognosisChoking.MeanChange(0, true, true, false, 1, 1), 0f, 1f); }
        Assert.AreEqual(severity, trend.At(9000), .00001f);
        for (int i = 0; i < 100; i++) { _ = trend.At(20000); }
        long bytes = GC.GetAllocatedBytesForCurrentThread(); _ = trend.At(20000);
        Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - bytes);
    }

    [TestMethod]
    public void ChokingStagesChangeBreathingAndProjectedCerebralRescueWindow()
    {
        _ = VitalFailureTests.Patient();
        XDocument defs = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs/HediffDefs/Hediffs_Choking.xml"));
        HediffDef choking = new() { stages = defs.Descendants("stages").First().Elements().Select(stage => new HediffStage
        { minSeverity = (float?)stage.Element("minSeverity") ?? 0, capMods = [new PawnCapacityModifier { capacity = PawnCapacityDefOf.Breathing,
            setMax = (float)stage.Descendants("setMax").First() }] }).ToList() };
        // Exercise production stage selection and prognosis, with the same source profile in wound units.
        PrognosisTrend trend = new() { Cause = choking, Severity = .7f, Maximum = 1, Interval = 200, NextTick = 200,
            Choking = new() { Active = true, Interval = 750, NextTick = 750, SourcePresent = true,
                CoughCapable = true, PositiveFactor = 1, NegativeFactor = 1, Source = new(4, -.1f, false, false, 60) } };
        PhysiologySnapshot source = new() { Relevant = true, VitalEnabled = true, CanHaveBrain = true, ColdProtection = 1, ComaMultiplier = 1,
            RawCapacities = [1,1,1,1,1], MinimumCapacities = [0,0,0,0,0], ApplicableCapacities = 31,
            Trends = [trend], TrendCount = 1, NextBloodTick = 60, NextShockTick = 150, NextVitalTick = 250,
            Failure = new(int.MaxValue, null), CapacityTermCount = choking.stages.Count,
            CapacityTerms = choking.stages.Select((stage, index) => new PrognosisCapacityTerm { Capacity = 0, Trend = 0,
                MinimumSeverity = stage.minSeverity, NextSeverity = index + 1 < choking.stages.Count ? choking.stages[index+1].minSeverity : float.MaxValue,
                Maximum = stage.capMods![0].setMax, Factor = 1 }).ToArray() };
        Assert.AreEqual(.3f, PrognosisCapacity.Level(in source, 0, 0));
        var estimate = PhysiologyPrognosis.Estimate(in source, VitalFailureDefOf.MI_VitalFailureConfig, HediffDefOf.BloodLoss, KnownHediffDefOf.BrainDamage_Hypoxia);
        Assert.IsTrue(estimate.Ticks < PhysiologyPrognosis.HORIZON);
        source.Trends[0].Choking.SourcePresent = false;
        Assert.IsTrue(PrognosisCapacity.Level(in source, 0, 12000) > .5f);
        var rescued = PhysiologyPrognosis.Estimate(in source, VitalFailureDefOf.MI_VitalFailureConfig, HediffDefOf.BloodLoss, KnownHediffDefOf.BrainDamage_Hypoxia);
        Assert.IsTrue(rescued.Ticks > estimate.Ticks);
    }

    private sealed class Hemostasis : IInjuryState, IForecastHemostasis
    {
        public bool IsTemporarilyCoagulated => true;
        public float CoagulationMultiplier => 1f;
        public void CaptureHemostasis(out float constant, out float timedBase, out int remaining, out int duration)
        { constant = 1; timedBase = .2f; remaining = duration = 30000; }
    }
    private sealed class StatefulWound : CEWoundFixture, IStatefulInjury
    {
        public IInjuryState State { get; } = new Hemostasis();
        public override float BleedRate => base.BleedRate * .2f;
    }
}
