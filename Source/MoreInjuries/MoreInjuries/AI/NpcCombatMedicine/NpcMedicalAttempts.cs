using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

// Main-thread, unsaved, weak pawn keys. Never becomes drug-dose history.
internal static class NpcMedicalAttempts
{
    private sealed class PatientHistory
    {
        internal readonly Dictionary<JobDef, NpcMedicalRetry> Actions = [];
        internal readonly Dictionary<JobDef, string> Diagnostics = [];
    }
    private static readonly ConditionalWeakTable<Pawn, ConditionalWeakTable<Pawn, PatientHistory>> s_history = new();
    private static readonly ConditionalWeakTable<Job, Attempt> s_active = new();
    private static NpcMedicalRetry Retry(Pawn doctor, Pawn patient, JobDef action)
    {
        Dictionary<JobDef, NpcMedicalRetry> actions = s_history.GetOrCreateValue(doctor).GetOrCreateValue(patient).Actions;
        if (!actions.TryGetValue(action, out NpcMedicalRetry? retry)) { actions[action] = retry = new(); }
        return retry;
    }

    internal static bool Automatic(Pawn doctor, Job job) => job.def is not null && NpcMedicalAi.ActorEligible(doctor) && !job.playerForced
        && (job.endAfterTendedOnce || job.def == KnownJobDefOf.MI_NpcTend || job.def == KnownJobDefOf.MI_NpcPainSelf || job.def == KnownJobDefOf.MI_NpcRestorativeSelf || job.def == KnownJobDefOf.MI_NpcExtract);

    internal static bool Ready(Pawn doctor, Pawn patient, JobDef action, Thing? supply)
        => NpcDeparture.ProbingRetry(doctor) || Retry(doctor, patient, action).Allowed(Find.TickManager.TicksGame, Context(doctor, patient, supply));
    internal static int RetryDeadline(Pawn doctor, Pawn patient, JobDef action) => Retry(doctor, patient, action).Deadline;
    internal static bool ExtractionAllowed(Pawn doctor, Pawn patient)
        => !NpcMedicalCompatibility.SafetyOwned(doctor) && Ready(doctor, patient, KnownJobDefOf.MI_NpcExtract, null);
    internal static void Progress(Job job)
    {
        if (s_active.TryGetValue(job, out Attempt? attempt) && attempt.Matches(job)) { attempt.Progress = true; }
    }

    internal static bool Allowed(Pawn doctor, Pawn patient, JobDef action, Thing? supply)
    {
        NpcMedicalRetry retry = Retry(doctor, patient, action);
        bool ready = NpcDeparture.ProbingRetry(doctor) || retry.Allowed(Find.TickManager.TicksGame, Context(doctor, patient, supply));
        bool safe = NpcMedicalSafety.Allowed(doctor, patient, supply);
        if (Logger.IsEnabled && Prefs.DevMode && !NpcDeparture.ProbingRetry(doctor))
        {
            Dictionary<JobDef, string> diagnostics = s_history.GetOrCreateValue(doctor).GetOrCreateValue(patient).Diagnostics;
            string state = !safe ? "incoming fire/safety" : !ready ? "retry=" + retry.Deadline : "eligible";
            if (!diagnostics.TryGetValue(action, out string? previous) || previous != state)
            {
                diagnostics[action] = state;
                Logger.Log($"More Injuries NPC attempt: {doctor.ThingID}->{patient.ThingID}; job={action.defName}; supply={supply?.ThingID}; stage=selection; {state}; impact={NpcMedicalSafety.Recent(doctor) || NpcMedicalSafety.Recent(patient)}; safety={NpcMedicalCompatibility.SafetyOwned(doctor)}.");
            }
        }
        return ready && safe;
    }

    internal static bool Allowed(Pawn doctor, Pawn patient, Job job)
        => Allowed(doctor, patient, job.def, job.def == KnownJobDefOf.MI_NpcPainSelf || job.def == KnownJobDefOf.MI_NpcRestorativeSelf ? job.targetA.Thing : job.targetB.Thing);

    private static string Source(Pawn doctor, Pawn patient, Thing? supply) => supply is null ? "none"
        : supply == doctor.carryTracker.CarriedThing || supply.ParentHolder == doctor.inventory ? "own"
        : supply.ParentHolder == patient.inventory ? "patient"
        : supply.ParentHolder is Pawn_InventoryTracker ? "inventory" : "ground";

    private static string Indication(Pawn patient) => string.Join(";", patient.health.hediffSet.hediffs.Select(hediff =>
        hediff.GetUniqueLoadID() + ":" + hediff.IsTended() + ":" + (int)(hediff.Severity * 10f)))
        + "/pain:" + (int)(patient.health.hediffSet.PainTotal * 10f)
        + "/urgency:" + (int)(NpcMedicalAi.Urgency(patient) / 10f) + "/shock:" + patient.health.InPainShock;

    private sealed class HealthSnapshot
    {
        private readonly Dictionary<Hediff, (float Severity, bool Tended, bool Tendable)> _hediffs;
        private readonly float _pain, _bleeding;
        private readonly float[] _capacities;
        private static readonly PawnCapacityDef[] s_capacities = [PawnCapacityDefOf.Consciousness,
            PawnCapacityDefOf.Breathing, PawnCapacityDefOf.BloodPumping, PawnCapacityDefOf.Moving, PawnCapacityDefOf.Manipulation];
        internal HealthSnapshot(Pawn patient)
        {
            _hediffs = patient.health.hediffSet.hediffs.ToDictionary(hediff => hediff,
                hediff => (hediff.Severity, hediff.IsTended(), hediff.TendableNow()));
            _pain = patient.health.hediffSet.PainTotal;
            _bleeding = patient.health.hediffSet.BleedRateTotal;
            _capacities = s_capacities.Select(capacity => patient.health.capacities.GetLevel(capacity)).ToArray();
        }
        internal bool Improved(Pawn patient) => patient.health.hediffSet.PainTotal < _pain - .000001f
            || patient.health.hediffSet.BleedRateTotal < _bleeding - .000001f
            || s_capacities.Where((capacity, index) => patient.health.capacities.GetLevel(capacity) > _capacities[index] + .000001f).Any()
            || _hediffs.Any(pair => !patient.health.hediffSet.hediffs.Contains(pair.Key)
                || pair.Key.Severity < pair.Value.Severity - .000001f
                || !pair.Value.Tended && pair.Key.IsTended() || pair.Value.Tendable && !pair.Key.TendableNow());
    }

    private static string Context(Pawn doctor, Pawn patient, Thing? supply) => Indication(patient) + "/" + patient.Position
        + "/care:" + patient.playerSettings?.medCare + "/relation:" + NpcMedicalAi.Relation(doctor, patient)
        + "/doctor:" + NpcMedicalGeneration.CanDoctor(doctor) + "/work:" + MedicalTiming.CanWork(doctor)
        + "/manip:" + doctor.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
        + "/source:" + Source(doctor, patient, supply);

    internal static Attempt? Begin(Pawn doctor, Job job, Pawn patient, Thing? supply)
    {
        if (!Automatic(doctor, job)) { return null; }
        if (s_active.TryGetValue(job, out Attempt? previous))
        {
            if (previous.Matches(job)) { return previous; }
            Finish(previous, JobCondition.InterruptOptional);
        }
        Attempt attempt = new(doctor, patient, job, supply);
        s_active.Add(job, attempt);
        attempt.Diagnose("begin");
        return attempt;
    }

    internal static void Stage(Job job, string stage)
    {
        if (!s_active.TryGetValue(job, out Attempt? attempt) || !attempt.Matches(job) || attempt.Stage == stage) { return; }
        attempt.Stage = stage;
        attempt.Diagnose("stage");
    }

    internal static void Applying(Job job)
    {
        if (!s_active.TryGetValue(job, out Attempt? attempt) || !attempt.Matches(job)) { return; }
        attempt.Applying();
        Stage(job, "application");
    }

    internal static void Applied(Job job, bool accepted)
    {
        if (!s_active.TryGetValue(job, out Attempt? attempt) || !attempt.Matches(job)) { return; }
        NpcQuickAid.Applied(attempt.Doctor, attempt.Patient, job, accepted);
        attempt.Progress |= accepted && attempt.Improved;
        // Natural healing improves later; an actually consumed restorative whose
        // indicated stage appeared is progress now, without a persistent cooldown.
        if (accepted && (attempt.Action == KnownJobDefOf.MI_NpcRestorativeSelf || attempt.Action == KnownJobDefOf.MI_NpcRestorativeAssist)
            && attempt.Supply is { Destroyed: true } consumed && NpcMedicalDrugs.EffectName(consumed.def) is string effect
            && attempt.Patient.health.hediffSet.hediffs.Any(hediff => hediff.def.defName == effect)) { attempt.Progress = true; }
    }

    internal static void Finish(Job job, JobCondition condition)
    {
        if (!s_active.TryGetValue(job, out Attempt? attempt)) { return; }
        Finish(attempt, condition);
    }

    internal static void Finish(Attempt attempt, JobCondition condition)
    {
        if (attempt.Completed) { return; }
        attempt.Completed = true;
        NpcQuickAid.Finish(attempt.Job, attempt.JobId, attempt.Action);
        if (s_active.TryGetValue(attempt.Job, out Attempt? current) && current == attempt)
        { s_active.Remove(attempt.Job); }
        attempt.Progress |= attempt.Improved;
        NpcDeparture.Finish(attempt.Job, attempt.JobId, attempt.Action, attempt.Progress);
        bool resolved = attempt.Patient.Dead || (attempt.Action == KnownJobDefOf.MI_NpcExtract ? !attempt.Patient.Downed
            : NpcMedicalAi.Urgency(attempt.Patient) <= 0f && !attempt.Patient.health.HasHediffsNeedingTend());
        NpcMedicalRetry retry = Retry(attempt.Doctor, attempt.Patient, attempt.Action);
        retry.Complete(Find.TickManager.TicksGame, attempt.Context, attempt.Progress || resolved);
        attempt.Diagnose("finish=" + condition + "; progress=" + attempt.Progress + "; resolved=" + resolved + "; retry=" + retry.Deadline);
    }

    internal sealed class Attempt
    {
        internal readonly Pawn Doctor, Patient;
        internal readonly Job Job;
        internal readonly JobDef Action;
        private readonly int _jobId;
        internal int JobId => _jobId;
        internal bool Completed;
        private readonly Thing? _supply;
        internal Thing? Supply => _supply;
        internal readonly string Context;
        private HealthSnapshot? _beforeApplication;
        internal void Applying() => _beforeApplication = new(Patient);
        internal bool Improved => _beforeApplication?.Improved(Patient) == true;
        internal string Stage = "retrieval";
        internal bool Progress;
        internal Attempt(Pawn doctor, Pawn patient, Job job, Thing? supply)
        {
            Doctor = doctor; Patient = patient; Job = job; Action = job.def; _jobId = job.loadID; _supply = supply;
            Context = NpcMedicalAttempts.Context(doctor, patient, supply);
        }
        internal bool Matches(Job? job) => !Completed && job is not null && ReferenceEquals(Job, job)
            && job.loadID == _jobId && job.def == Action;
        internal void Diagnose(string transition)
        {
            if (!Logger.IsEnabled || !Prefs.DevMode) { return; }
            Logger.Log($"More Injuries NPC attempt: {Doctor.ThingID}->{Patient.ThingID}; job={Action.defName}; supply={_supply?.ThingID}; stage={Stage}; {transition}; impact={NpcMedicalSafety.Recent(Doctor) || NpcMedicalSafety.Recent(Patient)}; safety={NpcMedicalCompatibility.SafetyOwned(Doctor)}.");
        }
    }
}
