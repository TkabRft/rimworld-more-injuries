using System.Collections.Generic;
using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

public sealed class JobDriver_NpcEvacuate : JobDriver
{
    private int _nextSafetyCheck;
    private bool _safe;
    private bool Safe()
    {
        if (job.targetA.Pawn is not Pawn patient || !NpcEvacuation.Eligible(pawn, patient)
            || !NpcEvacuation.StepAllowed(pawn, job)) { return false; }
        int now = Find.TickManager.TicksGame;
        if (now >= _nextSafetyCheck)
        {
            _nextSafetyCheck = now + 60;
            _safe = NpcEvacuation.RouteValid(pawn, job);
        }
        return _safe;
    }
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        bool reserved = NpcEvacuation.RouteValid(pawn, job) && NpcMedicalReservations.PatientAvailable(pawn, job.targetA.Pawn)
            && pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        if (!reserved) { pawn.Map?.reservationManager.ReleaseClaimedBy(pawn, job); }
        return reserved;
    }
    protected override IEnumerable<Toil> MakeNewToils()
    {
        Pawn patient = job.targetA.Pawn;
        AddFinishAction(condition =>
        {
            if (pawn.Spawned && pawn.carryTracker.CarriedThing == patient)
            { pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _); }
        });
        this.FailOn(() => !Safe());
        // Keep the original four toil indices, including carried-patient resumes.
        Toil approach = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        var startApproach = approach.initAction;
        approach.initAction = () =>
        {
            if (pawn.carryTracker.CarriedThing == patient) { ReadyForNextToil(); }
            else { startApproach(); }
        };
        yield return approach;
        Toil pickup = Toils_Haul.StartCarryThing(TargetIndex.A);
        var startPickup = pickup.initAction;
        pickup.initAction = () =>
        {
            if (pawn.carryTracker.CarriedThing == patient) { return; }
            if (!NpcEvacuation.RouteValid(pawn, job) || !NpcMedicalReservations.PatientAvailable(pawn, patient)
                || !pawn.CanReachImmediate(patient, PathEndMode.Touch))
            { EndJobWith(JobCondition.Incompletable); return; }
            job.count = 1;
            startPickup();
        };
        yield return pickup;
        yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
        yield return Toils_General.Do(() =>
        {
            if (pawn.carryTracker.CarriedThing != patient || pawn.Position != job.targetB.Cell
                || !NpcEvacuation.RouteValid(pawn, job)) { EndJobWith(JobCondition.Incompletable); return; }
            pawn.ExitMap(false, Rot4.Invalid);
        });
    }
}

[HarmonyPatch(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.FindCombatEnhancingDrug))]
internal static class NpcMedicalVanillaDrugPatch
{
    private static void Postfix(Pawn ___pawn, ref Thing? __result)
    {
        if (__result is not null && NpcMedicalAi.Managed(___pawn)
            && (NpcMedicalGeneration.IsPerformance(__result.def) || NpcMedicalDrugs.Restorative(__result.def) || __result.def == KnownThingDefOf.Epinephrine))
        { __result = null; }
    }
}
