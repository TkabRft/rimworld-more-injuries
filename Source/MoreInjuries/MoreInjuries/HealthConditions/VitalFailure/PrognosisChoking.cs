using System;

namespace MoreInjuries.HealthConditions.VitalFailure;

internal struct PrognosisChoking
{
    internal bool Active, SourcePresent, Tended, CoughCapable;
    internal int Interval, NextTick;
    internal float PositiveFactor, NegativeFactor;
    internal PrognosisBleeding Source;

    internal float At(in PrognosisTrend trend, int ticks, bool holdControl = false)
    {
        float severity = trend.Severity;
        int chokingTick = Math.Max(1, NextTick), dailyTick = Math.Max(1, trend.NextTick);
        int interval = Math.Max(1, Interval);
        while (Math.Min(chokingTick, dailyTick) <= ticks)
        {
            int now = Math.Min(chokingTick, dailyTick);
            if (now == dailyTick)
            {
                float factor = 1f;
                if (trend.Cause?.stages is { } stages)
                { foreach (Verse.HediffStage stage in stages) { if (stage.minSeverity <= severity) { factor = stage.severityGainFactor; } } }
                severity += (trend.Rate + trend.StageRate * factor + (now < trend.TendTicks ? trend.TendedRate : 0f))
                    * trend.Interval / 60000f;
                dailyTick += Math.Max(1, trend.Interval);
                severity = Math.Max(0f, Math.Min(trend.Maximum, severity));
                if (severity <= 0f) { return 0f; }
            }
            if (now == chokingTick)
            {
                float rate = SourcePresent ? Source.At(now, holdControl) : 0f;
                severity = Math.Max(0f, Math.Min(1f, severity + MeanChange(rate, SourcePresent, Tended,
                    CoughCapable, PositiveFactor, NegativeFactor)));
                if (severity <= 0f) { return 0f; }
                chokingTick += interval;
            }
        }
        return severity;
    }

    internal static float MeanChange(float rate, bool present, bool tended, bool coughCapable, float positive, float negative)
    {
        float increase = .1f, decrease = 0f;
        if (present && rate > .01f) { increase += Math.Max(.05f, Math.Min(.25f, rate / 5f)); }
        else { decrease = !present || tended ? .075f : .025f; }
        return increase * increase / (2f * (increase + decrease)) * positive
            - decrease * decrease / (2f * (increase + decrease)) * negative
            - (rate <= 0f && coughCapable ? .1f : 0f);
    }
}
