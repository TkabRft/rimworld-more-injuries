using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.AI.NpcCombatMedicine;
using Verse;

namespace MoreInjuries.HarmonyTests;

[TestClass]
public sealed class NpcCeSafetyTests
{
    public sealed class Suppression
    {
        public bool isSuppressed;
        public float CurrentSuppression { get; set; }
        public float SuppressionThreshold => 5f;
        public bool IsHunkering => throw new InvalidOperationException("Mutating CE getter must not be called");
    }
    public static class Jobs { public static JobDef RunForCover = new(), HunkerDown = new(); }
    public sealed class Unsupported { public static bool isSuppressed; public double CurrentSuppression => 2; public float SuppressionThreshold => 1; }

    [TestMethod]
    public void ValidatedReadsProtectSuppressionAndCoverWithoutHunkeringGetter()
    {
        NpcMedicalCeSafety safety = new(typeof(Suppression), typeof(Jobs));
        Suppression comp = new();
        Assert.IsTrue(safety.Supported);
        Assert.IsFalse(safety.Suppressed(comp));
        comp.isSuppressed = true; Assert.IsTrue(safety.Suppressed(comp));
        comp.isSuppressed = false; comp.CurrentSuppression = 6; Assert.IsTrue(safety.Suppressed(comp));
        Assert.IsTrue(safety.OwnsJob(Jobs.RunForCover)); Assert.IsTrue(safety.OwnsJob(Jobs.HunkerDown));
        Assert.IsFalse(safety.OwnsJob(new()));
    }

    [TestMethod]
    public void UnsupportedFieldsKeepRecognizedJobProtection()
    {
        NpcMedicalCeSafety safety = new(typeof(Unsupported), typeof(Jobs));
        Assert.IsFalse(safety.Supported); Assert.IsFalse(safety.Suppressed(new Unsupported()));
        Assert.IsTrue(safety.OwnsJob(Jobs.HunkerDown));
        Assert.IsFalse(new NpcMedicalCeSafety(null, null).Supported);
    }
}
