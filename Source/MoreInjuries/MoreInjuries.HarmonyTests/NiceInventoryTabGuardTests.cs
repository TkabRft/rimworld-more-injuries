using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInjuries.Integrations.NiceInventoryTab;
using Verse;

namespace MoreInjuries.HarmonyTests
{
    [TestClass, DoNotParallelize]
    public sealed class NiceInventoryTabGuardTests
    {
        private static int s_calls;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool UnsafeGrenadeCheck(ThingDef def)
        {
            s_calls++;
            return def.IsWeapon && def.weaponTags!.Contains("CE_AI_Grenade");
        }

        [TestMethod]
        public void RealDetourHandlesMissingTagsAndPreservesTaggedWeaponBehavior()
        {
            const string owner = "MoreInjuries.test.NiceInventoryTabGuard";
            MethodInfo target = AccessTools.Method(typeof(NiceInventoryTabGuardTests), nameof(UnsafeGrenadeCheck));
            Harmony harmony = new(owner);
            ThingDef untaggedWeapon = new() { IsWeapon = true };
            Assert.ThrowsExactly<NullReferenceException>(() => UnsafeGrenadeCheck(untaggedWeapon));
            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(NiceInventoryTabGuard), nameof(NiceInventoryTabGuard.Prefix)));
                s_calls = 0;
                Assert.IsFalse(UnsafeGrenadeCheck(untaggedWeapon));
                Assert.AreEqual(0, s_calls, "The unsafe original must not run for a missing tag list.");
                Assert.IsNull(untaggedWeapon.weaponTags, "The guard must not mutate definitions.");
                Assert.IsFalse(UnsafeGrenadeCheck(new()));
                Assert.IsFalse(UnsafeGrenadeCheck(new() { IsWeapon = true, weaponTags = [] }));
                Assert.IsFalse(UnsafeGrenadeCheck(new() { IsWeapon = true, weaponTags = ["CE_AI_Rifle"] }));
                Assert.IsTrue(UnsafeGrenadeCheck(new() { IsWeapon = true, weaponTags = ["CE_AI_Grenade"] }));
                Assert.IsFalse(UnsafeGrenadeCheck(new() { IsWeapon = false, weaponTags = ["CE_AI_Grenade"] }));
                Assert.AreEqual(4, s_calls, "Tagged definitions must still pass through the original check.");
            }
            finally
            {
                harmony.UnpatchAll(owner);
            }
            Assert.ThrowsExactly<NullReferenceException>(() => UnsafeGrenadeCheck(untaggedWeapon));
        }
    }
}

// Minimal game boundary. The production guard above is the code exercised by Harmony.
namespace Verse
{
    public sealed class ThingDef
    {
        public bool IsWeapon;
        public bool IsMedicine;
        public List<string>? weaponTags;
    }
    public static class Log
    {
        public static void Warning(string message) { }
    }
}
