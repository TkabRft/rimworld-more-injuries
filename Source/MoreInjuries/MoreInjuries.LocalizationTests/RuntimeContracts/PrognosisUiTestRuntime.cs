namespace UnityEngine
{
    public readonly struct Rect(float x, float y, float width, float height)
    {
        public readonly float x = x, y = y, width = width, height = height;
        public float yMax => y + height;
    }
    public readonly record struct Color(int Value) { public static readonly Color white = new(1); }
    public static class GUI { public static Color color; }
}
namespace Verse
{
    public enum ProgramState { Entry, Playing }
    public static class Current { public static ProgramState ProgramState = ProgramState.Playing; }
    public static class Text { public static float LineHeight = 24; }
    public static class Widgets
    {
        public static UnityEngine.Color WindowBGFillColor = new(2);
        public static int Draws;
        public static string LastLabel = "";
        public static void DrawBoxSolid(UnityEngine.Rect rect, UnityEngine.Color color) => Draws++;
        public static void Label(UnityEngine.Rect rect, string label) { LastLabel = Draws <= 1 ? label : LastLabel + " | " + label; Draws++; }
    }
    public static class TooltipHandler
    {
        public static string LastTip = "";
        public static void TipRegion(UnityEngine.Rect rect, string tip) => LastTip = tip;
    }
    public static partial class Find
    {
        public static readonly RimWorld.MainTabsRoot MainTabsRoot = new();
        public static readonly RimWorld.Selector Selector = new();
    }
    internal static class PrognosisTestText
    {
        private static readonly Dictionary<string, string> s_text = new[] { "Lang_Prognosis.xml", "Lang_PrognosisHints.xml" }
            .SelectMany(file => System.Xml.Linq.XDocument.Load(Path.Combine(
                MoreInjuries.LocalizationTests.RuntimeContracts.RuntimeDefinitionTests.ModRoot,
                "Languages/English/Keyed", file)).Root!.Elements())
            .ToDictionary(element => element.Name.LocalName, element => element.Value.Replace("\\n", "\n"));
        internal static string Translate(string key, object[] arguments) => string.Format(s_text[key], arguments);
    }
    public static class TruncateBoundary { public static string Truncate(this string label, float width) => label; }
    public partial class HediffDef { public string LabelCap => label; }
    public static partial class GenDate { public static string ToStringTicksToPeriod(int ticks) => ticks.ToString(); }
}
namespace RimWorld
{
    public sealed class ITab_Pawn_Health;
    public sealed class MainTabWindow_Inspect { public Type? OpenTabType; }
    public sealed class MainButtonDef { public object TabWindow = new MainTabWindow_Inspect(); }
    public static class MainButtonDefOf { public static readonly MainButtonDef Inspect = new(); }
    public sealed class MainTabsRoot { public MainButtonDef? OpenTab; }
    public sealed class Selector { public object? SingleSelectedThing; }
    public static class HealthCardUtility
    {
        public static void DrawHediffListing(UnityEngine.Rect rect, Verse.Pawn pawn, bool showBloodLoss) { }
    }
}
