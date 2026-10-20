using MoreInjuries.Defs.WellKnown;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

// Pathfinding/thermal/placement boundaries for the real transport driver and
// Harmony carry guard. No claim that these simulate Unity or serialization.
internal static class NpcExtraction
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Pawn, State> s_state = new();
    private sealed class State { internal bool Suspended; }
    internal static bool Carrier(Pawn pawn) => pawn.CarrierCapable && !pawn.Downed;
    internal static bool PatientAllowed(Pawn pawn, Pawn patient) => !patient.Dead && patient.Downed && !pawn.HostileTo(patient);
    internal static bool DestinationValid(Pawn pawn, Job job) => pawn.DestinationValid && PatientAllowed(pawn, job.targetA.Pawn);
    internal static void CommitCarry(Pawn carrier, Job job) { }
    internal static bool StepAllowed(Pawn carrier, Job job) => carrier.RouteValid;
    internal static bool PickupValid(Pawn pawn, Job job) => pawn.RouteValid && DestinationValid(pawn, job) && job.targetA.Pawn.Position == job.targetC.Cell;
    internal static bool CanCarryThroughSafety(Pawn carrier, Pawn patient, Job extraction, Job safety) => carrier.RouteValid && carrier.DestinationValid;
    internal static bool Suspending(Pawn pawn) => s_state.GetOrCreateValue(pawn).Suspended;
    internal static void Suspension(Pawn pawn, bool value) => s_state.GetOrCreateValue(pawn).Suspended = value;
    internal static Job? Active(Pawn pawn) => pawn.CurJob.def == KnownJobDefOf.MI_NpcExtract ? pawn.CurJob
        : pawn.jobs.jobQueue.FirstOrDefault(queued => queued.job.def == KnownJobDefOf.MI_NpcExtract)?.job;
    internal sealed class Assessment(Pawn pawn, Pawn patient, IntVec3 origin)
    { internal bool Path(IntVec3 start, IntVec3 end, PathEndMode mode, out float ticks, out float exposure) { ticks = 100; exposure = 0; return pawn.RouteValid; } }
    internal static void Arrived(Pawn pawn, Job job) { NpcMedicalAttempts.Progress(job); }
    internal static bool Cleared(Pawn pawn, Pawn patient) => false;
}
internal static class NpcExtractionEnvironment { internal static void Impact(Pawn pawn, bool explosive) { } }
internal static partial class NpcMedicalAi { internal static Func<Pawn, Pawn, Job?>? OnHandoff; internal static Job? Handoff(Pawn pawn, Pawn patient) => pawn.DoctorCapable ? OnHandoff?.Invoke(pawn, patient) : null; }
