using MoreInjuries.AI;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.Integrations.CombatExtended;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass, DoNotParallelize]
public sealed class SuperclottingEmergencyTests
{
    [TestCleanup]
    public void Cleanup()
    {
        ModsConfig.BiotechActive = false;
        DefDatabase<GeneDef>.AllDefsListForReading.Clear();
        CombatExtendedPrognosisTests.Initialize(false);
        GenClosest.Device = null;
    }

    private static (Pawn Doctor, Pawn Patient, Hediff Wound, Gene Gene) Pair(float rate)
    {
        CombatExtendedPrognosisTests.Initialize(false);
        Pawn doctor = VitalFailureTests.Patient(), patient = VitalFailureTests.Patient();
        doctor.Map = doctor.MapHeld = patient.Map = patient.MapHeld = new();
        patient.playerSettings = new(); patient.health.hediffSet.NativeBleeding = true;
        GeneDef definition = new() { defName = "Superclotting" };
        DefDatabase<GeneDef>.AllDefsListForReading.Clear();
        DefDatabase<GeneDef>.AllDefsListForReading.Add(definition);
        Gene gene = new() { def = definition };
        patient.genes = new(); patient.genes.GenesListForReading.Add(gene);
        ModsConfig.BiotechActive = true;
        Hediff wound = VitalFailureTests.Add(patient, new(), 1);
        wound.TestBleedingEligible = true; wound.TestBleedRate = rate;
        KnownResearchProjectDefOf.BasicAnatomy.IsFinished = KnownResearchProjectDefOf.BasicFirstAid.IsFinished = true;
        KnownResearchProjectDefOf.AdvancedFirstAid.IsFinished = KnownResearchProjectDefOf.Cpr.IsFinished = true;
        KnownResearchProjectDefOf.EmergencyMedicine.IsFinished = true;
        MoreInjuriesMod.Settings.EnableAdrenaline = false;
        return (doctor, patient, wound, gene);
    }

    private static Thing Supply(Pawn doctor, ThingDef definition, float value = 2)
    {
        Thing supply = new() { def = definition, Value = value };
        doctor.Devices[definition] = supply;
        return supply;
    }

    [TestMethod]
    [DataRow(.0999f, false)]
    [DataRow(.1f, true)]
    [DataRow(.5f, true)]
    public void MinorThresholdAppliesToBothDraftedAndRoutineCare(float rate, bool treat)
    {
        (Pawn doctor, Pawn patient, _, _) = Pair(rate);
        Supply(doctor, KnownThingDefOf.Bandage);
        Supply(doctor, KnownThingDefOf.HemostaticAgent, 6);
        foreach (bool aftercare in new[] { false, true })
        {
            if (treat) { Assert.AreSame(KnownJobDefOf.Bandage, EmergencyCare.CreateJob(doctor, patient, aftercare)!.def); }
            else { Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, aftercare)); }
        }
        Assert.AreEqual(string.Empty, patient.Vital!.PrognosisTooltip);
    }

    [TestMethod]
    public void OnlyAnActivePatientGeneWithBiotechAndItsDefinitionChangesSelection()
    {
        (Pawn doctor, Pawn patient, _, Gene gene) = Pair(.05f);
        Supply(doctor, KnownThingDefOf.Bandage);
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false));
        gene.Active = false;
        Assert.AreSame(KnownJobDefOf.Bandage, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        gene.Active = true; ModsConfig.BiotechActive = false;
        Assert.AreSame(KnownJobDefOf.Bandage, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        ModsConfig.BiotechActive = true; DefDatabase<GeneDef>.AllDefsListForReading.Clear();
        Assert.AreSame(KnownJobDefOf.Bandage, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        DefDatabase<GeneDef>.AllDefsListForReading.Add(gene.def);
        doctor.genes = patient.genes; patient.genes = null;
        Assert.AreSame(KnownJobDefOf.Bandage, EmergencyCare.CreateJob(doctor, patient, false)!.def,
            "The doctor's gene must not change the patient's care.");
    }

    [TestMethod]
    public void MinorThresholdUsesEachWoundsPawnNormalizedContribution()
    {
        (Pawn doctor, Pawn patient, Hediff wound, _) = Pair(.15f);
        Supply(doctor, KnownThingDefOf.Bandage);
        patient.HealthScale = 2f;
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false));
        patient.HealthScale = 1f; wound.TestBleedRate = .06f;
        Hediff another = VitalFailureTests.Add(patient, new(), 1);
        another.TestBleedingEligible = true; another.TestBleedRate = .06f;
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false), "Minor wounds do not become eligible just because their total exceeds .1/day.");
        another.TestBleedRate = .2f;
        Assert.AreSame(KnownJobDefOf.Bandage, EmergencyCare.CreateJob(doctor, patient, false)!.def);
    }

    [TestMethod]
    public void BandagesGainPreferenceWhileHemostatRemainsAFeasibleFallback()
    {
        (Pawn doctor, Pawn patient, _, Gene gene) = Pair(2f);
        Thing bandage = Supply(doctor, KnownThingDefOf.Bandage);
        Supply(doctor, KnownThingDefOf.HemostaticAgent, 6);
        Assert.AreSame(KnownJobDefOf.Bandage, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        bandage.Forbidden = true;
        Assert.AreSame(KnownJobDefOf.Hemostat, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        bandage.Forbidden = false; gene.Active = false;
        Assert.AreSame(KnownJobDefOf.Hemostat, EmergencyCare.CreateJob(doctor, patient, false)!.def);
    }

    [TestMethod]
    public void AutomaticTourniquetsStayDisabledButExplicitFirstAidTendsWoundsAndKeepsSafeRemoval()
    {
        (Pawn doctor, Pawn patient, Hediff wound, Gene gene) = Pair(2f);
        BodyPartRecord limb = new();
        patient.LimbBleeding.Add(new(limb, 2f));
        Supply(doctor, KnownThingDefOf.Tourniquet);
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false));
        gene.Active = false;
        Assert.AreSame(KnownJobDefOf.UseTourniquet, EmergencyCare.CreateJob(doctor, patient, false)!.def);
        gene.Active = true; patient.Downed = true; wound.TestBleedRate = .05f;
        patient.LimbBleeding.Clear();
        Assert.AreSame(JobDefOf.TendPatient, EmergencyCare.CreateJob(doctor, patient, true)!.def);
        gene.Active = false;
        Assert.AreSame(JobDefOf.TendPatient, EmergencyCare.CreateJob(doctor, patient, true)!.def);
        gene.Active = true;
        wound.Tended = true; wound.TestBleedRate = 0f;
        VitalFailureTests.Add(patient, KnownHediffDefOf.TourniquetApplied, .1f).Part = limb;
        Assert.AreSame(KnownJobDefOf.RemoveTourniquet, EmergencyCare.CreateJob(doctor, patient, true)!.def);
    }

    [TestMethod]
    [DataRow("arrest")]
    [DataRow("critical airway")]
    [DataRow("airway")]
    [DataRow("heart attack")]
    [DataRow("transfusion")]
    public void EveryFeasibleOtherEmergencyPrecedesEvenQuickMajorBleeding(string condition)
    {
        (Pawn doctor, Pawn patient, _, _) = Pair(3f);
        Supply(doctor, KnownThingDefOf.HemostaticAgent, 6);
        JobDef expected = KnownJobDefOf.PerformCpr;
        switch (condition)
        {
            case "arrest": VitalFailureTests.Add(patient, KnownHediffDefOf.CardiacArrest, .1f); break;
            case "critical airway":
            case "airway":
                VitalFailureTests.Add(patient, KnownHediffDefOf.ChokingOnBlood, .1f);
                patient.health.capacities.Levels[PawnCapacityDefOf.Breathing] = condition == "airway" ? .8f : .2f;
                break;
            case "heart attack":
                VitalFailureTests.Add(patient, KnownHediffDefOf.HeartAttack, .1f);
                Supply(doctor, KnownThingDefOf.Defibrillator, 100);
                expected = KnownJobDefOf.UseDefibrillator;
                break;
            case "transfusion":
                VitalFailureTests.Add(patient, HediffDefOf.BloodLoss, .5f);
                Supply(doctor, KnownThingDefOf.SalineBag);
                expected = KnownJobDefOf.Saline;
                break;
        }
        foreach (bool aftercare in new[] { false, true })
        { Assert.AreSame(expected, EmergencyCare.CreateJob(doctor, patient, aftercare)!.def); }
        if (condition == "transfusion")
        {
            doctor.Devices[KnownThingDefOf.SalineBag].Reservable = false;
            Assert.AreSame(KnownJobDefOf.Hemostat, EmergencyCare.CreateJob(doctor, patient, false)!.def,
                "An unavailable emergency action must not block feasible bleeding control.");
        }
    }

    [TestMethod]
    public void CeFallbackIgnoresMinorWoundsAsTriggersAndKeepsNativeBatchesForLargerOnes()
    {
        (Pawn doctor, Pawn patient, Hediff original, _) = Pair(.05f);
        CombatExtendedPrognosisTests.Initialize(true); // Resets neither patient genes nor Biotech activation.
        patient.health.hediffSet.hediffs.Remove(original);
        DefDatabase<JobDef>.AllDefsListForReading.Clear();
        DefDatabase<JobDef>.AllDefsListForReading.Add(new() { defName = "Stabilize", driverClass = typeof(CombatExtended.JobDriver_Stabilize) });
        CEWoundFixture wound = new() { pawn = patient, def = new(), Part = new(), UntreatedRate = .06f, Severity = 1 };
        wound.Stabilization = new() { parent = wound }; wound.comps.Add(wound.Stabilization);
        patient.health.hediffSet.hediffs.Add(wound);
        CEWoundFixture minor = new() { pawn = patient, def = new(), Part = new(), UntreatedRate = .06f, Severity = 1 };
        minor.Stabilization = new() { parent = minor }; minor.comps.Add(minor.Stabilization);
        patient.health.hediffSet.hediffs.Add(minor);
        doctor.inventory.innerContainer.Add(new Medicine());
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false));
        wound.UntreatedRate = .1f;
        Assert.AreEqual("Stabilize", ((JobDef)EmergencyCare.CreateJob(doctor, patient, false)!.def).defName);
        wound.Stabilization.Stabilize(doctor, new Medicine());
        Assert.IsNull(EmergencyCare.CreateJob(doctor, patient, false));
    }
}
