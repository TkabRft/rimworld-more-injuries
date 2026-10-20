using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Defs.WellKnown;
using Verse;
using Verse.AI;

namespace Verse
{
    public sealed partial class Pawn
    {
        public Verse.AI.Group.Lord? Lord;
        public bool Drafted, Mental;
        public Verse.AI.Group.Lord? GetLord() => Lord;
    }
}
namespace Verse.AI
{
    public sealed class DutyDef { public string defName = "fixture-duty"; }
    public sealed class PawnDuty { public DutyDef def = new(); }
}
namespace Verse.AI.Group
{
    public class LordJob;
    public class LordToil;
    public class LordToil_ExitMap : LordToil;
    public sealed class Lord
    {
        public LordJob LordJob = new RimWorld.LordJob_AssaultColony();
        public LordToil CurLordToil = new RimWorld.LordToil_StealCover();
        public readonly List<Pawn> ownedPawns = [];
        public List<string>? questTags;
    }
}
namespace RimWorld
{
    public sealed class LordJob_AssaultColony : Verse.AI.Group.LordJob;
    public sealed class LordJob_Siege : Verse.AI.Group.LordJob;
    public sealed class LordToil_StealCover : Verse.AI.Group.LordToil;
    public sealed class LordToil_ExitMapAndDefendSelf : Verse.AI.Group.LordToil;
    public sealed class LordToil_PanicFlee : Verse.AI.Group.LordToil;
    public static partial class JobDefOf
    {
        public static readonly JobDef Steal = new() { defName = "Steal" }, Goto = new() { defName = "Goto" },
            Wait_Combat = new() { defName = "Wait_Combat" };
    }
    public class JobDriver_TakeAndExitMap : JobDriver
    {
        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_General.Do(() => pawn.carryTracker.TryStartCarry(job.targetA.Thing, 1));
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            yield return Toils_General.Do(() => pawn.ExitMap(false, Rot4.Invalid));
        }
    }
}
namespace MoreInjuries.AI.NpcCombatMedicine
{
    internal static partial class NpcMedicalAi
    {
        internal static bool CanTakeDecision(Pawn pawn) => Managed(pawn) && pawn.Spawned && !pawn.Downed
            && !pawn.Drafted && !pawn.Mental && pawn.CurJob?.playerForced != true;
        internal static bool ShouldWithdraw(Pawn pawn) => Urgency(pawn) >= 70
            || pawn.health.capacities.GetLevel(RimWorld.PawnCapacityDefOf.Moving) < .5f;
        internal static bool Owns(Job? job) => job is not null && (job.def == KnownJobDefOf.MI_NpcPainAssist
            || job.def == KnownJobDefOf.MI_NpcRestorativeAssist || job.def == KnownJobDefOf.MI_NpcTend
            || job.def == KnownJobDefOf.MI_NpcExtract || job.def == KnownJobDefOf.MI_NpcEvacuate);
        internal static Func<Pawn, Pawn, bool, Job?>? OnDepartureCare;
        internal static Job? DepartureCare(Pawn actor, Pawn patient, bool preview)
        {
            if (OnDepartureCare is not null) { return OnDepartureCare(actor, patient, preview); }
            (bool handled, Job? job) = NpcQuickAid.Decide(actor, patient, 2, preview);
            return handled ? job : NpcPainCare.Select(actor, patient,
                allowed: (action, supply) => NpcMedicalAttempts.Allowed(actor, patient, action, supply));
        }
    }
}
