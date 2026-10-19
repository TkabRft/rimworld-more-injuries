namespace MoreInjuries.HealthConditions.HeavyBleeding.Overrides;

// Internal read-only view; no new public patch surface or serialized state.
internal interface IForecastHemostasis
{
    void CaptureHemostasis(out float constant, out float timedBase, out int remaining, out int duration);
}
