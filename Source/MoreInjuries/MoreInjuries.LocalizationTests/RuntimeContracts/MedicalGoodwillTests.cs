using MoreInjuries.AI.Jobs.Outcomes;
using RimWorld;
using Verse;

namespace MoreInjuries.LocalizationTests.RuntimeContracts
{
    [TestClass, DoNotParallelize]
    public sealed class MedicalGoodwillTests
    {
        [TestMethod]
        public void NpcTreatmentNeverChangesPlayerGoodwillOrSendsPlayerViolationSignals()
        {
            Faction target = new(), other = new();
            Pawn patient = new() { Faction = target };
            foreach (Faction? actorFaction in new Faction?[] { target, other, null })
            {
                Faction.OfPlayer.Changes.Clear(); QuestUtility.Signals = 0;
                JobOutcomeDoer_FactionRelations outcome = new() { Amount = 5, Violation = true };
                Assert.IsTrue(outcome.TryDoOutcome(new Pawn { Faction = actorFaction }, patient, null));
                Assert.HasCount(0, Faction.OfPlayer.Changes);
                Assert.AreEqual(0, QuestUtility.Signals);
            }
        }

        [TestMethod]
        public void PlayerTreatmentRetainsAmountReasonFriendlyFilterAndViolationSignals()
        {
            Faction.OfPlayer.Changes.Clear(); QuestUtility.Signals = 0;
            Pawn doctor = new() { Faction = Faction.OfPlayer }, patient = new() { Faction = new() };
            JobOutcomeDoer_FactionRelations outcome = new() { Amount = 5, Friendly = true };
            Assert.IsTrue(outcome.TryDoOutcome(doctor, patient, null));
            Assert.HasCount(1, Faction.OfPlayer.Changes);
            Assert.AreEqual((patient.Faction, 5, outcome.HistoryEventDef), Faction.OfPlayer.Changes[0]);
            patient.Faction.Hostile = true;
            Assert.IsTrue(outcome.TryDoOutcome(doctor, patient, null));
            Assert.HasCount(1, Faction.OfPlayer.Changes);
            outcome.Friendly = false; outcome.Violation = true; outcome.Amount = -10;
            outcome.TryDoOutcome(doctor, patient, null);
            Assert.HasCount(2, Faction.OfPlayer.Changes);
            Assert.AreEqual(-10, Faction.OfPlayer.Changes[1].Amount);
            Assert.AreEqual(1, QuestUtility.Signals);
            patient.Faction = Faction.OfPlayer;
            outcome.TryDoOutcome(doctor, patient, null);
            Assert.HasCount(2, Faction.OfPlayer.Changes);
            patient.QuestLodger = true;
            outcome.TryDoOutcome(doctor, patient, null);
            Assert.HasCount(3, Faction.OfPlayer.Changes);
        }

        [TestMethod]
        public void ProductionLoggingHasNoDirectGameLogBypasses()
        {
            string root = Path.Combine(RuntimeDefinitionTests.ModRoot, "Source/MoreInjuries/MoreInjuries");
            foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (relative.StartsWith("obj/") || relative.StartsWith("bin/") || relative.StartsWith("artifacts/")
                    || relative == "Logging/Logger.cs") { continue; }
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(path),
                    @"\b(?:Verse\.)?Log\.(?:Message|Warning|Error|WarningOnce|ErrorOnce)\s*\("), relative);
            }
        }
    }
}

namespace MoreInjuries.AI.Jobs.Outcomes
{
    public partial class JobOutcomeDoer_FactionRelations
    {
        public int Amount;
        public bool Violation, Friendly;
        public partial int GoodwillChange => Amount;
        public partial HistoryEventDef HistoryEventDef => Event;
        public partial bool IsViolation => Violation;
        public partial bool OnlyIfFriendly => Friendly;
        private readonly HistoryEventDef Event = new();
    }
}

namespace RimWorld
{
    public sealed class HistoryEventDef { public string defName = "medical treatment"; }
    public sealed class Faction
    {
        public static readonly Faction OfPlayer = new();
        public readonly List<(Faction Target, int Amount, HistoryEventDef Reason)> Changes = [];
        public bool temporary, Hostile;
        public bool IsPlayerSafe() => this == OfPlayer;
        public bool HostileTo(Faction other) => Hostile;
        public bool TryAffectGoodwillWith(Faction target, int change, bool canSendHostilityLetter, HistoryEventDef reason)
        { Changes.Add((target, change, reason)); return true; }
    }
    public static class QuestUtility
    {
        public const string QuestTargetSignalPart_SurgeryViolation = "SurgeryViolation";
        public static int Signals;
        public static void SendQuestTargetSignals(object? tags, string signal, object subject) => Signals++;
    }
}

namespace Verse
{
    public sealed partial class Pawn
    {
        public Faction? Faction;
        public bool QuestLodger;
        public List<string> questTags = [];
        public bool IsQuestLodger() => QuestLodger;
        public object Named(string name) => this;
    }
}
