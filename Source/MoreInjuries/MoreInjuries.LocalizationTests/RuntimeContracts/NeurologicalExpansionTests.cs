using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.BrainDamage;
using MoreInjuries.HealthConditions.Secondary.Handlers.HediffMakers;
using MoreInjuries.HealthConditions.VitalFailure;
using MoreInjuries.Integrations.DeathRattle;
using MoreInjuries.Versioning;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class NeurologicalExpansionTests
{
    private static void Set(object instance, string field, object? value)
        => instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    internal static void Profiles()
    {
        string root = RuntimeDefinitionTests.ModRoot;
        XDocument xml = XDocument.Load(Path.Combine(root, "Defs/Internal/NeurologicalProfiles.xml"));
        Dictionary<string, HediffDef> outcomes = [];
        foreach (string name in xml.Descendants("hediffDef").Select(e => e.Value).Distinct())
        {
            NeurologicalConditionProperties properties = new();
            Set(properties, "animalEligible", name is "MI_DiffuseBrainDamage" or "BrainDamage_MotorDysfunction");
            outcomes[name] = new() { defName = name, Extension = properties, maxSeverity = 1 };
        }
        NeurologicalDefOf.MI_DiffuseBrainDamage = outcomes["MI_DiffuseBrainDamage"];
        NeurologicalDefOf.MI_ArtificialComa = new() { defName = "MI_ArtificialComa", lethalSeverity = 1 };
        NeurologicalDefOf.WakeUpHigh = new() { defName = "WakeUpHigh" };
        Dictionary<string, NeurologicalProfileDef> profiles = [];
        foreach (XElement element in xml.Root!.Elements().Where(e => e.Name.LocalName.EndsWith("NeurologicalProfileDef", StringComparison.Ordinal)))
        {
            NeurologicalProfileDef profile = new() { defName = element.Element("defName")!.Value };
            var entries = (List<WeightedHediffMakerDef>)typeof(NeurologicalProfileDef).GetField("outcomes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(profile)!;
            foreach (XElement outcome in element.Element("outcomes")!.Elements())
            {
                entries.Add(new() { Target = outcomes[outcome.Element("hediffDef")!.Value],
                    TestWeight = float.Parse(outcome.Element("weight")!.Value, CultureInfo.InvariantCulture),
                    Minimum = float.Parse(outcome.Element("minSeverity")!.Value, CultureInfo.InvariantCulture),
                    Maximum = float.Parse(outcome.Element("maxSeverity")!.Value, CultureInfo.InvariantCulture) });
            }
            profiles[profile.defName] = profile;
        }
        NeurologicalConfigDef config = new();
        XElement node = xml.Root.Elements().Last();
        foreach (string field in new[] { "defaultProfile", "hypoxiaProfile", "strokeProfile", "comaProfile" })
        { Set(config, field, profiles[node.Element(field)!.Value]); }
        foreach (string field in new[] { "directBrainChanceFactor", "concussionThreshold", "concussionChance", "comaHypoxiaMultiplier" })
        { Set(config, field, float.Parse(node.Element(field)!.Value, CultureInfo.InvariantCulture)); }
        var milestones = (List<NeurologicalMilestone>)typeof(NeurologicalConfigDef).GetField("injuryMilestones", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(config)!;
        foreach (XElement point in node.Element("injuryMilestones")!.Elements())
        {
            NeurologicalMilestone milestone = new();
            Set(milestone, "injury", float.Parse(point.Element("injury")!.Value, CultureInfo.InvariantCulture));
            Set(milestone, "lastingSeverity", float.Parse(point.Element("lastingSeverity")!.Value, CultureInfo.InvariantCulture));
            milestones.Add(milestone);
        }
        var mappings = (List<NeurologicalDamageMapping>)typeof(NeurologicalConfigDef).GetField("damageProfiles", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(config)!;
        foreach (XElement mapping in node.Element("damageProfiles")!.Elements())
        {
            NeurologicalDamageMapping row = new(); Set(row, "damageDefName", mapping.Element("damageDefName")!.Value);
            Set(row, "profile", profiles[mapping.Element("profile")!.Value]); mappings.Add(row);
        }
        config.ResolveReferences(); NeurologicalDefOf.MI_NeurologicalConfig = config;
        NeurologicalDefOf.MI_InduceArtificialComa = new() { defName = "MI_InduceArtificialComa" };
        NeurologicalDefOf.MI_AwakenArtificialComa = new() { defName = "MI_AwakenArtificialComa" };
    }

    [TestInitialize]
    public void Initialize()
    {
        Profiles(); Rand.ChanceHandler = _ => false;
        MoreInjuriesMod.Settings.EnableNeuralDamage = true;
        DeathRattleBridge.Active = false; DeathRattleBridge.Owned.Clear(); DeathRattleBridge.Recipes.Clear();
        DeathRattleBridge.Blocker = null; DeathRattleBridge.MigratingPawn = null; Find.LetterStack.Texts.Clear();
    }

    [TestMethod]
    public void AnimalCerebralBurdenUsesTheSameClinicalWindowAcrossSizes()
    {
        foreach (float health in new[] { 1f, 10f, 100f })
        {
            Pawn pawn = VitalFailureTests.Patient(); pawn.Tracked = false; pawn.RaceProps.Humanlike = false;
            pawn.RaceProps.body.AllParts[0].def.MaxHealth = health;
            pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0;
            for (int tick = 0; tick <= 3750; tick++) { VitalFailureTests.Tick(pawn, tick); }
            Hediff brain = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.BrainDamage_Hypoxia)!;
            Assert.AreEqual(8f + 128f * 250 / 60000, CerebralInjury.ClinicalSeverity(brain), .015f);
            Assert.IsTrue(brain.CauseDeathNow());
            Assert.IsFalse(MoreInjuries.HealthConditions.HeavyBleeding.TraumaEligibility.Supports(pawn));
        }
    }

    [TestMethod]
    public void NonAirBreathingDoesNotRemoveCirculatoryRequirements()
    {
        Pawn pawn = VitalFailureTests.Patient(); pawn.IsMutant = true; pawn.mutant.Def.breathesAir = false;
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0;
        VitalFailureTests.Tick(pawn, 0);
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(VitalFailureDefOf.MI_RespiratoryFailure));
        Assert.IsFalse(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.BrainDamage_Hypoxia));
        pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 0; pawn.Vital!.Invalidate();
        VitalFailureTests.Tick(pawn, 250);
        Assert.IsTrue(pawn.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest));
        Assert.AreEqual(1f, pawn.Vital.Deprivation);
    }

    [TestMethod]
    public void ComaHalvesBrainProductionWithoutChangingFailureTimersOrVacuumDeath()
    {
        float[] brain = new float[2], respiratory = new float[2];
        for (int mode = 0; mode < 2; mode++)
        {
            Pawn pawn = VitalFailureTests.Patient(); pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0;
            if (mode == 1) { VitalFailureTests.Add(pawn, NeurologicalDefOf.MI_ArtificialComa, .001f); }
            VitalFailureTests.Tick(pawn, 0);
            brain[mode] = pawn.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.BrainDamage_Hypoxia)!.Severity - .001f;
            respiratory[mode] = pawn.health.hediffSet.GetFirstHediffOfDef(VitalFailureDefOf.MI_RespiratoryFailure)!.Severity;
            Assert.IsTrue(new Hediff { pawn = pawn, def = new() { lethalSeverity = 1 }, Severity = 1 }.CauseDeathNow(), "Native vacuum and other unrelated lethal conditions remain lethal.");
        }
        Assert.AreEqual(brain[0] * .5f, brain[1], .000001f); Assert.AreEqual(respiratory[0], respiratory[1]);
    }

    [TestMethod]
    public void LegacyScalingReloadAndGrowthDoNotChangeClinicalSeverity()
    {
        Pawn pawn = VitalFailureTests.Patient(); BodyPartRecord part = pawn.RaceProps.body.AllParts[0]; part.def.MaxHealth = 2;
        Hediff brain = new() { pawn = pawn, Part = part, def = KnownHediffDefOf.BrainDamage_Hypoxia, Severity = 3 };
        HediffComp_CerebralScale comp = new() { parent = brain }; brain.TestComps.Add(comp);
        Scribe.Load([], comp.CompExposeData); Scribe.mode = LoadSaveMode.PostLoadInit; comp.CompExposeData(); Scribe.mode = LoadSaveMode.Inactive;
        Assert.AreEqual(.6f, brain.Severity, .00001f); Assert.AreEqual(3f, CerebralInjury.ClinicalSeverity(brain), .00001f);
        var save = Scribe.Save(comp.CompExposeData); Scribe.Load(save, comp.CompExposeData);
        Scribe.mode = LoadSaveMode.PostLoadInit; comp.CompExposeData(); Scribe.mode = LoadSaveMode.Inactive;
        Assert.AreEqual(.6f, brain.Severity, .00001f);
        part.def.MaxHealth = 4; comp.Rebase(); Assert.AreEqual(3f, CerebralInjury.ClinicalSeverity(brain), .00001f);
    }

    [TestMethod]
    public void SuspensionAndNativeDeathProtectionAreAuthoritative()
    {
        Pawn pawn = VitalFailureTests.Patient(); pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0;
        VitalFailureTests.Tick(pawn, 0); Hediff failure = pawn.health.hediffSet.GetFirstHediffOfDef(VitalFailureDefOf.MI_RespiratoryFailure)!;
        float severity = failure.Severity; pawn.Suspended = true;
        for (int t = 250; t <= 60000; t += 250) { VitalFailureTests.Tick(pawn, t); }
        Assert.AreEqual(severity, failure.Severity);
        pawn.Suspended = false; pawn.Vital!.Resume(); VitalFailureTests.Tick(pawn, 60250);
        Assert.IsTrue(failure.Severity < severity + .1f, "No suspended-time catchup.");
        pawn.health.hediffSet.HasPreventsDeath = true;
        Hediff brain = VitalFailureTests.Add(pawn, KnownHediffDefOf.BrainDamage_Hypoxia, 8);
        Assert.IsFalse(brain.CauseDeathNow());
    }

    [TestMethod]
    public void ProfilesUseActualXmlMappingsAndZeroWeightsExcludeOutcomes()
    {
        Pawn pawn = VitalFailureTests.Patient(); NeurologicalConfigDef config = NeurologicalDefOf.MI_NeurologicalConfig;
        Assert.AreEqual("MI_Neuro_Blunt", config.ForDamage(new() { defName = "Blunt" }).defName);
        Assert.AreEqual("MI_Neuro_Penetrating", config.ForDamage(new() { defName = "Bullet" }).defName);
        Assert.AreSame(config.DefaultProfile, config.ForDamage(new() { defName = "UnlistedModDamage" }));
        NeurologicalProfileDef profile = config.ForDamage(new() { defName = "Blunt" });
        Assert.AreEqual(3f, profile.Outcomes[0].Weight);
        pawn.RaceProps.Humanlike = false;
        for (int i = 0; i < 100; i++)
        {
            Rand.RangeFraction = i / 99f; var selected = NeurologicalOutcomes.Choose(pawn, profile)!;
            Assert.IsTrue(selected.HediffDef.defName is "MI_DiffuseBrainDamage" or "BrainDamage_MotorDysfunction");
        }
        foreach (var outcome in profile.Outcomes) { outcome.TestWeight = 0; }
        Assert.IsNull(NeurologicalOutcomes.Choose(pawn, profile));
    }

    [TestMethod]
    public void SourceAndConcussionEpisodeMetadataSurviveReload()
    {
        Pawn pawn = VitalFailureTests.Patient(); Hediff concussion = new() { pawn = pawn, def = KnownHediffDefOf.Concussion, Severity = .8f, Part = pawn.RaceProps.body.AllParts[0] };
        HediffComp_NeurologicalSource comp = new() { parent = concussion };
        int rolls = 0; Rand.ChanceHandler = _ => { rolls++; return true; };
        comp.Record(new() { defName = "Blunt" }, false);
        int count = pawn.health.hediffSet.hediffs.Count;
        comp.Record(new() { defName = "Bullet" }, false); Assert.AreEqual(count, pawn.health.hediffSet.hediffs.Count);
        var data = Scribe.Save(comp.CompExposeData); HediffComp_NeurologicalSource restored = new() { parent = concussion };
        Scribe.Load(data, restored.CompExposeData); restored.Record(new() { defName = "Blunt" }, false);
        Assert.AreEqual(1, rolls); Assert.AreEqual("MI_Neuro_Blunt", restored.Profile.defName);
    }

    [TestMethod]
    public void RegenerationUsesOneControllerAndWaitsForTheUnderlyingInjury()
    {
        Pawn pawn = VitalFailureTests.Patient(); ModsConfig.AnomalyActive = true;
        BodyPartRecord brain = pawn.RaceProps.body.AllParts[0];
        Hediff lasting = new() { pawn = pawn, Part = brain, def = NeurologicalDefOf.MI_DiffuseBrainDamage, Severity = 1 };
        Hediff_Injury injury = new() { pawn = pawn, Part = brain, def = KnownHediffDefOf.OrganHypoxia, Severity = .1f };
        pawn.health.hediffSet.hediffs.AddRange([lasting, injury, new() { pawn = pawn, def = new() { stages = [new() { regeneration = 100 }] } }]);
        VitalFailureTests.Tick(pawn, 0); Assert.AreEqual(1f, lasting.Severity);
        pawn.health.RemoveHediff(injury); pawn.Vital!.Invalidate();
        for (int t = 250; t <= 60000; t += 250) { VitalFailureTests.Tick(pawn, t); }
        Assert.AreEqual(.75f, lasting.Severity, .0001f);
        pawn.IsMutant = true; pawn.mutant.Def.removeChronicIllnesses = true; NeurologicalOutcomes.Transform(pawn);
        Assert.IsFalse(pawn.health.hediffSet.hediffs.Contains(lasting));
    }

    [TestMethod]
    public void ComaProcedureRejectsHerbalCareAndForbiddenReversal()
    {
        Pawn pawn = VitalFailureTests.Patient(); pawn.playerSettings = new() { medCare = MedicalCareCategory.HerbalOrWorse };
        Recipe_InduceArtificialComa recipe = new(); Assert.IsFalse(recipe.AvailableOnNow(pawn));
        pawn.playerSettings.medCare = MedicalCareCategory.NormalOrWorse; Assert.IsTrue(recipe.AvailableOnNow(pawn));
        pawn.IsMutant = true; pawn.mutant.Def.canUseDrugs = false; Assert.IsFalse(recipe.AvailableOnNow(pawn));
        pawn.mutant.Def.canUseDrugs = true; pawn.mutant.Def.drugWhitelist = [new() { defName = "OtherDrug" }]; Assert.IsFalse(recipe.AvailableOnNow(pawn));
        pawn.mutant.Def.drugWhitelist.Add(ThingDefOf.WakeUp); Assert.IsTrue(recipe.AvailableOnNow(pawn));
        XDocument xml = XDocument.Load(Path.Combine(RuntimeDefinitionTests.ModRoot, "Defs/RecipeDefs/Surgeries/Surgery_ArtificialComa.xml"));
        XElement induction = xml.Root!.Elements().First(); Assert.AreEqual("5", induction.Element("ingredients")!.Element("li")!.Element("count")!.Value);
        foreach (XElement filter in induction.Descendants().Where(e => e.Name.LocalName is "filter" or "fixedIngredientFilter"))
        { CollectionAssert.AreEquivalent(new[] { "MedicineIndustrial", "MedicineUltratech" }, filter.Descendants("thingDefs").Elements().Select(e => e.Value).ToArray()); }
    }

    [TestMethod]
    public void ExistingComaIsReversedByWakeUpWithoutRemovingNaturalComa()
    {
        Pawn pawn = VitalFailureTests.Patient(); Hediff coma = VitalFailureTests.Add(pawn, NeurologicalDefOf.MI_ArtificialComa, .75f);
        Hediff natural = VitalFailureTests.Add(pawn, VitalFailureDefOf.MI_Coma, .5f);
        VitalFailureTests.Add(pawn, NeurologicalDefOf.WakeUpHigh, .5f);
        HediffComp_ArtificialComa comp = new() { parent = coma }; float adjustment = 0;
        comp.CompPostTick(ref adjustment); Assert.IsFalse(pawn.health.hediffSet.hediffs.Contains(coma));
        Assert.IsTrue(pawn.health.hediffSet.hediffs.Contains(natural));
    }

    [TestMethod]
    public void MigrationPreservesDamageIdentifiersAndRunsOncePerSavedGame()
    {
        Pawn pawn = VitalFailureTests.Patient(); DeathRattleBridge.Active = true;
        GameComponent_DeathRattleTransition transition = new(new());
        HediffDef sourceDef = new() { defName = "BrainDamage" }; DeathRattleBridge.Owned.Add(sourceDef);
        Hediff old = new() { pawn = pawn, Part = pawn.RaceProps.body.AllParts[0], def = sourceDef, Severity = .83f, ageTicks = 777, loadID = 91 };
        pawn.health.hediffSet.hediffs.Add(old); GameComponent_DeathRattleTransition.Register(pawn); transition.LoadedGame();
        Assert.IsTrue(transition.Ready); Hediff migrated = pawn.health.hediffSet.hediffs.Single();
        Assert.AreSame(NeurologicalDefOf.MI_DiffuseBrainDamage, migrated.def); Assert.AreEqual(.83f, migrated.Severity);
        Assert.AreEqual(777, migrated.ageTicks); Assert.AreEqual(old.GetUniqueLoadID(), migrated.GetUniqueLoadID());
        var save = Scribe.Save(transition.ExposeData); GameComponent_DeathRattleTransition reload = new(new());
        Scribe.Load(save, reload.ExposeData); GameComponent_DeathRattleTransition.Register(pawn); reload.LoadedGame();
        Assert.IsTrue(reload.Ready); Assert.AreEqual(1, Find.LetterStack.Texts.Count); Assert.AreEqual(1, pawn.health.hediffSet.hediffs.Count);
        DeathRattleBridge.Active = false; new GameComponent_DeathRattleTransition(new()).LoadedGame();
        Assert.AreEqual(1, Find.LetterStack.Texts.Count);
    }

    [TestMethod]
    public void UnsupportedMigrationPreflightLeavesOriginalConditionsUntouched()
    {
        Pawn pawn = VitalFailureTests.Patient(); DeathRattleBridge.Active = true;
        GameComponent_DeathRattleTransition transition = new(new()); HediffDef source = new() { defName = "UnknownDRState" };
        DeathRattleBridge.Owned.Add(source); Hediff old = new() { def = source, pawn = pawn, Severity = .9f };
        pawn.health.hediffSet.hediffs.Add(old); GameComponent_DeathRattleTransition.Register(pawn); transition.LoadedGame();
        Assert.IsFalse(transition.Ready); Assert.AreSame(old, pawn.health.hediffSet.hediffs.Single()); Assert.AreEqual(.9f, old.Severity);
    }

    [TestMethod]
    [DataRow("ClinicalDeathNoHeartbeat")]
    [DataRow("ClinicalDeathAsphyxiation")]
    [DataRow("LiverFailure")]
    [DataRow("KidneyFailure")]
    [DataRow("IntestinalFailure")]
    [DataRow("Coma")]
    [DataRow("ArtificialComa")]
    public void EveryLegacyFailureAndComaHasAnOwnedDestination(string name)
    {
        Pawn pawn = VitalFailureTests.Patient(); DeathRattleBridge.Active = true;
        Dictionary<string, HediffDef> targets = new()
        {
            ["ClinicalDeathNoHeartbeat"] = KnownHediffDefOf.CardiacArrest,
            ["ClinicalDeathAsphyxiation"] = VitalFailureDefOf.MI_RespiratoryFailure,
            ["LiverFailure"] = VitalFailureDefOf.MI_LiverFailure,
            ["KidneyFailure"] = VitalFailureDefOf.MI_RenalFailure,
            ["IntestinalFailure"] = VitalFailureDefOf.MI_MetabolicFailure,
            ["Coma"] = VitalFailureDefOf.MI_Coma,
            ["ArtificialComa"] = NeurologicalDefOf.MI_ArtificialComa
        };
        if (name == "ClinicalDeathNoHeartbeat") { pawn.health.hediffSet.Capacities[PawnCapacityDefOf.BloodPumping] = 0; }
        if (name == "ClinicalDeathAsphyxiation") { pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Breathing] = 0; }
        if (name == "LiverFailure") { pawn.RaceProps.body.AllParts[2].Efficiency = 0; }
        if (name == "KidneyFailure") { pawn.RaceProps.body.AllParts[3].Efficiency = pawn.RaceProps.body.AllParts[4].Efficiency = 0; }
        if (name == "IntestinalFailure") { pawn.health.hediffSet.Capacities[VitalFailureDefOf.Metabolism] = 0; }
        if (name == "Coma") { pawn.health.hediffSet.Capacities[PawnCapacityDefOf.Consciousness] = 0; }
        GameComponent_DeathRattleTransition transition = new(new()); HediffDef source = new() { defName = name };
        DeathRattleBridge.Owned.Add(source); pawn.health.hediffSet.hediffs.Add(new() { def = source, pawn = pawn, Severity = .4f, loadID = 123 });
        GameComponent_DeathRattleTransition.Register(pawn); transition.LoadedGame();
        Assert.IsTrue(transition.Ready); Assert.IsFalse(pawn.health.hediffSet.HasHediff(source));
        Assert.AreEqual(.4f, pawn.health.hediffSet.GetFirstHediffOfDef(targets[name])!.Severity);
    }

    [TestMethod]
    public void FailedNativeConversionRollsBackEarlierConversions()
    {
        Pawn pawn = VitalFailureTests.Patient(); DeathRattleBridge.Active = true;
        GameComponent_DeathRattleTransition transition = new(new());
        HediffDef damage = new() { defName = "BrainDamage" }, coma = new() { defName = "ArtificialComa" };
        DeathRattleBridge.Owned.UnionWith([damage, coma]);
        Hediff originalDamage = new() { pawn = pawn, def = damage, Severity = .8f, loadID = 1 };
        Hediff originalComa = new() { pawn = pawn, def = coma, Severity = .4f, loadID = 2 };
        pawn.health.hediffSet.hediffs.AddRange([originalDamage, originalComa]);
        pawn.health.ThrowOnAdd = NeurologicalDefOf.MI_ArtificialComa;
        GameComponent_DeathRattleTransition.Register(pawn); transition.LoadedGame();
        Assert.IsFalse(transition.Ready);
        CollectionAssert.AreEqual(new[] { originalDamage, originalComa }, pawn.health.hediffSet.hediffs);
        Assert.AreEqual(.8f, originalDamage.Severity); Assert.IsEmpty(transition.ReferenceReplacements);
    }

    [TestMethod]
    public void ExistingDestinationWinsAndReferencesAreMappedWithoutAddingDamage()
    {
        Pawn pawn = VitalFailureTests.Patient(); DeathRattleBridge.Active = true;
        GameComponent_DeathRattleTransition transition = new(new()); HediffDef source = new() { defName = "BrainDamage" };
        DeathRattleBridge.Owned.Add(source);
        Hediff existing = new() { def = NeurologicalDefOf.MI_DiffuseBrainDamage, pawn = pawn, Severity = .9f, loadID = 11 };
        Hediff legacy = new() { def = source, pawn = pawn, Severity = .6f, loadID = 10 };
        pawn.health.hediffSet.hediffs.AddRange([existing, legacy]);
        GameComponent_DeathRattleTransition.Register(pawn); transition.LoadedGame();
        Assert.IsTrue(transition.Ready); Assert.AreSame(existing, pawn.health.hediffSet.hediffs.Single());
        Assert.AreEqual(.9f, existing.Severity); Assert.AreEqual("Hediff_11", transition.ReferenceReplacements["Hediff_10"]);
    }

    [TestMethod]
    public void MigratedBillCannotKeepHerbalIngredientsAndDoctorJobIsRequeued()
    {
        Pawn pawn = VitalFailureTests.Patient(); DeathRattleBridge.Active = true;
        GameComponent_DeathRattleTransition transition = new(new()); RecipeDef old = new() { defName = "ArtificialComa" };
        DeathRattleBridge.Owned.Add(old); DeathRattleBridge.Recipes[old] = NeurologicalDefOf.MI_InduceArtificialComa;
        ThingDef herbal = new(), industrial = new();
        NeurologicalDefOf.MI_InduceArtificialComa.fixedIngredientFilter.SetAllow(industrial, true);
        Bill bill = new() { recipe = old }; bill.ingredientFilter.SetAllow(herbal, true); bill.ingredientFilter.SetAllow(industrial, true);
        pawn.BillStack = new() { Bills = [bill] }; pawn.CurJob = new() { bill = bill };
        GameComponent_DeathRattleTransition.Register(pawn); transition.LoadedGame();
        Assert.IsTrue(transition.Ready); Assert.AreSame(NeurologicalDefOf.MI_InduceArtificialComa, bill.recipe);
        Assert.IsFalse(bill.ingredientFilter.Allows(herbal)); Assert.IsTrue(bill.ingredientFilter.Allows(industrial));
        Assert.AreEqual(1, pawn.jobs.Ended);
    }

    [TestMethod]
    public void DependencyAndDisabledModulePreventReadyStatus()
    {
        foreach (bool dependency in new[] { false, true })
        {
            Pawn pawn = VitalFailureTests.Patient(); DeathRattleBridge.Active = true;
            DeathRattleBridge.Blocker = dependency ? "OtherMod" : null;
            MoreInjuriesMod.Settings.EnableDelayedVitalFailure = dependency;
            GameComponent_DeathRattleTransition transition = new(new()); GameComponent_DeathRattleTransition.Register(pawn); transition.LoadedGame();
            Assert.IsFalse(transition.Ready);
        }
    }

    [TestMethod]
    public void RegenerationRemainsAvailableWithVitalFailureDisabled()
    {
        Pawn pawn = VitalFailureTests.Patient(); MoreInjuriesMod.Settings.EnableDelayedVitalFailure = false; ModsConfig.AnomalyActive = true;
        Hediff lasting = new() { def = NeurologicalDefOf.MI_DiffuseBrainDamage, pawn = pawn, Severity = 1 };
        pawn.health.hediffSet.hediffs.AddRange([lasting, new() { pawn = pawn, def = new() { stages = [new() { regeneration = 100 }] } }]);
        VitalFailureTests.Tick(pawn, 0); float severity = lasting.Severity;
        VitalFailureTests.Tick(pawn, 0); Assert.AreEqual(severity, lasting.Severity, "Repeated scheduling does not apply recovery twice.");
        Assert.IsTrue(severity < 1);
    }

    [TestMethod]
    public void CustomSerializedComponentsBlockMigrationInsteadOfBeingDiscarded()
    {
        Pawn pawn = VitalFailureTests.Patient(); DeathRattleBridge.Active = true;
        GameComponent_DeathRattleTransition transition = new(new());
        HediffDef source = new() { defName = "BrainDamage", comps = [new() { compClass = typeof(HediffComp_CerebralScale) }] };
        DeathRattleBridge.Owned.Add(source); Hediff old = new() { def = source, pawn = pawn, Severity = .6f };
        pawn.health.hediffSet.hediffs.Add(old); GameComponent_DeathRattleTransition.Register(pawn); transition.LoadedGame();
        Assert.IsFalse(transition.Ready); Assert.AreSame(old, pawn.health.hediffSet.hediffs.Single());
    }

    [TestMethod]
    public void SavedCopyRemovesOnlyDeathRattleMetadataAndRemapsMergedReferences()
    {
        XmlDocument document = new(); document.LoadXml("<savegame><meta><modIds><li>Core</li><li>Troopersmith1.DeathRattle</li><li>MI</li></modIds><modNames><li>Core</li><li>DR</li><li>MI</li></modNames><modSteamIds><li>0</li><li>1</li><li>2</li></modSteamIds></meta><hediff>MI_DiffuseBrainDamage</hediff><reference>Hediff_3</reference></savegame>");
        TransitionSaveValidator.RemapReferences(document, new Dictionary<string, string> { ["Hediff_3"] = "Hediff_7" });
        Assert.IsNull(TransitionSaveValidator.FindResidual(document, new HashSet<string> { "BrainDamage", "Coma" }));
        TransitionSaveValidator.RemoveDependency(document); Assert.AreEqual("CoreMI", document.SelectSingleNode("/*/meta/modIds")!.InnerText);
        Assert.AreEqual("Hediff_7", document.SelectSingleNode("/*/reference")!.InnerText);
        document.DocumentElement!.AppendChild(document.CreateElement("def")).InnerText = "BrainDamage";
        Assert.IsNotNull(TransitionSaveValidator.FindResidual(document, new HashSet<string> { "BrainDamage" }));
    }

    [TestMethod]
    public void RepeatedGeneExtractionKeepsItsNativeLethalCapacityRoute()
    {
        Pawn pawn = VitalFailureTests.Patient(); ModsConfig.BiotechActive = true;
        VitalFailureTests.Add(pawn, new() { defName = "XenogermReplicating" }, 2);
        Assert.IsFalse(pawn.Vital!.MayDelay(PawnCapacityDefOf.Consciousness));
        pawn.health.hediffSet.hediffs[0].Severity = 1;
        Assert.IsTrue(pawn.Vital.MayDelay(PawnCapacityDefOf.Consciousness));
    }

    [TestMethod]
    public void WarmProfileAndScalingHelpersAllocateNothing()
    {
        Pawn pawn = VitalFailureTests.Patient(); NeurologicalProfileDef profile = NeurologicalDefOf.MI_NeurologicalConfig.HypoxiaProfile;
        for (int i = 0; i < 100; i++) { NeurologicalOutcomes.Choose(pawn, profile); CerebralInjury.Scale(pawn, pawn.RaceProps.body.AllParts[0]); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { NeurologicalOutcomes.Choose(pawn, profile); CerebralInjury.Scale(pawn, pawn.RaceProps.body.AllParts[0]); }
        Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
