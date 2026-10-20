using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.Drugs.Epinephrine;
using MoreInjuries.HealthConditions.Fractures;
using MoreInjuries.HealthConditions.HeavyBleeding.Transfusions;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal sealed class NpcMedicalState
{
    internal bool Medic;
    internal int LastDecision = -1000;
    internal Job? NativeMedicalAttempt;
    internal int ThreatTick = -1000;
    internal readonly List<Pawn> Threats = [];
    internal readonly NpcDrugBudget Drugs = new();
}

internal static class NpcMedicalAi
{
    private static readonly ConditionalWeakTable<Pawn, NpcMedicalState> s_states = new();
    internal static NpcMedicalState State(Pawn pawn) => s_states.GetOrCreateValue(pawn);
    internal static bool Managed(Pawn pawn) => MoreInjuriesMod.Settings.EnableNpcCombatMedicine && ActorEligible(pawn);
    internal static bool ActorEligible(Pawn pawn) => pawn.RaceProps.Humanlike && pawn.RaceProps.IsFlesh && !pawn.Dead
        && !pawn.IsColonistPlayerControlled && !pawn.IsSlaveOfColony
        && (pawn.IsPrisoner || pawn.Faction is { IsPlayer: false }
            && (pawn.kindDef.isFighter || pawn.equipment?.Primary?.def.IsWeapon == true));

    internal static bool Owns(Job? job) => NpcMedicalCompatibility.IsStabilization(job) || job is not null && (job.def == KnownJobDefOf.MI_NpcTend
        || job.def == KnownJobDefOf.MI_NpcReposition || job.def == KnownJobDefOf.MI_NpcWithdraw
        || job.def == KnownJobDefOf.MI_NpcExtract || job.def == KnownJobDefOf.MI_NpcEvacuate || job.def == KnownJobDefOf.MI_NpcPerformanceDrug
        || job.def == KnownJobDefOf.MI_NpcPainSelf || job.def == KnownJobDefOf.MI_NpcPainAssist)
        || job?.def.driverClass is Type driver && typeof(JobDriver_TendPatient).IsAssignableFrom(driver)
        || job?.def.driverClass?.Namespace?.StartsWith("MoreInjuries.HealthConditions", StringComparison.Ordinal) == true;

    internal static bool CanTakeDecision(Pawn pawn) => Managed(pawn) && pawn.Spawned && !pawn.Downed
        && !pawn.Drafted && (!pawn.InMentalState || PrisonBreak(pawn)) && pawn.CurJob?.playerForced != true;

    internal static bool PrisonBreak(Pawn pawn) => pawn.IsPrisoner
        && pawn.GetLord()?.LordJob?.GetType().Name.IndexOf("PrisonBreak", StringComparison.Ordinal) >= 0;
    internal static bool OwnsSurvival(Pawn pawn) => NpcMedicalModel.Survival(MoreInjuriesMod.Settings.EnableNpcCombatMedicine,
        MoreInjuriesMod.Settings.NpcMedicalAiMode, Managed(pawn), CanTakeDecision(pawn), PreservedDuty(pawn), pawn.IsPrisoner, PrisonBreak(pawn));

    internal static bool IsMedic(Pawn pawn) => State(pawn).Medic || pawn.inventory.innerContainer.Any(thing =>
        thing.def == KnownThingDefOf.Defibrillator || thing.def == KnownThingDefOf.SuctionDevice
        || thing.def == KnownThingDefOf.Bandage && thing.stackCount >= 6);

    internal static float Severity(Pawn pawn, string defName) => pawn.health.hediffSet.hediffs
        .FirstOrDefault(hediff => hediff.def.defName == defName)?.Severity ?? 0f;
    internal static float Urgency(Pawn patient) => Mathf.Max(NpcMedicalTriage.Urgency(patient), NpcPainCare.Priority(patient));

    internal static int Relation(Pawn doctor, Pawn patient)
    {
        MoreInjuriesSettings settings = MoreInjuriesMod.Settings;
        if (patient.Dead || !patient.RaceProps.Humanlike || !patient.RaceProps.IsFlesh || doctor.HostileTo(patient)
            || patient.IsMutant && !patient.mutant.Def.entitledToMedicalCare) { return -1; }
        int Rank(int relation) => NpcMedicalModel.CareRank(relation,
            NpcPainCare.Priority(patient) > 0f && patient.health.InPainShock ? Mathf.Max(70f, Urgency(patient)) : Urgency(patient), settings.NpcTreatOwnFaction,
            settings.NpcTreatFriendly, settings.NpcTreatNeutral, settings.NpcTreatPlayer);
        if (patient == doctor) { return Rank(0); }
        if (patient.IsColonistPlayerControlled)
        { return patient.playerSettings?.medCare is not MedicalCareCategory.NoCare
            && (patient.Downed || patient.CurJob?.playerForced != true) ? Rank(4) : -1; }
        if (patient.Faction == doctor.Faction || patient.GetLord() is { } group && group == doctor.GetLord())
        { return Rank(1); }
        if (doctor.Faction is null || patient.Faction is null) { return Rank(3); }
        return doctor.Faction.RelationKindWith(patient.Faction) == FactionRelationKind.Ally
            ? Rank(2) : Rank(3);
    }

    internal static Job? Decide(Pawn doctor)
    {
        ObserveNativeAttempt(doctor);
        if (!CanTakeDecision(doctor) || Owns(doctor.CurJob) || PreservedDuty(doctor) || NpcMedicalCompatibility.SafetyOwned(doctor)) { return null; }
        NpcMedicalState state = State(doctor);
        int now = Find.TickManager.TicksGame;
        if (now - state.LastDecision < 150) { return null; }
        state.LastDecision = now;
        bool threatened = Threats(doctor).Any(enemy => GenSight.LineOfSight(enemy.Position, doctor.Position, doctor.Map));
        if (threatened) { state.Drugs.Threat(now); }
        if (NpcExtraction.Resume(doctor) is Job pending) { return pending; }
        bool canDoctor = NpcMedicalGeneration.CanDoctor(doctor);
        // Self-ingestion does not require doctoring. Other care still does.
        {
            float radius = IsMedic(doctor) ? 24f : 12f;
            List<Pawn> patients = Nearby(doctor, radius).Where(patient => Relation(doctor, patient) >= 0 && (Urgency(patient) > 0f || NpcExtraction.Enabled && patient.Downed))
                .OrderByDescending(Urgency).ThenBy(patient => Relation(doctor, patient))
                .ThenBy(patient => patient.Position.DistanceToSquared(doctor.Position)).ToList();
            foreach (Pawn patient in patients)
            {
                Job? extraction = NpcExtraction.Consider(doctor, patient, out bool unsafePatient);
                if (extraction is not null) { return extraction; }
                if (unsafePatient)
                {
                    if (canDoctor && Urgency(patient) >= 90f && ImmediateStabilization(doctor, patient) is Job lifesaving)
                    { return lifesaving; }
                    continue;
                }
                if (patient != doctor && !canDoctor || !NpcMedicalReservations.PatientAvailable(doctor, patient)
                    || patient != doctor && !doctor.CanReach(patient, PathEndMode.Touch, Danger.Deadly)) { continue; }
                Job? treatment = canDoctor && NpcMedicalTriage.Urgency(patient) >= NpcPainCare.Priority(patient)
                    ? Treatment(doctor, patient) : null;
                treatment ??= NpcPainCare.Select(doctor, patient, pocketOnly: patient == doctor && (threatened || NpcMedicalSafety.Recent(doctor)),
                    allowed: (action, supply) => NpcMedicalAttempts.Allowed(doctor, patient, action, supply));
                treatment ??= canDoctor ? Treatment(doctor, patient) : null;
                if (treatment is null || !NpcMedicalAttempts.Allowed(doctor, patient, treatment)) { continue; }
                if (threatened && Exposure(doctor, doctor.Position) > 0)
                {
                    // A dose already in one's pocket needs no exposed retrieval or
                    // medical procedure; native ingestion still owns interruptions.
                    if (treatment.def == KnownJobDefOf.MI_NpcPainSelf
                        && treatment.targetA.Thing.ParentHolder == doctor.inventory) { return treatment; }
                    if (MoreInjuriesMod.Settings.NpcMedicalAiMode >= 1 && patient == doctor && SaferPosition(doctor) is IntVec3 cell)
                    { return Move(KnownJobDefOf.MI_NpcReposition, cell); }
                    if (Urgency(patient) < 90f || !patient.Position.InHorDistOf(doctor.Position, 1.5f)
                        || Threats(doctor).Any(enemy => enemy.Position.InHorDistOf(doctor.Position, 8f))) { continue; }
                }
                return Commit(doctor, patient, treatment);
            }
        }
        if (OwnsSurvival(doctor))
        {
            Job? withdrawal = Withdrawal(doctor, ShouldWithdraw(doctor));
            if (withdrawal is not null) { return withdrawal; }
        }
        return !NpcMedicalSafety.Recent(doctor) && threatened && (!doctor.IsPrisoner || PrisonBreak(doctor)) ? Performance(doctor, state) : null;
    }

    internal static Job? Handoff(Pawn doctor, Pawn patient)
    {
        if (!Managed(doctor) || !NpcMedicalGeneration.CanDoctor(doctor) || Relation(doctor, patient) < 0
            || !NpcMedicalReservations.PatientAvailable(doctor, patient)) { return null; }
        Job? care = Treatment(doctor, patient) ?? NpcPainCare.Select(doctor, patient,
            allowed: (action, supply) => NpcMedicalAttempts.Allowed(doctor, patient, action, supply));
        return care is not null && NpcMedicalAttempts.Allowed(doctor, patient, care) ? Commit(doctor, patient, care) : null;
    }

    internal static Job? ImmediateStabilization(Pawn doctor, Pawn patient)
    {
        if (!patient.Position.InHorDistOf(doctor.Position, 1.5f)
            || Threats(doctor).Any(enemy => enemy.Position.InHorDistOf(doctor.Position, 8f))) { return null; }
        return Treatment(doctor, patient, immediateOnly: true);
    }

    private static Job Commit(Pawn doctor, Pawn patient, Job treatment)
    {
        if (NpcMedicalCompatibility.IsStabilization(treatment))
        {
            State(doctor).NativeMedicalAttempt = treatment;
            NpcMedicalAttempts.Begin(doctor, treatment, patient, treatment.targetB.Thing);
            // CE owns its native toils. Observe the bounded attempt from selection
            // to return of control, without patching or replacing its driver.
            NpcMedicalAttempts.Applying(treatment);
        }
        return treatment;
    }

    private static void ObserveNativeAttempt(Pawn doctor)
    {
        NpcMedicalState state = State(doctor);
        if (state.NativeMedicalAttempt is not Job previous || doctor.CurJob == previous) { return; }
        state.NativeMedicalAttempt = null;
        NpcMedicalAttempts.Finish(previous, JobCondition.InterruptOptional);
    }

    private static bool PreservedDuty(Pawn pawn)
    {
        string? job = pawn.CurJob?.def.defName;
        if (job is "Kidnap" or "CarryDownedPawn" or "EnterTransporter" or "EnterTransporterAndWait" or "EnterShuttle") { return true; }
        if (pawn.CurJob?.exitMapOnArrival == true) { return true; }
        // Quest scripts and rituals retain ownership of their actors.
        string? lord = pawn.GetLord()?.LordJob?.GetType().Name;
        return lord is not null && (lord.Contains("Ritual") || lord.Contains("Ceremony") || lord.Contains("Bestowing"));
    }

    private static Job? Treatment(Pawn doctor, Pawn patient, bool immediateOnly = false)
    {
        if (patient.playerSettings?.medCare == MedicalCareCategory.NoCare && !(doctor == patient && doctor.IsPrisoner)) { return null; }
        TechLevel tech = NpcMedicalGeneration.Profile(doctor).TechLevel;
        NpcDrugBudget budget = State(patient).Drugs;
        Dictionary<ThingDef, Thing?> supplies = new();
        Thing? FindDevice(ThingDef def)
        {
            if (!supplies.TryGetValue(def, out Thing? item)) { supplies[def] = item = FindSupply(doctor, patient, def); }
            return item;
        }
        EmergencyCare.NpcContext context = new()
        {
            Basic = tech >= TechLevel.Medieval, Advanced = tech >= TechLevel.Industrial,
            Emergency = tech >= TechLevel.Industrial, Cpr = tech >= TechLevel.Medieval,
            ClinicalAllowed = budget.ClinicalAllowed(Find.TickManager.TicksGame, MoreInjuriesMod.Settings.NpcClinicalInterval),
            IgnoreCare = doctor == patient && doctor.IsPrisoner,
            Medicine = () => FindMedicine(doctor, patient),
            Find = FindDevice,
            Allowed = (action, supply) => immediateOnly ? ImmediateAllowed(doctor, patient, action, supply)
                : NpcMedicalAttempts.Allowed(doctor, patient, action, supply)
        };
        // A carried simple scavenged treatment may be used below its normal supply tier.
        context.Basic |= FindDevice(KnownThingDefOf.Tourniquet) is not null;
        context.Advanced |= FindDevice(KnownThingDefOf.HemostaticAgent) is not null;
        Job? job = EmergencyCare.CreateJob(doctor, patient, aftercare: false, context);
        if (job is not null) { return job; }
        if (immediateOnly) { return null; }
        if (!Threats(doctor).Any() && EmergencyCare.CreateSafeTourniquetRemoval(doctor, patient) is Job removal
            && NpcMedicalAttempts.Allowed(doctor, patient, removal))
        { removal.endAfterTendedOnce = true; return removal; }
        if (Urgency(patient) < 70f && !Threats(doctor).Any())
        {
            if (patient.health.hediffSet.HasHediff(KnownHediffDefOf.Fracture) && FindSupply(doctor, patient, KnownThingDefOf.Splint) is Thing splint)
            {
                Job splinting = JobDriver_UseSplint.GetDispatcher(doctor, patient, splint).CreateJob();
                splinting.endAfterTendedOnce = true;
                if (NpcMedicalAttempts.Allowed(doctor, patient, splinting)) { return splinting; }
            }
        }
        if (patient.health.HasHediffsNeedingTend() && (!Threats(doctor).Any() || patient.health.hediffSet.BleedRateTotal > .5f))
        {
            Thing? medicine = FindMedicine(doctor, patient);
            Job tend = JobMaker.MakeJob(KnownJobDefOf.MI_NpcTend, patient, medicine);
            tend.count = medicine is null ? 0 : 1;
            tend.endAfterTendedOnce = true;
            if (NpcMedicalAttempts.Allowed(doctor, patient, tend)) { return tend; }
        }
        return null;
    }

    private static bool ImmediateAllowed(Pawn doctor, Pawn patient, JobDef action, Thing? supply)
    {
        int duration = action == KnownJobDefOf.PerformCpr ? 360 : 600;
        bool clinical = action == KnownJobDefOf.PerformCpr || action == KnownJobDefOf.UseDefibrillator
            || action == KnownJobDefOf.UseSuctionDevice || action == KnownJobDefOf.UseEpinephrine
            || action == KnownJobDefOf.UseTourniquet || action == KnownJobDefOf.UseHemostaticAgent || action == KnownJobDefOf.UseBandage;
        return clinical && MedicalTiming.Duration(doctor, duration) <= 600
            && (supply is null || supply.ParentHolder == doctor.inventory || supply.PositionHeld.InHorDistOf(doctor.Position, 1.5f))
            && !NpcMedicalCompatibility.SafetyOwned(doctor)
            && NpcMedicalAttempts.Ready(doctor, patient, action, supply);
    }

    internal static Thing? FindMedicine(Pawn doctor, Pawn patient)
    {
        MedicalCareCategory care = doctor == patient && doctor.IsPrisoner ? MedicalCareCategory.Best
            : patient.playerSettings?.medCare ?? MedicalCareCategory.Best;
        if (care is MedicalCareCategory.NoCare or MedicalCareCategory.NoMeds) { return null; }
        foreach (ThingDef def in new[] { ThingDefOf.MedicineUltratech, ThingDefOf.MedicineIndustrial, ThingDefOf.MedicineHerbal })
        { if (care.AllowsMedicine(def) && FindSupply(doctor, patient, def) is Thing item) { return item; } }
        return null;
    }

    internal static Thing? FindSupply(Pawn doctor, Pawn patient, ThingDef def)
    {
        bool Allowed(Thing item) => item.def == def && !item.Destroyed && item.stackCount > 0 && !item.IsForbidden(doctor)
            && doctor.CanReserve(item, 10, stackCount: 1)
            && NpcMedicalReservations.DeliveryAllowed(doctor, patient, item);
        Thing? Own(Pawn holder) => holder.inventory?.innerContainer.FirstOrDefault(Allowed);
        Thing? result = Own(doctor) ?? Own(patient);
        if (result is not null) { return result; }
        foreach (Pawn holder in Nearby(doctor, 12f))
        {
            if (holder != doctor && holder != patient && holder.Faction == doctor.Faction && !holder.HostileTo(doctor)
                && holder.Position.InHorDistOf(doctor.Position, 12f) && !holder.IsForbidden(doctor)
                && doctor.CanReach(holder, PathEndMode.Touch, Danger.Some) && Own(holder) is Thing supply) { return supply; }
        }
        return GenClosest.ClosestThingReachable(doctor.Position, doctor.Map, ThingRequest.ForDef(def),
            PathEndMode.ClosestTouch, TraverseParms.For(doctor, Danger.Some), maxDistance: 12f, validator: Allowed,
            searchRegionsMin: 0, searchRegionsMax: 12, forceAllowGlobalSearch: false);
    }

    private static IEnumerable<Pawn> Nearby(Pawn pawn, float radius)
        => GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, radius, true).OfType<Pawn>();
    private static IEnumerable<Pawn> Threats(Pawn pawn)
    {
        NpcMedicalState state = State(pawn);
        int now = Find.TickManager.TicksGame;
        if (now - state.ThreatTick >= 150)
        {
            state.ThreatTick = now;
            state.Threats.Clear();
            state.Threats.AddRange(Nearby(pawn, 35f).Where(enemy => !enemy.Dead && !enemy.Downed && enemy.HostileTo(pawn)));
        }
        return state.Threats.Where(enemy => enemy.Spawned && !enemy.Dead && !enemy.Downed && enemy.HostileTo(pawn));
    }
    private static int Exposure(Pawn pawn, IntVec3 cell) => Threats(pawn).Count(enemy => GenSight.LineOfSight(enemy.Position, cell, pawn.Map)
        && CoverUtility.CalculateOverallBlockChance(cell, enemy.Position, pawn.Map) < .5f);
    private static IntVec3? SaferPosition(Pawn pawn)
    {
        int exposure = Exposure(pawn, pawn.Position);
        IntVec3? best = null;
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, 12f, false))
        {
            if (!cell.InBounds(pawn.Map) || !cell.Standable(pawn.Map) || cell.IsForbidden(pawn)
                || cell.DistanceTo(pawn.Position) * pawn.TicksPerMoveCardinal > 600f
                || !pawn.CanReserveAndReach(cell, PathEndMode.OnCell, Danger.Some)) { continue; }
            int next = Exposure(pawn, cell);
            if (next < exposure) { exposure = next; best = cell; if (next == 0) { break; } }
        }
        if (best is IntVec3 destination)
        {
            PawnPath path = pawn.Map.pathFinder.FindPathNow(pawn.Position, destination, pawn, peMode: PathEndMode.OnCell);
            try { if (!path.Found || path.TotalCost > NpcMedicalModel.REPOSITION_TIMEOUT) { return null; } }
            finally { path.ReleaseToPool(); }
        }
        return best;
    }
    private static Job Move(JobDef def, IntVec3 cell)
    {
        Job job = JobMaker.MakeJob(def, cell);
        if (def == KnownJobDefOf.MI_NpcReposition)
        {
            job.expiryInterval = NpcMedicalModel.REPOSITION_TIMEOUT;
            job.checkOverrideOnExpire = false;
        }
        return job;
    }
    private static bool ShouldWithdraw(Pawn pawn) => Urgency(pawn) >= 70f
        || pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving) < .5f
        || pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) < .5f;

    private static Job? Withdrawal(Pawn pawn, bool mustWithdraw)
    {
        bool canEvacuate = !NpcMedicalSafety.Recent(pawn) && pawn.carryTracker.CarriedThing is null
            && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
            && pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving) >= .5f;
        Pawn? patient = !canEvacuate ? null : Nearby(pawn, 12f).Where(other => other != pawn && other.Downed && !other.Dead
            && other.Faction == pawn.Faction && !other.HostileTo(pawn) && other.Position.InHorDistOf(pawn.Position, 12f)
            && pawn.carryTracker.AvailableStackSpace(other.def) > 0
            && !NpcMedicalSafety.Recent(other) && !NpcMedicalCompatibility.SafetyOwned(other)
            && pawn.CanReserveAndReach(other, PathEndMode.Touch, Danger.Some)).OrderByDescending(Urgency).FirstOrDefault();
        if (!mustWithdraw && patient is null) { return null; }
        if (!CellFinder.TryFindRandomEdgeCellWith(cell => cell.Standable(pawn.Map)
            && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some), pawn.Map, CellFinder.EdgeRoadChance_Neutral, out IntVec3 exit)) { return null; }
        if (patient is not null && Exposure(pawn, patient.Position) == 0)
        { return JobMaker.MakeJob(KnownJobDefOf.MI_NpcEvacuate, patient, exit); }
        if (!mustWithdraw) { return null; }
        Job job = Move(KnownJobDefOf.MI_NpcWithdraw, exit);
        job.exitMapOnArrival = true;
        return job;
    }

    private static Job? Performance(Pawn pawn, NpcMedicalState state)
    {
        MoreInjuriesSettings settings = MoreInjuriesMod.Settings;
        int now = Find.TickManager.TicksGame;
        if (!settings.NpcPerformanceDrugs || !state.Drugs.PerformanceAllowed(now, settings.NpcPerformanceMaximum, settings.NpcPerformanceInterval)) { return null; }
        if (!state.Drugs.OpportunityRolled)
        {
            state.Drugs.OpportunityRolled = true;
            state.Drugs.OpportunityAllowed = Rand.Chance(settings.NpcPerformanceUseChance);
            NpcMedicalMemory.Remember(pawn);
        }
        if (!state.Drugs.OpportunityAllowed) { return null; }
        Thing? item = pawn.inventory.innerContainer.Where(thing => NpcMedicalGeneration.IsPerformance(thing.def))
            .OrderByDescending(thing => thing.def.BaseMarketValue).FirstOrDefault(thing => SafePerformance(pawn, thing));
        if (item is null) { return null; }
        Job job = JobMaker.MakeJob(KnownJobDefOf.MI_NpcPerformanceDrug, item);
        job.count = 1;
        return job;
    }

    internal static bool SafePerformance(Pawn pawn, Thing item)
    {
        if (item.Destroyed || item.stackCount < 1 || item.IsForbidden(pawn) || pawn.IsTeetotaler()
            || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || Urgency(pawn) >= 70f
            || Severity(pawn, "DrugOverdose") >= .15f || Severity(pawn, "GoJuiceHigh") > 0f || Severity(pawn, "YayoHigh") > 0f
            || Severity(pawn, "MorphineBuildup") >= .04f || Severity(pawn, "KetamineBuildup") > 0f
            || Severity(pawn, "ChloroformBuildup") > 0f || Severity(pawn, "AlcoholHigh") > 0f) { return false; }
        if (item.def == KnownThingDefOf.Morphine)
        {
            return pawn.health.hediffSet.PainTotal >= .25f && Severity(pawn, "BloodLoss") < .2f
                && Severity(pawn, "HypovolemicShock") <= 0f
                && pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) >= .8f
                && pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing) >= .9f
                && pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping) >= .9f;
        }
        return true;
    }
}

public sealed class JobGiver_NpcCombatMedicine : ThinkNode_JobGiver
{
    protected override Job? TryGiveJob(Pawn pawn) => NpcMedicalAi.Decide(pawn);
}
