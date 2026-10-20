using RimWorld;
using System.Collections.Generic;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

internal sealed class VitalAnatomy
{
    private static readonly Dictionary<BodyDef, VitalAnatomy> s_cache = [];
    internal readonly BodyPartRecord[] Brains;
    internal readonly BodyPartRecord[] Pumps;
    internal readonly BodyPartRecord[] Livers;
    internal readonly BodyPartRecord[] Kidneys;

    private VitalAnatomy(BodyDef body)
    {
        Brains = Parts(body, BodyPartTagDefOf.ConsciousnessSource);
        Pumps = Parts(body, BodyPartTagDefOf.BloodPumpingSource);
        Livers = Parts(body, BodyPartTagDefOf.BloodFiltrationLiver);
        Kidneys = Parts(body, BodyPartTagDefOf.BloodFiltrationKidney);
    }

    internal static VitalAnatomy For(BodyDef body)
    {
        if (!s_cache.TryGetValue(body, out VitalAnatomy? anatomy))
        {
            anatomy = new VitalAnatomy(body);
            s_cache.Add(body, anatomy);
        }
        return anatomy;
    }

    private static BodyPartRecord[] Parts(BodyDef body, BodyPartTagDef tag)
    {
        List<BodyPartRecord> parts = [];
        foreach (BodyPartRecord part in body.AllParts)
        {
            if (part.def.tags?.Contains(tag) == true)
            {
                parts.Add(part);
            }
        }
        return parts.ToArray();
    }

    internal static bool AnyPresent(HediffSet set, BodyPartRecord[] parts)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            if (!set.PartIsMissing(parts[i])) { return true; }
        }
        return false;
    }

    internal static bool AllFailed(HediffSet set, BodyPartRecord[] parts)
    {
        if (parts.Length == 0)
        {
            return false;
        }
        for (int i = 0; i < parts.Length; i++)
        {
            if (!set.PartIsMissing(parts[i]) && PawnCapacityUtility.CalculatePartEfficiency(set, parts[i]) > VitalFailureModel.ZERO_FUNCTION)
            {
                return false;
            }
        }
        return true;
    }

    internal bool BrainsDestroyed(HediffSet set)
    {
        if (Brains.Length == 0)
        {
            return false;
        }
        for (int i = 0; i < Brains.Length; i++)
        {
            if (!set.PartIsMissing(Brains[i]) && set.GetPartHealth(Brains[i]) > VitalFailureModel.ZERO_FUNCTION)
            {
                return false;
            }
        }
        return true;
    }

    internal BodyPartRecord? SurvivingBrain(HediffSet set)
    {
        for (int i = 0; i < Brains.Length; i++)
        {
            if (!set.PartIsMissing(Brains[i]) && set.GetPartHealth(Brains[i]) > VitalFailureModel.ZERO_FUNCTION)
            {
                return Brains[i];
            }
        }
        return null;
    }
}
