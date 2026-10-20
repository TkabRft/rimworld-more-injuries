using MoreInjuries.Initialization;
using MoreInjuries.Localization;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using static MoreInjuries.MoreInjuriesSettings.Defaults;

namespace MoreInjuries;

internal static class MoreInjuriesSettingsUi
{
    internal static void Draw(Listing_Standard list, MoreInjuriesSettings settings)
    {
        SettingsUi.Section(list, "MI_Settings_General", "MI_Settings_GeneralTip", () => DrawGeneral(list, settings));
        list.Gap();
        list.Label("MI_Settings_Features".Translate());
        AI.NpcCombatMedicine.NpcMedicalSettingsUi.Draw(list, settings);
        SettingsUi.Section(list, "MI_Settings_Features_Fractures", "MI_Settings_Features_FracturesTip", () => DrawFractures(list, settings));
        SettingsUi.Section(list, "MI_Settings_Features_Respiratory", "MI_Settings_Features_RespiratoryTip", () => DrawRespiratory(list, settings));
        SettingsUi.Section(list, "MI_Settings_Features_Spalling", "MI_Settings_Features_SpallingTip", () => DrawSpalling(list, settings));
        SettingsUi.Section(list, "MI_Settings_VitalFailure_Title", "MI_Settings_VitalFailure_TitleTip", () => DrawVitalFailure(list, settings));
        SettingsUi.Section(list, "MI_Settings_Features_HypovolemicShock", "MI_Settings_Features_HypovolemicShockTip", () => DrawHypovolemicShock(list, settings));
        SettingsUi.Section(list, "MI_Settings_Features_LethalTriad", "MI_Settings_Features_LethalTriadTip", () => DrawLethalTriad(list, settings));
        SettingsUi.Section(list, "MI_Settings_Features_NeuralDamage", "MI_Settings_Features_NeuralDamageTip", () => DrawNeuralDamage(list, settings));
        SettingsUi.Section(list, "MI_Settings_Features_Concussion", "MI_Settings_Features_ConcussionTip", () => DrawConcussion(list, settings));
        SettingsUi.Section(list, "MI_Settings_Features_HemorrhagicStroke", "MI_Settings_Features_HemorrhagicStrokeTip", () => DrawHemorrhagicStroke(list, settings));
        SettingsUi.Section(list, "MI_Settings_Features_EmpDamage", "MI_Settings_Features_EmpDamageTip", () => DrawEmpDamage(list, settings));
        SettingsUi.Section(list, "MI_Settings_Features_Adrenaline", "MI_Settings_Features_AdrenalineTip", () => DrawAdrenaline(list, settings));
        SettingsUi.Section(list, "MI_Settings_Features_HydrostaticShock", "MI_Settings_Features_HydrostaticShockTip", () => DrawHydrostaticShock(list, settings));
        SettingsUi.Section(list, "MI_Settings_Features_Tourniquets", "MI_Settings_Features_TourniquetsTip", () => DrawTourniquets(list, settings));
        SettingsUi.Section(list, "MI_Settings_Features_HearingDamage", "MI_Settings_Features_HearingDamageTip", () => DrawHearingDamage(list, settings));
        SettingsUi.Section(list, "MI_Settings_Features_Misc", "MI_Settings_Features_MiscTip", () => DrawMisc(list, settings));
    }

    private static void DrawGeneral(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_General_PreventCumulativeInjuryDeathLabel".Translate(PREVENT_DEATH_BY_LETHAL_DAMAGE_THRESHOLD_DEFAULT.NamedDefault()), ref settings.PreventDeathByLethalDamageThreshold,
            "MI_Settings_General_PreventCumulativeInjuryDeathTooltip".Translate());
        list.CheckboxLabeled("MI_Settings_General_LoggingLabel".Translate(ENABLE_LOGGING_DEFAULT.NamedDefault()), ref settings.EnableLogging,
            "MI_Settings_General_LoggingTooltip".Translate());
        list.CheckboxLabeled("MI_Settings_General_LoggingVerboseLabel".Translate(ENABLE_VERBOSE_LOGGING_DEFAULT.NamedDefault()), ref settings.EnableVerboseLogging,
            "MI_Settings_General_LoggingVerboseTooltip".Translate());
        if (Find.CurrentMap is not null)
        {
            if (SettingsUi.Button(list, "MI_Settings_General_DebugAction_FixBionicsLabel".Translate(),
                "MI_Settings_General_DebugAction_FixBionicsTooltip".Translate()))
            {
                IEnumerable<Pawn> humans = Find.CurrentMap.mapPawns.AllPawns.Where(static pawn => pawn.def == ThingDefOf.Human);
                foreach (Pawn human in humans)
                {
                    FixMisplacedBionicsModExtension.FixPawn(human);
                }
            }
        }
    }

    private static void DrawFractures(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_Features_Fractures_EnableLabel".Translate(ENABLE_FRACTURES_DEFAULT.NamedDefault()), ref settings.EnableFractures,
            "MI_Settings_Features_Fractures_EnableTooltip".Translate());
        list.Label("MI_Settings_Features_Fractures_DamageThresholdLabel".Translate(settings.FractureDamageTreshold.NamedValue(), FRACTURE_DAMAGE_TRESHOLD_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Fractures_DamageThresholdTooltip".Translate());
        SettingsUi.Slider(list, ref settings.FractureDamageTreshold, 1f, 20f, "MI_Settings_Features_Fractures_DamageThresholdTooltip", ceiling: true);
        list.Label("MI_Settings_Features_Fractures_FractureChanceLabel".Translate(settings.FractureChanceOnDamage.NamedValue(), FRACTURE_CHANCE_ON_DAMAGE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Fractures_FractureChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.FractureChanceOnDamage, 0f, 1f, "MI_Settings_Features_Fractures_FractureChanceTooltip");
        list.CheckboxLabeled("MI_Settings_Features_Fractures_EnableLacerationsLabel".Translate(ENABLE_BONE_FRAGMENT_LACERATIONS_DEFAULT.NamedDefault()), ref settings.EnableBoneFragmentLacerations,
            "MI_Settings_Features_Fractures_EnableLacerationsTooltip".Translate());
        list.Label("MI_Settings_Features_Fractures_LacerationChanceLabel".Translate(settings.SplinteringFractureChance.NamedValue(), SPLINTERING_FRACTURE_CHANCE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Fractures_LacerationChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.SplinteringFractureChance, 0f, 1f, "MI_Settings_Features_Fractures_LacerationChanceTooltip");
        list.Label("MI_Settings_Features_Fractures_LacerationInjuryChanceLabel".Translate(settings.BoneFragmentLacerationChancePerBodyPart.NamedValue(), BONE_FRAGMENT_LACERATION_CHANCE_PER_BODY_PART_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Fractures_LacerationInjuryChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.BoneFragmentLacerationChancePerBodyPart, 0f, 1f, "MI_Settings_Features_Fractures_LacerationInjuryChanceTooltip");
        list.CheckboxLabeled("MI_Settings_Features_Fractures_ApplySplintJobLabel".Translate(ENABLE_APPLY_SPLINT_JOB_DEFAULT.NamedDefault()), ref settings.EnableApplySplintJob,
            "MI_Settings_Features_Fractures_ApplySplintJobTooltip".Translate());
    }

    private static void DrawRespiratory(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_Features_Respiratory_EnableChokingLabel".Translate(ENABLE_CHOKING_DEFAULT.NamedDefault()), ref settings.EnableChoking,
            "MI_Settings_Features_Respiratory_EnableChokingTooltip".Translate());
        list.CheckboxLabeled("MI_Settings_Features_Respiratory_EnableChokingSoundLabel".Translate(ENABLE_CHOKING_SOUNDS_DEFAULT.NamedDefault()), ref settings.EnableChokingSounds,
            "MI_Settings_Features_Respiratory_EnableChokingSoundTooltip".Translate());
        list.Label("MI_Settings_Features_Respiratory_ChokingMinBleedRateLabel".Translate(settings.ChokingMinimumBleedRate.NamedValue(), CHOKING_MINIMUM_BLEED_RATE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Respiratory_ChokingMinBleedRateTooltip".Translate());
        SettingsUi.Slider(list, ref settings.ChokingMinimumBleedRate, 0f, 1f, "MI_Settings_Features_Respiratory_ChokingMinBleedRateTooltip");
        list.Label("MI_Settings_Features_Respiratory_ChokingChanceLabel".Translate(settings.ChokingChanceOnDamage.NamedValue(), CHOKING_CHANCE_ON_DAMAGE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Respiratory_ChokingChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.ChokingChanceOnDamage, 0f, 1f, "MI_Settings_Features_Respiratory_ChokingChanceTooltip");
        list.Label("MI_Settings_Features_Respiratory_ChokingSuctionDeviceMinSuccessRateLabel".Translate(settings.SuctionDeviceMinimumSuccessRate.NamedValue(), SUCTION_DEVICE_MINIMUM_SUCCESS_RATE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Respiratory_ChokingSuctionDeviceMinSuccessRateTooltip".Translate());
        SettingsUi.Slider(list, ref settings.SuctionDeviceMinimumSuccessRate, 0f, 1f, "MI_Settings_Features_Respiratory_ChokingSuctionDeviceMinSuccessRateTooltip");
        list.CheckboxLabeled("MI_Settings_Features_Respiratory_EnableInhalationLabel".Translate(ENABLE_FIRE_INHALATION_DEFAULT.NamedDefault()), ref settings.EnableFireInhalation,
            "MI_Settings_Features_Respiratory_EnableInhalationTooltip".Translate());
        list.CheckboxLabeled("MI_Settings_Features_Respiratory_EnableLungCollapseLabel".Translate(ENABLE_LUNG_COLLAPSE_DEFAULT.NamedDefault()), ref settings.EnableLungCollapse,
            "MI_Settings_Features_Respiratory_EnableLungCollapseTooltip".Translate());
        list.Label("MI_Settings_Features_Respiratory_LungCollapseChanceLabel".Translate(settings.LungCollapseChanceOnThermobaricDamage.NamedValue(), LUNG_COLLAPSE_CHANCE_ON_THERMOBARIC_DAMAGE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Respiratory_LungCollapseChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.LungCollapseChanceOnThermobaricDamage, 0f, 1f, "MI_Settings_Features_Respiratory_LungCollapseChanceTooltip");
        list.Label("MI_Settings_Features_Respiratory_LungCollapseOnPerforationChanceLabel".Translate(settings.LungCollapseChanceOnPerforatingDamage.NamedValue(), LUNG_COLLAPSE_CHANCE_ON_PERFORATING_DAMAGE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Respiratory_LungCollapseOnPerforationChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.LungCollapseChanceOnPerforatingDamage, 0f, 1f, "MI_Settings_Features_Respiratory_LungCollapseOnPerforationChanceTooltip");
        list.Label("MI_Settings_Features_Respiratory_LungCollapseMaxSeverityRootLabel".Translate(settings.LungCollapseMaxSeverityRoot.NamedValue(), LUNG_COLLAPSE_MAX_SEVERITY_ROOT_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Respiratory_LungCollapseMaxSeverityRootTooltip".Translate());
        SettingsUi.Slider(list, ref settings.LungCollapseMaxSeverityRoot, 0.1f, 1f, "MI_Settings_Features_Respiratory_LungCollapseMaxSeverityRootTooltip");
    }

    private static void DrawSpalling(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_Features_Spalling_EnableLabel".Translate(ENABLE_SPALLING_DEFAULT.NamedDefault()), ref settings.EnableSpalling,
            "MI_Settings_Features_Spalling_EnableTooltip".Translate());
        list.Label("MI_Settings_Features_Spalling_DamageThresholdLabel".Translate(settings.ArmorHealthSpallingThreshold.NamedValue(), ARMOR_HEALTH_SPALLING_THRESHOLD_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Spalling_DamageThresholdTooltip".Translate());
        SettingsUi.Slider(list, ref settings.ArmorHealthSpallingThreshold, 0.1f, 1f, "MI_Settings_Features_Spalling_DamageThresholdTooltip");
        list.Label("MI_Settings_Features_Spalling_ChanceLabel".Translate(settings.SpallingChance.NamedValue(), SPALLING_CHANCE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Spalling_ChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.SpallingChance, 0f, 1f, "MI_Settings_Features_Spalling_ChanceTooltip");
    }

    private static void DrawVitalFailure(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_VitalFailure_EnableLabel".Translate(), ref settings.EnableDelayedVitalFailure,
            "MI_Settings_VitalFailure_EnableTooltip".Translate());
        list.CheckboxLabeled("MI_Prognosis_SettingLabel".Translate(), ref settings.EnablePhysiologyDeathEstimate,
            "MI_Prognosis_SettingTooltip".Translate());
    }

    private static void DrawHypovolemicShock(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_Features_HypovolemicShock_EnableLabel".Translate(ENABLE_HYPOVOLEMIC_SHOCK_DEFAULT.NamedDefault()), ref settings.EnableHypovolemicShock,
            "MI_Settings_Features_HypovolemicShock_EnableTooltip".Translate());
        list.Label("MI_Settings_Features_HypovolemicShock_HypoxiaChanceLabel".Translate(settings.OrganHypoxiaChance.NamedValue(), ORGAN_HYPOXIA_CHANCE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_HypovolemicShock_HypoxiaChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.OrganHypoxiaChance, 0f, 1f, "MI_Settings_Features_HypovolemicShock_HypoxiaChanceTooltip");
        list.Label("MI_Settings_Features_HypovolemicShock_HypoxiaReductionFactorLabel".Translate(settings.OrganHypoxiaChanceReductionFactor.NamedValue(), ORGAN_HYPOXIA_CHANCE_REDUCTION_FACTOR_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_HypovolemicShock_HypoxiaReductionFactorTooltip".Translate());
        SettingsUi.Slider(list, ref settings.OrganHypoxiaChanceReductionFactor, 0f, 1f, "MI_Settings_Features_HypovolemicShock_HypoxiaReductionFactorTooltip");
        list.CheckboxLabeled("MI_Settings_Features_HypovolemicShock_EnableCardiacArrestLabel".Translate(ENABLE_CARDIAC_ARREST_ON_HIGH_BLOOD_LOSS_DEFAULT.NamedDefault()), ref settings.EnableCardiacArrestOnHighBloodLoss,
            "MI_Settings_Features_HypovolemicShock_EnableCardiacArrestTooltip".Translate());
        list.Label("MI_Settings_Features_HypovolemicShock_CardiacArrestChanceLabel".Translate(settings.CardiacArrestChanceOnHighBloodLoss.NamedValue(), CARDIAC_ARREST_CHANCE_ON_HIGH_BLOOD_LOSS_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_HypovolemicShock_CardiacArrestChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.CardiacArrestChanceOnHighBloodLoss, 0f, 1f, "MI_Settings_Features_HypovolemicShock_CardiacArrestChanceTooltip");
        list.Label("MI_Settings_Features_HypovolemicShock_CardiacArrestDefibrillationChanceLabel".Translate(settings.DefibrillatorMinimumSuccessRate.NamedValue(), DEFIBRILLATOR_MINIMUM_SUCCESS_RATE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_HypovolemicShock_CardiacArrestDefibrillationChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.DefibrillatorMinimumSuccessRate, 0f, 1f, "MI_Settings_Features_HypovolemicShock_CardiacArrestDefibrillationChanceTooltip");
        list.Label("MI_Settings_Features_HypovolemicShock_DestroyDefibrillatorOnUseChanceLabel".Translate(settings.DefibrillatorBreakOnUseRate.NamedValue(), DEFIBRILLATOR_BREAK_ON_USE_RATE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_HypovolemicShock_DestroyDefibrillatorOnUseChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.DefibrillatorBreakOnUseRate, 0f, 1f, "MI_Settings_Features_HypovolemicShock_DestroyDefibrillatorOnUseChanceTooltip");
    }

    private static void DrawLethalTriad(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_Features_LethalTriad_EnableLabel".Translate(ENABLE_ADVANCED_TRAUMA_SIMULATION_DEFAULT.NamedDefault()), ref settings.EnableAdvancedTraumaSimulation,
            "MI_Settings_Features_LethalTriad_EnableTooltip".Translate());
        list.CheckboxLabeled("MI_Settings_Features_LethalTriad_PreventDeathByBloodLoss_Label".Translate(PREVENT_DEATH_BY_BLOOD_LOSS_DEFAULT.NamedDefault()), ref settings.PreventDeathByBloodLoss,
            "MI_Settings_Features_LethalTriad_PreventDeathByBloodLoss_Tooltip".Translate());
        list.Label("MI_Settings_Features_LethalTriad_HypoxiaAcidosisConversionFactorLabel".Translate(Math.Round(settings.HypoxiaAcidosisConversionFactor * 100f, 2).NamedValue(), Math.Round(HYPOXIA_ACIDOSIS_CONVERSION_FACTOR_DEFAULT * 100f, 2).NamedDefault()), -1,
            "MI_Settings_Features_LethalTriad_HypoxiaAcidosisConversionFactorTooltip".Translate());
        SettingsUi.Slider(list, ref settings.HypoxiaAcidosisConversionFactor, 0f, 5f, "MI_Settings_Features_LethalTriad_HypoxiaAcidosisConversionFactorTooltip", decimals: 4, displayFactor: 100f);
        list.Label("MI_Settings_Features_LethalTriad_CoagulopathySalineIvSafetyThresholdLabel".Translate(settings.IndependentCoagulopathySalineIvSafetyThreshold.NamedValue(), INDEPENDENT_COAGULOPATHY_SALINE_IV_SAFETY_THRESHOLD_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_LethalTriad_CoagulopathySalineIvSafetyThresholdTooltip".Translate());
        SettingsUi.Slider(list, ref settings.IndependentCoagulopathySalineIvSafetyThreshold, 0f, 1f, "MI_Settings_Features_LethalTriad_CoagulopathySalineIvSafetyThresholdTooltip");
    }

    private static void DrawNeuralDamage(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_Features_NeuralDamage_EnableLabel".Translate(ENABLE_NEURAL_DAMAGE_DEFAULT.NamedDefault()), ref settings.EnableNeuralDamage,
            "MI_Settings_Features_NeuralDamage_EnableTooltip".Translate());
        list.Label("MI_Settings_Features_NeuralDamage_TreatmentReductionFactorLabel".Translate(settings.NeuralDamageChanceReductionFactor.NamedValue(), NEURAL_DAMAGE_CHANCE_REDUCTION_FACTOR_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_NeuralDamage_TreatmentReductionFactorTooltip".Translate());
        SettingsUi.Slider(list, ref settings.NeuralDamageChanceReductionFactor, 0.01f, 1f, "MI_Settings_Features_NeuralDamage_TreatmentReductionFactorTooltip");
        list.CheckboxLabeled("MI_Settings_Features_NeuralDamage_EnablePersonalityShiftLabel".Translate(NEURAL_DAMAGE_ENABLE_PERSONALITY_SHIFT_DEFAULT.NamedDefault()), ref settings.NeuralDamageEnablePersonalityShift,
            "MI_Settings_Features_NeuralDamage_EnablePersonalityShiftTooltip".Translate());
    }

    private static void DrawConcussion(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_Features_Concussion_EnableLabel".Translate(ENABLE_CONCUSSION_DEFAULT.NamedDefault()), ref settings.EnableConcussion,
            "MI_Settings_Features_Concussion_EnableTooltip".Translate());
        list.Label("MI_Settings_Features_Concussion_DamageThresholdLabel".Translate(settings.ConcussionThreshold.NamedValue(), CONCUSSION_THRESHOLD_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Concussion_DamageThresholdTooltip".Translate());
        SettingsUi.Slider(list, ref settings.ConcussionThreshold, 1f, 50f, "MI_Settings_Features_Concussion_DamageThresholdTooltip", ceiling: true);
        list.Label("MI_Settings_Features_Concussion_ConcussionChanceLabel".Translate(settings.ConcussionChance.NamedValue(), CONCUSSION_CHANCE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Concussion_ConcussionChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.ConcussionChance, 0f, 1f, "MI_Settings_Features_Concussion_ConcussionChanceTooltip");
    }

    private static void DrawHemorrhagicStroke(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_Features_HemorrhagicStroke_EnableLabel".Translate(ENABLE_HEMORRHAGIC_STROKE_DEFAULT.NamedDefault()), ref settings.EnableHemorrhagicStroke,
            "MI_Settings_Features_HemorrhagicStroke_EnableTooltip".Translate());
        list.Label("MI_Settings_Features_HemorrhagicStroke_DamageThresholdLabel".Translate(settings.HemorrhagicStrokeThreshold.NamedValue(), HEMORRHAGIC_STROKE_THRESHOLD_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_HemorrhagicStroke_DamageThresholdTooltip".Translate());
        SettingsUi.Slider(list, ref settings.HemorrhagicStrokeThreshold, 1f, 50f, "MI_Settings_Features_HemorrhagicStroke_DamageThresholdTooltip", ceiling: true);
        list.Label("MI_Settings_Features_HemorrhagicStroke_StrokeChanceLabel".Translate(settings.HemorrhagicStrokeChance.NamedValue(), HEMORRHAGIC_STROKE_CHANCE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_HemorrhagicStroke_StrokeChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.HemorrhagicStrokeChance, 0f, 1f, "MI_Settings_Features_HemorrhagicStroke_StrokeChanceTooltip");
    }

    private static void DrawEmpDamage(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_Features_EmpDamage_EnableLabel".Translate(ENABLE_EMP_DAMAGE_TO_BIONICS_DEFAULT.NamedDefault()), ref settings.EnableEmpDamageToBionics,
            "MI_Settings_Features_EmpDamage_EnableTooltip".Translate());
        list.Label("MI_Settings_Features_EmpDamage_ChanceLabel".Translate(settings.EmpDamageToBionicsChance.NamedValue(), EMP_DAMAGE_TO_BIONICS_CHANCE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_EmpDamage_ChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.EmpDamageToBionicsChance, 0f, 1f, "MI_Settings_Features_EmpDamage_ChanceTooltip");
    }

    private static void DrawAdrenaline(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_Features_Adrenaline_EnableLabel".Translate(ENABLE_ADRENALINE_DEFAULT.NamedDefault()), ref settings.EnableAdrenaline,
            "MI_Settings_Features_Adrenaline_EnableTooltip".Translate());
        list.Label("MI_Settings_Features_Adrenaline_DamageThresholdLabel".Translate(settings.CertainAdrenalineThreshold.NamedValue(), CERTAIN_ADRENALINE_THRESHOLD_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Adrenaline_DamageThresholdTooltip".Translate());
        SettingsUi.Slider(list, ref settings.CertainAdrenalineThreshold, 1f, 50f, "MI_Settings_Features_Adrenaline_DamageThresholdTooltip", decimals: 0);
        list.Label("MI_Settings_Features_Adrenaline_ChanceLabel".Translate(settings.AdrenalineChanceOnDamage.NamedValue(), ADRENALINE_CHANCE_ON_DAMAGE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Adrenaline_ChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.AdrenalineChanceOnDamage, 0f, 1f, "MI_Settings_Features_Adrenaline_ChanceTooltip");
    }

    private static void DrawHydrostaticShock(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_Features_HydrostaticShock_EnableLabel".Translate(ENABLE_HYDROSTATIC_SHOCK_DEFAULT.NamedDefault()), ref settings.EnableHydrostaticShock,
            "MI_Settings_Features_HydrostaticShock_EnableTooltip".Translate());
        list.Label("MI_Settings_Features_HydrostaticShock_ChanceLabel".Translate(settings.HydrostaticShockChanceOnDamage.NamedValue(), HYDROSTATIC_SHOCK_CHANCE_ON_DAMAGE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_HydrostaticShock_ChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.HydrostaticShockChanceOnDamage, 0f, 1f, "MI_Settings_Features_HydrostaticShock_ChanceTooltip");
    }

    private static void DrawTourniquets(Listing_Standard list, MoreInjuriesSettings settings)
    {
        list.CheckboxLabeled("MI_Settings_Features_Tourniquets_EnableLabel".Translate(TOURNIQUETS_CAN_CAUSE_GANGRENE_DEFAULT.NamedDefault()), ref settings.TourniquetsCanCauseGangrene,
            "MI_Settings_Features_Tourniquets_EnableTooltip".Translate());
        list.Label("MI_Settings_Features_Tourniquets_GangreneMeanTimeLabel".Translate(
            Named.Keys.Format_TimeHours.Translate(Math.Round(settings.MeanTimeBetweenGangreneOnTourniquet / 2500f, 1).Named(Named.Params.HOURS)).NamedValue(),
            Named.Keys.Format_TimeHours.Translate(Math.Round(MEAN_TIME_BETWEEN_GANGRENE_ON_TOURNIQUET_DEFAULT / 2500f, 1).Named(Named.Params.HOURS)).NamedDefault()), -1,
            "MI_Settings_Features_Tourniquets_GangreneMeanTimeTooltip".Translate());
        SettingsUi.Slider(list, ref settings.MeanTimeBetweenGangreneOnTourniquet, 0.1f, 24f, "MI_Settings_Features_Tourniquets_GangreneMeanTimeTooltip", displayFactor: 1f / 2500f, floor: true);
        list.Label("MI_Settings_Features_Tourniquets_GangreneDryChanceLabel".Translate(settings.DryGangreneChance.NamedValue(), DRY_GANGRENE_CHANCE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Tourniquets_GangreneDryChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.DryGangreneChance, 0f, 1f, "MI_Settings_Features_Tourniquets_GangreneDryChanceTooltip");
        list.Label("MI_Settings_Features_Tourniquets_WetGangreneMeanTimeLabel".Translate(
            Named.Keys.Format_TimeDays.Translate(Math.Round(settings.DryGangreneMeanTimeToInfection / 60_000f, 1).Named(Named.Params.DAYS)).NamedValue(),
            Named.Keys.Format_TimeDays.Translate(Math.Round(DRY_GANGRENE_MEAN_TIME_TO_INFECTION_DEFAULT / 60_000f, 1).Named(Named.Params.DAYS)).NamedDefault()), -1,
            "MI_Settings_Features_Tourniquets_WetGangreneMeanTimeTooltip".Translate());
        SettingsUi.Slider(list, ref settings.DryGangreneMeanTimeToInfection, 0.1f, 15f, "MI_Settings_Features_Tourniquets_WetGangreneMeanTimeTooltip", displayFactor: 1f / 60000f, floor: true);
        list.CheckboxLabeled("MI_Settings_Features_Tourniquets_ShowGizmoLabel".Translate(ENABLE_TOURNIQUET_GIZMO_DEFAULT.NamedDefault()), ref settings.EnableTourniquetGizmo,
            "MI_Settings_Features_Tourniquets_ShowGizmoTooltip".Translate());
        list.Label("MI_Settings_Features_Tourniquets_MinBleedRateForAutoTourniquetLabel".Translate(settings.MinBleedRateForAutoTourniquet.NamedValue(), MIN_BLEED_RATE_FOR_AUTO_TOURNIQUET_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Tourniquets_MinBleedRateForAutoTourniquetTooltip".Translate());
        SettingsUi.Slider(list, ref settings.MinBleedRateForAutoTourniquet, 0.05f, 5f, "MI_Settings_Features_Tourniquets_MinBleedRateForAutoTourniquetTooltip");
    }

    private static void DrawHearingDamage(Listing_Standard list, MoreInjuriesSettings settings)
    {
        bool basicBefore = settings.EnableBasicHearingDamage;
        bool advancedBefore = settings.EnableAdvancedHearingDamage;
        list.CheckboxLabeled("MI_Settings_Features_HearingDamage_EnableLabel".Translate(ENABLE_BASIC_HEARING_DAMAGE_DEFAULT.NamedDefault()), ref settings.EnableBasicHearingDamage,
            "MI_Settings_Features_HearingDamage_EnableTooltip".Translate());
        list.CheckboxLabeled("MI_Settings_Features_HearingDamage_EnableAdvancedLabel".Translate(ENABLE_ADVANCED_HEARING_DAMAGE_DEFAULT.NamedDefault()), ref settings.EnableAdvancedHearingDamage,
            "MI_Settings_Features_HearingDamage_EnableAdvancedTooltip".Translate());
        if (basicBefore != settings.EnableBasicHearingDamage || advancedBefore != settings.EnableAdvancedHearingDamage)
        {
            settings.EnableAdvancedHearingDamage &= settings.EnableBasicHearingDamage;
        }
        list.Label("MI_Settings_Features_HearingDamage_GunshotFactorLabel".Translate(settings.HearingDamageTemporarySeverityFactor.NamedValue(), HEARING_DAMAGE_TEMPORARY_SEVERITY_FACTOR_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_HearingDamage_GunshotFactorTooltip".Translate());
        SettingsUi.Slider(list, ref settings.HearingDamageTemporarySeverityFactor, 0.01f, 1f, "MI_Settings_Features_HearingDamage_GunshotFactorTooltip");
        list.Label("MI_Settings_Features_HearingDamage_ExplosionFactorLabel".Translate(settings.HearingDamageTemporarySeverityFactorExplosions.NamedValue(), HEARING_DAMAGE_TEMPORARY_SEVERITY_FACTOR_EXPLOSIONS_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_HearingDamage_ExplosionFactorTooltip".Translate());
        SettingsUi.Slider(list, ref settings.HearingDamageTemporarySeverityFactorExplosions, 0.01f, 2f, "MI_Settings_Features_HearingDamage_ExplosionFactorTooltip");
        list.CheckboxLabeled("MI_Settings_Features_HearingDamage_PermanentEnableLabel".Translate(HEARING_DAMAGE_MAY_BECOME_PERMANENT_DEFAULT.NamedDefault()), ref settings.HearingDamageMayBecomePermanent,
            "MI_Settings_Features_HearingDamage_PermanentEnableTooltip".Translate());
        list.Label("MI_Settings_Features_HearingDamage_PermanentMaxChanceLabel".Translate(settings.HearingDamagePermanentChanceFactor.NamedValue(), HEARING_DAMAGE_PERMANENT_CHANCE_FACTOR_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_HearingDamage_PermanentMaxChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.HearingDamagePermanentChanceFactor, 0.01f, 1f, "MI_Settings_Features_HearingDamage_PermanentMaxChanceTooltip");
    }

    private static void DrawMisc(Listing_Standard list, MoreInjuriesSettings settings)
    {
        if (ModLister.BiotechInstalled)
        {
            list.CheckboxLabeled("MI_Settings_Features_Misc_IntegrationBiotechLabel".Translate(BIOTECH_ENABLE_INTEGRATION_DEFAULT.NamedDefault()), ref settings.BiotechEnableIntegration,
                "MI_Settings_Features_Misc_IntegrationBiotechTooltip".Translate());
        }
        if (ModLister.AnomalyInstalled)
        {
            list.CheckboxLabeled("MI_Settings_Features_Misc_IntegrationAnomalyLabel".Translate(ANOMALY_ENABLE_CONDITIONS_FOR_SHAMBLERS_DEFAULT.NamedDefault()), ref settings.AnomalyEnableConditionsForShamblers,
                "MI_Settings_Features_Misc_IntegrationAnomalyTooltip".Translate());
            list.CheckboxLabeled("MI_Settings_Features_Misc_AnomalyRegenerationTreatsStructuralDamageLabel".Translate(ANOMALY_REGENERATION_TREATS_STRUCTURAL_DAMAGE_DEFAULT.NamedDefault()), ref settings.AnomalyRegenerationTreatsStructuralDamage,
                "MI_Settings_Features_Misc_AnomalyRegenerationTreatsStructuralDamageTooltip".Translate());
        }
        list.Label("MI_Settings_Features_Misc_BleedingReductionFactorEnclosedLabel".Translate(settings.ClosedInternalWouldBleedingModifier.NamedValue(), CLOSED_INTERNAL_WOULD_BLEEDING_MODIFIER_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Misc_BleedingReductionFactorEnclosedTooltip".Translate());
        SettingsUi.Slider(list, ref settings.ClosedInternalWouldBleedingModifier, 0f, 1f, "MI_Settings_Features_Misc_BleedingReductionFactorEnclosedTooltip");
        list.CheckboxLabeled("MI_Settings_Features_Misc_ParalysisEnableLabel".Translate(ENABLE_PARALYSIS_DEFAULT.NamedDefault()), ref settings.EnableParalysis,
            "MI_Settings_Features_Misc_ParalysisEnableTooltip".Translate());
        list.Label("MI_Settings_Features_Misc_ParalysisDamageThresholdLabel".Translate(settings.ParalysisDamageTreshold50Percent.NamedValue(), PARALYSIS_DAMAGE_TRESHOLD_50_PERCENT_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Misc_ParalysisDamageThresholdTooltip".Translate());
        SettingsUi.Slider(list, ref settings.ParalysisDamageTreshold50Percent, 1f, 20f, "MI_Settings_Features_Misc_ParalysisDamageThresholdTooltip", decimals: 0);
        list.Label("MI_Settings_Features_Misc_IntestinalSpillChanceLabel".Translate(settings.IntestinalSpillingChanceOnDamage.NamedValue(), INTESTINAL_SPILLING_CHANCE_ON_DAMAGE_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Misc_IntestinalSpillChanceTooltip".Translate());
        SettingsUi.Slider(list, ref settings.IntestinalSpillingChanceOnDamage, 0f, 1f, "MI_Settings_Features_Misc_IntestinalSpillChanceTooltip");
        list.Label("MI_Settings_Features_Misc_BloodHarvestMinSkillLabel".Translate(settings.BloodTransfusionHarvestMinimumSkill.NamedValue(), BLOOD_TRANSFUSION_HARVEST_MINIMUM_SKILL_DEFAULT.NamedDefault()), -1,
            "MI_Settings_Features_Misc_BloodHarvestMinSkillTooltip".Translate());
        SettingsUi.Slider(list, ref settings.BloodTransfusionHarvestMinimumSkill, 0, 20, "MI_Settings_Features_Misc_BloodHarvestMinSkillTooltip");
    }
}

file static class NamedExtensions
{
    /// <summary>
    /// Returns <paramref name="obj"/> as a named argument called <c>VALUE</c>.
    /// </summary>
    public static NamedArgument NamedValue<T>(this T obj) =>
        obj.ToStringSafe().Named(Named.Params.VALUE);

    /// <summary>
    /// Returns <paramref name="obj"/> as a named argument called <c>VALUE_DEFAULT</c>.
    /// </summary>
    public static NamedArgument NamedDefault<T>(this T obj) =>
        obj.ToStringSafe().Named(Named.Params.DEFAULT);
}
