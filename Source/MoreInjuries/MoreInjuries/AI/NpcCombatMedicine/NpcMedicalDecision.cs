using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

// This is the live priority sequence; tests replace only its native service boundaries.
internal static class NpcMedicalDecision
{
    private sealed class Diagnostics { internal readonly Dictionary<string, string> Reasons = []; }
    private static readonly ConditionalWeakTable<Pawn, ConditionalWeakTable<Pawn, Diagnostics>> s_diagnostics = new();

    internal static void Report(Pawn actor, Pawn patient, int mode, string stage, string reason)
    {
        if (!Logger.IsEnabled || !Prefs.DevMode) { return; }
        Dictionary<string, string> states = s_diagnostics.GetOrCreateValue(actor).GetOrCreateValue(patient).Reasons;
        string state = mode + ":" + reason;
        if (states.TryGetValue(stage, out string? previous) && previous == state) { return; }
        states[stage] = state;
        Logger.Log($"More Injuries NPC decision: {actor.ThingID}->{patient.ThingID}; mode={mode}; stage={stage}; {reason}; urgency={NpcMedicalAi.Urgency(patient)}; canDoctor={NpcMedicalGeneration.CanDoctor(actor)}.");
    }

    internal static Job? Choose(Pawn actor, int mode, IReadOnlyList<Pawn> patients,
        Func<Pawn, (Job? Job, bool Unsafe)> extraction, Func<Pawn, Job?> immediate,
        Func<Pawn, Job?> care, Func<Pawn, Job?> evacuation, Func<Job?> withdrawal,
        Func<Pawn, (bool Handled, Job? Job)>? quick = null)
    {
        foreach (Pawn patient in patients)
        {
            if (quick is not null)
            {
                (bool handled, Job? firstAid) = quick(patient);
                if (firstAid is not null) { return firstAid; }
                if (handled) { continue; }
            }
            bool unsafePatient = false;
            if (mode >= 1)
            {
                (Job? transport, bool danger) = extraction(patient);
                unsafePatient = danger;
                if (transport is not null)
                { Report(actor, patient, mode, "care", "local extraction/stabilization=" + transport.def.defName); return transport; }
            }
            Job? treatment = unsafePatient ? immediate(patient) : care(patient);
            Report(actor, patient, mode, "care", treatment is not null ? "treatment=" + treatment.def.defName
                : unsafePatient ? "unsafe position; no immediate lifesaving care" : "no feasible permitted treatment");
            if (treatment is not null) { return treatment; }
        }
        if (mode < 2) { return null; }
        // Complete the care pass for every patient before committing an exit carry.
        foreach (Pawn patient in patients)
        {
            Job? transport = evacuation(patient);
            if (transport is not null)
            { Report(actor, patient, mode, "evacuation", "selected=" + transport.def.defName); return transport; }
        }
        return withdrawal();
    }
}
