// In-memory save/load stand-ins exercise production ExposeData methods, including nested history.
namespace Verse;

public interface IExposable { void ExposeData(); }
public enum LoadSaveMode { Inactive, Saving, LoadingVars, ResolvingCrossRefs, PostLoadInit }
public enum LookMode { Value, Deep }

public static class Scribe
{
    public static LoadSaveMode mode;
    public static Dictionary<string, object?> Data = [];

    public static Dictionary<string, object?> Save(Action expose)
    {
        var oldData = Data; var oldMode = mode;
        try { Data = []; mode = LoadSaveMode.Saving; expose(); return Data; }
        finally { Data = oldData; mode = oldMode; }
    }

    public static void Load(Dictionary<string, object?> data, Action expose)
    {
        var oldData = Data; var oldMode = mode;
        try { Data = data; mode = LoadSaveMode.LoadingVars; expose(); }
        finally { Data = oldData; mode = oldMode; }
    }

    internal static void Value<T>(ref T value, string key, T defaultValue)
    {
        if (mode == LoadSaveMode.Saving) { Data[key] = value; }
        else if (mode == LoadSaveMode.LoadingVars) { value = Data.TryGetValue(key, out object? stored) ? (T)stored! : defaultValue; }
    }
}

public static class Scribe_Values
{
    public static void Look(ref bool value, string key, bool defaultValue = false) => Scribe.Value(ref value, key, defaultValue);
    public static void Look(ref int value, string key, int defaultValue = 0) => Scribe.Value(ref value, key, defaultValue);
    public static void Look(ref float value, string key, float defaultValue = 0f) => Scribe.Value(ref value, key, defaultValue);
}

public static class Scribe_Deep
{
    public static void Look<T>(ref T? value, string key) where T : class, IExposable
    {
        if (Scribe.mode == LoadSaveMode.Saving) { Scribe.Data[key] = value is null ? null : Scribe.Save(value.ExposeData); }
        else if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            if (!Scribe.Data.TryGetValue(key, out object? data) || data is not Dictionary<string, object?> stored) { value = null; return; }
            value = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
            Scribe.Load(stored, value.ExposeData);
        }
    }
}

public static class Scribe_Collections
{
    public static void Look<T>(ref List<T>? value, string key, LookMode lookMode)
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            if (value is null) { Scribe.Data[key] = null; return; }
            Scribe.Data[key] = value.Select(item => item is IExposable deep ? (object)Scribe.Save(deep.ExposeData) : item!).ToList();
        }
        else if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            if (!Scribe.Data.TryGetValue(key, out object? data) || data is not List<object> stored) { value = null; return; }
            value = [];
            foreach (object item in stored)
            {
                if (lookMode == LookMode.Deep)
                {
                    T entry = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
                    Scribe.Load((Dictionary<string, object?>)item, ((IExposable)entry!).ExposeData);
                    value.Add(entry);
                }
                else { value.Add((T)item); }
            }
        }
    }
}
