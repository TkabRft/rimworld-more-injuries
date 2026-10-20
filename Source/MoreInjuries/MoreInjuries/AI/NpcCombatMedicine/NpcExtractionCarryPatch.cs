using System;
using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
internal static class NpcExtractionCarryPatch
{
    // Departure admission plus the existing transport/safety ownership guard.
    private static bool Prefix(Pawn ___pawn, ref Job newJob, ref bool resumeCurJobAfterwards,
        ref bool? keepCarryingThingOverride, out Pawn? __state)
    {
        __state = null;
        Job original = newJob;
        if (!NpcDeparture.BeforeStart(___pawn, ref newJob)) { return false; }
        if (newJob != original)
        {
            resumeCurJobAfterwards = false;
            keepCarryingThingOverride = newJob.def == RimWorld.JobDefOf.Wait_Combat;
        }
        if (!NpcMedicalAi.ActorEligible(___pawn) || ___pawn.Downed || !NpcExtraction.Carrier(___pawn)
            || ___pawn.carryTracker.CarriedThing is not Pawn patient || NpcExtraction.Active(___pawn) is not Job extraction
            || extraction.playerForced || extraction.targetA.Thing != patient || !RecognizedSafety(newJob)
            || !NpcExtraction.CanCarryThroughSafety(___pawn, patient, extraction, newJob)) { return true; }
        keepCarryingThingOverride = true;
        // Do not repeatedly queue cover jobs on top of the original mission.
        if (___pawn.CurJob?.def == KnownJobDefOf.MI_NpcExtract)
        {
            resumeCurJobAfterwards = true;
            NpcExtraction.Suspension(___pawn, true);
            __state = ___pawn;
        }
        return true;
    }
    internal static bool RecognizedSafety(Job? job) => job?.def.driverClass is Type driver
        && (driver.Assembly.GetName().Name == "CombatExtended" && job.def.defName is "RunForCover" or "HunkerDown"
            || driver.Assembly.GetName().Name == "CelsiusPatchesAI" && job.def.defName == "CPAI_SeekThermalShelter");
    private static Exception? Finalizer(Exception? __exception, Pawn? __state)
    {
        if (__state is not null) { NpcExtraction.Suspension(__state, false); }
        return __exception;
    }
}
