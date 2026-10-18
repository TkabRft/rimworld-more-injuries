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
        public static void DrawBoxSolid(UnityEngine.Rect rect, UnityEngine.Color color) => Draws++;
        public static void Label(UnityEngine.Rect rect, string label) => Draws++;
    }
    public static class TooltipHandler { public static void TipRegion(UnityEngine.Rect rect, string tip) { } }
    public static class TruncateBoundary { public static string Truncate(this string label, float width) => label; }
    public partial class HediffDef { public string LabelCap => label; }
    public static partial class GenDate { public static string ToStringTicksToPeriod(int ticks) => ticks.ToString(); }
}
namespace RimWorld
{
    public static class HealthCardUtility
    {
        public static void DrawHediffListing(UnityEngine.Rect rect, Verse.Pawn pawn, bool showBloodLoss) { }
    }
}
