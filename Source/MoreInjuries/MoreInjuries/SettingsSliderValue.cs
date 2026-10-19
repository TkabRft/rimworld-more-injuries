namespace MoreInjuries;

internal static class SettingsSliderValue
{
    internal static float AfterEdit(float original, float before, float after, float displayFactor = 1f,
        int decimals = 2, bool ceiling = false, bool floor = false)
    {
        // Viewing settings must not round, clamp or otherwise rewrite stored values.
        if (before == after)
        {
            return original;
        }
        float value = after / displayFactor;
        if (ceiling)
        {
            return (float)Math.Ceiling(value);
        }
        if (floor)
        {
            return (float)Math.Floor(value);
        }
        return (float)Math.Round(value, decimals);
    }
}
