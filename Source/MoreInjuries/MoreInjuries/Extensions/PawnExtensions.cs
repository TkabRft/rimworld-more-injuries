using MoreInjuries.Roslyn.Future.ThrowHelpers;
using RimWorld;
using Verse;

namespace MoreInjuries.Extensions;

public static class PawnExtensions
{
    public static int GetMedicalSkillLevelOrDefault(this Pawn pawn, int defaultValue = 10)
    {
        if (pawn.skills?.GetSkill(SkillDefOf.Medicine) is SkillRecord skill)
        {
            return skill.Level;
        }
        if (!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
        {
            // e.g., paramedic mechanoids / non-human pawns that can be doctors
            // assume they're good at it
            return 15;
        }
        return defaultValue;
    }

    public static bool IsActivelyHostileTo(this Pawn pawn, Pawn other)
    {
        Throw.ArgumentNullException.IfNull(pawn);
        Throw.ArgumentNullException.IfNull(other);
        return !pawn.Downed && pawn.HostileTo(other.Faction);
    }

    public static bool HasOxygenDeficiencyImmunity(this Pawn pawn)
    {
        if (pawn.genes is null)
        {
            return false;
        }

        if (ModsConfig.BiotechActive && HasActiveGene(pawn, "Deathless"))
        {
            return true;
        }

        if (ModsConfig.OdysseyActive && HasActiveGene(pawn, "VacuumResistance_Total"))
        {
            return true;
        }

        return false;
    }

    private static bool HasActiveGene(Pawn pawn, string defName)
    {
        if (pawn.genes is not Pawn_GeneTracker genes)
        {
            return false;
        }
        GeneDef geneDef = DefDatabase<GeneDef>.GetNamedSilentFail(defName);
        return geneDef != null && genes.HasActiveGene(geneDef);
    }
}
