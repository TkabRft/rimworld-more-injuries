using System;

namespace MoreInjuries.HealthConditions.HeavyBleeding;

// Shared by live wounds and numeric forecasts. Time is active hediff age, never wall/world time.
internal static class TimedHemostasis
{
    internal const int INTERVAL = 250;

    internal static float Multiplier(float initial, int total, int remaining)
    {
        if (total <= 0 || remaining <= 0) { return 1f; }
        int elapsed = Math.Max(0, total - remaining);
        int stage = elapsed / INTERVAL * INTERVAL;
        return initial + (1f - initial) * stage / total;
    }

    internal static int UntilNextStage(int total, int remaining)
        => Math.Min(remaining, INTERVAL - Math.Max(0, total - remaining) % INTERVAL);
}

internal struct TimedHemostasisClock
{
    private int _age, _remaining, _total, _next;
    internal void Reset(int total, int remaining, int age)
    {
        _total = total; _remaining = remaining; _age = age;
        _next = age + TimedHemostasis.UntilNextStage(total, remaining);
    }
    internal int RemainingAt(int age) => Math.Max(0, _remaining - Math.Max(0, age - _age));
    internal bool Advance(int age, out int remaining)
    {
        remaining = _remaining;
        if (_remaining <= 0 || age < _next) { return false; }
        remaining = RemainingAt(age);
        Reset(_total, remaining, age);
        return true;
    }
}
