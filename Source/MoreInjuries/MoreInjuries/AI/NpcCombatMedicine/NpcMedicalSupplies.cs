using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static class NpcMedicalSupplies
{
    internal static Thing? Find(Pawn doctor, Pawn patient, ThingDef def)
    {
        bool Allowed(Thing item) => item.def == def && !item.Destroyed && item.stackCount > 0 && !item.IsForbidden(doctor)
            && doctor.CanReserve(item, 10, stackCount: 1)
            && NpcMedicalReservations.DeliveryAllowed(doctor, patient, item);
        Thing? Own(Pawn holder) => holder.inventory?.innerContainer.FirstOrDefault(Allowed);
        Thing? Allied(Thing thing) => thing is Pawn holder && holder != doctor && holder != patient
            && holder.Faction == doctor.Faction && !holder.HostileTo(doctor) && !holder.IsForbidden(doctor)
            && doctor.CanReach(holder, PathEndMode.Touch, Danger.Some) ? Own(holder) : null;
        Thing? result = Own(doctor) ?? Own(patient);
        if (result is not null) { return result; }
        result = GenRadial.RadialDistinctThingsAround(doctor.Position, doctor.Map, 12f, true)
            .Select(Allied).FirstOrDefault(item => item is not null);
        if (result is not null) { return result; }
        result = GenClosest.ClosestThingReachable(doctor.Position, doctor.Map, ThingRequest.ForDef(def),
            PathEndMode.ClosestTouch, TraverseParms.For(doctor, Danger.Some), maxDistance: 12f, validator: Allowed,
            searchRegionsMin: 0, searchRegionsMax: 12, forceAllowGlobalSearch: false);
        if (result is not null || NpcQuickAid.SupplyOrigin(patient) is not IntVec3 origin) { return result; }
        Thing[] nearby = GenRadial.RadialDistinctThingsAround(origin, doctor.Map, 12f, true).ToArray();
        return nearby.Select(Allied).FirstOrDefault(item => item is not null)
            ?? nearby.Where(thing => thing.Spawned && thing is not Pawn && Allowed(thing))
                .OrderBy(thing => thing.PositionHeld.DistanceToSquared(doctor.Position)).FirstOrDefault();
    }
}
