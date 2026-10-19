using System.Runtime.CompilerServices;
using Verse;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static class NpcMedicalSafety
{
    private sealed class Impact { internal int Tick; }
    private static readonly ConditionalWeakTable<Pawn, Impact> s_impacts = new();

    internal static void NotifyDamage(Pawn pawn, DamageInfo damage, float amount)
    {
        if (!MoreInjuriesMod.Settings.EnableNpcCombatMedicine || amount <= 0f || pawn.Map is null
            || !damage.Def.ExternalViolenceFor(pawn) || !(damage.Def.isRanged || damage.Def.isExplosive)) { return; }
        s_impacts.GetOrCreateValue(pawn).Tick = Find.TickManager.TicksGame;
    }

    internal static bool Recent(Pawn pawn) => s_impacts.TryGetValue(pawn, out Impact? impact)
        && Find.TickManager.TicksGame - impact.Tick < 600;

    internal static bool Allowed(Pawn doctor, Pawn patient, Thing? supply)
    {
        if (NpcMedicalCompatibility.SafetyOwned(doctor)) { return false; }
        if (!Recent(doctor) && !Recent(patient)) { return true; }
        bool pocket = supply is null || supply.ParentHolder == doctor.inventory || supply == doctor.carryTracker.CarriedThing;
        return pocket && (doctor == patient || NpcMedicalAi.Urgency(patient) >= 90f
            && patient.Position.InHorDistOf(doctor.Position, 1.5f));
    }
}
