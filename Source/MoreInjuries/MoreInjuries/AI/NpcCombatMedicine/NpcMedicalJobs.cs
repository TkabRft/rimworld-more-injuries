using System.Collections.Generic;
using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

public sealed class JobDriver_NpcTend : JobDriver_TendPatient
{
    protected override IEnumerable<Toil> MakeNewToils()
    {
        AddFinishAction(_ => NpcMedicalReservations.RestockUnusedMedicine(pawn, job));
        this.FailOn(() => job.targetA.Pawn is not Pawn patient || patient.Dead
            || pawn != patient && (pawn.HostileTo(patient) || patient.playerSettings?.medCare == MedicalCareCategory.NoCare));
        Thing? medicine = job.targetB.Thing;
        if (medicine?.ParentHolder is Pawn_InventoryTracker holder && holder.pawn != pawn)
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            job.SetTarget(TargetIndex.C, holder.pawn);
            yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.C)
                .FailOn(() => medicine.ParentHolder != holder || holder.pawn.IsForbidden(pawn)
                    || NpcMedicalModel.TravelExpired(Find.TickManager.TicksGame, job.startTick));
            yield return Toils_Haul.TakeFromOtherInventory(medicine, pawn.inventory.innerContainer,
                holder.innerContainer, 1, TargetIndex.B);
        }
        foreach (Toil toil in base.MakeNewToils())
        {
            if (toil.defaultCompleteMode == ToilCompleteMode.PatherArrival)
            { toil.FailOn(() => NpcMedicalModel.TravelExpired(Find.TickManager.TicksGame, job.startTick)); }
            yield return toil;
        }
    }
}

public sealed class JobDriver_NpcEvacuate : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
        => pawn.carryTracker.CarriedThing is null && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
            && pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
    protected override IEnumerable<Toil> MakeNewToils()
    {
        // Carried patients are deliberately despawned. A global despawn check would abort evacuation.
        this.FailOn(() => job.targetA.Pawn is not Pawn patient || patient.Dead || !patient.Downed
            || pawn.HostileTo(patient) || patient.MapHeld != pawn.Map);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        yield return Toils_Haul.StartCarryThing(TargetIndex.A);
        yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
        yield return Toils_General.Do(() =>
        {
            if (pawn.carryTracker.CarriedThing != job.targetA.Thing) { EndJobWith(JobCondition.Incompletable); return; }
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
            && (NpcMedicalGeneration.IsPerformance(__result.def) || __result.def == KnownThingDefOf.Epinephrine))
        { __result = null; }
    }
}
