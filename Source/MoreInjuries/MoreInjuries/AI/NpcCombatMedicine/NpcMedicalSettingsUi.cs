using Verse;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static class NpcMedicalSettingsUi
{
    private static readonly string[] s_modes = ["MI_NpcMedicine_Mode0", "MI_NpcMedicine_Mode1", "MI_NpcMedicine_Mode2"];
    private static readonly string[] s_modeTips = ["MI_NpcMedicine_Mode0Tip", "MI_NpcMedicine_Mode1Tip", "MI_NpcMedicine_Mode2Tip"];
    private static readonly string[] s_selections = ["MI_NpcMedicine_Selection0", "MI_NpcMedicine_Selection1", "MI_NpcMedicine_Selection2"];
    private static readonly string[] s_selectionTips = ["MI_NpcMedicine_Selection0Tip", "MI_NpcMedicine_Selection1Tip", "MI_NpcMedicine_Selection2Tip"];

    internal static void Draw(Listing_Standard list, MoreInjuriesSettings settings)
    {
        SettingsUi.Section(list, "MI_NpcMedicine_Title", "MI_NpcMedicine_TitleTip", () => DrawContents(list, settings));
    }

    private static void DrawContents(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_NpcMedicine_Enabled".Translate(), ref settings.EnableNpcCombatMedicine, "MI_NpcMedicine_EnabledTip".Translate());
        SettingsUi.Choice(list, "MI_NpcMedicine_Mode", "MI_NpcMedicine_ModeTip", settings.NpcMedicalAiMode,
            s_modes, s_modeTips, selected => settings.NpcMedicalAiMode = selected);
        SettingsUi.Section(list, "MI_NpcMedicine_Equipment", "MI_NpcMedicine_EquipmentTip", () =>
        {
            Slider(list, "MI_NpcMedicine_Supplies", "MI_NpcMedicine_SuppliesTip", ref settings.NpcSupplyMultiplier, 0f, 2f);
            Slider(list, "MI_NpcMedicine_Medics", "MI_NpcMedicine_MedicsTip", ref settings.NpcMedicMultiplier, 0f, 2f);
            Slider(list, "MI_NpcMedicine_Scavenging", "MI_NpcMedicine_ScavengingTip", ref settings.NpcScavengingChance, 0f, .1f, percent: true);
            list.CheckboxLabeled("MI_NpcMedicine_Training".Translate(), ref settings.NpcTrainMedics, "MI_NpcMedicine_TrainingTip".Translate());
        });
        SettingsUi.Section(list, "MI_NpcMedicine_Care", "MI_NpcMedicine_CareTip", () =>
        {
            list.CheckboxLabeled("MI_NpcMedicine_Own".Translate(), ref settings.NpcTreatOwnFaction, "MI_NpcMedicine_OwnTip".Translate());
            list.CheckboxLabeled("MI_NpcMedicine_Friendly".Translate(), ref settings.NpcTreatFriendly, "MI_NpcMedicine_FriendlyTip".Translate());
            list.CheckboxLabeled("MI_NpcMedicine_Neutral".Translate(), ref settings.NpcTreatNeutral, "MI_NpcMedicine_NeutralTip".Translate());
            list.CheckboxLabeled("MI_NpcMedicine_Player".Translate(), ref settings.NpcTreatPlayer, "MI_NpcMedicine_PlayerTip".Translate());
        });
        SettingsUi.Section(list, "MI_NpcMedicine_Drugs", "MI_NpcMedicine_DrugsTip", () =>
        {
            list.CheckboxLabeled("MI_NpcMedicine_PainCare".Translate(), ref settings.NpcPainCare, "MI_NpcMedicine_PainCareTip".Translate());
            Slider(list, "MI_NpcMedicine_PainThreshold", "MI_NpcMedicine_PainThresholdTip", ref settings.NpcPainThreshold, 0f, 1f, percent: true);
            list.CheckboxLabeled("MI_NpcMedicine_Boosts".Translate(), ref settings.NpcPerformanceDrugs, "MI_NpcMedicine_BoostsTip".Translate());
            SettingsUi.Choice(list, "MI_NpcMedicine_Selection", "MI_NpcMedicine_SelectionTip", settings.NpcDrugSelection,
                s_selections, s_selectionTips, selected => settings.NpcDrugSelection = selected);
            Slider(list, "MI_NpcMedicine_Prevalence", "MI_NpcMedicine_PrevalenceTip", ref settings.NpcPerformancePrevalence, 0f, 2f);
            Slider(list, "MI_NpcMedicine_UseChance", "MI_NpcMedicine_UseChanceTip", ref settings.NpcPerformanceUseChance, 0f, 1f, percent: true);
            Integer(list, "MI_NpcMedicine_Maximum", "MI_NpcMedicine_MaximumTip", ref settings.NpcPerformanceMaximum, 3);
            Integer(list, "MI_NpcMedicine_PerformanceInterval", "MI_NpcMedicine_PerformanceIntervalTip", ref settings.NpcPerformanceInterval, 60000);
            Integer(list, "MI_NpcMedicine_ClinicalInterval", "MI_NpcMedicine_ClinicalIntervalTip", ref settings.NpcClinicalInterval, 2500);
        });
    }

    private static void Slider(Listing_Standard list, string key, string tooltipKey, ref float value, float min, float max, bool percent = false)
    {
        string display = percent ? value.ToStringPercent() : value.ToString("0.##") + "\u00D7";
        list.Label(key.Translate() + ": " + display, -1f, tooltipKey.Translate());
        SettingsUi.Slider(list, ref value, min, max, tooltipKey);
    }

    private static void Integer(Listing_Standard list, string key, string tooltipKey, ref int value, int max)
    {
        list.Label(key.Translate() + ": " + value.ToString(), -1f, tooltipKey.Translate());
        SettingsUi.Slider(list, ref value, 0, max, tooltipKey);
    }
}
