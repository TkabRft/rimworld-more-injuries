using MoreInjuries.AI;
using Verse;
using Verse.AI;

namespace Verse
{
    public partial class Thing
    {
        public float MedicinePotency = 1f;
        public float GetStatValue(object stat) => MedicinePotency;
    }
    public sealed partial class Pawn
    {
        public bool WorkTypeIsDisabled(object work) => !DoctorCapable;
    }
}
namespace MoreInjuries.Extensions
{
    public static class FirstAidPawnExtensions
    {
        public static bool IsActivelyHostileTo(this Pawn pawn, Pawn other) => !pawn.Downed && pawn.HostileTo(other);
    }
}
namespace Verse.AI
{
    public partial class Pawn_JobTracker
    {
        public bool AcceptOrderedJobs = true;
        public bool TryTakeOrderedJob(Job job, bool requestQueueing = false)
        {
            if (!AcceptOrderedJobs) { return false; }
            job.playerForced = true;
            if (requestQueueing) { jobQueue.Add(new() { job = job }); }
            else { curJob = job; }
            return true;
        }
    }
    public static class FirstAidDriverExtensions
    {
        public static void FailOnAggroMentalState(this JobDriver driver, TargetIndex target) { }
    }
}
namespace RimWorld
{
    public static class WorkTypeDefOf { public static readonly object Doctor = new(); }
    public static partial class JobDefOf { public static readonly JobDef TendPatient = new() { defName = "TendPatient" }; }
}
namespace MoreInjuries.AI
{
    // The emergency selector is source-linked separately in RuntimeContracts;
    // this boundary drives the real dispatcher and field-tending lifecycle.
    internal static class EmergencyCare
    {
        internal static Func<Pawn, Pawn, Job?>? Emergency;
        internal static Job? CreateJob(Pawn doctor, Pawn patient, bool aftercare)
            => Emergency?.Invoke(doctor, patient) ?? (patient.Downed && patient.health.hediffSet.hediffs.Any(wound => wound.TendableNow())
                ? JobMaker.MakeJob(RimWorld.JobDefOf.TendPatient, patient) : null);
    }
}
