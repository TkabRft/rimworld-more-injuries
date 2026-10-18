using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.BrainDamage;
using MoreInjuries.HealthConditions.HypovolemicShock;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

// Authored gameplay advice lives in Lang_PrognosisHints.xml. Selection uses the cached UI snapshot only.
internal static class PrognosisRescueHints
{
    internal static string Tooltip(in PhysiologySnapshot source, in PhysiologyEstimate estimate)
    {
        if (source.NativeComaProtection || source.Protected && source.ProtectionTicks <= 0) { return "MI_Prognosis_Protected".Translate(); }
        if (estimate.CapacityLabel is not null)
        { return "MI_Prognosis_CapacityFailure".Translate(estimate.CapacityLabel); }
        if (estimate.Ticks == int.MaxValue || estimate.Cause is null) { return "MI_Prognosis_NoCause".Translate(); }
        string cause = estimate.Cause.LabelCap;
        if (estimate.Cause == KnownHediffDefOf.BrainDamage_Hypoxia
            && source.ClinicalBrain >= VitalFailureDefOf.MI_VitalFailureConfig.TerminalCerebralSeverity)
        { return "MI_Prognosis_Tooltip".Translate(cause, "MI_PrognosisHint_Terminal".Translate()); }
        if (estimate.Cause == NeurologicalDefOf.MI_ArtificialComa && !source.CanWakeUp)
        { return "MI_Prognosis_Tooltip".Translate(cause, "MI_PrognosisHint_WakeUpRestricted".Translate()); }
        List<string> hints = [];
        if (estimate.Cause == KnownHediffDefOf.BrainDamage_Hypoxia || estimate.Cause == HediffDefOf.BloodLoss)
        {
            if (source.BleedRate >= .1f) { hints.Add("MI_PrognosisHint_Bleeding".Translate()); }
            if (source.BloodLoss > .45f || source.Dilution > .45f || estimate.Cause == HediffDefOf.BloodLoss)
            { hints.Add("MI_PrognosisHint_Blood".Translate()); }
            if (estimate.Cause == KnownHediffDefOf.BrainDamage_Hypoxia)
            {
                if (source.ShockPresent && source.CanDevelopShock
                    && ShockPerfusionModel.EffectiveShock(source.Shock, source.BloodLoss) > .5f)
                { hints.Add("MI_PrognosisHint_Shock".Translate()); }
                if (source.Pumping < .5f) { AddHeart(in source, hints); }
                if (source.Breathing < .5f) { AddBreathing(in source, hints); }
            }
        }
        else if (estimate.Cause == KnownHediffDefOf.CardiacArrest) { AddHeart(in source, hints); }
        else if (estimate.Cause == VitalFailureDefOf.MI_RespiratoryFailure) { AddBreathing(in source, hints); }
        else if (estimate.Cause == VitalFailureDefOf.MI_LiverFailure) { hints.Add("MI_PrognosisHint_Liver".Translate()); }
        else if (estimate.Cause == VitalFailureDefOf.MI_RenalFailure) { hints.Add("MI_PrognosisHint_Kidney".Translate()); }
        else if (estimate.Cause == VitalFailureDefOf.MI_FiltrationFailure) { hints.Add("MI_PrognosisHint_Filtration".Translate()); }
        else if (estimate.Cause == VitalFailureDefOf.MI_MetabolicFailure) { hints.Add("MI_PrognosisHint_Metabolism".Translate()); }
        else if (estimate.Cause == VitalFailureDefOf.MI_Coma) { hints.Add("MI_PrognosisHint_Consciousness".Translate()); }
        else if (estimate.Cause == NeurologicalDefOf.MI_ArtificialComa) { hints.Add("MI_PrognosisHint_WakeUp".Translate()); }
        if (hints.Count == 0)
        {
            hints.Add((estimate.Cause == KnownHediffDefOf.BrainDamage_Hypoxia
                ? "MI_PrognosisHint_Oxygen" : "MI_PrognosisHint_Underlying").Translate());
        }
        return "MI_Prognosis_Tooltip".Translate(cause, "MI_PrognosisHint_Advice".Translate(string.Join("; ", hints)));
    }

    private static void AddHeart(in PhysiologySnapshot source, List<string> hints)
        => hints.Add((source.Arrest && source.RestartableHeart
            ? source.EarlyArrest ? "MI_PrognosisHint_Defibrillate" : "MI_PrognosisHint_Cpr"
            : "MI_PrognosisHint_Heart").Translate());

    private static void AddBreathing(in PhysiologySnapshot source, List<string> hints)
    {
        if (source.Choking) { hints.Add("MI_PrognosisHint_Airway".Translate()); }
        if (source.CollapsedLung)
        { hints.Add((source.LungNeedsSurgery ? "MI_PrognosisHint_Lung" : "MI_PrognosisHint_TendLung").Translate()); }
        if (!source.Choking && !source.CollapsedLung) { hints.Add("MI_PrognosisHint_Breathing".Translate()); }
    }
}
