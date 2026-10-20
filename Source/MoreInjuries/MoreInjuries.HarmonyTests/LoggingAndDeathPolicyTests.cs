using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.AI.NpcCombatMedicine;
using MoreInjuries.Patches;
using Verse;

namespace MoreInjuries.HarmonyTests
{
    [TestClass, DoNotParallelize]
    public sealed class LoggingAndDeathPolicyTests
    {
        private const string OWNER = "MoreInjuries.test.LethalDamageThreshold";

        [TestInitialize]
        public void Initialize() => new NpcPainCareTests().Initialize();

        [TestCleanup]
        public void Cleanup() => new Harmony(OWNER).UnpatchAll(OWNER);

        [TestMethod]
        public void DisabledLoggingSilencesEveryLevelEvenInDeveloperMode()
        {
            MoreInjuriesMod.Settings.EnableVerboseLogging = true;
            Prefs.DevMode = true;
            Logger.Log("ordinary"); Logger.LogVerbose("verbose"); Logger.LogDebug("debug");
            Logger.Warning("warning"); Logger.Error("error"); Logger.LogAlways("critical");
            try { Logger.ConfigError("configuration"); }
            catch (InvalidOperationException) { } // Debug validation still throws; it must not write a log.
            Pawn actor = new(), patient = new();
            NpcMedicalDecision.Report(actor, patient, 2, "care", "no treatment");
            Assert.HasCount(0, Log.Messages);
            MoreInjuriesMod.Settings.EnableLogging = true;
            NpcMedicalDecision.Report(actor, patient, 2, "care", "no treatment");
            Assert.HasCount(1, Log.Messages, "Disabled diagnostics must not consume the first enabled report.");
        }

        [TestMethod]
        public void EnabledLoggingRetainsLevelsAndVerboseNeedsBothFlags()
        {
            MoreInjuriesMod.Settings.EnableLogging = true;
            Logger.Log("ordinary"); Logger.Warning("warning"); Logger.Error("error"); Logger.LogAlways("critical");
            Logger.LogVerbose("hidden");
            Assert.HasCount(4, Log.Messages);
            MoreInjuriesMod.Settings.EnableVerboseLogging = true;
            Logger.LogVerbose("visible");
            Assert.HasCount(5, Log.Messages);
            StringAssert.Contains(Log.Messages[4], "VERBOSE: visible");
        }

        [TestMethod]
        public void LiveHarmonyHookDefaultsOnAndRestoresNativeCheckWhenDisabled()
        {
            Assert.IsTrue(MoreInjuriesMod.Settings.PreventDeathByLethalDamageThreshold);
            new Harmony(OWNER).CreateClassProcessor(typeof(Patch_LethalDamageThreshold)).Patch();
            Pawn_HealthTracker health = new() { InjuryDamage = 200 };
            Assert.IsFalse(health.ShouldBeDeadFromLethalDamageThreshold());
            Assert.AreEqual(0, health.Checks, "Do not run the native threshold or its cause-of-death logging.");
            MoreInjuriesMod.Settings.PreventDeathByLethalDamageThreshold = false;
            Assert.IsTrue(health.ShouldBeDeadFromLethalDamageThreshold());
            Assert.AreEqual(1, health.Checks);
            health.InjuryDamage = 20;
            Assert.IsFalse(health.ShouldBeDeadFromLethalDamageThreshold());
            MoreInjuriesMod.Settings.PreventDeathByLethalDamageThreshold = true;
            Assert.IsFalse(health.ShouldBeDeadFromLethalDamageThreshold());
            Assert.AreEqual(2, health.Checks);
        }
    }
}

namespace Verse
{
    // The game reference contains signatures only. Run the actual Harmony prefix against
    // a counting boundary; native capacities and other death routes remain game acceptance.
    public sealed class Pawn_HealthTracker
    {
        public float InjuryDamage;
        public int Checks;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool ShouldBeDeadFromLethalDamageThreshold() { Checks++; return InjuryDamage >= 150; }
    }

    public static partial class Log
    {
        public static void Error(string message) => Messages.Add(message);
    }
}
