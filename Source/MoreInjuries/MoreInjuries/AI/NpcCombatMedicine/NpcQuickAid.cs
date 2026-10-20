using System;
using System.Runtime.CompilerServices;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

// Main-thread, transient coordination. No dose history or job/save format changes.
internal static class NpcQuickAid
{
    internal const int BUDGET = 600;
    private const int EPISODE_HOLD = 2500;
    private sealed class Round
    {
        internal Map? Map;
        internal IntVec3 Origin;
        internal int Deadline, HoldUntil;
        internal bool PainUsed, BleedingUsed;
    }
    private sealed class Grant(Pawn actor, Pawn patient, Job job, Round round, Option option)
    {
        internal readonly Pawn Actor = actor, Patient = patient;
        internal readonly int Id = job.loadID, Duration = option.Duration;
        internal readonly float Utility = option.Utility, Cost = option.Cost;
        internal readonly JobDef Action = job.def;
        internal readonly Round Round = round;
        internal int NextCheck, ApplicationEnd = -1;
        internal bool Applied;
        internal bool Matches(Job candidate) => candidate.loadID == Id && candidate.def == Action && candidate.targetA.Thing == Patient;
    }
    internal sealed class Option(Func<Job?> create, JobDef action, Thing? supply, float utility, int duration, NpcPainDose dose = default,
        float cost = -1f)
    {
        internal readonly Func<Job?> Create = create;
        internal readonly JobDef Action = action;
        internal readonly Thing? Supply = supply;
        internal readonly int Duration = duration;
        internal readonly NpcPainDose Dose = dose;
        internal readonly float Cost = cost >= 0f ? cost : supply?.def.BaseMarketValue ?? 0f;
        internal bool Critical;
        internal float Utility = utility, Score, Peak, Exposure, Ticks;
    }
    private static readonly ConditionalWeakTable<Pawn, Round> s_rounds = new();
    private static readonly ConditionalWeakTable<Job, Grant> s_grants = new();

    private static Round? Existing(Pawn patient)
    {
        if (!s_rounds.TryGetValue(patient, out Round? round)) { return null; }
        if (round.Map != patient.MapHeld || Find.TickManager.TicksGame >= round.HoldUntil)
        { s_rounds.Remove(patient); return null; }
        return round;
    }
    private static Round Start(Pawn patient, IntVec3 origin)
    {
        Round? round = Existing(patient);
        if (round is not null) { return round; }
        int now = Find.TickManager.TicksGame;
        round = new() { Map = patient.MapHeld, Origin = origin, Deadline = now + BUDGET, HoldUntil = now + EPISODE_HOLD };
        s_rounds.Add(patient, round);
        return round;
    }
    internal static void Delivered(Pawn actor, Pawn patient, IntVec3 origin)
    {
        if (NpcMedicalAi.Managed(actor) && patient.Downed) { Start(patient, origin); }
    }
    internal static bool SourceNearby(Pawn actor, Pawn patient, IntVec3 cell)
        => cell.InHorDistOf(actor.Position, 12f) || Existing(patient) is Round round
            && round.Map == actor.Map && Find.TickManager.TicksGame < round.Deadline
            && cell.InHorDistOf(round.Origin, 12f);
    internal static IntVec3? SupplyOrigin(Pawn patient) => Existing(patient) is Round round
        && Find.TickManager.TicksGame < round.Deadline ? round.Origin : null;

    private static bool Pain(JobDef action) => action == KnownJobDefOf.MI_NpcPainAssist;
    private static bool Bleeding(JobDef action) => action == KnownJobDefOf.UseHemostaticAgent
        || action == KnownJobDefOf.UseBandage || action == KnownJobDefOf.UseTourniquet;
    private static bool Available(Round? round, JobDef action) => round is null
        || Find.TickManager.TicksGame <= round.Deadline && !(Pain(action) && round.PainUsed || Bleeding(action) && round.BleedingUsed);

    internal static (bool Handled, Job? Job) Decide(Pawn actor, Pawn patient, int mode, bool preview = false)
    {
        if (!patient.Downed && Existing(patient) is not null && NpcMedicalAi.Urgency(patient) < 90f) { return (true, null); }
        if (actor == patient || !patient.Downed || !patient.health.InPainShock && Existing(patient) is null
            || NpcMedicalTriage.Urgency(patient) > 65f) { return (false, null); }
        Round? round = Existing(patient);
        Option? best = null;
        NpcExtraction.Assessment assessment = new(actor, patient, patient.Position);
        bool canHelp = NpcMedicalGeneration.CanDoctor(actor) && NpcMedicalReservations.PatientAvailable(actor, patient)
            && NpcMedicalAi.Relation(actor, patient) >= 0 && !NpcMedicalCompatibility.SafetyOwned(actor);
        if (canHelp)
        {
            foreach (ThingDef def in NpcMedicalDrugs.Analgesics)
            {
                Thing? dose = NpcMedicalAi.FindSupply(actor, patient, def);
                if (dose is null || !Available(round, KnownJobDefOf.MI_NpcPainAssist)
                    || !NpcMedicalAttempts.Ready(actor, patient, KnownJobDefOf.MI_NpcPainAssist, dose))
                {
                    NpcMedicalDecision.Report(actor, patient, mode, "quick dose " + def.defName, "supply unavailable or round/retry deferred");
                    continue;
                }
                if (!NpcPainCare.Evaluate(actor, patient, dose, out NpcPainDose projection, out string reason))
                { NpcMedicalDecision.Report(actor, patient, mode, "quick dose " + def.defName, reason); continue; }
                NpcMedicalDecision.Report(actor, patient, mode, "quick dose " + def.defName,
                    "feasible; recovery=" + projection.Recovery + "; function="
                        + (projection.Function > .000001f ? "improves" : projection.Function < -.000001f ? "declines" : "unchanged"));
                int duration = MedicalTiming.Duration(actor, 40);
                float utility = PainUtility(patient, dose, projection, duration);
                Option option = new(() =>
                {
                    Job job = JobMaker.MakeJob(KnownJobDefOf.MI_NpcPainAssist, patient, dose);
                    job.count = 1; job.endAfterTendedOnce = true; return job;
                }, KnownJobDefOf.MI_NpcPainAssist, dose, utility, duration, projection);
                Consider(option);
            }
            NpcMedicalAi.PreviewQuickBleeding(actor, patient, (create, action, supply, score, duration, retrieval, cost) =>
            {
                if (!Available(round, action) || !NpcMedicalAttempts.Ready(actor, patient, action, supply)) { return; }
                Option bleeding = new(create, action, supply, score * (600f + duration + retrieval + 20f * cost)
                    / (600f + duration + 20f * cost) * 600f, duration, cost: cost)
                { Critical = HealthUtility.TicksUntilDeathDueToBloodLoss(patient) <= BUDGET };
                Consider(bleeding);
            });
        }
        void Consider(Option option)
        {
            if (!Assess(actor, patient, option.Supply, option.Duration, assessment,
                out float ticks, out float peak, out float exposure) || ticks > Remaining(round)) { return; }
            option.Ticks = ticks; option.Peak = peak; option.Exposure = exposure;
            option.Score = option.Utility * (600f + option.Duration + 20f * option.Cost) / (600f + ticks + 20f * option.Cost)
                - Penalty(peak, exposure);
            if (option.Score > 0f && (best is null || Better(option, best))) { best = option; }
        }
        NpcExtraction.Plan? extraction = mode >= 1 ? NpcExtraction.Preview(actor, patient, out _) : null;
        float extractionScore = extraction is null ? 0f : Math.Max(.001f,
            65f * Math.Max(0f, Hazard(extraction.Origin) - Hazard(extraction.Target))
                * 600f / (600f + extraction.Ticks) - TransportPenalty(actor, patient, extraction, assessment));
        if (extraction is not null && (best is null || !best.Critical && extractionScore > best.Score))
        {
            NpcMedicalDecision.Report(actor, patient, mode, "quick aid", "extract before aid");
            return (true, preview ? extraction.CreatePreview() : extraction.Create());
        }
        if (best is null)
        {
            NpcMedicalDecision.Report(actor, patient, mode, "quick aid", canHelp
                ? "defer; unsafe/unavailable dose, route or round budget" : "defer; incapability, reservation or safety owner");
            return (true, null);
        }
        Job? selected = best.Create();
        if (selected is null) { return (true, null); }
        if (!preview) { GrantJob(actor, patient, selected, best); }
        NpcMedicalDecision.Report(actor, patient, mode, "quick aid", "selected " + selected.def.defName);
        return (true, selected);
    }

    private static bool Better(Option next, Option previous)
    {
        if (next.Critical != previous.Critical) { return next.Critical; }
        if (next.Score != previous.Score) { return next.Score > previous.Score; }
        if (next.Peak != previous.Peak) { return next.Peak < previous.Peak; }
        if (next.Ticks != previous.Ticks) { return next.Ticks < previous.Ticks; }
        if (next.Dose.OverdoseRisk != previous.Dose.OverdoseRisk) { return next.Dose.OverdoseRisk < previous.Dose.OverdoseRisk; }
        if (next.Dose.AddictionRisk != previous.Dose.AddictionRisk) { return next.Dose.AddictionRisk < previous.Dose.AddictionRisk; }
        return (next.Supply?.def.BaseMarketValue ?? 0f) < (previous.Supply?.def.BaseMarketValue ?? 0f);
    }
    private static float PainUtility(Pawn patient, Thing dose, NpcPainDose projection, int duration)
    {
        float benefit = Math.Max(0f, (patient.health.hediffSet.PainTotal - projection.Pain)
            / Math.Max(.01f, patient.GetStatValue(StatDefOf.PainShockThreshold)));
        return 600f * EmergencyPriority.Score(65f, benefit, duration, 0, dose.def.BaseMarketValue)
            + (projection.Recovery ? 65f : 0f) + 65f * (projection.Function - benefit)
            - 40f * Math.Max(0f, projection.BleedingFactor - 1f) * patient.health.hediffSet.BleedRateTotal;
    }
    internal static float Hazard(NpcExtractionRisk risk) => Math.Max(risk.Known ? 0f : 1f,
        risk.Worst + .25f * (risk.Total - risk.Worst));
    internal static float Penalty(float peak, float exposure) => 50f * peak + 100f * exposure / BUDGET;
    private static float Remaining(Round? round) => round is null ? BUDGET : round.Deadline - Find.TickManager.TicksGame;

    private static float TransportPenalty(Pawn actor, Pawn patient, NpcExtraction.Plan plan, NpcExtraction.Assessment assessment)
    {
        float peak = 0f, exposure = 0f;
        IntVec3 pickup = patient.Position;
        foreach (bool carrying in new[] { false, true })
        {
            PawnPath path = actor.Map.pathFinder.FindPathNow(carrying ? pickup : actor.Position,
                carrying ? plan.Destination : patient.Position, actor, peMode: carrying ? PathEndMode.OnCell : PathEndMode.Touch);
            try
            {
                if (!path.Found || path.NodesReversed.Count == 0) { return float.PositiveInfinity; }
                float weight = path.TotalCost / Math.Max(1, path.NodesReversed.Count);
                foreach (IntVec3 cell in path.NodesReversed)
                {
                    float risk = Hazard(assessment.At(cell, out _));
                    float patientRisk = carrying ? risk : Hazard(plan.Origin);
                    peak = Math.Max(peak, Math.Max(risk, patientRisk));
                    exposure += (risk + patientRisk) * weight;
                }
                if (!carrying) { pickup = path.NodesReversed[0]; }
            }
            finally { path.ReleaseToPool(); }
        }
        // Preserve local extraction's existing best-effort fallback policy.
        return Penalty(peak, exposure);
    }

    private static bool SafeCell(Pawn actor, Pawn patient, IntVec3 cell, NpcExtraction.Assessment assessment, out float hazard)
    {
        NpcExtractionRisk risk = assessment.At(cell, out _);
        hazard = Hazard(risk);
        return NpcExtractionEnvironment.Boundary(actor, actor, actor.Position, cell)
            && risk.Fire <= 0f && risk.Thermal < 1f && !assessment.ImmediateThreat(cell);
    }
    private static bool Assess(Pawn actor, Pawn patient, Thing? supply, int duration, NpcExtraction.Assessment assessment,
        out float ticks, out float peak, out float exposure)
    {
        ticks = duration; peak = exposure = 0f;
        if (!actor.Spawned || !patient.Spawned || actor.Map != patient.Map || patient.Dead
            || NpcMedicalCompatibility.SafetyOwned(actor) || NpcMedicalSafety.ExplosiveDanger(patient)
            || !SafeCell(actor, patient, patient.Position, assessment, out float patientRisk)) { return false; }
        IntVec3 start = actor.Position;
        bool Route(IntVec3 end, PathEndMode mode, ref float elapsed, ref float maximum, ref float integrated)
        {
            PawnPath path = actor.Map.pathFinder.FindPathNow(start, end, actor, peMode: mode);
            try
            {
                if (!path.Found || path.TotalCost > BUDGET || path.NodesReversed.Count == 0) { return false; }
                float weight = path.TotalCost / Math.Max(1, path.NodesReversed.Count);
                foreach (IntVec3 cell in path.NodesReversed)
                {
                    if (!SafeCell(actor, patient, cell, assessment, out float risk)) { return false; }
                    maximum = Math.Max(maximum, Math.Max(risk, patientRisk));
                    integrated += (risk + patientRisk) * weight;
                }
                elapsed += path.TotalCost;
                start = path.NodesReversed[0];
                return true;
            }
            finally { path.ReleaseToPool(); }
        }
        if (supply is not null && supply.ParentHolder != actor.inventory && supply != actor.carryTracker.CarriedThing
            && !Route(supply.PositionHeld, PathEndMode.Touch, ref ticks, ref peak, ref exposure)) { return false; }
        if (!Route(patient.Position, PathEndMode.Touch, ref ticks, ref peak, ref exposure)
            || !SafeCell(actor, patient, start, assessment, out float applicationRisk)) { return false; }
        peak = Math.Max(peak, Math.Max(applicationRisk, patientRisk));
        exposure += (applicationRisk + patientRisk) * duration;
        return ticks <= BUDGET;
    }
    private static void GrantJob(Pawn actor, Pawn patient, Job job, Option option)
    {
        Round round = Start(patient, patient.Position);
        s_grants.Remove(job);
        s_grants.Add(job, new Grant(actor, patient, job, round, option));
    }
    internal static bool HasGrant(Pawn actor, Pawn patient) => actor.CurJob is Job job
        && s_grants.TryGetValue(job, out Grant? grant) && grant.Matches(job) && grant.Actor == actor && grant.Patient == patient;
    internal static bool Continue(Pawn actor, Pawn patient, Job job, bool final = false)
    {
        if (!s_grants.TryGetValue(job, out Grant? grant) || !grant.Matches(job)) { return true; }
        if (grant.Applied) { return true; }
        if (!NpcMedicalAi.Managed(actor) || !patient.Downed || !NpcMedicalGeneration.CanDoctor(actor)
            || !NpcMedicalReservations.PatientAvailable(actor, patient) || NpcMedicalAi.Relation(actor, patient) < 0
            || NpcMedicalCompatibility.SafetyOwned(actor) || NpcMedicalSafety.ExplosiveDanger(patient)
            || !Available(grant.Round, grant.Action)) { return false; }
        NpcExtraction.Assessment assessment = new(actor, patient, patient.Position);
        if (actor.pather.MovingNow && actor.pather.nextCell.IsValid
            && !SafeCell(actor, patient, actor.pather.nextCell, assessment, out _)) { return false; }
        int now = Find.TickManager.TicksGame;
        if (!final && now < grant.NextCheck) { return true; }
        grant.NextCheck = now + 60;
        int duration = final ? 0 : grant.ApplicationEnd >= 0 ? Math.Max(0, grant.ApplicationEnd - now) : grant.Duration;
        if (!Assess(actor, patient, job.targetB.Thing, duration, assessment, out float ticks, out float peak, out float exposure)
            || ticks > Remaining(grant.Round)) { return false; }
        float utility = grant.Utility;
        if (Pain(grant.Action))
        {
            if (job.targetB.Thing is not Thing dose || !NpcPainCare.Evaluate(actor, patient, dose, out NpcPainDose projection, out _))
            { return false; }
            utility = PainUtility(patient, dose, projection, grant.Duration);
        }
        return utility * (600f + grant.Duration + 20f * grant.Cost) / (600f + ticks + 20f * grant.Cost)
            - Penalty(peak, exposure) > 0f;
    }
    internal static void Applying(Job job, int duration)
    {
        if (s_grants.TryGetValue(job, out Grant? grant) && grant.Matches(job))
        { grant.ApplicationEnd = Find.TickManager.TicksGame + duration; }
    }
    internal static void Applied(Pawn actor, Pawn patient, Job job, bool accepted)
    {
        if (!accepted || !s_grants.TryGetValue(job, out Grant? grant) || !grant.Matches(job)) { return; }
        grant.Applied = true;
        if (Pain(grant.Action)) { grant.Round.PainUsed = true; }
        if (Bleeding(grant.Action)) { grant.Round.BleedingUsed = true; }
    }
    internal static void Finish(Job job, int identity, JobDef action)
    {
        if (s_grants.TryGetValue(job, out Grant? grant) && grant.Id == identity && grant.Action == action)
        { s_grants.Remove(job); }
    }
}
