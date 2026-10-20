using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static partial class NpcExtraction
{
    internal sealed class Assessment
    {
        private readonly Pawn _carrier, _patient;
        private readonly IntVec3 _origin;
        private readonly List<Thing> _threats;
        private readonly Dictionary<IntVec3, (NpcExtractionRisk Risk, bool Allowed)>? _cache;
        internal Assessment(Pawn carrier, Pawn patient, IntVec3 origin, bool evacuation = false)
        {
            _carrier = carrier; _patient = patient; _origin = origin;
            if (evacuation) { _cache = new(); }
            IEnumerable<Thing> candidates = evacuation
                ? carrier.Map.mapPawns.AllPawnsSpawned.Cast<Thing>().Concat(carrier.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                : GenRadial.RadialDistinctThingsAround(origin, carrier.Map, 35f, true);
            IEnumerable<Thing> threats = candidates
                .Where(thing => thing.HostileTo(carrier) && (thing is Pawn enemy && !enemy.Downed && !enemy.Dead
                    || thing is Building_Turret));
            _threats = (evacuation ? threats : threats.Take(16)).ToList();
        }
        internal NpcExtractionRisk At(IntVec3 cell, out bool allowed)
        {
            if (_cache is not null && _cache.TryGetValue(cell, out var cached))
            { allowed = cached.Allowed; return cached.Risk; }
            Map map = _carrier.Map;
            allowed = NpcExtractionEnvironment.Boundary(_carrier, _patient, _origin, cell);
            float thermal = NpcExtractionEnvironment.Thermal(_carrier, _patient, _origin, cell, out bool known, out bool thermalAllowed);
            allowed &= thermalAllowed;
            float combat = NpcExtractionEnvironment.CeDanger(map, cell, out bool ceKnown);
            known &= ceKnown;
            foreach (Thing threat in _threats)
            {
                if (!threat.Spawned || threat.Destroyed) { continue; }
                if (threat.Position.InHorDistOf(cell, 4f)) { combat = Math.Max(combat, 2f); }
                else if (GenSight.LineOfSight(threat.Position, cell, map))
                { combat = Math.Max(combat, 1f - CoverUtility.CalculateOverallBlockChance(cell, threat.Position, map)); }
            }
            float fire = cell.ContainsStaticFire(map) ? 2f
                : GenRadial.RadialCellsAround(cell, 1.5f, false).Any(next => next.InBounds(map) && next.ContainsStaticFire(map)) ? .5f : 0f;
            NpcExtractionRisk result = new(combat, NpcExtractionEnvironment.RecentImpact(map, cell), fire, thermal, known);
            if (_cache is not null) { _cache[cell] = (result, allowed); }
            return result;
        }
        internal bool ImmediateThreat(IntVec3 cell) => _threats.Any(threat => threat.Spawned && !threat.Destroyed
            && threat.Position.InHorDistOf(cell, 8f));
        internal bool Path(IntVec3 start, IntVec3 end, PathEndMode mode, out float ticks, out float exposure, bool carrying = true)
        {
            ticks = exposure = 0f;
            PawnPath path = _carrier.Map.pathFinder.FindPathNow(start, end, _carrier, peMode: mode);
            try
            {
                if (!path.Found || path.TotalCost > NpcExtractionModel.TRAVEL_BUDGET) { return false; }
                ticks = path.TotalCost;
                foreach (IntVec3 cell in path.NodesReversed)
                {
                    // A path may leave a burning origin; never walk into another
                    // burning/forbidden/confinement-violating cell to rescue.
                    NpcExtractionRisk risk = At(cell, out bool allowed);
                    // Before pickup, the casualty stays at its origin. Only the
                    // approaching actor crosses these cells and room boundaries.
                    if (!carrying) { allowed = NpcExtractionEnvironment.Boundary(_carrier, _carrier, _carrier.Position, cell); }
                    if (cell != start && (!allowed || risk.Combat >= 2f)) { return false; }
                    exposure += risk.Worst;
                }
                return true;
            }
            finally { path.ReleaseToPool(); }
        }
    }

}
