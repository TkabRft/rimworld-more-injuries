using System;
using Verse;
using Verse.AI;

namespace MoreInjuries.AI;

internal static class EmergencyTravel
{
    internal static bool InInventory(Pawn doctor, Thing device) => doctor.inventory.innerContainer.Contains(device)
        || doctor.carryTracker?.CarriedThing == device;

    internal static int ExtraTicks(Pawn doctor, Pawn patient, Thing? device, bool exact)
    {
        if (device is null || InInventory(doctor, device)) { return 0; }
        if (patient.inventory.innerContainer.Contains(device)) { return 25; }
        if (doctor.Map is not { } map || patient.MapHeld != map || device.MapHeld != map) { return int.MaxValue; }
        int transfer = device.ParentHolder is Pawn_InventoryTracker ? 25 : 0;
        if (!exact)
        {
            float cells = doctor.PositionHeld.DistanceTo(device.PositionHeld) + device.PositionHeld.DistanceTo(patient.PositionHeld)
                - doctor.PositionHeld.DistanceTo(patient.PositionHeld);
            return transfer + (int)Math.Ceiling(Math.Max(0f, cells) * doctor.TicksPerMoveCardinal);
        }
        int Path(IntVec3 from, IntVec3 to)
        {
            PawnPath path = map.pathFinder.FindPathNow(from, to, doctor, peMode: PathEndMode.ClosestTouch);
            try { return path.Found ? (int)Math.Ceiling(path.TotalCost) : int.MaxValue; }
            finally { path.ReleaseToPool(); }
        }
        int first = Path(doctor.PositionHeld, device.PositionHeld), second = Path(device.PositionHeld, patient.PositionHeld);
        if (first == int.MaxValue || second == int.MaxValue) { return int.MaxValue; }
        int direct = Path(doctor.PositionHeld, patient.PositionHeld);
        if (direct == int.MaxValue) { return int.MaxValue; }
        return (int)Math.Min(int.MaxValue, Math.Max(0L, (long)first + second - direct) + transfer);
    }
}
