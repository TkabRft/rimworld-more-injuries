using Verse;

namespace Verse
{
    public sealed class Map { public readonly Verse.AI.PathFinder pathFinder = new(); }
    public partial class Thing
    {
        public Map? Map, MapHeld;
        public IntVec3 PositionHeld;
        public bool Forbidden, Reservable = true;
        public bool IsForbidden(Pawn pawn) => Forbidden;
    }
    public sealed partial class Pawn
    {
        public Thing? CarriedDevice;
        public bool CanReserve(Thing thing, int count, int stackCount) => thing.Reservable;
    }
    public readonly struct ThingRequest
    { public static ThingRequest ForDef(ThingDef def) => new(); public static ThingRequest ForGroup(ThingRequestGroup group) => new(); }
    public readonly struct TraverseParms { public static TraverseParms For(Pawn pawn) => new(); }
    public static class GenClosest
    {
        public static Thing? Device;
        public static int Calls;
        public static Thing? ClosestThingReachable(int root, Map map, ThingRequest request, Verse.AI.PathEndMode mode,
            TraverseParms parms, float maxDistance, Predicate<Thing> validator, int searchRegionsMin, int searchRegionsMax,
            bool forceAllowGlobalSearch)
        {
            Calls++;
            Assert.AreEqual(12f, maxDistance); Assert.AreEqual(12, searchRegionsMax);
            Assert.IsFalse(forceAllowGlobalSearch);
            return Device is { } device && Math.Abs(device.PositionHeld - root) <= maxDistance && validator(device) ? device : null;
        }
    }
}
namespace Verse.AI { public enum PathEndMode { ClosestTouch } }
namespace MoreInjuries.Things
{
    public static partial class MedicalDeviceHelper
    {
        public const int MAX_MEDICAL_DEVICE_RESERVATIONS = 10;
        public static Thing? FindMedicalDevice(Pawn doctor, Pawn patient, ThingDef def, HediffDef[] targets, bool fromInventoryOnly)
        {
            Assert.IsTrue(fromInventoryOnly);
            return patient.playerSettings?.medCare == RimWorld.MedicalCareCategory.NoCare ? null : doctor.CarriedDevice;
        }
    }
}
namespace MoreInjuries
{
    public sealed partial class TestSettings { public float SuctionDeviceMinimumSuccessRate = .25f; }
}
namespace MoreInjuries.Defs.WellKnown
{
    public static partial class KnownResearchProjectDefOf
    {
        public static readonly ResearchDef EmergencyMedicine = new();
    }
    public sealed class ResearchDef { public bool IsFinished = true; }
}
