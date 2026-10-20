using MoreInjuries.AI.NpcCombatMedicine;
using Verse.AI;

namespace Verse
{
    public readonly struct Rot4 { public static Rot4 Invalid => new(); }
    public sealed partial class Map
    {
        public IntVec3 Size = new(100, 0, 100);
        public readonly MapPawns mapPawns = new();
        public readonly PathFinder pathFinder = new();
        public readonly ListerThings listerThings = new();
        public Func<IntVec3, IntVec3, bool>? Sight;
        internal readonly Dictionary<IntVec3, NpcExtractionRisk> Risks = [];
        public readonly HashSet<IntVec3> Blocked = [];
    }
    public sealed class MapPawns { public readonly List<Pawn> AllPawnsSpawned = []; }
    public enum ThingRequestGroup { BuildingArtificial, Medicine }
    public sealed class ListerThings
    { public readonly List<Thing> Buildings = []; public List<Thing> ThingsInGroup(ThingRequestGroup group) => Buildings; }
    public static class GenRadial
    {
        public static IEnumerable<Thing> RadialDistinctThingsAround(IntVec3 origin, Map map, float radius, bool center)
            => map.mapPawns.AllPawnsSpawned.Cast<Thing>().Concat(map.listerThings.Buildings).Where(thing => thing.Position.InHorDistOf(origin, radius));
        public static IEnumerable<IntVec3> RadialCellsAround(IntVec3 origin, float radius, bool center)
        {
            for (int x = -1; x <= 1; x++)
            { for (int z = -1; z <= 1; z++) { if (x != 0 || z != 0 || center) { yield return new(origin.x + x, 0, origin.z + z); } } }
        }
    }
    public static class GenSight
    { public static bool LineOfSight(IntVec3 from, IntVec3 to, Map map) => map.Sight?.Invoke(from, to) ?? true; }
    public static class SafetyCellExtensions
    {
        public static bool ContainsStaticFire(this IntVec3 cell, Map map) => map.Risks.TryGetValue(cell, out var risk) && risk.Fire > 0f;
        public static bool HostileTo(this Thing thing, Pawn pawn) => thing is Pawn enemy ? enemy.HostileTo(pawn) : thing is RimWorld.Building_Turret turret && turret.Hostile;
    }
    public sealed class PathFinder
    {
        public Func<IntVec3, IntVec3, PathEndMode, PawnPath>? Find;
        public int Released;
        public PawnPath FindPathNow(IntVec3 start, IntVec3 end, Pawn pawn, PathEndMode peMode)
        {
            PawnPath path = Find?.Invoke(start, end, peMode) ?? new() { NodesReversed = [end, start], TotalCost = start.DistanceToSquared(end) };
            path.Found &= pawn.RouteValid;
            path.OnRelease = () => Released++;
            return path;
        }
    }
}
namespace RimWorld
{
    public sealed class Building_Turret : Verse.Thing { public bool Hostile = true; }
    public static class CoverUtility
    { public static float CalculateOverallBlockChance(Verse.IntVec3 cell, Verse.IntVec3 enemy, Verse.Map map) => 0f; }
}
namespace Verse.AI
{
    public class JobDriver_Goto : JobDriver
    {
        public Toil FinishExitToil()
        {
            Toil toil = Toils_General.Do(() => { });
            toil.AddFinishAction(() => pawn.ExitMap(false, Verse.Rot4.Invalid));
            return toil;
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            if (job.exitMapOnArrival) { yield return Toils_General.Do(() => pawn.ExitMap(false, Verse.Rot4.Invalid)); }
        }
    }
    public sealed class Pawn_MindState { public Verse.Thing? knownExploder; public PawnDuty? duty; }
    public sealed class PawnPathFollower { public bool MovingNow; public Verse.IntVec3 nextCell = Verse.IntVec3.Invalid; }
    public sealed class PawnPath
    {
        public bool Found = true;
        public float TotalCost;
        public List<Verse.IntVec3> NodesReversed = [];
        public Action? OnRelease;
        public void ReleaseToPool() => OnRelease?.Invoke();
    }
}
