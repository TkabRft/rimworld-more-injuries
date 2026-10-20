using MoreInjuries.HealthConditions;
using Verse;

namespace Verse
{
    public sealed class Corpse { public Pawn InnerPawn = null!; }
    public sealed class FloatMenuOption(string label) { public string Label = label; }
}
namespace MoreInjuries
{
    public sealed partial class TestSettings { public bool EnableChoking = true; }
}
namespace MoreInjuries.HealthConditions
{
    public sealed record UIBuilder<T>(HashSet<string> Keys, List<T> Options);
}
// Native menu construction is a boundary. The production comp owner/eligibility
// routing and the production physiology policy are linked into this assembly.
namespace MoreInjuries.HealthConditions.CardiacArrest
{
    internal static class CardiacArrestWorker
    {
        internal static int Calls;
        internal static void AddEmergencyOptions(Pawn patient, UIBuilder<FloatMenuOption> builder, Pawn selected)
        { Calls++; builder.Options.Add(new("cardiac")); }
    }
}
namespace MoreInjuries.HealthConditions.Choking
{
    internal static class ChokingWorker
    {
        internal static int Calls;
        internal static void AddEmergencyOptions(Pawn patient, UIBuilder<FloatMenuOption> builder, Pawn selected)
        { Calls++; builder.Options.Add(new("airway")); }
    }
}
