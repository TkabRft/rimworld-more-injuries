using System;
using System.Collections.Generic;
using System.Linq;

namespace MoreInjuries.AI.NpcCombatMedicine;

// A generation-only budget. Runtime pickup still uses the inventory's actual fit check.
internal readonly struct NpcMedicalSpace(float mass, float bulk)
{
    internal float Mass { get; } = float.IsNaN(mass) ? 0f : Math.Max(0f, mass);
    internal float Bulk { get; } = float.IsNaN(bulk) ? 0f : Math.Max(0f, bulk);
    internal bool Fits(float mass, float bulk) => mass <= Mass + .00001f && bulk <= Bulk + .00001f;
}

internal sealed class NpcMedicalLoadout<T> where T : notnull
{
    internal sealed class Item(T key, int wanted, int existing, float mass, float bulk, bool essential, int priority)
    {
        internal T Key { get; } = key;
        internal int Missing { get; } = Math.Max(0, wanted - existing);
        internal bool NeedsCore { get; } = essential && existing <= 0 && wanted > 0;
        internal float Mass { get; } = mass;
        internal float Bulk { get; } = bulk;
        internal bool Essential { get; } = essential;
        internal int Priority { get; } = priority;
    }

    private readonly List<Item> _items;
    internal NpcMedicalLoadout(IEnumerable<Item> items) => _items = items.OrderBy(item => item.Priority).ToList();

    internal bool FitsCore(NpcMedicalSpace space) => space.Fits(
        _items.Where(item => item.NeedsCore).Sum(item => item.Mass),
        _items.Where(item => item.NeedsCore).Sum(item => item.Bulk));

    internal float CoreCoverage(NpcMedicalSpace space)
    {
        if (FitsCore(space)) { return 1f; }
        Dictionary<T, int> allocated = Allocate(space);
        List<Item> core = _items.Where(item => item.Essential && item.Missing > 0).ToList();
        return core.Count == 0 ? 1f : (float)core.Count(item => !item.NeedsCore || allocated.ContainsKey(item.Key)) / core.Count;
    }

    internal Dictionary<T, int> Allocate(NpcMedicalSpace space)
    {
        Dictionary<T, int> added = new();
        float mass = space.Mass, bulk = space.Bulk;
        // Get one of each essential treatment/device before filling stacks of consumables.
        foreach (Item item in _items.Where(item => item.NeedsCore)) { Take(item, 1); }
        foreach (Item item in _items.Where(item => item.Essential)) { Take(item, item.Missing - Count(item)); }
        foreach (Item item in _items.Where(item => !item.Essential)) { Take(item, item.Missing); }
        return added;

        int Count(Item item) => added.TryGetValue(item.Key, out int count) ? count : 0;
        void Take(Item item, int requested)
        {
            if (requested <= 0 || float.IsNaN(item.Mass) || float.IsNaN(item.Bulk)
                || float.IsInfinity(item.Mass) || float.IsInfinity(item.Bulk)
                || item.Mass < 0f || item.Bulk < 0f) { return; }
            int count = requested;
            if (item.Mass > 0f && (mass + .00001f) / item.Mass < count)
            { count = (int)Math.Floor((mass + .00001f) / item.Mass); }
            if (item.Bulk > 0f && (bulk + .00001f) / item.Bulk < count)
            { count = (int)Math.Floor((bulk + .00001f) / item.Bulk); }
            if (count <= 0) { return; }
            added[item.Key] = Count(item) + count;
            mass = Math.Max(0f, mass - count * item.Mass);
            bulk = Math.Max(0f, bulk - count * item.Bulk);
        }
    }

    internal static TCarrier SelectMedic<TCarrier>(IEnumerable<TCarrier> candidates, Func<TCarrier, float> coreCoverage,
        Func<TCarrier, int> medical, Func<TCarrier, int> identity)
        => candidates.OrderByDescending(coreCoverage).ThenByDescending(medical).ThenBy(identity).First();
}
