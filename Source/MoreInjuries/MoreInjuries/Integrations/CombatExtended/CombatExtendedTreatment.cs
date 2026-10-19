using HarmonyLib;
using MoreInjuries.HealthConditions.HeavyBleeding.Overrides;
using RimWorld;
using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoreInjuries.Integrations.CombatExtended;

internal static partial class CombatExtendedBridge
{
    private static bool s_treatmentResolved;
    private static Func<Hediff, bool>? s_canStabilize;
    private static Func<float>? s_searchRadiusSquared;
    private static JobDef? s_stabilizeJob;

    internal static bool CanStabilize(Hediff wound) => s_canStabilize?.Invoke(wound) == true;

    private static void BindTreatment()
    {
        s_treatmentResolved = true;
        try
        {
            Type utility = AccessTools.TypeByName("CombatExtended.CE_Utility") ?? throw new MissingMemberException("CE_Utility");
            s_canStabilize = AccessTools.MethodDelegate<Func<Hediff, bool>>(AccessTools.Method(utility, "CanBeStabilized", [typeof(Hediff)]));
            Type controller = AccessTools.TypeByName("CombatExtended.Controller") ?? throw new MissingMemberException("Controller");
            object settings = AccessTools.Field(controller, "settings").GetValue(null) ?? throw new MissingMemberException("settings");
            PropertyInfo radius = settings.GetType().GetProperty("MedicineSearchRadiusSquared") ?? throw new MissingMemberException("MedicineSearchRadiusSquared");
            s_searchRadiusSquared = Expression.Lambda<Func<float>>(Expression.Property(Expression.Constant(settings), radius)).Compile();
            JobDef job = DefDatabase<JobDef>.GetNamedSilentFail("Stabilize");
            if (job?.driverClass?.FullName != "CombatExtended.JobDriver_Stabilize") { throw new MissingMemberException("Stabilize job"); }
            s_stabilizeJob = job;
        }
        catch (Exception exception)
        {
            s_canStabilize = null;
            Log.Warning("More Injuries: CE stabilization is unavailable to automatic emergency care. " + exception.Message);
        }
    }

    internal static Job? TryCreateStabilizationJob(Pawn doctor, Pawn patient, Func<Thing?>? boundedMedicine = null, bool ignoreCare = false)
    {
        VerifyRegistration();
        if (!s_active || !s_available || (!ignoreCare && patient.playerSettings?.medCare is MedicalCareCategory.NoCare or MedicalCareCategory.NoMeds)) { return null; }
        if (!s_treatmentResolved) { BindTreatment(); }
        if (s_canStabilize is null || s_stabilizeJob is null) { return null; }
        bool needed = false;
        foreach (Hediff wound in patient.health.hediffSet.GetHediffsTendable())
        {
            if (wound is IStatefulInjury injury && (injury.State.IsTemporarilyCoagulated || injury.State.CoagulationMultiplier < 1f)) { continue; }
            if (s_canStabilize(wound)) { needed = true; break; }
        }
        if (!needed) { return null; }
        MedicalCareCategory care = ignoreCare ? MedicalCareCategory.Best : patient.playerSettings?.medCare ?? MedicalCareCategory.Best;
        bool Allowed(Thing thing) => thing is Medicine && !thing.Destroyed && thing.stackCount > 0
            && care.AllowsMedicine(thing.def) && !thing.IsForbidden(doctor)
            && doctor.CanReserve(thing, stackCount: 1);
        Medicine? best = null;
        float potency = -1f;
        void Consider(Thing? thing)
        {
            if (thing is not Medicine medicine || !Allowed(medicine)) { return; }
            float candidate = medicine.GetStatValue(StatDefOf.MedicalPotency);
            if (candidate > potency) { best = medicine; potency = candidate; }
        }
        if (boundedMedicine is not null) { Consider(boundedMedicine()); }
        else
        {
            Consider(doctor.carryTracker?.CarriedThing);
            if (doctor.inventory is { } carried) { foreach (Thing thing in carried.innerContainer) { Consider(thing); } }
            if (patient.inventory is { } personal) { foreach (Thing thing in personal.innerContainer) { Consider(thing); } }
        }
        if (boundedMedicine is null && best is null && doctor.Map is { } map && patient.MapHeld == map)
        {
            float radius = Mathf.Sqrt(Mathf.Max(0f, s_searchRadiusSquared!()));
            Thing? Nearby(IntVec3 center) => GenClosest.ClosestThingReachable(center, map,
                ThingRequest.ForGroup(ThingRequestGroup.Medicine), PathEndMode.ClosestTouch, TraverseParms.For(doctor),
                maxDistance: radius, validator: Allowed, searchRegionsMin: 0, searchRegionsMax: 12, forceAllowGlobalSearch: false);
            best = (Nearby(patient.PositionHeld) ?? Nearby(doctor.PositionHeld)) as Medicine;
        }
        if (best is null) { return null; }
        Job result = JobMaker.MakeJob(s_stabilizeJob, patient, best);
        result.count = 1;
        return result;
    }
}
