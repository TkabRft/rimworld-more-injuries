using HarmonyLib;
using MoreInjuries.HealthConditions.VitalFailure;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoreInjuries.Patches;

[HarmonyPatch(typeof(HealthCardUtility), nameof(HealthCardUtility.DrawHediffListing))]
internal static class Patch_PhysiologyDeathEstimate
{
    internal static void Postfix(Rect rect, Pawn pawn, bool __runOriginal)
    {
        if (!__runOriginal || !MoreInjuriesMod.Settings.EnablePhysiologyDeathEstimate || Current.ProgramState != ProgramState.Playing
            || VitalFailureComp.For(pawn) is not { } controller
            || !controller.TryGetPrognosis(out PhysiologySnapshot source, out PhysiologyEstimate estimate)) { return; }
        Rect footer = new(rect.x, rect.yMax - Text.LineHeight, rect.width, Text.LineHeight);
        string remaining = source.Protected ? "MI_Prognosis_Protected".Translate()
            : estimate.Ticks == int.MaxValue ? "MI_Prognosis_NoPrediction".Translate()
            : "MI_Prognosis_Time".Translate(GenDate.ToStringTicksToPeriod(estimate.Ticks));
        string label = "MI_Prognosis_Footer".Translate(source.BleedRate.ToStringPercent(), remaining);
        string cause = estimate.Cause?.LabelCap ?? "MI_Prognosis_NoCause".Translate();
        Color previous = GUI.color;
        try
        {
            GUI.color = Color.white;
            // Vanilla reserves this line even when bleeding has stopped. Leave the hediff list intact.
            Widgets.DrawBoxSolid(footer, Widgets.WindowBGFillColor);
            Widgets.Label(footer, label.Truncate(footer.width));
            TooltipHandler.TipRegion(footer, "MI_Prognosis_Tooltip".Translate(label, cause));
        }
        finally { GUI.color = previous; }
    }
}
