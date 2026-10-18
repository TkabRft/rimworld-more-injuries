// Native boundaries used by production-linked anatomy and DLC tests.
namespace Verse
{
    public class DamageDef : Def;
    public sealed partial class Pawn
    {
        public bool Suspended;
        public bool IsMutant;
        public RimWorld.Pawn_MutantTracker mutant = new();
        public PlayerSettings? playerSettings;
    }
    public sealed class PlayerSettings { public RimWorld.MedicalCareCategory medCare = RimWorld.MedicalCareCategory.Best; }
}
namespace RimWorld
{
    [System.AttributeUsage(System.AttributeTargets.Class)] public sealed class DefOfAttribute : System.Attribute;
    public static class DefOfHelper { public static void EnsureInitializedInCtor(System.Type type) { } }
    public enum MedicalCareCategory { NoCare, NoMeds, HerbalOrWorse, NormalOrWorse, Best }
    public sealed class MutantDef { public bool breathesAir = true, entitledToMedicalCare = true; }
    public sealed class Pawn_MutantTracker { public MutantDef Def = new(); public void Turn() { } public void Revert() { } }
    public sealed class Pawn_GeneTracker { }
}
