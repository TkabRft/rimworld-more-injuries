using RimWorld;
using UnityEngine;
using Verse;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static class NpcMedicalSettingsUi
{
    private static bool s_open, s_equipment, s_care, s_drugs;
    private static readonly string[] s_modes = ["MI_NpcMedicine_Mode0", "MI_NpcMedicine_Mode1", "MI_NpcMedicine_Mode2"];
    private static readonly string[] s_selections = ["MI_NpcMedicine_Selection0", "MI_NpcMedicine_Selection1", "MI_NpcMedicine_Selection2"];
    internal static void Draw(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.GapLine();
        if (list.ButtonText("MI_NpcMedicine_Title".Translate())) { s_open = !s_open; }
        if (!s_open) { return; }
        settings.NpcMedicalAiMode = Mathf.Clamp(settings.NpcMedicalAiMode, 0, 2);
        settings.NpcDrugSelection = Mathf.Clamp(settings.NpcDrugSelection, 0, 2);
        Check(list, "MI_NpcMedicine_Enabled", ref settings.EnableNpcCombatMedicine, "MI_NpcMedicine_EnabledTip");
        if (list.ButtonText("MI_NpcMedicine_Mode".Translate() + ": " + s_modes[settings.NpcMedicalAiMode].Translate()))
        {
            Find.WindowStack.Add(new FloatMenu([
                new FloatMenuOption("MI_NpcMedicine_Mode0".Translate(), () => settings.NpcMedicalAiMode = 0),
                new FloatMenuOption("MI_NpcMedicine_Mode1".Translate(), () => settings.NpcMedicalAiMode = 1),
                new FloatMenuOption("MI_NpcMedicine_Mode2".Translate(), () => settings.NpcMedicalAiMode = 2)]));
        }
        if (list.ButtonText("MI_NpcMedicine_Equipment".Translate())) { s_equipment = !s_equipment; }
        if (s_equipment)
        {
            Slider(list, "MI_NpcMedicine_Supplies", ref settings.NpcSupplyMultiplier, 0f, 2f);
            Slider(list, "MI_NpcMedicine_Medics", ref settings.NpcMedicMultiplier, 0f, 2f);
            Slider(list, "MI_NpcMedicine_Scavenging", ref settings.NpcScavengingChance, 0f, .1f, percent: true);
            Check(list, "MI_NpcMedicine_Training", ref settings.NpcTrainMedics, "MI_NpcMedicine_TrainingTip");
        }
        if (list.ButtonText("MI_NpcMedicine_Care".Translate())) { s_care = !s_care; }
        if (s_care)
        {
            Check(list, "MI_NpcMedicine_Own", ref settings.NpcTreatOwnFaction, "MI_NpcMedicine_OwnTip");
            Check(list, "MI_NpcMedicine_Friendly", ref settings.NpcTreatFriendly, "MI_NpcMedicine_FriendlyTip");
            Check(list, "MI_NpcMedicine_Neutral", ref settings.NpcTreatNeutral, "MI_NpcMedicine_NeutralTip");
            Check(list, "MI_NpcMedicine_Player", ref settings.NpcTreatPlayer, "MI_NpcMedicine_PlayerTip");
        }
        if (list.ButtonText("MI_NpcMedicine_Drugs".Translate())) { s_drugs = !s_drugs; }
        if (s_drugs)
        {
            Check(list, "MI_NpcMedicine_Boosts", ref settings.NpcPerformanceDrugs, "MI_NpcMedicine_BoostsTip");
            if (list.ButtonText("MI_NpcMedicine_Selection".Translate() + ": " + s_selections[settings.NpcDrugSelection].Translate()))
            { settings.NpcDrugSelection = (settings.NpcDrugSelection + 1) % 3; }
            Slider(list, "MI_NpcMedicine_Prevalence", ref settings.NpcPerformancePrevalence, 0f, 2f);
            Slider(list, "MI_NpcMedicine_UseChance", ref settings.NpcPerformanceUseChance, 0f, 1f, percent: true);
            Integer(list, "MI_NpcMedicine_Maximum", ref settings.NpcPerformanceMaximum, 3);
            Integer(list, "MI_NpcMedicine_PerformanceInterval", ref settings.NpcPerformanceInterval, 60000);
            Integer(list, "MI_NpcMedicine_ClinicalInterval", ref settings.NpcClinicalInterval, 2500);
        }
    }

    private static void Check(Listing_Standard list, string key, ref bool value, string tip)
        => list.CheckboxLabeled(key.Translate(), ref value, tip.Translate());
    private static void Slider(Listing_Standard list, string key, ref float value, float min, float max, bool percent = false)
    {
        string display = percent ? value.ToStringPercent() : value.ToString("0.##") + "×";
        list.Label(key.Translate() + ": " + display);
        value = (float)System.Math.Round(list.Slider(Mathf.Clamp(value, min, max), min, max), 2);
    }
    private static void Integer(Listing_Standard list, string key, ref int value, int max)
    {
        list.Label(key.Translate() + ": " + value);
        value = Mathf.RoundToInt(list.Slider(Mathf.Clamp(value, 0, max), 0, max));
    }
}
