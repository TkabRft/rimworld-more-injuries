// Harmony installation is recorded here, not executed. The CE fixture invokes the recorded observer
// before its multiplier. Actual detours and GUI acceptance still require the two mods in-game.
using System.Reflection;
using System.Reflection.Emit;
using MoreInjuries.Integrations.CombatExtended;
using MoreInjuries.LocalizationTests.RuntimeContracts;
using Verse;

namespace HarmonyLib
{
    public static class Priority { public const int First = 800; }
    public sealed class HarmonyMethod(Type type, string name)
    {
        public MethodInfo method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
        public string[] before = [];
        public int priority;
    }
    public sealed record TestPatch(string owner, HarmonyMethod Patch)
    { public MethodInfo PatchMethod => Patch.method; }
    public sealed class TestPatchInfo { public List<TestPatch> Postfixes = []; }
    public sealed class Harmony(string id)
    {
        public static readonly Dictionary<MethodBase, TestPatchInfo> Patches = [];
        public static bool RegisteredCE = true;
        public static TestPatchInfo GetPatchInfo(MethodBase method)
        {
            if (!Patches.TryGetValue(method, out TestPatchInfo? patches)) { Patches[method] = patches = new(); }
            if (RegisteredCE && method.Name == "get_BleedRate" && !patches.Postfixes.Any(patch => patch.owner == CombatExtendedBridge.CE_HARMONY_OWNER))
            { patches.Postfixes.Add(new(CombatExtendedBridge.CE_HARMONY_OWNER, new(typeof(CombatExtended.HarmonyCE.Harmony_HediffWithComps_BleedRate_Patch), "Postfix"))); }
            return patches;
        }
        public void Patch(MethodBase original, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null)
        { if (postfix is not null) { GetPatchInfo(original).Postfixes.Add(new(id, postfix)); } }
        public void UnpatchAll(string owner)
        { foreach (TestPatchInfo patches in Patches.Values) { patches.Postfixes.RemoveAll(patch => patch.owner == owner); } }
        public void Unpatch(MethodBase original, MethodInfo patch) => GetPatchInfo(original).Postfixes.RemoveAll(entry => entry.PatchMethod == patch);
    }
    public static partial class AccessTools
    {
        public static bool UnsupportedCE;
        public static Type TypeByName(string name) => name == "CombatExtended.HediffComp_Stabilize"
            ? UnsupportedCE ? typeof(HediffComp) : typeof(CEStabilizeFixture)
            : typeof(AccessTools).Assembly.GetType(name)!;
        public static MethodInfo? Method(Type type, string name, Type[] parameters)
            => type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, parameters, null);
        public static FieldRef<object, F> FieldRefAccess<F>(Type type, string name)
        {
            FieldInfo field = Field(type, name) ?? throw new MissingFieldException(name);
            DynamicMethod method = new("TestFieldAccess", typeof(F).MakeByRefType(), [typeof(object)], typeof(AccessTools), true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, type); il.Emit(OpCodes.Ldflda, field); il.Emit(OpCodes.Ret);
            return (FieldRef<object, F>)method.CreateDelegate(typeof(FieldRef<object, F>));
        }
    }
}
namespace CombatExtended.HarmonyCE
{
    internal static class Harmony_HediffWithComps_BleedRate_Patch
    { internal static void Postfix(Hediff instance, ref float result) { } }
}
namespace MoreInjuries.HealthConditions.HeavyBleeding.Overrides
{
    internal interface IStatefulInjury { IInjuryState State { get; } }
    internal interface IInjuryState { bool IsTemporarilyCoagulated { get; } float CoagulationMultiplier { get; } }
}
namespace Verse
{
    public enum BodyPartDepth { Outside, Inside }
    public class Hediff_MissingPart : CEWoundFixture;
    public static class CETestTypes
    {
        public static IEnumerable<Type> AllSubclassesNonAbstract(this Type type) => [typeof(CEWoundFixture)];
    }
}
namespace RimWorld { public sealed class Medicine : Thing { public float Potency = 1f; } }
namespace MoreInjuries.LocalizationTests.RuntimeContracts
{
    public class CEWoundFixture : HediffWithComps
    {
        public float UntreatedRate;
        public CEStabilizeFixture? Stabilization;
        public override float BleedRate
        {
            get
            {
                float rate = Tended || Severity <= 0 ? 0 : UntreatedRate;
                CombatExtendedBridge.ObserveBleeding(this, rate);
                return rate * (Stabilization?.BleedModifier ?? 1f);
            }
        }
    }
    // CE's state transitions expressed independently of the closed-form numeric projection.
    public sealed class CEStabilizeFixture : HediffComp
    {
        private bool stabilized;
        private float bleedModifier = 1f;
        public bool Stabilized => stabilized;
        public float RawModifier => bleedModifier;
        public float InitialModifier;
        public float BleedModifier => Math.Clamp((parent is Hediff_MissingPart ? bleedModifier * .5f : bleedModifier)
            + (parent.Part?.depth == BodyPartDepth.Inside ? .2f : 0f), 0f, 1f);
        public void Stabilize(Pawn medic, RimWorld.Medicine medicine)
        {
            if (stabilized) { return; }
            bleedModifier = InitialModifier; stabilized = true;
            CombatExtendedBridge.Changed(this);
        }
        public void CompPostTickInterval(ref float adjustment, int delta)
        {
            CombatExtendedBridge.BeforeTick(this, out float previous);
            if (stabilized && bleedModifier < 1 && parent.pawn.IsHashIntervalTick(60, delta))
            { bleedModifier += .01f; if (bleedModifier >= 1) { bleedModifier = 1; } }
            CombatExtendedBridge.AfterTick(this, previous);
        }
    }
}
