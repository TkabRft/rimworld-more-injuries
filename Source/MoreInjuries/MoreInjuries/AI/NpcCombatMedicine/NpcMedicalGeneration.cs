using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.HeavyBleeding.Transfusions;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static class NpcMedicalGeneration
{
    [ThreadStatic] private static bool t_inGroup;
    private static readonly ConditionalWeakTable<Pawn, GenerationState> s_states = new();
    private sealed class GenerationState { internal bool Pending, New; }

    internal static NpcMedicalProfileDef Profile(Pawn pawn)
    {
        NpcMedicalProfileDef? profile = pawn.kindDef.GetModExtension<NpcMedicalProfileExtension>()?.Profile
            ?? pawn.Faction?.def.GetModExtension<NpcMedicalProfileExtension>()?.Profile;
        if (profile is not null) { return profile; }
        TechLevel level = pawn.Faction?.def.techLevel ?? TechLevel.Neolithic;
        if (level < TechLevel.Neolithic) { level = TechLevel.Neolithic; }
        if (level > TechLevel.Ultra) { level = TechLevel.Ultra; }
        return DefDatabase<NpcMedicalProfileDef>.AllDefsListForReading.First(def => def.TechLevel == level);
    }

    internal static ThingDef Blood => JobDriver_UseBloodBag.JobDeviceDef;
    internal static void GearGenerated(Pawn pawn)
    {
        GenerationState state = s_states.GetOrCreateValue(pawn);
        state.Pending = true;
        state.New = false;
    }
    internal static void NewlyGenerated(Pawn pawn) => s_states.GetOrCreateValue(pawn).New = true;
    internal static void Accepted(Pawn pawn, PawnGenerationRequest request)
    {
        if (t_inGroup || request.ForceNoGear || !s_states.TryGetValue(pawn, out GenerationState state) || !state.Pending) { return; }
        state.Pending = false;
        if (!NpcMedicalAi.Managed(pawn)) { return; }
        NpcMedicalProfileDef profile = Profile(pawn);
        bool medic = CanDoctor(pawn) && Rand.Chance(profile.MedicChance.min * MoreInjuriesMod.Settings.NpcMedicMultiplier);
        if (medic || Rand.Chance(profile.EquippedChance.min)) { Provision(pawn, profile, medic, 0f, state.New); }
    }

    internal static IEnumerable<Pawn> Group(IEnumerable<Pawn> source, PawnGroupMakerParms parms)
    {
        // Enumerate exactly once under a scoped context. No grants before successful completion.
        List<Pawn> pawns;
        float points = parms.points;
        bool previous = t_inGroup;
        t_inGroup = true;
        try { pawns = source.ToList(); }
        finally { t_inGroup = previous; }
        if (MoreInjuriesMod.Settings.EnableNpcCombatMedicine)
        {
            List<Pawn> candidates = pawns.Where(pawn => NpcMedicalAi.Managed(pawn)
                && s_states.TryGetValue(pawn, out GenerationState state) && state.Pending).ToList();
            if (Logger.IsEnabled && Prefs.DevMode)
            {
                Logger.Log($"More Injuries NPC medicine: faction={parms.faction?.def.defName}, points={points:0}, generated={pawns.Count}, fresh eligible={candidates.Count}.");
            }
            foreach (IGrouping<NpcMedicalProfileDef, Pawn> group in candidates.GroupBy(Profile))
            {
                NpcMedicalProfileDef profile = group.Key;
                List<Pawn> members = group.ToList();
                float strength = NpcMedicalModel.Strength(points);
                int equipped = NpcMedicalModel.Quota(members.Count,
                    NpcMedicalModel.Interpolate(profile.EquippedChance.min, profile.EquippedChance.max, strength));
                int medics = Math.Min(equipped, NpcMedicalModel.Quota(members.Count,
                    NpcMedicalModel.Interpolate(profile.MedicChance.min, profile.MedicChance.max, strength), MoreInjuriesMod.Settings.NpcMedicMultiplier));
                List<Pawn> capable = members.Where(CanDoctor).ToList();
                Dictionary<Pawn, Dictionary<ThingDef, int>> medicKits = new();
                while (medicKits.Count < medics && capable.Count > 0)
                {
                    Dictionary<ThingDef, int> kit = RollKit(profile, true, strength);
                    Dictionary<Pawn, float> coverage = capable.ToDictionary(pawn => pawn,
                        pawn => Loadout(pawn, kit).CoreCoverage(NpcMedicalCompatibility.Space(pawn)));
                    Pawn medic = NpcMedicalLoadout<ThingDef>.SelectMedic(capable, pawn => coverage[pawn],
                        pawn => pawn.skills.GetSkill(SkillDefOf.Medicine).Level, pawn => pawn.thingIDNumber);
                    medicKits.Add(medic, kit);
                    capable.Remove(medic);
                }
                HashSet<Pawn> selectedMedics = new(medicKits.Keys);
                List<Pawn> recipients = selectedMedics.ToList();
                recipients.AddRange(members.Where(pawn => !selectedMedics.Contains(pawn)).OrderBy(_ => Rand.Value).Take(equipped - recipients.Count));
                foreach (Pawn pawn in recipients)
                {
                    medicKits.TryGetValue(pawn, out Dictionary<ThingDef, int>? kit);
                    Provision(pawn, profile, selectedMedics.Contains(pawn), strength, s_states.GetOrCreateValue(pawn).New, kit);
                }
                if (Logger.IsEnabled && Prefs.DevMode)
                {
                    Logger.Log($"More Injuries NPC medicine: profile={profile.defName}, candidates={members.Count}, equipped={recipients.Count}, capable doctors={capable.Count + selectedMedics.Count}, medic quota={medics}, selected medics={selectedMedics.Count}.");
                    HashSet<ThingDef> medical = new(profile.Medic.Where(item => item.ThingDef is not null).Select(item => item.ThingDef!))
                    { Blood, KnownThingDefOf.SalineBag };
                    foreach (Pawn medic in selectedMedics)
                    {
                        string supplies = string.Join(", ", medic.inventory.innerContainer.Where(item => medical.Contains(item.def) || item.def.IsMedicine)
                            .Select(item => item.def.defName + "=" + item.stackCount));
                        Logger.Log($"More Injuries NPC medicine: medic={medic.ThingID}, name={medic.LabelShort}, Medical={medic.skills.GetSkill(SkillDefOf.Medicine).Level}, actual supplies=[{supplies}].");
                    }
                }
            }
            foreach (Pawn pawn in candidates) { s_states.GetOrCreateValue(pawn).Pending = false; }
        }
        foreach (Pawn pawn in pawns) { yield return pawn; }
    }

    internal static bool CanDoctor(Pawn pawn) => pawn.skills is not null && !pawn.WorkTagIsDisabled(WorkTags.Caring)
        && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
        && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight);

    private static void Provision(Pawn pawn, NpcMedicalProfileDef profile, bool medic, float strength, bool newlyGenerated,
        Dictionary<ThingDef, int>? rolledKit = null)
    {
        NpcMedicalAi.State(pawn).Medic = medic;
        if (medic) { NpcMedicalMemory.Remember(pawn); }
        if (medic && newlyGenerated && MoreInjuriesMod.Settings.NpcTrainMedics)
        {
            SkillRecord skill = pawn.skills.GetSkill(SkillDefOf.Medicine);
            if (!skill.TotallyDisabled && skill.Level < profile.MedicalSkillFloor) { skill.Level = profile.MedicalSkillFloor; }
        }
        Dictionary<ThingDef, int> counts = rolledKit ?? RollKit(profile, medic, strength);
        NpcMedicalSpace space = NpcMedicalCompatibility.Space(pawn);
        Dictionary<ThingDef, int> allocation = Loadout(pawn, counts).Allocate(space);
        foreach (KeyValuePair<ThingDef, int> entry in allocation)
        { TopUp(pawn, entry.Key, Count(pawn, entry.Key) + entry.Value); }
        if (Logger.IsEnabled && Prefs.DevMode && medic)
        {
            Logger.Log($"More Injuries NPC medicine: medic capacity before kit={space.Mass:0.00} kg/{space.Bulk:0.00} bulk; requested={counts.Values.Sum()}, granted={allocation.Values.Sum()} units.");
        }
        AddExtras(pawn, profile, strength, counts);
    }

    private static Dictionary<ThingDef, int> RollKit(NpcMedicalProfileDef profile, bool medic, float strength)
    {
        Dictionary<ThingDef, int> counts = new();
        IReadOnlyList<NpcMedicalItem> kit = medic ? profile.Medic : profile.Personal;
        foreach (NpcMedicalItem entry in kit.Where(item => item.ChoiceGroup == 0)) { Roll(entry); }
        foreach (IGrouping<int, NpcMedicalItem> choices in kit.Where(item => item.ChoiceGroup != 0).GroupBy(item => item.ChoiceGroup))
        { Roll(choices.RandomElement()); }
        void Roll(NpcMedicalItem entry)
        {
            if (entry.ThingDef is ThingDef drug && !NpcMedicalDrugs.GrantAllowed(drug)
                && !(drug == KnownThingDefOf.Morphine && MoreInjuriesMod.Settings.NpcDrugSelection != 2
                    && NpcMedicalDrugs.Stim(profile.TechLevel) is not null)) { return; }
            if (!Rand.Chance(entry.Chance)) { return; }
            int count = entry.Count.RandomInRange;
            if (!entry.Unscaled) { count = NpcMedicalModel.Quantity(count, MoreInjuriesMod.Settings.NpcSupplyMultiplier, strength); }
            if (entry.Fluid)
            {
                for (int i = 0; i < count; i++) { Add(NpcMedicalModel.Saline(Rand.Value) ? KnownThingDefOf.SalineBag : Blood, 1); }
            }
            else if (entry.ThingDef == KnownThingDefOf.Morphine && NpcMedicalDrugs.Stim(profile.TechLevel) is ThingDef stim)
            {
                int replacement = NpcDrugProvisioning.RestorativeShare(count, medic, !medic && count == 1 && Rand.Chance(.5f));
                Add(stim, replacement);
                if (NpcMedicalDrugs.GrantAllowed(entry.ThingDef)) { Add(entry.ThingDef, count - replacement); }
            }
            else if (entry.ThingDef is not null) { Add(entry.ThingDef, count); }
        }
        void Add(ThingDef def, int count) { counts.TryGetValue(def, out int old); counts[def] = old + count; }
        return counts;
    }

    private static NpcMedicalLoadout<ThingDef> Loadout(Pawn pawn, Dictionary<ThingDef, int> kit)
    {
        List<NpcMedicalLoadout<ThingDef>.Item> items = [];
        foreach (KeyValuePair<ThingDef, int> entry in kit)
        {
            Thing sample = ThingMaker.MakeThing(entry.Key);
            try
            {
                bool clinical = NpcMedicalDrugs.Clinical(entry.Key);
                bool essential = !clinical && !IsPerformance(entry.Key) && entry.Key != KnownThingDefOf.Ketamine
                    && entry.Key != KnownThingDefOf.Chloroform;
                items.Add(new(entry.Key, entry.Value, Count(pawn, entry.Key), sample.GetStatValue(StatDefOf.Mass),
                    NpcMedicalCompatibility.Bulk(sample), essential, Priority(entry.Key), clinical));
            }
            finally { sample.Destroy(); }
        }
        return new(items);
    }

    private static void AddExtras(Pawn pawn, NpcMedicalProfileDef profile, float strength, Dictionary<ThingDef, int> clinicalTargets)
    {
        if (Rand.Chance(MoreInjuriesMod.Settings.NpcScavengingChance))
        {
            ThingDef? scavenged;
            if (Rand.Chance(.1f)) { scavenged = ThingDefOf.MedicineUltratech; }
            else
            {
                NpcMedicalProfileDef next = DefDatabase<NpcMedicalProfileDef>.AllDefsListForReading
                    .Where(def => def.TechLevel > profile.TechLevel).OrderBy(def => def.TechLevel).FirstOrDefault() ?? profile;
                List<NpcMedicalItem> options = next.Personal.Where(item => !item.Reusable
                    && (item.ThingDef is null || NpcMedicalDrugs.GrantAllowed(item.ThingDef))).ToList();
                List<ThingDef> drugs = MoreInjuriesMod.Settings.NpcPerformanceDrugs && MoreInjuriesMod.Settings.NpcDrugSelection != 2
                    ? next.PerformanceDrugs.Where((_, index) => next.PerformanceWeights[index] > 0f).ToList() : [];
                int roll = options.Count + drugs.Count == 0 ? -1 : Rand.Range(0, options.Count + drugs.Count);
                if (roll < 0) { scavenged = null; }
                else if (roll >= options.Count) { scavenged = drugs[roll - options.Count]; }
                else
                {
                    NpcMedicalItem choice = options[roll];
                    scavenged = choice.Fluid ? NpcMedicalModel.Saline(Rand.Value) ? KnownThingDefOf.SalineBag : Blood : choice.ThingDef;
                }
            }
            if (scavenged is not null && NpcMedicalDrugs.GrantAllowed(scavenged))
            { TopUp(pawn, scavenged, Count(pawn, scavenged) + 1); }
        }
        MoreInjuriesSettings settings = MoreInjuriesMod.Settings;
        if (!settings.NpcPerformanceDrugs || settings.NpcDrugSelection == 2
            || !Rand.Chance(NpcMedicalModel.Interpolate(profile.PerformanceChance.min, profile.PerformanceChance.max, strength) * settings.NpcPerformancePrevalence)) { return; }
        ThingDef? performance = settings.NpcDrugSelection == 1 ? KnownThingDefOf.Morphine : SelectDrug(profile);
        if (performance is null) { return; }
        int reserve = NpcMedicalModel.Quantity(profile.PerformanceCount.RandomInRange, settings.NpcSupplyMultiplier, strength);
        clinicalTargets.TryGetValue(KnownThingDefOf.Morphine, out int clinicalMorphine);
        int existing = pawn.inventory.innerContainer.Where(thing => IsPerformance(thing.def)
            || thing.TryGetComp<CompDrug>()?.Props.isCombatEnhancingDrug == true).Sum(thing => thing.stackCount);
        int missing = NpcDrugProvisioning.MissingBoosters(reserve, existing, Count(pawn, KnownThingDefOf.Morphine), clinicalMorphine);
        TopUp(pawn, performance, Count(pawn, performance) + missing);
    }

    internal static bool IsPerformance(ThingDef def) => NpcMedicalDrugs.Booster(def);
    private static ThingDef? SelectDrug(NpcMedicalProfileDef profile)
    {
        float total = profile.PerformanceWeights.Sum();
        float roll = Rand.Value * total;
        for (int i = 0; i < profile.PerformanceDrugs.Count; i++)
        { roll -= profile.PerformanceWeights[i]; if (roll < 0f) { return profile.PerformanceDrugs[i]; } }
        return null;
    }
    private static int Priority(ThingDef def) => def == KnownThingDefOf.Bandage || def == KnownThingDefOf.HemostaticAgent
        || def == KnownThingDefOf.Tourniquet || def.IsMedicine ? 0 : def == KnownThingDefOf.SalineBag || def == Blood ? 1 : 2;
    private static int Count(Pawn pawn, ThingDef def) => pawn.inventory.innerContainer.Where(thing => thing.def == def).Sum(thing => thing.stackCount);
    private static void TopUp(Pawn pawn, ThingDef def, int target)
    {
        int remaining = target - Count(pawn, def);
        while (remaining > 0)
        {
            Thing thing = ThingMaker.MakeThing(def);
            thing.stackCount = Math.Min(remaining, def.stackLimit);
            int capacity = NpcMedicalCompatibility.Fit(pawn, thing);
            if (capacity <= 0) { thing.Destroy(); return; }
            thing.stackCount = Math.Min(thing.stackCount, capacity);
            int added = thing.stackCount;
            if (!pawn.inventory.innerContainer.TryAdd(thing)) { thing.Destroy(); return; }
            remaining -= added;
            NpcMedicalCompatibility.InventoryChanged(pawn);
        }
    }
}

[HarmonyPatch(typeof(PawnGenerator), "GenerateGearFor")]
internal static class NpcMedicalGearPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix(Pawn pawn) { if (MoreInjuriesMod.Settings.EnableNpcCombatMedicine) { NpcMedicalGeneration.GearGenerated(pawn); } }
}

[HarmonyPatch(typeof(PawnGenerator), "GenerateNewPawnInternal")]
internal static class NpcMedicalNewPawnPatch
{
    private static void Postfix(Pawn __result) { if (__result is not null) { NpcMedicalGeneration.NewlyGenerated(__result); } }
}

[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), [typeof(PawnGenerationRequest)])]
internal static class NpcMedicalAcceptedPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix(Pawn __result, PawnGenerationRequest request)
    { if (__result is not null && MoreInjuriesMod.Settings.EnableNpcCombatMedicine) { NpcMedicalGeneration.Accepted(__result, request); } }
}

[HarmonyPatch(typeof(PawnGroupMakerUtility), nameof(PawnGroupMakerUtility.GeneratePawns), [typeof(PawnGroupMakerParms), typeof(bool)])]
internal static class NpcMedicalGroupPatch
{
    private static void Postfix(ref IEnumerable<Pawn> __result, PawnGroupMakerParms parms)
    { if (MoreInjuriesMod.Settings.EnableNpcCombatMedicine) { __result = NpcMedicalGeneration.Group(__result, parms); } }
}
