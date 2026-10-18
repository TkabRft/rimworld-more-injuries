using Verse;
using Verse.AI;

namespace Verse
{
    public class Hediff_AddedPart : Hediff;
    public static class ModLister { public static bool BiotechInstalled => ModsConfig.BiotechActive; }
    public sealed partial class Pawn { public JobTracker jobs = new(); }
    public sealed partial class JobTracker { public void TryTakeOrderedJob(Job job) { } }
}
namespace Verse.AI
{
    public sealed partial class Job { public object def = new(); public int count; public object? source; }
    public static class JobMaker { public static Job MakeJob(object def, params object[] targets) => new() { def = def }; }
}
namespace UnityEngine
{
    public static partial class Mathf
    {
        public static float Exp(float value) => MathF.Exp(value);
        public static float Max(float a, float b) => Math.Max(a, b);
    }
}
namespace MoreInjuries.AI.Audio
{
    public interface ISoundDefProvider<T>;
    public static class CachedSoundDefProvider
    {
        private sealed class Provider<T> : ISoundDefProvider<T>;
        public static ISoundDefProvider<T> Of<T>(object def) => new Provider<T>();
    }
}
namespace MoreInjuries.AI.TreatmentModifiers
{
    public static class TreatmentTestExtensions { public static float GetTreatmentEffectivenessModifier(this Hediff hediff, object job) => 1f; }
}
namespace MoreInjuries.AI.Jobs
{
    public interface IJobDescriptor { Job CreateJob(); void StartJob(); }
    public abstract partial class JobDriver_UseMedicalDevice
    {
        protected Job job = new();
        protected virtual ThingDef DeviceDef => new();
        protected virtual Audio.ISoundDefProvider<Pawn> SoundDefProvider => null!;
        protected virtual int BaseTendDuration => 0;
        protected virtual bool IsTreatable(Hediff hediff) => true;
        protected virtual int GetMedicalDeviceCountToFullyHeal(Pawn patient) => 0;
        public bool TestApply(Pawn doctor, Pawn patient, Thing? device = null) => ApplyDevice(doctor, patient, device);
    }
    public abstract class JobDriver_UseMedicalDevice_TargetsHediffDefs : JobDriver_UseMedicalDevice
    {
        protected abstract HediffDef[] HediffDefs { get; }
    }
    public sealed class ExtendedJobParameters
    {
        public static T Create<T>(Pawn doctor, bool fromInventoryOnly) where T : new() => new();
    }
}
namespace MoreInjuries.Things
{
    public static class ReusabilityUtility { public static void TryDestroyReusableIngredient(Thing device, Pawn doctor) { } }
}
namespace MoreInjuries.Defs.WellKnown
{
    public static class KnownThingDefOf { public static readonly ThingDef Defibrillator = new(); }
    public static class KnownSoundDefOf { public static readonly object PerformCpr = new(); public static readonly object Defibrillator = new(); }
    public static class KnownJobDefOf { public static readonly object PerformCpr = new(); public static readonly object UseDefibrillator = new(); }
}
namespace RimWorld
{
    public static class BodyPartDefOf { public static readonly BodyPartDef Heart = new(); }
}
