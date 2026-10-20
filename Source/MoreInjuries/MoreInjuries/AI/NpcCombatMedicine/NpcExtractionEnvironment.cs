using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static class NpcExtractionEnvironment
{
    private sealed class MapState
    {
        internal readonly List<(IntVec3 Cell, int Tick, float Radius)> Impacts = [];
        internal object? Celsius, Danger;
    }
    private static readonly ConditionalWeakTable<Map, MapState> s_maps = new();
    private static readonly HashSet<string> s_warnings = [];
    private static readonly MethodInfo? s_bridge = Method("CelsiusPatchesAI.RescueSafety", "CelsiusPatchesAI", "TryGetExtractionRisk",
        typeof(bool), typeof(Pawn), typeof(Pawn), typeof(IntVec3), typeof(IntVec3), typeof(float).MakeByRefType(), typeof(bool).MakeByRefType());
    private static readonly MethodInfo? s_celsius = Method("Celsius.TemperatureInfo", "Celsius", "GetTemperatureForCell", typeof(float), typeof(int));
    private static readonly MethodInfo? s_danger = Method("CombatExtended.DangerTracker", "CombatExtended", "DangerAt", typeof(float), typeof(IntVec3));

    private static MethodInfo? Method(string name, string assembly, string method, Type result, params Type[] args)
    {
        Type? type = AccessTools.TypeByName(name);
        if (type?.Assembly.GetName().Name != assembly) { return null; }
        MethodInfo? info = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance, null, args, null);
        return info?.DeclaringType == type && info.ReturnType == result ? info : null;
    }
    private static void Warn(string key, string reason)
    {
        if (s_warnings.Add(key)) { Log.Warning("More Injuries: extraction " + key + " unavailable: " + reason); }
    }

    internal static void Impact(Pawn pawn, bool explosive)
    {
        if (pawn.Map is not Map map) { return; }
        List<(IntVec3 Cell, int Tick, float Radius)> list = s_maps.GetOrCreateValue(map).Impacts;
        int now = Find.TickManager.TicksGame;
        list.RemoveAll(impact => now - impact.Tick >= 600);
        if (list.Any(impact => impact.Tick == now && impact.Cell == pawn.Position)) { return; }
        if (list.Count >= 64) { list.RemoveAt(0); }
        list.Add((pawn.Position, now, explosive ? 6f : 3f));
    }

    internal static float RecentImpact(Map map, IntVec3 cell)
    {
        int now = Find.TickManager.TicksGame;
        List<(IntVec3 Cell, int Tick, float Radius)> impacts = s_maps.GetOrCreateValue(map).Impacts;
        impacts.RemoveAll(impact => now - impact.Tick >= 600);
        float risk = 0f;
        foreach (var impact in impacts)
        {
            float proximity = Math.Max(0f, 1f - cell.DistanceTo(impact.Cell) / impact.Radius);
            risk = Math.Max(risk, proximity * (1f - (now - impact.Tick) / 600f));
        }
        return risk;
    }

    internal static bool Boundary(Pawn carrier, Pawn patient, IntVec3 origin, IntVec3 cell)
    {
        Map map = carrier.Map;
        if (!cell.InBounds(map) || cell.Fogged(map) || !cell.Standable(map) || cell.IsForbidden(carrier)
            || cell.ContainsStaticFire(map) || cell.GetEdifice(map) is Building_TrapExplosive) { return false; }
        if (cell.GetEdifice(map) is Building_Door door && !door.FreePassage && !door.PawnCanOpen(carrier)) { return false; }
        if (TraverseParms.For(carrier).fenceBlocked && cell.GetEdifice(map)?.def.building?.isFence == true) { return false; }
        if (carrier.playerSettings?.EffectiveAreaRestrictionInPawnCurrentMap is Area area && !area[cell]) { return false; }
        if (carrier.IsPrisoner && !NpcMedicalAi.PrisonBreak(carrier) && cell.GetRoom(map) != carrier.GetRoom()) { return false; }
        if (patient.IsPrisoner && !NpcMedicalAi.PrisonBreak(patient) && origin.GetRoom(map) != cell.GetRoom(map)) { return false; }
        return true;
    }

    internal static float Thermal(Pawn carrier, Pawn patient, IntVec3 origin, IntVec3 cell, out bool known, out bool allowed)
    {
        known = false; allowed = true;
        if (ModsConfig.IsActive("mrpiromaniak.celsiuspatchesai") && s_bridge is not null)
        {
            try
            {
                object[] args = [carrier, patient, origin, cell, 0f, false];
                if (s_bridge.Invoke(null, args) is true && args[4] is float risk && NpcExtractionModel.Finite(risk))
                { known = true; allowed = (bool)args[5]; return risk; }
            }
            catch (Exception exception) { Warn("Celsius companion", exception.Message); }
        }
        else if (ModsConfig.IsActive("mrpiromaniak.celsiuspatchesai")) { Warn("Celsius companion", "unsupported read-only risk signature"); }
        float temperature;
        if (ModsConfig.IsActive("Garwel.Celsius"))
        {
            try
            {
                MapState state = s_maps.GetOrCreateValue(carrier.Map);
                state.Celsius ??= carrier.Map.components.FirstOrDefault(comp => s_celsius?.DeclaringType?.IsInstanceOfType(comp) == true);
                if (s_celsius is null || state.Celsius is null) { Warn("Celsius", "unsupported temperature API"); return 1f; }
                temperature = (float)s_celsius.Invoke(state.Celsius, [carrier.Map.cellIndices.CellToIndex(cell)]);
            }
            catch (Exception exception) { Warn("Celsius", exception.Message); return 1f; }
        }
        else { temperature = GenTemperature.GetTemperatureForCell(cell, carrier.Map); }
        FloatRange doctor = carrier.SafeTemperatureRange(), casualty = patient.SafeTemperatureRange();
        float value = Math.Max(NpcExtractionModel.Thermal(temperature, doctor.min, doctor.max),
            NpcExtractionModel.Thermal(temperature, casualty.min, casualty.max));
        float Vulnerability(Pawn pawn) => 1f + 3f * Math.Max(NpcMedicalAi.Severity(pawn, "Heatstroke"),
            Math.Max(NpcMedicalAi.Severity(pawn, "Hypothermia"), NpcMedicalAi.Severity(pawn, "HypothermicSlowdown")));
        value *= Math.Max(Vulnerability(carrier), Vulnerability(patient));
        // Celsius alone can modify weather-dependent tolerances. Without its
        // companion bridge, a destination estimate is deliberately uncertain.
        known = NpcExtractionModel.Finite(value) && !ModsConfig.IsActive("Garwel.Celsius");
        return NpcExtractionModel.Finite(value) ? value : 1f;
    }

    internal static float CeDanger(Map map, IntVec3 cell, out bool known)
    {
        known = !ModsConfig.IsActive("CETeam.CombatExtended");
        if (!ModsConfig.IsActive("CETeam.CombatExtended")) { return 0f; }
        try
        {
            MapState state = s_maps.GetOrCreateValue(map);
            state.Danger ??= map.components.FirstOrDefault(comp => s_danger?.DeclaringType?.IsInstanceOfType(comp) == true);
            if (s_danger is null || state.Danger is null) { Warn("CE danger", "unsupported danger tracker"); return 0f; }
            float value = (float)s_danger.Invoke(state.Danger, [cell]);
            known = NpcExtractionModel.Finite(value);
            return known ? Math.Max(0f, value) : 0f;
        }
        catch (Exception exception) { Warn("CE danger", exception.Message); return 0f; }
    }
}
