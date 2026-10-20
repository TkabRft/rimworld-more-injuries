using System;
using System.Linq;
using System.Runtime.CompilerServices;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static class NpcPainCare
{
    private sealed class Diagnostic { internal string? Reason; }
    private static readonly ConditionalWeakTable<Pawn, ConditionalWeakTable<Pawn, Diagnostic>> s_diagnostics = new();
    internal static NpcPainAssessment Assess(Pawn patient)
    {
        bool otherCause = patient.health.forceDowned;
        if (patient.Downed)
        {
            foreach (PawnCapacityDef capacity in new[] { PawnCapacityDefOf.Consciousness, PawnCapacityDefOf.Moving,
                PawnCapacityDefOf.Breathing, PawnCapacityDefOf.BloodPumping })
            { otherCause |= !patient.health.capacities.CapableOf(capacity); }
        }
        return new(patient.health.hediffSet.PainTotal, patient.GetStatValue(StatDefOf.PainShockThreshold),
            patient.health.InPainShock, patient.Downed, otherCause);
    }

    internal static float Priority(Pawn patient)
    {
        if (!MoreInjuriesMod.Settings.NpcPainCare) { return 0f; }
        NpcPainAssessment assessment = Assess(patient);
        return NpcPainPolicy.Priority(assessment.Pain, assessment.Threshold, assessment.InPainShock, assessment.Downed,
            Mathf.Clamp01(MoreInjuriesMod.Settings.NpcPainThreshold));
    }

    internal static bool SourceAllowed(Pawn actor, Pawn patient, Thing? dose)
    {
        if (dose is null || dose.Destroyed || dose.stackCount < 1) { return false; }
        if (dose == actor.carryTracker.CarriedThing) { return true; }
        if (dose.ParentHolder == actor.inventory) { return true; }
        NpcMedicalCompatibility.InventoryChanged(actor);
        if (NpcMedicalCompatibility.Fit(actor, dose) < 1) { return false; }
        if (dose.ParentHolder is Pawn_InventoryTracker holder)
        {
            return !holder.pawn.IsForbidden(actor) && !actor.HostileTo(holder.pawn)
                && (holder.pawn == patient || holder.pawn.Faction == actor.Faction)
                && NpcQuickAid.SourceNearby(actor, patient, holder.pawn.Position)
                && actor.CanReach(holder.pawn, PathEndMode.Touch, Danger.Some);
        }
        return dose.Spawned && !dose.IsForbidden(actor) && NpcQuickAid.SourceNearby(actor, patient, dose.Position)
            && actor.CanReach(dose, PathEndMode.ClosestTouch, Danger.Some);
    }

    internal static bool Willing(Pawn actor, Pawn patient, ThingDef drug)
    {
        // Native dependency permission must not override an explicit actor refusal.
        if (actor.IsTeetotaler() || actor == patient && !PawnUtility.CanTakeDrug(actor, drug)) { return false; }
        if (!ModsConfig.IdeologyActive) { return true; }
        bool self = actor == patient;
        if (!IdeoUtility.DoerWillingToDo(self ? HistoryEventDefOf.IngestedDrug : HistoryEventDefOf.AdministeredDrug, actor)) { return false; }
        if (drug.ingestible.drugCategory == DrugCategory.Social
            && !IdeoUtility.DoerWillingToDo(self ? HistoryEventDefOf.IngestedRecreationalDrug : HistoryEventDefOf.AdministeredRecreationalDrug, actor)) { return false; }
        return drug.ingestible.drugCategory != DrugCategory.Hard
            || IdeoUtility.DoerWillingToDo(self ? HistoryEventDefOf.IngestedHardDrug : HistoryEventDefOf.AdministeredHardDrug, actor);
    }

    internal static bool Evaluate(Pawn actor, Pawn patient, Thing dose, out NpcPainDose projection, out string reason)
        => EvaluateClinical(actor, patient, dose, out projection, out reason, NpcDrugPurpose.Pain);

    internal static bool EvaluateClinical(Pawn actor, Pawn patient, Thing dose, out NpcPainDose projection, out string reason, NpcDrugPurpose purpose)
    {
        projection = default;
        reason = "clinical care disabled or no indication";
        if (!NpcMedicalAi.Managed(actor) || (purpose == NpcDrugPurpose.Pain ? Priority(patient) : NpcRestorativeCare.Priority(patient)) <= 0f) { return false; }
        reason = "patient or medical-care restriction";
        if (patient.Dead || !patient.RaceProps.Humanlike || !patient.RaceProps.IsFlesh
            || patient.IsMutant && !patient.mutant.Def.entitledToMedicalCare || actor.HostileTo(patient)
            || !(actor == patient && actor.IsPrisoner)
                && patient.playerSettings?.medCare is MedicalCareCategory.NoCare or MedicalCareCategory.NoMeds) { return false; }
        reason = "actor incapable or unwilling";
        if (actor != patient && !NpcMedicalGeneration.CanDoctor(actor)
            || actor == patient && (actor.Downed || !actor.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))) { return false; }
        reason = "unavailable or unsupported dose";
        if (!(purpose == NpcDrugPurpose.Pain ? NpcMedicalDrugs.Analgesics.Contains(dose.def) : NpcMedicalDrugs.Restorative(dose.def)) || dose.Destroyed || dose.stackCount < 1
            || !dose.IngestibleNow || dose.IsForbidden(actor) || !SourceAllowed(actor, patient, dose)) { return false; }
        reason = "actor refuses drug";
        if (!Willing(actor, patient, dose.def)) { return false; }
        if (!VitalSafety(patient, dose.def, out reason)) { return false; }
        return NpcPainProjection.TryDose(patient, dose.def, out projection, out reason, purpose);
    }

    internal static bool VitalSafety(Pawn patient, ThingDef drug, out string reason)
    {
        reason = "dangerous depressant combination";
        if (NpcMedicalAi.Severity(patient, "KetamineBuildup") > 0f || NpcMedicalAi.Severity(patient, "ChloroformBuildup") > 0f
            || NpcMedicalAi.Severity(patient, "AlcoholHigh") > 0f) { return false; }
        if (drug == KnownThingDefOf.Morphine)
        {
            reason = "morphine unsafe with blood loss, shock or depressed vital capacities";
            if (NpcMedicalAi.Severity(patient, "BloodLoss") >= .2f || NpcMedicalAi.Severity(patient, "HypovolemicShock") > 0f
                || patient.health.capacities.GetLevel(PawnCapacityDefOf.Breathing) < .9f
                || patient.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping) < .9f) { return false; }
        }
        return true;
    }

    internal static Job? Select(Pawn actor, Pawn patient, bool pocketOnly = false, Func<JobDef, Thing?, bool>? allowed = null)
    {
        if (Priority(patient) <= 0f || actor.carryTracker.CarriedThing is not null) { return null; }
        Thing? best = null;
        NpcPainDose bestDose = default;
        string reason = "no available safe analgesic";
        float threshold = patient.GetStatValue(StatDefOf.PainShockThreshold);
        foreach (ThingDef def in NpcMedicalDrugs.Analgesics)
        {
            Thing? item = pocketOnly ? actor.inventory?.innerContainer.FirstOrDefault(thing => thing.def == def)
                : NpcMedicalAi.FindSupply(actor, patient, def);
            if (item is null || allowed?.Invoke(actor == patient ? KnownJobDefOf.MI_NpcPainSelf : KnownJobDefOf.MI_NpcPainAssist, item) == false) { continue; }
            if (!Evaluate(actor, patient, item, out NpcPainDose dose, out reason)) { continue; }
            if (best is null || Compare(actor, item, dose, best, bestDose, threshold) < 0) { best = item; bestDose = dose; }
        }
        Diagnose(actor, patient, best?.def.defName ?? reason);
        if (best is null) { return null; }
        Job job = actor == patient ? JobMaker.MakeJob(KnownJobDefOf.MI_NpcPainSelf, best)
            : JobMaker.MakeJob(KnownJobDefOf.MI_NpcPainAssist, patient, best);
        job.count = 1;
        job.endAfterTendedOnce = true;
        return job;
    }

    private static int Compare(Pawn actor, Thing left, NpcPainDose a, Thing right, NpcPainDose b, float threshold)
    {
        int comparison = b.Recovery.CompareTo(a.Recovery);
        if (comparison == 0) { comparison = b.Function.CompareTo(a.Function); }
        if (comparison == 0) { comparison = a.BleedingFactor.CompareTo(b.BleedingFactor); }
        if (comparison == 0) { comparison = (a.Pain >= threshold).CompareTo(b.Pain >= threshold); }
        if (comparison == 0) { comparison = a.PhysiologicalRisk.CompareTo(b.PhysiologicalRisk); }
        if (comparison == 0) { comparison = a.OverdoseRisk.CompareTo(b.OverdoseRisk); }
        if (comparison == 0) { comparison = a.AddictionRisk.CompareTo(b.AddictionRisk); }
        if (comparison == 0) { comparison = Effort(actor, left).CompareTo(Effort(actor, right)); }
        return comparison != 0 ? comparison : left.def.BaseMarketValue.CompareTo(right.def.BaseMarketValue);
    }

    private static float Effort(Pawn actor, Thing item) => item.ParentHolder == actor.inventory ? 0f
        : 1f + item.PositionHeld.DistanceToSquared(actor.Position);

    internal static bool FinalCheck(Pawn actor, Pawn patient, Thing dose)
    {
        bool accessible = dose.ParentHolder == actor.inventory || dose == actor.carryTracker.CarriedThing
            || dose.Spawned && actor.CanReachImmediate(dose, PathEndMode.Touch);
        bool allowed = accessible && (actor.CurJob is not Job active || NpcQuickAid.Continue(actor, patient, active, final: true))
            && (actor == patient || actor.CanReachImmediate(patient, PathEndMode.Touch)
            && NpcMedicalAi.Relation(actor, patient) >= 0);
        string reason = "dose or patient no longer accessible";
        allowed = allowed && Evaluate(actor, patient, dose, out _, out reason);
        Diagnose(actor, patient, allowed ? "administer " + dose.def.defName : reason);
        return allowed;
    }

    private static void Diagnose(Pawn actor, Pawn patient, string reason)
    {
        if (!Logger.IsEnabled || !Prefs.DevMode) { return; }
        Diagnostic diagnostic = s_diagnostics.GetOrCreateValue(actor).GetOrCreateValue(patient);
        if (diagnostic.Reason == reason) { return; }
        diagnostic.Reason = reason;
        NpcPainAssessment assessment = Assess(patient);
        Logger.Log($"More Injuries NPC pain: {actor.ThingID} -> {patient.ThingID}: {reason}; pain={assessment.Pain:0.###}, threshold={assessment.Threshold:0.###}, painShock={assessment.InPainShock}, collapse={assessment.Collapse}.");
    }
}
