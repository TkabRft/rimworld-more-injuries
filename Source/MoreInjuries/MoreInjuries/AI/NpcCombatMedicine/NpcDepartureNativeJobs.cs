using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static class NpcDepartureNativeJobs
{
    // No inserted/reordered toils or changed saved indices. Evaluate lazily so
    // native saved-job initialization retains its original execution order.
    internal static IEnumerable<Toil> Guard(JobDriver driver, IEnumerable<Toil> original)
    {
        Job job = driver.job;
        int identity = job.loadID;
        JobDef action = job.def;
        Pawn actor = driver.pawn;
        foreach (Toil toil in original)
        {
            if (NpcDeparture.DepartureJob(job))
            {
                Action? initialize = toil.initAction, tick = toil.tickAction;
                Action<int>? interval = toil.tickIntervalAction;
                toil.initAction = () =>
                {
                    if (NpcDeparture.YieldNative(actor, job, identity, action, force: true)) { initialize?.Invoke(); }
                };
                toil.tickAction = () =>
                {
                    if (NpcDeparture.YieldNative(actor, job, identity, action, force: false)) { tick?.Invoke(); }
                };
                if (interval is not null) { toil.tickIntervalAction = ticks =>
                {
                    if (NpcDeparture.YieldNative(actor, job, identity, action, force: false)) { interval(ticks); }
                }; }
                Wrap(toil.preInitActions, force: true);
                Wrap(toil.preTickActions, force: false);
                if (toil.preTickIntervalActions is { } intervals)
                {
                    for (int index = 0; index < intervals.Count; index++)
                    {
                        Action<int> previous = intervals[index];
                        intervals[index] = ticks =>
                        {
                            if (NpcDeparture.YieldNative(actor, job, identity, action, force: false)) { previous(ticks); }
                        };
                    }
                }
                if (toil.finishActions is { } finishes)
                {
                    for (int index = 0; index < finishes.Count; index++)
                    {
                        Action previous = finishes[index];
                        // Guard driver-owned arrival/exit callbacks. Keep native
                        // toil cleanup and other mods' finish callbacks intact.
                        if (!DriverCallback(previous)) { continue; }
                        finishes[index] = () =>
                        {
                            if (NpcDeparture.FinishNative(actor, job, identity, action)) { previous(); }
                        };
                    }
                }
            }
            yield return toil;
        }

        void Wrap(List<Action>? actions, bool force)
        {
            if (actions is null) { return; }
            for (int index = 0; index < actions.Count; index++)
            {
                Action previous = actions[index];
                actions[index] = () =>
                {
                    if (NpcDeparture.YieldNative(actor, job, identity, action, force)) { previous(); }
                };
            }
        }
    }
    private static bool DriverCallback(Action callback)
    {
        for (Type? type = callback.Method.DeclaringType; type is not null; type = type.DeclaringType)
        { if (type == typeof(JobDriver_Goto) || type == typeof(JobDriver_TakeAndExitMap)) { return true; } }
        return false;
    }
}

[HarmonyPatch(typeof(JobDriver_TakeAndExitMap), "MakeNewToils")]
internal static class NpcDepartureTheftPatch
{
    private static void Postfix(JobDriver_TakeAndExitMap __instance, ref IEnumerable<Toil> __result)
        => __result = NpcDepartureNativeJobs.Guard(__instance, __result);
}

[HarmonyPatch(typeof(JobDriver_Goto), "MakeNewToils")]
internal static class NpcDepartureGotoPatch
{
    private static void Postfix(JobDriver_Goto __instance, ref IEnumerable<Toil> __result)
        => __result = NpcDepartureNativeJobs.Guard(__instance, __result);
}
