using System.Reflection;
using System.Xml.Linq;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.BrainDamage;
using MoreInjuries.HealthConditions.VitalFailure;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class NeurologicalMilestoneTests
{
    private static Pawn Patient()
    {
        Pawn pawn = VitalFailureTests.Patient();
        MoreInjuriesMod.Settings.EnableNeuralDamage = true;
        Rand.RangeFraction = 0f;
        return pawn;
    }

    private static Hediff_Injury Injury(Pawn pawn, float severity, bool hypoxic = true, BodyPartRecord? part = null)
    {
        Hediff_Injury injury = new() { pawn = pawn, def = hypoxic ? KnownHediffDefOf.BrainDamage_Hypoxia : new() { defName = "Bullet" },
            Part = part ?? pawn.RaceProps.body.AllParts[0], Severity = severity };
        pawn.health.hediffSet.hediffs.Add(injury);
        return injury;
    }

    private static void Scan(Pawn pawn, int tick)
    {
        pawn.Vital!.Invalidate();
        VitalFailureTests.Tick(pawn, tick);
    }

    private static Hediff? Outcome(Pawn pawn, BodyPartRecord? part = null) => pawn.health.hediffSet.hediffs
        .Find(h => h.def == NeurologicalDefOf.MI_DiffuseBrainDamage && (part is null || h.Part == part));

    [TestMethod]
    public void ActualXmlMilestonesDeepenOneOutcomeAndSkippedStepsAwardOnlyTheirSum()
    {
        Pawn pawn = Patient(); Hediff injury = Injury(pawn, .49f);
        float[] thresholds = [.5f, 1f, 2f, 3f, 4f, 6f];
        float[] cumulative = [.05f, .10f, .25f, .40f, .60f, .80f];
        Scan(pawn, 0); Assert.IsNull(Outcome(pawn));
        for (int i = 0; i < thresholds.Length; i++)
        {
            injury.Severity = thresholds[i]; Scan(pawn, (i + 1) * 250);
            Assert.AreEqual(cumulative[i], Outcome(pawn)!.Severity, .00001f);
        }
        Assert.HasCount(1, pawn.health.hediffSet.hediffs.Where(h => h.def.GetModExtension<NeurologicalConditionProperties>() is not null));
        pawn = Patient(); Injury(pawn, 6); Scan(pawn, 0);
        Assert.AreEqual(.8f, Outcome(pawn)!.Severity, .00001f);
    }

    [TestMethod]
    public void ReloadTreatmentAndPartialHealingDoNotReplayAwards()
    {
        Pawn pawn = Patient(); Hediff injury = Injury(pawn, 3); Scan(pawn, 0);
        var data = Scribe.Save(pawn.Vital!.PostExposeData);
        pawn.Vital = new() { parent = pawn }; Scribe.Load(data, pawn.Vital.PostExposeData);
        pawn.Vital.InitializeLegacyMilestones();
        Outcome(pawn)!.Severity = .1f; injury.Severity = 1; Scan(pawn, 250);
        injury.Severity = 3; Scan(pawn, 500); Assert.AreEqual(.1f, Outcome(pawn)!.Severity);
        injury.Severity = 4; Scan(pawn, 750);
        Assert.AreEqual(.3f, Outcome(pawn)!.Severity, .00001f, "Only the new .2 increment survives treatment.");
        Assert.IsTrue(data.ContainsKey("neurologicalMilestoneStates"));
    }

    [TestMethod]
    public void LegacyInjuriesAndDisabledSettingsOnlyAwardFutureWorsening()
    {
        Pawn pawn = Patient(); Hediff injury = Injury(pawn, 3);
        Scribe.Load([], pawn.Vital!.PostExposeData); pawn.Vital.InitializeLegacyMilestones(); Scan(pawn, 0);
        Assert.IsNull(Outcome(pawn));
        injury.Severity = 4; Scan(pawn, 250); Assert.AreEqual(.2f, Outcome(pawn)!.Severity, .00001f);
        MoreInjuriesMod.Settings.EnableNeuralDamage = false; injury.Severity = 6; Scan(pawn, 500);
        MoreInjuriesMod.Settings.EnableNeuralDamage = true; Scan(pawn, 750);
        Assert.AreEqual(.2f, Outcome(pawn)!.Severity, .00001f);
    }

    [TestMethod]
    public void HealingAllInjuriesEndsAnEpisodeAndLaterInjuryCanAddAgain()
    {
        Pawn pawn = Patient(); Hediff injury = Injury(pawn, 2); Scan(pawn, 0);
        pawn.health.RemoveHediff(injury); Scan(pawn, 250);
        Injury(pawn, 2); Scan(pawn, 500); Assert.AreEqual(.5f, Outcome(pawn)!.Severity, .00001f);
        pawn.health.hediffSet.hediffs.RemoveAll(h => h is Hediff_Injury); Scan(pawn, 750);
        Injury(pawn, 6); Scan(pawn, 1000); Assert.AreEqual(1f, Outcome(pawn)!.Severity, .00001f);
    }

    [TestMethod]
    public void TraumaticWoundsAggregateWithoutRerollOrConcussionDoubleCount()
    {
        Pawn pawn = Patient(); BodyPartRecord part = pawn.RaceProps.body.AllParts[0];
        Hediff a = Injury(pawn, .3f, false), b = Injury(pawn, .3f, false);
        int rolls = 0; Rand.ChanceHandler = _ => { rolls++; return true; };
        NeurologicalOutcomes.DirectInjury(pawn, b, part, new DamageInfo { Def = new() { defName = "Blunt" }, Amount = .3f });
        Scan(pawn, 0); Assert.AreEqual(.05f, Outcome(pawn)!.Severity, .00001f);
        b.Severity = 1.7f; NeurologicalOutcomes.DirectInjury(pawn, b, part, new DamageInfo { Def = new() { defName = "Bullet" }, Amount = 1.4f });
        Scan(pawn, 250); Assert.AreEqual(.25f, Outcome(pawn)!.Severity, .00001f);
        Hediff concussion = new() { pawn = pawn, def = KnownHediffDefOf.Concussion, Severity = .8f, Part = part };
        new HediffComp_NeurologicalSource { parent = concussion }.Record(new() { defName = "Bullet" }, true);
        Assert.AreEqual(0, rolls, "No direct-hit or duplicate-concussion probability roll.");
        Rand.ChanceHandler = _ => false;
    }

    [TestMethod]
    public void PartsSourcesAnatomyResizingAndZeroWeightsRemainIndependent()
    {
        Pawn pawn = Patient(); BodyPartRecord other = new() { def = new() { tags = [BodyPartTagDefOf.ConsciousnessSource], MaxHealth = 100 } };
        pawn.RaceProps.body.AllParts.Add(other);
        Injury(pawn, 2); Injury(pawn, 20, part: other); Injury(pawn, 1, false);
        Scan(pawn, 0);
        Assert.AreEqual(.35f, Outcome(pawn, pawn.RaceProps.body.AllParts[0])!.Severity, .00001f);
        Assert.AreEqual(.25f, Outcome(pawn, other)!.Severity, .00001f);
        other.def.MaxHealth = 50; Scan(pawn, 250);
        Assert.AreEqual(.25f, Outcome(pawn, other)!.Severity, .00001f, "Resizing must not manufacture an award.");
        pawn = Patient(); pawn.RaceProps.Humanlike = false;
        foreach (var maker in NeurologicalDefOf.MI_NeurologicalConfig.HypoxiaProfile.Outcomes) { maker.TestWeight = 0; }
        Injury(pawn, 6); Scan(pawn, 0); Assert.IsNull(Outcome(pawn));
    }

    [TestMethod]
    public void MilestonesWorkWithoutVitalFailureAndRemainPausedForSuspension()
    {
        Pawn pawn = Patient(); MoreInjuriesMod.Settings.EnableDelayedVitalFailure = false;
        Injury(pawn, 2, false); Scan(pawn, 0); Assert.AreEqual(.25f, Outcome(pawn)!.Severity, .00001f);
        pawn.Suspended = true; Hediff injury = pawn.health.hediffSet.hediffs.Find(h => h is Hediff_Injury)!;
        injury.Severity = 4; Scan(pawn, 250); Assert.AreEqual(.25f, Outcome(pawn)!.Severity, .00001f);
        pawn.Suspended = false; Scan(pawn, 500);
        Assert.AreEqual(.25f, Outcome(pawn)!.Severity, .00001f, "Suspended changes cannot replay a backlog.");
    }

    [TestMethod]
    public void OxygenImmunityAndDeathrestBlockHypoxicAwardsWithoutProtectingTraumaticInjury()
    {
        foreach (bool deathrest in new[] { false, true })
        {
            Pawn pawn = Patient();
            if (deathrest)
            {
                ModsConfig.BiotechActive = true;
                VitalFailureTests.Add(pawn, HediffDefOf.Deathrest, .1f);
            }
            else { pawn.OxygenImmune = true; }
            Injury(pawn, 6); Scan(pawn, 0); Assert.IsNull(Outcome(pawn));
            Injury(pawn, 1, false); Scan(pawn, 250);
            Assert.AreEqual(.1f, Outcome(pawn)!.Severity, .00001f, "Oxygen protection is not structural brain protection.");
        }
    }

    [TestMethod]
    public void DrugRiskValuesUseChosenBalanceAndRetainNativeDoseAndTolerance()
    {
        XDocument xml = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs/ThingDefs/Medicine/Medicine_Drugs.xml"));
        XElement[] chemicals = xml.Descendants("li").Where(e => e.Attribute("Class")?.Value == "CompProperties_Drug" && e.Element("chemical") is not null).ToArray();
        foreach ((string chemical, float chance) in new[] { ("MI_Morphine_Chemical", .05f), ("MI_Ketamine_Chemical", .02f) })
        {
            XElement row = chemicals.Single(e => e.Element("chemical")!.Value == chemical);
            Assert.AreEqual(chance, float.Parse(row.Element("addictiveness")!.Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.AreEqual("0.2", row.Element("existingAddictionSeverityOffset")!.Value);
            Assert.AreEqual("1", row.Element("needLevelOffset")!.Value);
        }
    }
}
