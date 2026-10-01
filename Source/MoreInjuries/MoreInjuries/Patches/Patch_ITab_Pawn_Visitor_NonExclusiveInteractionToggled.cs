using HarmonyLib;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.HeavyBleeding.Transfusions;
using RimWorld;
using Verse;

namespace MoreInjuries.Patches;

[HarmonyPatch(typeof(ITab_Pawn_Visitor), nameof(ITab_Pawn_Visitor.NonExclusiveInteractionToggled))]
public static class Patch_ITab_Pawn_Visitor_NonExclusiveInteractionToggled
{
    internal static void Postfix(PrisonerInteractionModeDef mode, bool enabled)
    {
        Logger.LogDebug($"ITab_Pawn_Visitor.NonExclusiveInteractionToggled: {mode.defName} {enabled}");

        if (Find.Selector.SingleSelectedThing is not Pawn { Spawned: true } pawn)
        {
            return;
        }
        if (enabled)
        {
            if (mode == KnownPrisonerInteractionModeDefOf.BloodBagFarm)
            {
                if (!KnownResearchProjectDefOf.BasicFirstAid.IsFinished)
                {
                    Messages.Message("MI_Message_ResearchRequired".Translate(mode.label, KnownResearchProjectDefOf.BasicFirstAid.label), pawn, MessageTypeDefOf.RejectInput);
                    pawn.guest.ToggleNonExclusiveInteraction(mode, enabled: false);
                    return;
                }
                if (ModsConfig.BiotechActive)
                {
                    DisableOtherInteraction(pawn, mode, PrisonerInteractionModeDefOf.HemogenFarm, RecipeDefOf.ExtractHemogenPack);
                }
            }
            else if (ModsConfig.BiotechActive && mode == PrisonerInteractionModeDefOf.HemogenFarm)
            {
                DisableOtherInteraction(pawn, mode, KnownPrisonerInteractionModeDefOf.BloodBagFarm, KnownRecipeDefOf.ExtractWholeBloodBag);
            }
            else
            {
                return;
            }
        }
        if (mode != KnownPrisonerInteractionModeDefOf.BloodBagFarm)
        {
            return;
        }
        Bill? bill = pawn.BillStack?.Bills?.Find(static b => b.recipe == KnownRecipeDefOf.ExtractWholeBloodBag);
        if (enabled)
        {
            if (bill is not null || !Recipe_ExtractBloodBag.CanSafelyBeQueued(pawn))
            {
                return;
            }
            HealthCardUtility.CreateSurgeryBill(pawn, KnownRecipeDefOf.ExtractWholeBloodBag, part: null!);
        }
        else
        {
            if (bill is null)
            {
                return;
            }
            pawn.BillStack!.Bills.Remove(bill);
        }
    }

    private static void DisableOtherInteraction(Pawn pawn, PrisonerInteractionModeDef mode, PrisonerInteractionModeDef otherMode, RecipeDef otherRecipe)
    {
        if (!pawn.guest.IsInteractionEnabled(otherMode))
        {
            return;
        }
        Messages.Message("MI_Message_OptionMutuallyExclusive".Translate(mode.label, otherMode.label), pawn, MessageTypeDefOf.RejectInput);
        pawn.guest.ToggleNonExclusiveInteraction(otherMode, enabled: false);
        pawn.BillStack?.Bills?.RemoveAll(bill => bill.recipe == otherRecipe);
    }
}
