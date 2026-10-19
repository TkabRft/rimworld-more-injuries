using System.Collections.Generic;
using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

public sealed class JobDriver_NpcTend : JobDriver_TendPatient
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
        => (pawn.carryTracker.CarriedThing is null || pawn.carryTracker.CarriedThing == job.targetB.Thing)
            && pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed)
            && (job.targetB.Thing is not Thing medicine || pawn.Reserve(medicine, job, 10, 1, errorOnFailed: errorOnFailed));

    protected override IEnumerable<Toil> MakeNewToils()
    {
        AddFinishAction(_ => NpcMedicalReservations.RestockUnusedMedicine(pawn, job));
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOn(() => job.targetA.Pawn is not Pawn patient || patient.Dead
            || patient.IsMutant && !patient.mutant.Def.entitledToMedicalCare
            || !NpcMedicalGeneration.CanDoctor(pawn) || !MedicalTiming.CanWork(pawn)
            || pawn != patient && pawn.HostileTo(patient)
            || !(pawn == patient && pawn.IsPrisoner) && (patient.playerSettings?.medCare == MedicalCareCategory.NoCare
                || job.targetB.Thing is Thing item && patient.playerSettings is { } settings && !settings.medCare.AllowsMedicine(item.def)));
        AddEndCondition(() => job.targetA.Pawn?.health.HasHediffsNeedingTend() == true ? JobCondition.Ongoing : JobCondition.Succeeded);
        Thing? medicine = job.targetB.Thing;
        if (medicine is not null)
        {
            if (medicine.ParentHolder is Pawn_InventoryTracker holder)
            {
                if (holder.pawn != pawn)
                {
                    job.SetTarget(TargetIndex.C, holder.pawn);
                    yield return Travel(TargetIndex.C, PathEndMode.Touch)
                        .FailOn(() => medicine.ParentHolder != holder || holder.pawn.IsForbidden(pawn));
                }
                // Transfer one reserved dose; never drop an inventory stack to make it reachable.
                yield return Toils_General.Do(() =>
                {
                    if (holder.pawn.IsForbidden(pawn) || holder.pawn != pawn && !pawn.CanReachImmediate(holder.pawn, PathEndMode.Touch)
                        || !NpcMedicalReservations.TakeMedicine(pawn, job, medicine, holder.innerContainer))
                    { EndJobWith(JobCondition.Incompletable); }
                });
            }
            else if (medicine != pawn.carryTracker.CarriedThing)
            {
                yield return Travel(TargetIndex.B, PathEndMode.ClosestTouch);
                yield return Toils_MedicalDevice.PickupDevice(TargetIndex.B, job.targetA.Pawn, _ => 1);
            }
            yield return Toils_General.Do(() =>
            {
                if (job.targetB.Thing is not Thing selected || selected.Destroyed
                    || pawn.carryTracker.CarriedThing != selected || !selected.def.IsMedicine)
                { EndJobWith(JobCondition.Incompletable); }
            });
        }
        Pawn patientPawn = job.targetA.Pawn;
        if (patientPawn != pawn) { yield return Travel(TargetIndex.A, PathEndMode.Touch); }
        int ticks = MedicalTiming.Duration(pawn, 600);
        Toil wait = Toils_General.Wait(ticks).WithProgressBarToilDelay(TargetIndex.A)
            .PlaySustainerOrSound(SoundDefOf.Interact_Tend);
        wait.activeSkill = () => SkillDefOf.Medicine;
        if (patientPawn != pawn)
        {
            Job? ownedWait = null;
            wait.AddPreInitAction(() =>
            {
                if (patientPawn.IsColonistPlayerControlled || patientPawn.CurJob?.playerForced == true) { return; }
                Job? previous = patientPawn.CurJob;
                PawnUtility.ForceWait(patientPawn, ticks, maintainPosture: true);
                if (patientPawn.CurJob != previous && (patientPawn.CurJobDef == JobDefOf.Wait
                    || patientPawn.CurJobDef == JobDefOf.Wait_MaintainPosture)) { ownedWait = patientPawn.CurJob; }
            });
            wait.AddFinishAction(() =>
            {
                if (ownedWait is not null && patientPawn.CurJob == ownedWait)
                { patientPawn.jobs.EndCurrentJob(JobCondition.InterruptForced); }
            });
            wait.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
        }
        yield return wait;
        // Preserve vanilla tending quality, medicine consumption, experience and health callbacks.
        yield return Toils_Tend.FinalizeTend(patientPawn);
    }

    private Toil Travel(TargetIndex target, PathEndMode end)
        => Toils_Goto.GotoThing(target, end).FailOnDespawnedNullOrForbidden(target)
            .FailOn(() => NpcMedicalModel.TravelExpired(Find.TickManager.TicksGame, job.startTick));
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
