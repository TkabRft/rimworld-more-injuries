using System;
using System.Linq;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static class NpcRestorativeCare
{
    internal static float Priority(Pawn patient) => !NpcMedicalDrugs.Restoratives.Any() ? 0f
        : patient.health.hediffSet.BleedRateTotal > 0f ? 30f
        : patient.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(injury => injury.CanHealNaturally()) ? 10f : 0f;

    internal static bool Evaluate(Pawn actor, Pawn patient, Thing dose, out NpcPainDose projection, out string reason)
        => NpcPainCare.EvaluateClinical(actor, patient, dose, out projection, out reason, NpcDrugPurpose.Restorative);

    internal static Job? Select(Pawn actor, Pawn patient, bool pocketOnly = false, Func<JobDef, Thing?, bool>? allowed = null)
    {
        if (Priority(patient) <= 0f || actor.carryTracker.CarriedThing is not null && !NpcDeparture.ProspectiveHands(actor)) { return null; }
        Thing? best = null;
        NpcPainDose bestDose = default;
        JobDef action = actor == patient ? KnownJobDefOf.MI_NpcRestorativeSelf : KnownJobDefOf.MI_NpcRestorativeAssist;
        foreach (ThingDef def in NpcMedicalDrugs.Restoratives)
        {
            Thing? item = pocketOnly ? actor.inventory?.innerContainer.FirstOrDefault(thing => thing.def == def)
                : NpcMedicalAi.FindSupply(actor, patient, def);
            if (item is null || allowed?.Invoke(action, item) == false
                || !Evaluate(actor, patient, item, out NpcPainDose dose, out _)) { continue; }
            if (best is null || Better(dose, item, bestDose, best))
            { best = item; bestDose = dose; }
        }
        if (best is null) { return null; }
        Job job = actor == patient ? JobMaker.MakeJob(action, best) : JobMaker.MakeJob(action, patient, best);
        job.count = 1;
        job.endAfterTendedOnce = true;
        return job;
    }

    private static bool Better(NpcPainDose next, Thing item, NpcPainDose previous, Thing best)
    {
        if (next.Recovery != previous.Recovery) { return next.Recovery; }
        if (next.Function != previous.Function) { return next.Function > previous.Function; }
        if (next.Benefit != previous.Benefit) { return next.Benefit > previous.Benefit; }
        if (next.OverdoseRisk != previous.OverdoseRisk) { return next.OverdoseRisk < previous.OverdoseRisk; }
        return item.def.BaseMarketValue < best.def.BaseMarketValue;
    }

    internal static bool FinalCheck(Pawn actor, Pawn patient, Thing dose) =>
        (actor.CurJob is not Job active || NpcDeparture.Continue(actor, active))
        && (dose.ParentHolder == actor.inventory || dose == actor.carryTracker.CarriedThing
            || dose.Spawned && actor.CanReachImmediate(dose, PathEndMode.Touch))
        && (actor == patient || actor.CanReachImmediate(patient, PathEndMode.Touch) && NpcMedicalAi.Relation(actor, patient) >= 0)
        && Evaluate(actor, patient, dose, out _, out _);
}
