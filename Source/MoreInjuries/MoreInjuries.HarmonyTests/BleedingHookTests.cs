using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.Integrations.CombatExtended;
using MoreInjuries.HealthConditions.VitalFailure;

namespace MoreInjuries.HarmonyTests
{
    [TestClass, DoNotParallelize]
    public sealed class BleedingHookTests
    {
        private const string CE = CombatExtendedBridge.CE_HARMONY_OWNER, MI = "MI.test.observer", OTHER = "other.test.owner";
        private static float s_observed;
        private static void Observe(float __result) => s_observed = __result;
        private static void CePostfix(NativeInjury __instance, ref float __result)
        {
            float modifier = __instance.RawModifier * (__instance.Missing ? .5f : 1f) + (__instance.Internal ? .2f : 0f);
            if (__result > 0) { __result *= Math.Max(0f, Math.Min(1f, modifier)); }
        }
        private static void OtherPostfix(ref float __result) => __result *= 1.1f;

        [TestMethod]
        public void RealDetoursApplyCeOnceInBothRegistrationOrdersAndRetainOtherPatches()
        {
            MethodInfo native = AccessTools.PropertyGetter(typeof(NativeInjury), nameof(NativeInjury.BleedRate));
            MethodInfo mi = AccessTools.PropertyGetter(typeof(MiInjury), nameof(MiInjury.BleedRate));
            MethodInfo cePatch = AccessTools.Method(typeof(BleedingHookTests), nameof(CePostfix));
            foreach (bool miFirst in new[] { false, true })
            {
                Harmony ce = new(CE), observer = new(MI), other = new(OTHER);
                try
                {
                    void RegisterMi() => observer.Patch(native, postfix: new HarmonyMethod(typeof(BleedingHookTests), nameof(Observe)) { before = [CE] });
                    if (miFirst) { RegisterMi(); }
                    ce.Patch(native, postfix: new HarmonyMethod(cePatch));
                    ce.Patch(mi, postfix: new HarmonyMethod(cePatch));
                    other.Patch(mi, postfix: new HarmonyMethod(typeof(BleedingHookTests), nameof(OtherPostfix)) { after = [CE] });
                    if (!miFirst) { RegisterMi(); }
                    Assert.IsTrue(CombatExtendedBleedingHooks.Normalize(observer, cePatch, [native], [mi]));
                    Assert.IsTrue(Harmony.GetPatchInfo(native).Postfixes.Any(p => p.PatchMethod == cePatch));
                    Assert.IsFalse(Harmony.GetPatchInfo(mi).Postfixes.Any(p => p.PatchMethod == cePatch));
                    Assert.IsTrue(Harmony.GetPatchInfo(mi).Owners.Contains(OTHER));
                    foreach (float raw in new[] { -.5f, 0f, .5f, 1f })
                    foreach (bool missing in new[] { false, true })
                    foreach (bool inside in new[] { false, true })
                    {
                        MiInjury wound = new() { RawModifier = raw, Missing = missing, Internal = inside, Hemostasis = .2f };
                        float actual = wound.BleedRate;
                        PrognosisBleeding profile = new(10f, raw, missing, inside, 60, constant: .2f);
                        Assert.AreEqual(profile.At(0) * 1.1f, actual, .000001f);
                        Assert.AreEqual(10f, s_observed, "Observer must see the native value before CE.");
                    }
                    // Normalizing again is safe and leaves native CE behavior intact.
                    Assert.IsTrue(CombatExtendedBleedingHooks.Normalize(observer, cePatch, [native], [mi]));
                }
                finally { ce.UnpatchAll(CE); observer.UnpatchAll(MI); other.UnpatchAll(OTHER); }
            }
        }

        [TestMethod]
        public void UnsupportedNativeRegistrationDoesNotRemoveAnyOverridePatch()
        {
            MethodInfo native = AccessTools.PropertyGetter(typeof(NativeInjury), nameof(NativeInjury.BleedRate));
            MethodInfo mi = AccessTools.PropertyGetter(typeof(MiInjury), nameof(MiInjury.BleedRate));
            MethodInfo patch = AccessTools.Method(typeof(BleedingHookTests), nameof(CePostfix));
            Harmony ce = new(CE);
            try
            {
                ce.Patch(mi, postfix: new HarmonyMethod(patch));
                Assert.IsFalse(CombatExtendedBleedingHooks.Normalize(new(MI), patch, [native], [mi]));
                Assert.IsTrue(Harmony.GetPatchInfo(mi).Postfixes.Any(p => p.PatchMethod == patch));
            }
            finally { ce.UnpatchAll(CE); }
        }
    }

    public class NativeInjury
    {
        public float RawModifier;
        public bool Missing, Internal;
        public virtual float BleedRate { [MethodImpl(MethodImplOptions.NoInlining)] get => 10f; }
    }
    public sealed class MiInjury : NativeInjury
    {
        public float Hemostasis;
        public override float BleedRate { [MethodImpl(MethodImplOptions.NoInlining)] get => base.BleedRate * Hemostasis; }
    }
}
namespace MoreInjuries.Integrations.CombatExtended
{ internal static class CombatExtendedBridge { internal const string CE_HARMONY_OWNER = "CombatExtended.HarmonyCE"; } }
namespace MoreInjuries.HealthConditions.VitalFailure
{
    internal struct PhysiologySnapshot
    { internal bool HoldBleedingControl; internal float BleedRate; internal int BleedingCount; internal PrognosisBleeding[]? Bleeding; }
}
