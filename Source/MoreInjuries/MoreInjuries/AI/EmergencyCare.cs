using MoreInjuries.AI.Jobs;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.Extensions;
using MoreInjuries.HealthConditions.CardiacArrest;
using MoreInjuries.HealthConditions.Choking;
using MoreInjuries.HealthConditions.Drugs.Epinephrine;
using MoreInjuries.HealthConditions.HeavyBleeding;
using MoreInjuries.HealthConditions.HeavyBleeding.Bandages;
using MoreInjuries.HealthConditions.HeavyBleeding.HemostaticAgents;
using MoreInjuries.HealthConditions.HeavyBleeding.Overrides;
using MoreInjuries.HealthConditions.HeavyBleeding.Tourniquets;
using MoreInjuries.HealthConditions.HeavyBleeding.Transfusions;
using MoreInjuries.HealthConditions.HypovolemicShock;
using MoreInjuries.Integrations.CombatExtended;
using MoreInjuries.Things;
using RimWorld;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI;

// Request-local candidates, shared by drafted/routine care. Never requests a prognosis.
internal static class EmergencyCare
{
    private enum Kind { None, Tourniquet, Hemostat, Bandage, Stabilize, Epinephrine, Defibrillator, Cpr, Suction, Saline, Blood }
    private sealed class History { internal bool MustResuscitate; }
    private static readonly ConditionalWeakTable<Pawn, History> s_history = new();
    private readonly record struct Candidate(Kind Kind, Thing? Device, BodyPartRecord? Limb, float Score,
        int Retrieval, float Cost, bool Critical, bool Quick, Job? NativeJob = null);

    internal static void NotifyTreatmentCompleted(Pawn patient, JobDef action)
    {
        bool bleeding = action == KnownJobDefOf.UseTourniquet || action == KnownJobDefOf.UseHemostaticAgent || action == KnownJobDefOf.UseBandage;
        bool resuscitation = action == KnownJobDefOf.PerformCpr || action == KnownJobDefOf.UseDefibrillator
            || action == KnownJobDefOf.UseSuctionDevice || action == KnownJobDefOf.UseEpinephrine;
        if (!bleeding && !resuscitation) { return; }
        s_history.GetOrCreateValue(patient).MustResuscitate = bleeding && IsCritical(patient);
    }

    private static bool IsCritical(Pawn patient) => patient.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest)
        || patient.health.hediffSet.HasHediff(KnownHediffDefOf.ChokingOnBlood)
            && patient.health.capacities.GetLevel(PawnCapacityDefOf.Breathing) <= .2f;

    internal static Job? CreateJob(Pawn doctor, Pawn patient, bool aftercare)
    {
        if (doctor == patient || patient.Dead || !doctor.CanReachPatient(patient)
            || patient.playerSettings?.medCare is MedicalCareCategory.NoCare
            || patient.IsMutant && !patient.mutant.Def.entitledToMedicalCare
            || doctor.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)
            || !doctor.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
            || !doctor.health.capacities.CapableOf(PawnCapacityDefOf.Sight)) { return null; }
        bool superclotting = ModsConfig.BiotechActive && patient.genes is { } genes
            && DefDatabase<GeneDef>.GetNamedSilentFail("Superclotting") is { } clotting && genes.HasActiveGene(clotting);
        bool arrest = patient.health.hediffSet.HasHediff(KnownHediffDefOf.CardiacArrest);
        Hediff? choking = patient.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.ChokingOnBlood);
        bool criticalAirway = choking is not null && patient.health.capacities.GetLevel(PawnCapacityDefOf.Breathing) <= .2f;
        bool critical = arrest || criticalAirway;
        bool cprAvailable = KnownResearchProjectDefOf.Cpr.IsFinished && (arrest || choking is not null);
        bool quickAllowed = !s_history.TryGetValue(patient, out History? history) || !history.MustResuscitate;
        Candidate best = default, rescue = default, quick = default, otherEmergency = default;
        bool miBleeding = false;
        float nativeBleeding = patient.health.hediffSet.BleedRateTotal, woundBleeding = 0f;
        foreach (Hediff injury in patient.health.hediffSet.hediffs) { woundBleeding += Math.Max(0f, injury.BleedRate); }
        float normalization = woundBleeding > 0f ? nativeBleeding / woundBleeding : 0f;
        bool TreatBleeding(float rate) => !superclotting || rate >= EmergencyPriority.MINOR_BLEEDING;
        bool Better(Candidate value, Candidate previous) => previous.Kind == Kind.None || value.Score > previous.Score
            || value.Score == previous.Score && (value.Retrieval < previous.Retrieval
                || value.Retrieval == previous.Retrieval && value.Cost < previous.Cost);
        void Consider(Kind kind, Thing? device, float urgency, float benefit, int baseTicks,
            bool reusable = false, bool criticalTreatment = false, float bleeding = 0f, BodyPartRecord? limb = null, Job? native = null)
        {
            if (urgency <= 0f || benefit <= 0f) { return; }
            if (device is not null && (device.Destroyed || device.stackCount <= 0 || device.IsForbidden(doctor)
                || !doctor.CanReserve(device, MedicalDeviceHelper.MAX_MEDICAL_DEVICE_RESERVATIONS, stackCount: 1))) { return; }
            int duration = kind == Kind.Stabilize ? baseTicks : MedicalTiming.Duration(doctor, baseTicks);
            int travel = EmergencyTravel.ExtraTicks(doctor, patient, device, exact: false);
            if (travel == int.MaxValue) { return; }
            if (critical && criticalTreatment && cprAvailable && travel > 0)
            {
                if (travel > EmergencyPriority.CRITICAL_BUDGET) { return; }
                travel = EmergencyTravel.ExtraTicks(doctor, patient, device, exact: true);
                if (travel > EmergencyPriority.CRITICAL_BUDGET) { return; }
            }
            float cost = reusable || device is null ? 0f : device.GetStatValue(StatDefOf.MarketValue);
            bool fast = bleeding >= .5f && travel == 0 && duration <= EmergencyPriority.CRITICAL_BUDGET
                && kind is Kind.Tourniquet or Kind.Hemostat;
            Candidate value = new(kind, device, limb, EmergencyPriority.Score(urgency, benefit, duration, travel, cost), travel, cost,
                criticalTreatment, fast, native);
            if (kind is Kind.Tourniquet or Kind.Hemostat or Kind.Bandage) { miBleeding = true; }
            if (Better(value, best)) { best = value; }
            if (kind is not (Kind.Tourniquet or Kind.Hemostat or Kind.Bandage or Kind.Stabilize)
                && Better(value, otherEmergency)) { otherEmergency = value; }
            if (criticalTreatment && Better(value, rescue)) { rescue = value; }
            if (fast && Better(value, quick)) { quick = value; }
        }
        void Devices(ThingDef def, Action<Thing> add, bool retrieve)
        {
            Thing? pocket = MedicalDeviceHelper.FindFeasibleInventoryDevice(doctor, def);
            if (pocket is not null) { add(pocket); }
            if (retrieve && MedicalDeviceHelper.FindRetrievedDevice(doctor, patient, def) is Thing nearby && nearby != pocket) { add(nearby); }
        }
        float Reduction(ThingDef def, float fallback) => 1f - (def.GetModExtension<HemostasisModExtension>()?.CoagulationMultiplier ?? 1f - fallback);

        if (!superclotting && KnownResearchProjectDefOf.BasicFirstAid.IsFinished && nativeBleeding > Mathf.Epsilon
            && MedicalDeviceHelper.FindFeasibleInventoryDevice(doctor, KnownThingDefOf.Tourniquet) is Thing tourniquet)
        {
            using BleedRateByLimbEnumerable limbs = BleedRateByLimbEnumerable.EvaluateLimbs(patient);
            bool competent = JobDriver_TourniquetBase.PawnKnowsWhatTheyreDoing(doctor);
            foreach ((BodyPartRecord limb, float rate) in limbs.OrderByDescending(static item => item.Value))
            {
                if (rate <= MoreInjuriesMod.Settings.MinBleedRateForAutoTourniquet
                    || limb.def == KnownBodyPartDefOf.Neck && (competent || !Rand.Chance(.2f))) { continue; }
                Consider(Kind.Tourniquet, tourniquet, EmergencyPriority.BleedingUrgency(rate * normalization), Reduction(KnownThingDefOf.Tourniquet, .95f), 90, bleeding: rate * normalization, limb: limb);
                break;
            }
        }
        Hediff? wound = patient.health.hediffSet.hediffs.Where(JobDriver_HemostasisBase.JobCanTreat)
            .OrderByDescending(static injury => injury.BleedRate).FirstOrDefault();
        if (wound is not null && TreatBleeding(wound.BleedRate * normalization))
        {
            float rate = wound.BleedRate * normalization;
            if (KnownResearchProjectDefOf.AdvancedFirstAid.IsFinished)
            { Devices(KnownThingDefOf.HemostaticAgent, item => Consider(Kind.Hemostat, item, EmergencyPriority.BleedingUrgency(rate),
                Reduction(KnownThingDefOf.HemostaticAgent, .95f) * EmergencyPriority.Suitability(true, rate, superclotting), 40, bleeding: rate), true); }
            if (KnownResearchProjectDefOf.BasicAnatomy.IsFinished)
            { Devices(KnownThingDefOf.Bandage, item => Consider(Kind.Bandage, item, EmergencyPriority.BleedingUrgency(rate),
                Reduction(KnownThingDefOf.Bandage, .8f) * EmergencyPriority.Suitability(false, rate, superclotting), 80, bleeding: rate), true); }
        }
        if (!miBleeding && nativeBleeding > Mathf.Epsilon && CombatExtendedBridge.TryCreateStabilizationJob(doctor, patient) is Job stabilize)
        {
            int count = 0; float rate = 0f;
            foreach (Hediff injury in patient.health.hediffSet.GetHediffsTendable())
            {
                if (!CombatExtendedBridge.CanStabilize(injury)) { continue; }
                count++; // Native batch time includes overlapping MI-controlled wounds too.
                if (injury is IStatefulInjury state && (state.State.IsTemporarilyCoagulated || state.State.CoagulationMultiplier < 1f)) { continue; }
                float contribution = injury.BleedRate * normalization;
                if (TreatBleeding(contribution)) { rate += contribution; }
            }
            Consider(Kind.Stabilize, stabilize.targetB.Thing, EmergencyPriority.BleedingUrgency(rate), 1f,
                Math.Max(1, (int)(60f * count / Math.Max(.001f, doctor.GetStatValue(StatDefOf.MedicalTendSpeed)))), native: stabilize);
        }
        if (KnownResearchProjectDefOf.EmergencyMedicine.IsFinished && patient.health.hediffSet.hediffs.Any(JobDriver_UseDefibrillator.JobCanTreat))
        {
            float urgency = arrest ? 100f : 60f;
            if (MoreInjuriesMod.Settings.EnableAdrenaline
                && (!patient.health.hediffSet.TryGetHediff(KnownHediffDefOf.AdrenalineRush, out Hediff? adrenaline)
                    || adrenaline.Severity < JobDriver_UseEpinephrine.SAFETY_THRESHOLD))
            { Devices(KnownThingDefOf.Epinephrine, item => Consider(Kind.Epinephrine, item, urgency, 1.75f, 40, criticalTreatment: arrest), !aftercare); }
            Devices(KnownThingDefOf.Defibrillator, item => Consider(Kind.Defibrillator, item, urgency, 1.5f, 180,
                reusable: true, criticalTreatment: arrest), true);
        }
        if (cprAvailable)
        {
            float urgency = arrest ? 100f : criticalAirway ? 80f : 20f * choking!.Severity;
            Consider(Kind.Cpr, null, urgency, 1f, 360, criticalTreatment: critical);
            if (!arrest && KnownResearchProjectDefOf.EmergencyMedicine.IsFinished)
            {
                // Preserve the airway-specific bounded search instead of broad map retrieval.
                Job airway = AutomaticAirwayTreatment.CreateJob(doctor, patient);
                if (airway.def == KnownJobDefOf.UseSuctionDevice)
                { Consider(Kind.Suction, airway.targetB.Thing, urgency, 1.5f, 600, reusable: true, criticalTreatment: criticalAirway); }
            }
        }
        if (KnownResearchProjectDefOf.EmergencyMedicine.IsFinished && patient.playerSettings?.medCare is not MedicalCareCategory.NoMeds)
        {
            float loss = patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0f;
            float dilution = patient.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.Hemodilution)?.Severity ?? 0f;
            float shock = patient.health.hediffSet.GetFirstHediffOfDef(KnownHediffDefOf.HypovolemicShock)?.Severity ?? 0f;
            float urgency = EmergencyPriority.TransfusionUrgency(loss, dilution, ShockPerfusionModel.EffectiveShock(shock, loss));
            if (MedicalDeviceHelper.FindMedicalDevice(doctor, patient, JobDriver_UseSalineBag.JobDeviceDef,
                static target => JobDriver_UseSalineBag.JobGetMedicalDeviceCountToFullyHeal(target, false), fromInventoryOnly: aftercare) is Thing saline)
            { Consider(Kind.Saline, saline, urgency, 1f, 720); }
            if (MedicalDeviceHelper.FindMedicalDevice(doctor, patient, JobDriver_UseBloodBag.JobDeviceDef,
                static target => JobDriver_UseBloodBag.JobGetMedicalDeviceCountToFullyHeal(target, false), fromInventoryOnly: aftercare) is Thing blood)
            { Consider(Kind.Blood, blood, urgency, 1f, 720); }
        }
        Candidate selected = critical && rescue.Kind != Kind.None
            ? !superclotting && quickAllowed && quick.Kind != Kind.None ? quick : rescue
            : superclotting && otherEmergency.Kind != Kind.None ? otherEmergency : best;
        Job? result = selected.Kind switch
        {
            Kind.Tourniquet => JobDriver_UseTourniquet.GetDispatcher(doctor, patient, selected.Device!, selected.Limb!).CreateJob(),
            Kind.Hemostat => JobDriver_UseHemostaticAgent.GetDispatcher(doctor, patient, selected.Device!, EmergencyTravel.InInventory(doctor, selected.Device!)).CreateJob(),
            Kind.Bandage => JobDriver_UseBandage.GetDispatcher(doctor, patient, selected.Device!, EmergencyTravel.InInventory(doctor, selected.Device!)).CreateJob(),
            Kind.Stabilize => selected.NativeJob,
            Kind.Epinephrine => JobDriver_UseEpinephrine.GetDispatcher(doctor, patient, selected.Device!).CreateJob(),
            Kind.Defibrillator => JobDriver_UseDefibrillator.GetDispatcher(doctor, patient, selected.Device!).CreateJob(),
            Kind.Cpr => JobDriver_PerformCpr.GetDispatcher(doctor, patient).CreateJob(),
            Kind.Suction => JobDriver_UseSuctionDevice.GetDispatcher(doctor, patient, selected.Device!).CreateJob(),
            Kind.Saline => JobDriver_UseSalineBag.GetDispatcher(doctor, patient, selected.Device!, aftercare, SalineTransfusionMode.Stabilize).CreateJob(),
            Kind.Blood => JobDriver_UseBloodBag.GetDispatcher(doctor, patient, selected.Device!, aftercare, fullyHeal: false).CreateJob(),
            _ => null
        };
        if (result is not null) { result.endAfterTendedOnce = true; return result; }
        if (!aftercare) { return null; }
        if (!superclotting && patient.Downed && patient.health.hediffSet.hediffs.Any(static injury => injury.TendableNow()))
        {
            Job tend = JobMaker.MakeJob(JobDefOf.TendPatient, patient);
            tend.endAfterTendedOnce = true;
            return tend;
        }
        if (patient.health.hediffSet.BleedRateTotal < Mathf.Epsilon && !HasUntreatedTourniquetWound(patient)
            && patient.health.hediffSet.hediffs.Find(static injury => injury.def == KnownHediffDefOf.TourniquetApplied) is Hediff tourniquetApplied)
        { return JobDriver_RemoveTourniquetSafely.GetDispatcher(doctor, patient, tourniquetApplied.Part).CreateJob(); }
        return null;
    }

    private static bool HasUntreatedTourniquetWound(Pawn patient)
    {
        foreach (Hediff tourniquet in patient.health.hediffSet.hediffs)
        {
            if (tourniquet.def != KnownHediffDefOf.TourniquetApplied) { continue; }
            foreach (Hediff wound in patient.health.hediffSet.hediffs)
            {
                if (wound is IStatefulInjury && wound.def.injuryProps?.bleedRate > 0f
                    && !wound.IsTended() && !wound.IsPermanent() && wound.IsOnBodyPartOrChildren(tourniquet.Part)) { return true; }
            }
        }
        return false;
    }
}
