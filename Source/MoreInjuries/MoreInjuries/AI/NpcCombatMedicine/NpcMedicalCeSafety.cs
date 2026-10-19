using System;
using System.Reflection;
using Verse;

namespace MoreInjuries.AI.NpcCombatMedicine;

// Read-only optional binding. In particular, IsHunkering must never be polled:
// CE's getter can reset suppression and end the current job.
internal sealed class NpcMedicalCeSafety
{
    private readonly Type? _type;
    private readonly FieldInfo? _suppressed;
    private readonly PropertyInfo? _current, _threshold;
    internal readonly JobDef? Cover, Hunker;
    internal bool Supported => _suppressed is not null && _current is not null && _threshold is not null;

    internal NpcMedicalCeSafety(Type? type, Type? jobDefs)
    {
        _type = type;
        FieldInfo? field = type?.GetField("isSuppressed", BindingFlags.Instance | BindingFlags.Public);
        if (field?.FieldType == typeof(bool) && field.DeclaringType == type && !field.IsStatic) { _suppressed = field; }
        _current = ReadableFloat(type, "CurrentSuppression");
        _threshold = ReadableFloat(type, "SuppressionThreshold");
        Cover = Job(jobDefs, "RunForCover");
        Hunker = Job(jobDefs, "HunkerDown");
    }

    private static PropertyInfo? ReadableFloat(Type? type, string name)
    {
        PropertyInfo? property = type?.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        MethodInfo? getter = property?.GetGetMethod();
        return property?.PropertyType == typeof(float) && getter?.DeclaringType == type
            && !getter.IsStatic && getter.GetParameters().Length == 0 ? property : null;
    }

    private static JobDef? Job(Type? type, string name)
    {
        FieldInfo? field = type?.GetField(name, BindingFlags.Static | BindingFlags.Public);
        return field is not null && field.DeclaringType == type && field.FieldType == typeof(JobDef) ? field.GetValue(null) as JobDef : null;
    }

    internal bool IsComp(object comp) => _type?.IsInstanceOfType(comp) == true;
    internal bool Suppressed(object comp) => Supported && IsComp(comp)
        && ((bool)_suppressed!.GetValue(comp) || (float)_current!.GetValue(comp) > (float)_threshold!.GetValue(comp));
    internal bool OwnsJob(JobDef? job) => job is not null && (job == Cover || job == Hunker);
}
