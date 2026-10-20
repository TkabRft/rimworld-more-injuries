using System;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal readonly record struct NpcExtractionRisk(float Combat, float Impact, float Fire, float Thermal, bool Known = true)
{
    internal float Worst => Math.Max(Math.Max(Combat, Impact), Math.Max(Fire, Thermal));
    internal float Total => Combat + Impact + Fire + Thermal;
    internal bool Hazard => Worst >= .15f;
    internal bool Safe => Known && Worst < .1f;
    internal bool BetterThan(NpcExtractionRisk other) => Worst < other.Worst - .02f
        || Math.Abs(Worst - other.Worst) <= .02f && Total < other.Total - .05f;
}

internal static class NpcExtractionModel
{
    internal const int TRAVEL_BUDGET = 600, FALLBACK_INTERVAL = 2500;
    internal static bool Enabled(bool enabled, int mode) => enabled && mode >= 1;
    internal static bool Carrier(bool downed, float moving, bool manipulation, float urgency)
        => !downed && moving >= .5f && manipulation && urgency < 70f;
    internal static bool Finite(float risk) => !float.IsNaN(risk) && !float.IsInfinity(risk);
    internal static float Thermal(float temperature, float min, float max)
    {
        if (!Finite(temperature) || !Finite(min) || !Finite(max)) { return float.NaN; }
        float excess = Math.Max(0f, Math.Max(min - temperature, temperature - max));
        return excess * excess / 600f; // 300 work ticks / 1800 thermal exposure units.
    }
}

internal sealed class NpcExtractionLimits
{
    internal int FallbackUntil, HoldUntil, StabilizedUntil;
    internal bool CanRelocate(int now, bool immediateDanger) => now >= HoldUntil || immediateDanger;
    internal bool CanFallback(int now) => now >= FallbackUntil;
    internal bool CanPrepare(int now) => now >= StabilizedUntil;
    internal void Fallback(int now) => FallbackUntil = now + NpcExtractionModel.FALLBACK_INTERVAL;
    internal void Prepare(int now) => StabilizedUntil = now + 2500;
    internal void Delivered(int now) => HoldUntil = now + 600;
}
