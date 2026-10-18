using System;
using RimWorld;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

// Numeric input captured once for an open health tab. Never ticks a real condition.
internal struct PrognosisTrend
{
    internal HediffDef Cause;
    internal float Severity, Maximum, Terminal, Rate, TendedRate, ImmuneRate, StageRate;
    internal int TendTicks, ImmunityTicks, Interval, NextTick, StartsAfterTicks;
    internal bool TemperatureRecovery;

    internal float At(int ticks)
    {
        ticks = Math.Max(0, ticks - StartsAfterTicks);
        if (TemperatureRecovery)
        {
            // Native temperature recovery: -clamp(severity * .027, .0015, .015), every 60 ticks.
            float severity = Severity;
            int steps = ticks < NextTick ? 0 : 1 + (ticks - NextTick) / 60;
            int high = Math.Min(steps, Math.Max(0, (int)Math.Ceiling((severity - .015f / .027f) / .015f)));
            severity -= high * .015f; steps -= high;
            if (steps > 0 && severity > .0015f / .027f)
            {
                int middle = Math.Min(steps, Math.Max(0, (int)Math.Ceiling(Math.Log((.0015f / .027f) / severity) / Math.Log(.973))));
                severity *= (float)Math.Pow(.973, middle); steps -= middle;
            }
            return Math.Max(0f, severity - steps * .0015f);
        }
        return Walk(ticks, terminal: false);
    }

    private float Walk(int ticks, bool terminal)
    {
        float result = Severity;
        int previous = 0;
        int budget = (Cause?.stages?.Count ?? 0) * 2 + 12;
        while (previous < ticks)
        {
            if (--budget < 0) { return terminal ? float.PositiveInfinity : result; }
            int next = ticks;
            if (TendTicks > previous && TendTicks < next) { next = TendTicks; }
            if (ImmunityTicks > previous && ImmunityTicks < next) { next = ImmunityTicks; }
            float rate = (ImmunityTicks >= 0 && previous >= ImmunityTicks ? ImmuneRate : Rate)
                + (previous < TendTicks ? TendedRate : 0f);
            float stageFactor = 1f, boundary = float.PositiveInfinity;
            if (Cause?.stages is { } stages)
            {
                for (int i = 0; i < stages.Count; i++)
                {
                    if (stages[i].minSeverity <= result) { stageFactor = stages[i].severityGainFactor; }
                }
                rate += StageRate * stageFactor;
                boundary = rate >= 0f ? float.PositiveInfinity : 0f;
                for (int i = 0; i < stages.Count; i++)
                {
                    float threshold = stages[i].minSeverity;
                    if (rate > 0f && threshold > result) { boundary = Math.Min(boundary, threshold); }
                    if (rate < 0f && threshold < result) { boundary = Math.Max(boundary, threshold); }
                }
            }
            else { rate += StageRate; }
            if (terminal && rate > 0f && Terminal > 0f && Terminal <= Maximum)
            {
                double duration = (Terminal - result) * GenDate.TicksPerDay / rate;
                if (duration <= next - previous && Terminal <= boundary)
                { return (float)(previous + Math.Max(0d, duration)); }
            }
            if (rate != 0f && !float.IsInfinity(boundary))
            {
                double duration = (boundary - result) * GenDate.TicksPerDay / rate;
                if (duration > 0d && duration < next - previous)
                { next = Math.Min(next, previous + Math.Max(1, (int)Math.Ceiling(duration))); }
            }
            result = Math.Max(0f, Math.Min(Maximum, result + rate * (next - previous) / GenDate.TicksPerDay));
            if (result <= 0f) { return terminal ? float.PositiveInfinity : 0f; }
            previous = next;
        }
        return terminal ? float.PositiveInfinity : result;
    }

    internal int UntilTerminal()
    {
        if (Terminal <= 0f || Terminal > Maximum) { return int.MaxValue; }
        if (Severity >= Terminal) { return 0; }
        if (TemperatureRecovery) { return int.MaxValue; }
        float deadline = Walk(int.MaxValue - StartsAfterTicks, terminal: true);
        if (float.IsInfinity(deadline) || deadline >= int.MaxValue - StartsAfterTicks) { return int.MaxValue; }
        long raw = (long)Math.Ceiling(deadline) + StartsAfterTicks;
        if (Interval > 0)
        { raw = NextTick + Math.Max(0L, (raw + Interval - 1) / Interval - 1) * Interval; }
        return raw >= int.MaxValue ? int.MaxValue : (int)raw;
    }

    internal PrognosisTrend After(int ticks)
    {
        PrognosisTrend result = this;
        result.Severity = At(ticks);
        result.TendTicks = Math.Max(0, TendTicks - ticks);
        result.ImmunityTicks = ImmunityTicks < 0 ? -1 : Math.Max(0, ImmunityTicks - ticks);
        result.StartsAfterTicks = Math.Max(0, StartsAfterTicks - ticks);
        result.NextTick = NextTick > ticks ? NextTick - ticks : Interval > 0 ? Interval - (ticks - NextTick) % Interval : 0;
        return result;
    }
}
