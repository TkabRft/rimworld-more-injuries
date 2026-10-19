using System;
using MoreInjuries.HealthConditions.HeavyBleeding;

namespace MoreInjuries.HealthConditions.VitalFailure;

// Rates are already normalized to the native pawn-total units. No live game objects enter the forecast.
internal readonly struct PrognosisBleeding(float rate, float modifier, bool missingPart, bool internalPart, int nextTick,
    bool decays = true, float constant = 1f, float timedBase = 1f, int remaining = 0, int duration = 0)
{
    internal PrognosisBleeding Scaled(float scale) => new(rate * scale, modifier, missingPart, internalPart, nextTick,
        decays, constant, timedBase, remaining, duration);

    internal float At(int ticks, bool holdControl = false)
    {
        if (holdControl) { ticks = 0; }
        int steps = ticks < nextTick ? 0 : 1 + (ticks - nextTick) / 60;
        float current = Math.Min(1f, modifier + (decays ? .01f * steps : 0f));
        if (missingPart) { current *= .5f; }
        if (internalPart) { current += .2f; }
        float tamponade = TimedHemostasis.Multiplier(timedBase, duration, Math.Max(0, remaining - ticks));
        return rate * constant * tamponade * Math.Max(0f, Math.Min(1f, current));
    }

    internal static float Total(in PhysiologySnapshot source, int ticks)
    {
        float rate = source.BleedRate;
        for (int i = 0; i < source.BleedingCount; i++)
        { rate += source.Bleeding![i].At(ticks, source.HoldBleedingControl) - source.Bleeding[i].At(0); }
        return Math.Max(0f, rate);
    }
}
