using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MoreInjuries.AI.NpcCombatMedicine;

// Main-thread, weak, unsaved coordination. Native lord state remains authoritative.
internal static class NpcDeparture
{
    internal const int INTERVAL = 150, IDLE_LIMIT = 1250, TOTAL_LIMIT = 7500, PATIENT_BUDGET = 4;
    internal enum Kind { Unhandled, Work, Wait, Protected, Released }
    internal readonly record struct Result(Kind Kind, Job? Job = null, string Reason = "");
    private sealed class Episode(int now)
    {
        internal readonly int Started = now;
        internal int Progress = now;
        internal bool Released;
        internal readonly HashSet<Pawn> Raidmates = [];
        internal bool Expired(int now) => Released |= now - Started >= TOTAL_LIMIT || now - Progress >= IDLE_LIMIT;
        internal void Advance()
        {
            int now = Find.TickManager.TicksGame;
            if (!Expired(now)) { Progress = now; }
        }
        internal int Deadline => Math.Min(Started + TOTAL_LIMIT, Progress + IDLE_LIMIT);
    }
    private sealed class Actor
    {
        internal Lord? Lord;
        internal int LastEvaluation = -1000, Cursor, Seen, WaitingUntil;
        internal bool Released, Busy;
        internal Lease? Pending;
        internal List<Pawn> Batch = [];
        internal readonly Dictionary<string, string> Diagnostics = [];
    }
    private sealed class Probe { internal int Hands, Retry; }
    private readonly struct ProbeScope : IDisposable
    {
        private readonly Probe _probe;
        private readonly bool _retry;
        internal ProbeScope(Pawn actor, bool retry = false)
        {
            _probe = s_probes.GetOrCreateValue(actor); _retry = retry;
            _probe.Hands++;
            if (retry) { _probe.Retry++; }
        }
        public void Dispose() { _probe.Hands--; if (_retry) { _probe.Retry--; } }
    }
    private sealed class Lease(Pawn actor, Pawn patient, Job job, Episode episode)
    {
        internal readonly Pawn Actor = actor, Patient = patient;
        internal readonly Job Job = job;
        internal readonly int Id = job.loadID;
        internal readonly JobDef Action = job.def;
        internal readonly Map Map = actor.Map;
        internal readonly Episode Episode = episode;
        internal float BestDistance = patient.PositionHeld.DistanceToSquared(job.targetB.Cell);
        internal bool Matches => Job.loadID == Id && Job.def == Action && Job.targetA.Thing == Patient;
    }
    private static readonly ConditionalWeakTable<Lord, Episode> s_episodes = new();
    private static readonly ConditionalWeakTable<Pawn, Actor> s_actors = new();
    private static readonly ConditionalWeakTable<Pawn, Probe> s_probes = new();
    private static readonly ConditionalWeakTable<Job, Lease> s_work = new();

    internal static bool ProspectiveHands(Pawn pawn) => s_probes.TryGetValue(pawn, out Probe? probe) && probe.Hands > 0
        && pawn.carryTracker.CarriedThing is not Pawn;
    internal static bool ProbingRetry(Pawn pawn) => s_probes.TryGetValue(pawn, out Probe? probe) && probe.Retry > 0;
    internal static bool IsWork(Job job) => s_work.TryGetValue(job, out Lease? lease) && lease.Matches;
    internal static bool DepartureJob(Job? job) => job is not null && !job.playerForced
        && (job.def == JobDefOf.Steal || job.def == JobDefOf.Goto && job.exitMapOnArrival);

    internal static bool InContext(Pawn pawn)
    {
        if (!NpcEvacuation.Enabled || !NpcMedicalAi.CanTakeDecision(pawn) || pawn.IsPrisoner) { return false; }
        Lord? lord = pawn.GetLord();
        if (lord?.questTags is { Count: > 0 }) { return false; }
        Type? mission = lord?.LordJob?.GetType(), phase = lord?.CurLordToil?.GetType();
        return (mission == typeof(LordJob_AssaultColony) || mission == typeof(LordJob_Siege))
            && (phase == typeof(LordToil_StealCover) || phase == typeof(LordToil_ExitMap)
                || phase == typeof(LordToil_ExitMapAndDefendSelf));
    }
    private static Actor State(Pawn pawn, Lord lord)
    {
        Actor state = s_actors.GetOrCreateValue(pawn);
        if (state.Lord != lord)
        {
            CancelPending(pawn, state);
            state.Lord = lord; state.LastEvaluation = -1000; state.Cursor = state.Seen = state.WaitingUntil = 0;
            state.Released = false; state.Diagnostics.Clear();
            state.Batch.Clear();
        }
        return state;
    }
    private static void Report(Pawn pawn, Actor state, string reason, string key = "state")
    {
        if (!Logger.IsEnabled || !Prefs.DevMode || state.Diagnostics.TryGetValue(key, out string previous) && previous == reason) { return; }
        state.Diagnostics[key] = reason;
        Logger.Log($"More Injuries NPC departure: pawn={pawn.ThingID}; mode={MoreInjuriesMod.Settings.NpcMedicalAiMode}; "
            + $"lord={pawn.GetLord()?.LordJob?.GetType().Name}; phase={pawn.GetLord()?.CurLordToil?.GetType().Name}; "
            + $"duty={pawn.mindState?.duty?.def?.defName}; job={pawn.CurJob?.def?.defName}/{pawn.CurJob?.loadID}; "
            + $"cargo={pawn.carryTracker.CarriedThing?.ThingID}; doctor={NpcMedicalGeneration.CanDoctor(pawn)}; "
            + $"recentDamage={NpcMedicalSafety.Recent(pawn)}; {reason}.");
    }
    internal static bool Owns(Pawn pawn)
    {
        if (!InContext(pawn) || NpcMedicalCompatibility.SafetyOwned(pawn) || NpcMedicalAi.ShouldWithdraw(pawn)) { return false; }
        if (pawn.CurJob is Job current && NpcMedicalAi.Owns(current))
        { return !IsWork(current) || Continue(pawn, current); }
        return s_actors.TryGetValue(pawn, out Actor? state) && state.Lord == pawn.GetLord() && !state.Released
            && s_episodes.TryGetValue(state.Lord, out Episode? episode) && !episode.Expired(Find.TickManager.TicksGame)
            && (state.Pending is not null || Find.TickManager.TicksGame < state.WaitingUntil);
    }

    internal static Result Decide(Pawn pawn, bool force = false)
    {
        if (!InContext(pawn)) { CancelPending(pawn); return default; }
        Lord lord = pawn.GetLord();
        Actor state = State(pawn, lord);
        if (state.Busy) { return new(Kind.Protected); }
        Report(pawn, state, $"departure entry; phase={lord.CurLordToil.GetType().Name}; "
            + $"duty={pawn.mindState?.duty?.def?.defName}; job={pawn.CurJob?.def.defName}/{pawn.CurJob?.loadID}; "
            + $"cargo={pawn.carryTracker.CarriedThing?.ThingID}", "entry");
        if (NpcMedicalCompatibility.SafetyOwned(pawn) || NpcMedicalAi.ShouldWithdraw(pawn) || !NpcExtraction.Carrier(pawn))
        { CancelPending(pawn, state); Report(pawn, state, "native safety/individual withdrawal or incapable rescuer"); return default; }
        Episode episode = s_episodes.GetValue(lord, _ => new Episode(Find.TickManager.TicksGame));
        episode.Raidmates.UnionWith(lord.ownedPawns);
        if (pawn.CurJob is Job active && NpcMedicalAi.Owns(active))
        {
            if (IsWork(active) && !Continue(pawn, active)) { return new(Kind.Released); }
            Report(pawn, state, "preserve existing medical work");
            return new(Kind.Protected);
        }
        if (pawn.carryTracker.CarriedThing is Pawn || ProtectedJob(pawn.CurJob)) { return default; }
        int now = Find.TickManager.TicksGame;
        if (state.Released || episode.Expired(now))
        { CancelPending(pawn, state); Report(pawn, state, "departure released; exhausted/latched episode"); return new(Kind.Released); }
        if (state.Pending is Lease pending)
        {
            if (pending.Matches && Continue(pawn, pending.Job)) { return new(Kind.Work, pending.Job, "prepared medical work"); }
            CancelPending(pawn, state);
        }
        if (!force && now - state.LastEvaluation < INTERVAL)
        { return new(Kind.Protected, Reason: "assessment throttled"); }
        bool refresh = now - state.LastEvaluation >= INTERVAL;
        if (refresh) { state.LastEvaluation = now; }
        state.Busy = true;
        try { return Evaluate(pawn, lord, state, episode, refresh); }
        finally { state.Busy = false; }
    }
    private static bool ProtectedJob(Job? job) => job?.playerForced == true
        || job?.def.defName is "Kidnap" or "CarryDownedPawn" or "CarryDownedPawnToExit" or "TakeWoundedGuest"
            or "EnterTransporter" or "EnterTransporterAndWait" or "EnterShuttle";

    private static Result Evaluate(Pawn pawn, Lord lord, Actor state, Episode episode, bool refresh)
    {
        List<Pawn> patients = pawn.Map.mapPawns.AllPawnsSpawned.Where(patient => patient != pawn && patient.Spawned
            && patient.Downed && !patient.Dead && patient.Faction == pawn.Faction && NpcMedicalAi.Relation(pawn, patient) >= 0)
            .OrderByDescending(episode.Raidmates.Contains).ThenByDescending(NpcMedicalAi.Urgency)
            .ThenBy(patient => patient.Position.DistanceToSquared(pawn.Position)).ThenBy(patient => patient.ThingID).ToList();
        Report(pawn, state, $"discovered casualties={patients.Count}; known raidmates={patients.Count(episode.Raidmates.Contains)}", "discovery");
        if (patients.Count == 0)
        { episode.Released = true; Report(pawn, state, "departure released; no eligible spawned casualties"); return new(Kind.Released); }
        if (refresh)
        {
            if (state.Seen >= patients.Count) { state.Seen = 0; }
            state.Cursor %= patients.Count;
            state.Batch = Enumerable.Range(0, Math.Min(PATIENT_BUDGET, patients.Count))
                .Select(index => patients[(state.Cursor + index) % patients.Count]).ToList();
            state.Cursor = (state.Cursor + state.Batch.Count) % patients.Count; state.Seen += state.Batch.Count;
        }
        // A fresh native exit check revalidates the same bounded shortlist.
        List<Pawn> batch = state.Batch.Where(patients.Contains).ToList();
        bool temporary = false;
        // Complete care consideration before choosing an exit carry.
        foreach (Pawn patient in batch)
        {
            if (!CanApproach(pawn, patient)) { Report(pawn, state, "reject " + patient.ThingID + "; access/approach budget", patient.ThingID); continue; }
            if (!NpcMedicalReservations.PatientAvailable(pawn, patient))
            {
                temporary |= AnotherHelper(pawn, patient);
                Report(pawn, state, "reject " + patient.ThingID + "; patient reservation", patient.ThingID); continue;
            }
            Job? preview;
            using (new ProbeScope(pawn)) { preview = NpcMedicalAi.DepartureCare(pawn, patient, preview: true); }
            if (preview is not null && Commit(pawn, patient, preview, episode, state) is Job work)
            { return new(Kind.Work, work, "care before departure"); }
            using (new ProbeScope(pawn, retry: true))
            {
                Job? retry = NpcMedicalAi.DepartureCare(pawn, patient, preview: true);
                if (retry is not null)
                {
                    int deadline = NpcMedicalAttempts.RetryDeadline(pawn, patient, retry.def);
                    temporary |= deadline > Find.TickManager.TicksGame && deadline <= episode.Deadline;
                }
            }
        }
        foreach (Pawn patient in batch)
        {
            if (!CanApproach(pawn, patient) || !NpcMedicalReservations.PatientAvailable(pawn, patient)) { continue; }
            bool possible;
            NpcEvacuation.Failure failure;
            IntVec3 exit;
            using (new ProbeScope(pawn))
            { possible = NpcEvacuation.TryPlan(pawn, patient, out exit, out failure, departure: true); }
            if (possible)
            {
                Job preview = Evacuation(patient, exit);
                if (Commit(pawn, patient, preview, episode, state) is Job work)
                { return new(Kind.Work, work, "evacuation before departure"); }
            }
            else if (failure == NpcEvacuation.Failure.RecentDamage)
            {
                using (new ProbeScope(pawn))
                { temporary |= NpcEvacuation.TryPlan(pawn, patient, out _, out _, departure: true, ignoreRecent: true); }
            }
            Report(pawn, state, "reject " + patient.ThingID + "; evacuation=" + failure, patient.ThingID);
        }
        if ((temporary || state.Seen < patients.Count) && SafeToWait(pawn))
        {
            state.WaitingUntil = Math.Min(Find.TickManager.TicksGame + INTERVAL, episode.Deadline);
            Job wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            wait.expiryInterval = state.WaitingUntil - Find.TickManager.TicksGame;
            wait.checkOverrideOnExpire = true;
            Report(pawn, state, temporary ? "bounded safe wait; temporary medical blocker" : "bounded safe wait; search continues");
            return new(Kind.Wait, wait);
        }
        state.Released = true; state.WaitingUntil = 0;
        Report(pawn, state, "departure released; no feasible care/route or safe temporary wait");
        return new(Kind.Released);
    }
    private static Job Evacuation(Pawn patient, IntVec3 exit)
    { Job job = JobMaker.MakeJob(KnownJobDefOf.MI_NpcEvacuate, patient, exit); job.count = 1; return job; }
    private static bool CanApproach(Pawn actor, Pawn patient)
    {
        if (!actor.CanReach(patient, PathEndMode.Touch, Danger.Some)) { return false; }
        PawnPath path = actor.Map.pathFinder.FindPathNow(actor.Position, patient.Position, actor, peMode: PathEndMode.Touch);
        try { return path.Found && path.TotalCost <= NpcMedicalModel.TRAVEL_TIMEOUT; }
        finally { path.ReleaseToPool(); }
    }
    private static bool SafeToWait(Pawn actor) => !NpcMedicalCompatibility.SafetyOwned(actor)
        && new NpcExtraction.Assessment(actor, actor, actor.Position, evacuation: true)
            .At(actor.Position, out bool allowed).Safe && allowed;
    private static bool AnotherHelper(Pawn actor, Pawn patient) => actor.Map.mapPawns.AllPawnsSpawned.Any(other =>
        other != actor && !other.Dead && !other.Downed && other.Faction == actor.Faction
        && other.CurJob?.targetA.Thing == patient && NpcMedicalAi.Owns(other.CurJob));

    private static Job? Commit(Pawn actor, Pawn patient, Job preview, Episode episode, Actor state)
    {
        if (episode.Expired(Find.TickManager.TicksGame) || !InContext(actor)
            || !NpcMedicalReservations.PatientAvailable(actor, patient)) { return null; }
        Thing? cargo = actor.carryTracker.CarriedThing;
        if (cargo is Pawn) { return null; }
        if (cargo is not null)
        {
            // The prospective assessment never changes the real carry tracker.
            Map claimMap = actor.Map;
            if (!actor.Reserve(patient, preview, errorOnFailed: false)) { return null; }
            try
            {
                if (!actor.carryTracker.TryDropCarriedThing(actor.Position, ThingPlaceMode.Near, out Thing placed)
                    || placed is null || actor.carryTracker.CarriedThing is not null)
                { Report(actor, state, "reject rescue; loot placement failed"); return null; }
            }
            finally { claimMap.reservationManager.ReleaseClaimedBy(actor, preview); }
        }
        if (!InContext(actor) || episode.Expired(Find.TickManager.TicksGame) || patient.Dead || !patient.Downed) { return null; }
        Job? chosen = NpcMedicalAi.DepartureCare(actor, patient, preview: false);
        if (chosen is null && NpcEvacuation.TryPlan(actor, patient, out IntVec3 exit, out _, departure: true))
        { chosen = Evacuation(patient, exit); }
        if (chosen is null) { return null; }
        Map preparedMap = actor.Map;
        if (!actor.Reserve(patient, chosen, errorOnFailed: false) || !NpcMedicalReservations.PrepareTacticalJob(actor, chosen))
        {
            preparedMap.reservationManager.ReleaseClaimedBy(actor, chosen);
            NpcMedicalAttempts.Finish(chosen, JobCondition.Incompletable);
            NpcQuickAid.Finish(chosen, chosen.loadID, chosen.def);
            return null;
        }
        Lease lease = new(actor, patient, chosen, episode);
        s_work.Remove(chosen); s_work.Add(chosen, lease);
        state.Pending = lease; state.WaitingUntil = 0;
        Report(actor, state, "selected " + chosen.def.defName + " for " + patient.ThingID);
        return chosen;
    }

    internal static bool Continue(Pawn actor, Job job)
    {
        if (!s_work.TryGetValue(job, out Lease? lease) || !lease.Matches) { return true; }
        if (lease.Actor != actor) { return false; }
        Pawn patient = lease.Patient;
        bool carried = actor.carryTracker.CarriedThing == patient;
        if (patient.Dead || patient.MapHeld != actor.Map || NpcMedicalAi.Relation(actor, patient) < 0) { return false; }
        if (carried)
        {
            float distance = actor.Position.DistanceToSquared(job.targetB.Cell);
            if (distance < lease.BestDistance) { lease.BestDistance = distance; lease.Episode.Advance(); }
            return true; // Existing drivers still require a safe route and preserve the patient on failure.
        }
        return InContext(actor) && patient.Downed && !NpcMedicalAi.ShouldWithdraw(actor)
            && !lease.Episode.Expired(Find.TickManager.TicksGame) && !NpcMedicalCompatibility.SafetyOwned(actor);
    }
    internal static void Finish(Job job, int identity, JobDef action, bool progress = false)
    {
        if (!s_work.TryGetValue(job, out Lease? lease) || lease.Id != identity || lease.Action != action) { return; }
        if (progress && !lease.Patient.Dead) { lease.Episode.Advance(); }
        if (lease.Matches) { lease.Map.reservationManager.ReleaseClaimedBy(lease.Actor, job); }
        s_work.Remove(job);
        if (s_actors.TryGetValue(lease.Actor, out Actor? state) && state.Pending == lease) { state.Pending = null; }
    }
    internal static bool Handoff(Pawn actor, Job transport, Job treatment)
    {
        if (!s_work.TryGetValue(transport, out Lease? previous) || !previous.Matches) { return true; }
        if (!Continue(actor, transport)) { return false; }
        Lease next = new(actor, previous.Patient, treatment, previous.Episode);
        s_work.Remove(treatment); s_work.Add(treatment, next);
        State(actor, actor.GetLord()).Pending = next;
        return true;
    }
    private static void CancelPending(Pawn actor, Actor state)
    {
        if (state.Pending is not Lease pending) { return; }
        state.Pending = null;
        if (!pending.Matches || actor.CurJob == pending.Job) { return; }
        NpcMedicalAttempts.Finish(pending.Job, JobCondition.InterruptOptional);
        pending.Map.reservationManager.ReleaseClaimedBy(actor, pending.Job);
        Finish(pending.Job, pending.Id, pending.Action);
    }
    private static void CancelPending(Pawn actor)
    { if (s_actors.TryGetValue(actor, out Actor? state)) { CancelPending(actor, state); } }

    internal static bool BeforeStart(Pawn pawn, ref Job proposed)
    {
        if (s_actors.TryGetValue(pawn, out Actor? state) && state.Pending is Lease pending && proposed != pending.Job
            && (!DepartureJob(proposed) || !InContext(pawn)))
        { CancelPending(pawn, state); }
        if (!DepartureJob(proposed) || !InContext(pawn)) { return true; }
        Result result = Decide(pawn);
        if (result.Kind is Kind.Protected)
        {
            if (pawn.CurJob is Job current && (NpcMedicalAi.Owns(current) || Find.TickManager.TicksGame < State(pawn, pawn.GetLord()).WaitingUntil))
            { pawn.Map?.reservationManager.ReleaseClaimedBy(pawn, proposed); return false; }
            return true;
        }
        if (result.Job is not Job replacement) { return true; }
        pawn.Map?.reservationManager.ReleaseClaimedBy(pawn, proposed);
        proposed = replacement;
        return true;
    }
    internal static bool YieldNative(Pawn actor, Job original, int identity, JobDef action, bool force)
    {
        if (actor.CurJob != original || original.loadID != identity || original.def != action) { return false; }
        if (!DepartureJob(original) || !InContext(actor)) { CancelPending(actor); return true; }
        Result result = Decide(actor, force);
        if (result.Job is null) { return true; }
        // Like the existing extraction handoff, start the prepared replacement
        // directly. A temporary wait retains loot instead of dropping it early.
        actor.jobs.StartJob(result.Job, JobCondition.InterruptOptional, resumeCurJobAfterwards: false,
            keepCarryingThingOverride: result.Kind == Kind.Wait);
        return false;
    }
    internal static bool FinishNative(Pawn actor, Job original, int identity, JobDef action)
    {
        if (actor.CurJob != original || original.loadID != identity || original.def != action) { return false; }
        if (!DepartureJob(original) || !InContext(actor)) { CancelPending(actor); return true; }
        // A finish callback runs inside native cleanup. Prepare ownership here,
        // but let the think node/StartJob guard start it after cleanup returns.
        return Decide(actor, force: true).Job is null;
    }
}
