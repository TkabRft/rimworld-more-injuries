using System;

namespace MoreInjuries.HealthConditions.VitalFailure;

internal struct PrognosisCapacityTerm
{
    internal int Capacity, Trend;
    internal float Severity, MinimumSeverity, NextSeverity, Offset, Factor, Maximum;
}

internal static class PrognosisCapacity
{
    internal static float Level(in PhysiologySnapshot source, int capacity, int ticks)
    {
        if (source.RawCapacities is null)
        {
            return capacity == 0 ? source.Breathing : capacity == 1 ? source.Pumping : 1f;
        }
        if ((source.ApplicableCapacities & (1 << capacity)) == 0) { return 1f; }
        float value = source.RawCapacities[capacity], factor = 1f, maximum = float.MaxValue;
        if (value > 0f)
        {
            for (int i = 0; i < source.CapacityTermCount; i++)
            {
                PrognosisCapacityTerm term = source.CapacityTerms![i];
                if (term.Capacity != capacity) { continue; }
                float severity = term.Trend < 0 ? term.Severity : source.Trends![term.Trend].At(ticks, source.HoldBleedingControl);
                if (severity <= 0f || severity < term.MinimumSeverity || severity >= term.NextSeverity) { continue; }
                value += term.Offset; factor *= term.Factor; maximum = Math.Min(maximum, term.Maximum);
            }
            value = Math.Min(maximum, value * factor);
        }
        return (float)Math.Round(Math.Max(source.MinimumCapacities![capacity], value) * 100f) / 100f;
    }
}
