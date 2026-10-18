// Additional stand-ins used by the source-linked shock progression tests.
namespace Verse
{
    public partial class HediffDef
    {
        public string label = "condition";
        public string Label { get => label; set => label = value; }
        public object? Extension;
    }

    public partial class Hediff
    {
        public HediffDef def = null!;
        public string Label => def.Label;
        public List<HediffComp> TestComps = [];
        public T? TryGetComp<T>() where T : HediffComp
        {
            foreach (HediffComp comp in TestComps) { if (comp is T match) { return match; } }
            return null;
        }
        public T? GetComp<T>() where T : HediffComp => TryGetComp<T>();
        public bool Tended
        {
            get => TryGetComp<HediffComp_TendDuration>()?.IsTended == true;
            set
            {
                HediffComp_TendDuration? tend = TryGetComp<HediffComp_TendDuration>();
                if (tend is null) { tend = new() { parent = this, tendQuality = 1f }; TestComps.Add(tend); }
                tend.tendTicksLeft = value ? 10000 : 0;
            }
        }
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

    public sealed partial class RaceProperties
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

    public sealed partial class HealthTracker
    {
        public HediffSet hediffSet = new();
        public void RemoveHediff(Hediff hediff) => hediffSet.hediffs.Remove(hediff);
    }

    public sealed partial class HediffSet
    {
        public List<Hediff> hediffs = [];
        public Hediff? GetFirstHediffOfDef(HediffDef def) => hediffs.Find(hediff => hediff.def == def);
        public bool HasHediff(HediffDef def) => GetFirstHediffOfDef(def) is not null;
    }

    public sealed class HediffComp_TendDuration : HediffComp
    {
        public float tendQuality;
        public int tendTicksLeft;
        public bool IsTended => tendTicksLeft > 0;
    }

    public partial class Thing;

    public static partial class GenDate
    {
        public const int TicksPerHour = 2500;
        public const int TicksPerDay = 60000;
    }

    public static class HealthUtility
    {
        public static void AdjustSeverity(Pawn pawn, HediffDef def, float amount)
        {
            Hediff? hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff is not null) { hediff.Severity += amount; return; }
            hediff = new() { pawn = pawn, def = def, Severity = amount };
            pawn.health.hediffSet.hediffs.Add(hediff);
            if (def == MoreInjuries.Defs.WellKnown.KnownHediffDefOf.Acidosis)
            {
                var comp = new MoreInjuries.HealthConditions.Acidosis.HediffComp_Acidosis
                {
                    parent = hediff, props = new MoreInjuries.HealthConditions.Acidosis.HediffCompProperties_Acidosis()
                };
                hediff.TestComps.Add(comp);
                hediff.TestComps.Add(new MoreInjuries.HealthConditions.Secondary.HediffComp_CausedBy { parent = hediff });
                comp.CompPostPostAdd(null);
            }
        }
    }

}

namespace RimWorld
{
    public static class HediffDefOf
    {
        public static readonly Verse.HediffDef BloodLoss = new();
        public static readonly Verse.HediffDef Deathrest = new();
        public static readonly Verse.HediffDef Hypothermia = new();
    }
}

namespace MoreInjuries.Defs.WellKnown
{
    public static class KnownHediffDefOf
    {
        public static readonly Verse.HediffDef AdrenalineRush = new();
        public static readonly Verse.HediffDef Acidosis = new() { Label = "acidosis" };
        public static readonly Verse.HediffDef HypovolemicShock = new() { Label = "hypovolemic shock" };
        public static readonly Verse.HediffDef Hemodilution = new();
        public static readonly Verse.HediffDef CardiacArrest = new();
        public static readonly Verse.HediffDef HeartAttack = new() { lethalSeverity = 1f };
        public static readonly Verse.HediffDef BrainDamage_Hypoxia = new();
        public static readonly Verse.HediffDef OrganHypoxia = new();
        public static readonly Verse.HediffDef Concussion = new();
        public static readonly Verse.HediffDef HemorrhagicStroke = new();
        public static readonly Verse.HediffDef ChokingOnBlood = new();
        public static readonly Verse.HediffDef LungCollapse = new();
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
        public bool EnableNeuralDamage = true;
        public bool AnomalyRegenerationTreatsStructuralDamage = true;
        public bool EnableDelayedVitalFailure = true;
        public bool EnablePhysiologyDeathEstimate = true;
        public bool PreventDeathByBloodLoss;
        public float DefibrillatorMinimumSuccessRate = .5f;
        public bool AnomalyEnableConditionsForShamblers = true;
        public bool EnableAdvancedTraumaSimulation = true;
        public float HypoxiaAcidosisConversionFactor = 0.015f;
    }

    public static partial class Logger
    {
        public static void LogVerbose(string message) { }
        public static void LogDebug(string message) { }
        public static void Warning(string message) { }
    }
}

namespace MoreInjuries.HealthConditions
{
    public sealed class MoreInjuryComp;
}

namespace UnityEngine
{
    public static partial class Mathf
    {
        public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int CeilToInt(float a) => (int)Math.Ceiling(a);
        public const float Epsilon = float.Epsilon;
    }
}

namespace MoreInjuries.HealthConditions.HypovolemicShock
{
    public partial class HediffCompProperties_Shock
    {
        public partial Verse.SimpleCurve BleedSeverityCurve => new(_ => 1f);
    }
}

namespace Verse
{
    public static partial class Find
    {
        public static readonly TickManager TickManager = new();
    }
    public sealed class TickManager
    {
        public int TicksGame;
    }
}
