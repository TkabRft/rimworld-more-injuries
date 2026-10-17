// Additional stand-ins used by the source-linked shock progression tests.
namespace Verse
{
    public sealed class HediffDef;

    public sealed partial class Hediff
    {
        public HediffDef def = null!;
        public bool Tended;
    }

    public sealed partial class Pawn
    {
        public string Name = "test pawn";
        public bool OxygenImmune;
        public HealthTracker health = new();
        public bool Tracked;
        public RaceProperties RaceProps = new();
        public bool HasComp<T>() => Tracked;
    }

    public sealed class RaceProperties
    {
        public bool IsAnomalyEntity;
        public bool IsFlesh = true;
        public float bleedRateFactor = 1f;
    }

    public static class ModsConfig
    {
        public static bool AnomalyActive;
        public static bool BiotechActive;
    }

    public sealed class HealthTracker
    {
        public HediffSet hediffSet = new();
        public void RemoveHediff(Hediff hediff) => hediffSet.hediffs.Remove(hediff);
    }

    public sealed class HediffSet
    {
        public List<Hediff> hediffs = [];
        public Hediff? GetFirstHediffOfDef(HediffDef def) => hediffs.Find(hediff => hediff.def == def);
        public bool HasHediff(HediffDef def) => GetFirstHediffOfDef(def) is not null;
    }

    public static class Scribe_Values
    {
        public static void Look(ref bool value, string key, bool defaultValue) { }
    }
}

namespace RimWorld
{
    public static class HediffDefOf
    {
        public static readonly Verse.HediffDef BloodLoss = new();
        public static readonly Verse.HediffDef Deathrest = new();
    }
}

namespace MoreInjuries.Defs.WellKnown
{
    public static class KnownHediffDefOf
    {
        public static readonly Verse.HediffDef AdrenalineRush = new();
    }
}

namespace MoreInjuries.Extensions
{
    public static class ShockExtensions
    {
        public static bool HasOxygenDeficiencyImmunity(this Verse.Pawn pawn) => pawn.OxygenImmune;
        public static bool IsTended(this Verse.Hediff hediff) => hediff.Tended;
    }
}

namespace MoreInjuries
{
    public static class MoreInjuriesMod
    {
        public static readonly TestSettings Settings = new();
    }

    public sealed class TestSettings
    {
        public bool EnableHypovolemicShock = true;
    }

    public static class Logger
    {
        public static void LogVerbose(string message) { }
    }
}

namespace MoreInjuries.HealthConditions
{
    public sealed class MoreInjuryComp;
}

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    }
}

namespace MoreInjuries.HealthConditions.HypovolemicShock
{
    public partial class HediffCompProperties_Shock
    {
        public partial Verse.SimpleCurve BleedSeverityCurve => new(_ => 1f);
    }
}
