using HarmonyLib;
using MoreInjuries.HealthConditions.VitalFailure;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoreInjuries.Patches;

[HarmonyPatch(typeof(HealthCardUtility), nameof(HealthCardUtility.DrawHediffListing))]
internal static class Patch_PhysiologyDeathEstimate
{
    private static string ShortTime(int ticks)
        => ticks >= 60000 ? "MI_Prognosis_DaysShort".Translate((ticks / 60000f).ToString("0.#"))
            : ticks >= 2500 ? "MI_Prognosis_HoursShort".Translate((ticks / 2500f).ToString("0.#"))
            : "MI_Prognosis_MinutesShort".Translate(System.Math.Max(1, (int)System.Math.Ceiling(ticks * 60f / 2500f)));

    internal static void Postfix(Rect rect, Pawn pawn, bool __runOriginal)
    {
        if (!__runOriginal || !MoreInjuriesMod.Settings.EnablePhysiologyDeathEstimate || Current.ProgramState != ProgramState.Playing
            || Find.MainTabsRoot.OpenTab != MainButtonDefOf.Inspect
            || Find.MainTabsRoot.OpenTab.TabWindow is not MainTabWindow_Inspect { OpenTabType: { } tabType }
            || tabType != typeof(ITab_Pawn_Health) || Find.Selector.SingleSelectedThing != pawn
            || VitalFailureComp.For(pawn) is not { } controller
            || !controller.TryGetPrognoses(out PhysiologySnapshot source, out PhysiologyEstimate held, out PhysiologyEstimate estimate)) { return; }
        if (source.BleedRate <= 0f && estimate.Ticks == int.MaxValue && held.Ticks == int.MaxValue && !source.TemporaryBleedingRisk) { return; }
        Rect footer = new(rect.x, rect.yMax - Text.LineHeight, rect.width, Text.LineHeight);
        string Status(PhysiologyEstimate value, bool uncertain) => source.NativeComaProtection || source.Protected && source.ProtectionTicks <= 0
            ? "MI_Prognosis_ProtectedShort".Translate() : uncertain && value.Ticks == int.MaxValue ? "MI_Prognosis_UnknownShort".Translate()
            : value.Ticks == int.MaxValue ? "MI_Prognosis_BeyondHorizon".Translate()
            : ShortTime(value.Ticks);
        Color previous = GUI.color;
        try
        {
            GUI.color = Color.white;
            // Vanilla reserves this line even when bleeding has stopped. Leave the hediff list intact.
            Widgets.DrawBoxSolid(footer, Widgets.WindowBGFillColor);
            float start = footer.x, width = footer.width;
            if (source.BleedRate > 0f)
            {
                Rect rate = new(start, footer.y, width * .28f, footer.height);
                Widgets.Label(rate, "MI_Prognosis_BleedingShort".Translate(source.BleedRate.ToStringPercent()).ToString().Truncate(rate.width));
                start += rate.width; width -= rate.width;
            }
            Rect current = new(start, footer.y, width * .4f, footer.height);
            Rect future = new(start + current.width, footer.y, width - current.width, footer.height);
            Widgets.Label(current, "MI_Prognosis_HeldShort".Translate(Status(held, false)).ToString().Truncate(current.width));
            Widgets.Label(future, "MI_Prognosis_UntendedShort".Translate(Status(estimate, source.BleedingUncertain)).ToString().Truncate(future.width));
            string tooltip = controller.PrognosisTooltip;
            if (source.BleedRate > 0f)
            { tooltip = "MI_Prognosis_Footer".Translate(source.BleedRate.ToStringPercent(),
                "MI_Prognosis_Scenarios".Translate(Status(held, false), Status(estimate, source.BleedingUncertain))) + "\n" + tooltip; }
            TooltipHandler.TipRegion(footer, tooltip);
        }
        finally { GUI.color = previous; }
    }
}
