// Only the boundary to vanilla ingestion is simulated; production administration,
// outcome dispatch, dose ownership, and chloroform dose selection are source-linked.
using MoreInjuries.AI.Jobs.Outcomes;
using Verse;

namespace Verse
{
    public sealed partial class Pawn
    {
        public int MedicalSkill = 10;
    }

    public sealed class ThingDef
    {
        public string defName = "test device";
        public JobOutcomeProperties_ModExtension Extension = new();
        public T? GetModExtension<T>() where T : class => Extension as T;
    }

    public sealed partial class Thing
    {
        public ThingDef def = new();
        public int stackCount = 1;
        public bool Destroyed;
        public bool IngestibleNow = true;
        public Action<Pawn, Thing>? OnIngested;

        public Thing SplitOff(int count)
        {
            Assert.AreEqual(1, count);
            if (stackCount == count) { return this; }
            stackCount -= count;
            return new Thing { def = def, OnIngested = OnIngested, IngestibleNow = IngestibleNow };
        }

        public void Destroy() => Destroyed = true;

        public float Ingested(Pawn patient, float nutritionWanted)
        {
            Assert.IsFalse(Destroyed);
            Assert.AreEqual(1, stackCount);
            Assert.AreEqual(0f, nutritionWanted);
            OnIngested?.Invoke(patient, this);
            Destroy();
            return 0f;
        }
    }
}

namespace Verse.AI
{
    public enum JobCondition { Incompletable }
}

namespace MoreInjuries
{
    public static partial class Logger
    {
        public static void Error(string message) { }
        public static void ConfigError(string message) { }
    }
}

namespace MoreInjuries.AI.Jobs
{
    public abstract partial class JobDriver_UseMedicalDevice
    {
        public bool Failed;
        protected virtual bool RequiresDevice => false;
        protected abstract bool ApplyDevice(Pawn doctor, Pawn patient, Thing? device);
        protected void EndJobWith(Verse.AI.JobCondition condition) => Failed = true;
    }
}

namespace MoreInjuries.AI.Jobs.Outcomes
{
    public sealed class JobOutcomeProperties_ModExtension
    {
        public IReadOnlyList<JobOutcomeDoer> OutcomeDoers = [];
    }

    public abstract class JobOutcomeDoer
    {
        public bool TryDoOutcome(Pawn doctor, Pawn patient, Thing? device) => DoOutcome(doctor, patient, device);
        protected abstract bool DoOutcome(Pawn doctor, Pawn patient, Thing? device);
    }
}

namespace RimWorld
{
    public abstract class IngestionOutcomeDoer_GiveHediff
    {
        public HediffDef hediffDef = new();
        public float severity;
        public void DoIngestionOutcome(Pawn pawn, Thing ingested, int count) => DoIngestionOutcomeSpecial(pawn, ingested, count);
        protected abstract void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int count);
    }
}

namespace MoreInjuries.Extensions
{
    public static class DrugExtensions
    {
        public static int GetMedicalSkillLevelOrDefault(this Pawn pawn) => pawn.MedicalSkill;
    }
}

namespace MoreInjuries.HealthConditions.Drugs
{
    public partial class IngestionOutcomeDoer_ChloroformBuildup
    {
        public SimpleCurve Minimum = new(skill => 0.35f - skill * 0.0025f);
        public SimpleCurve Maximum = new(skill => 0.85f - skill * 0.02f);
        private partial SimpleCurve MinSeverityOffsetByDoctorSkill => Minimum;
        private partial SimpleCurve MaxSeverityOffsetByDoctorSkill => Maximum;
    }
}
