using MoreInjuries.Defs.WellKnown;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

// Pathfinding/thermal/placement boundaries for the real transport driver and
// Harmony carry guard. No claim that these simulate Unity or serialization.
internal static partial class NpcExtraction
{
    internal static bool Enabled => MoreInjuriesMod.Settings.EnableNpcCombatMedicine && MoreInjuriesMod.Settings.NpcMedicalAiMode >= 1;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Pawn, State> s_state = new();
    private sealed class State { internal bool Suspended; }
    internal static bool Carrier(Pawn pawn) => pawn.CarrierCapable && !pawn.Downed;
    internal static bool PatientAllowed(Pawn pawn, Pawn patient) => !patient.Dead && patient.Downed && !pawn.HostileTo(patient) && patient.MapHeld == pawn.Map;
    internal static bool DestinationValid(Pawn pawn, Job job) => pawn.DestinationValid && PatientAllowed(pawn, job.targetA.Pawn);
    internal static void CommitCarry(Pawn carrier, Job job) { }
    internal static bool StepAllowed(Pawn carrier, Job job) => carrier.RouteValid;
    internal static bool PickupValid(Pawn pawn, Job job) => Enabled && pawn.RouteValid && DestinationValid(pawn, job) && job.targetA.Pawn.Position == job.targetC.Cell;
    internal static bool CanCarryThroughSafety(Pawn carrier, Pawn patient, Job extraction, Job safety) => carrier.RouteValid && carrier.DestinationValid;
    internal static bool Suspending(Pawn pawn) => s_state.GetOrCreateValue(pawn).Suspended;
    internal static void Suspension(Pawn pawn, bool value) => s_state.GetOrCreateValue(pawn).Suspended = value;
    internal static Job? Active(Pawn pawn) => pawn.CurJob.def == KnownJobDefOf.MI_NpcExtract ? pawn.CurJob
        : pawn.jobs.jobQueue.FirstOrDefault(queued => queued.job.def == KnownJobDefOf.MI_NpcExtract)?.job;
    internal static void Arrived(Pawn pawn, Job job) { NpcMedicalAttempts.Progress(job); NpcQuickAid.Delivered(pawn, job.targetA.Pawn, job.targetC.Cell); }
    internal static bool Cleared(Pawn pawn, Pawn patient) => false;
}
internal static class NpcExtractionEnvironment
{
    internal static void Impact(Pawn pawn, bool explosive) { }
    internal static float RecentImpact(Map map, IntVec3 cell) => map.Risks.TryGetValue(cell, out var risk) ? risk.Impact : 0f;
    internal static float CeDanger(Map map, IntVec3 cell, out bool known)
    { known = !map.Risks.TryGetValue(cell, out var risk) || risk.Known; return risk.Combat; }
    internal static float Thermal(Pawn carrier, Pawn patient, IntVec3 origin, IntVec3 cell, out bool known, out bool allowed)
    { known = !carrier.Map.Risks.TryGetValue(cell, out var risk) || risk.Known; allowed = !carrier.Map.Blocked.Contains(cell); return risk.Thermal; }
    internal static bool Boundary(Pawn carrier, Pawn patient, IntVec3 origin, IntVec3 cell)
        => !carrier.Map.Blocked.Contains(cell) && (!carrier.Map.Risks.TryGetValue(cell, out var risk) || risk.Fire == 0f);
}
internal static partial class NpcMedicalAi { internal static Func<Pawn, Pawn, Job?>? OnHandoff; internal static Job? Handoff(Pawn pawn, Pawn patient) => pawn.DoctorCapable ? OnHandoff?.Invoke(pawn, patient) : null; }
