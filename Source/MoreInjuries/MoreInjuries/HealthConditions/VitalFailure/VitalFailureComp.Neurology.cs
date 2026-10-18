using MoreInjuries.Defs.WellKnown;
using MoreInjuries.Extensions;
using MoreInjuries.HealthConditions.BrainDamage;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

public sealed partial class VitalFailureComp
{
    private int _milestoneVersion = 1;
    private List<NeurologicalEpisode>? _brainEpisodes;
    private bool HasNeurologicalEpisodes => _brainEpisodes is { Count: > 0 };

    private void ExposeNeurologicalEpisodes()
    {
        Scribe_Values.Look(ref _milestoneVersion, "neurologicalMilestoneVersion", 0);
        Scribe_Collections.Look(ref _brainEpisodes, "neurologicalMilestoneStates", LookMode.Deep);
    }

    internal void InitializeLegacyMilestones()
    {
        if (_milestoneVersion >= 1) { return; }
        BeginNeurologicalScan();
        foreach (Hediff injury in Patient.health.hediffSet.hediffs) { GatherNeurologicalInjury(injury); }
        ApplyNeurologicalMilestones(award: false);
        _milestoneVersion = 1;
    }

    private void BeginNeurologicalScan()
    {
        if (_brainEpisodes is null) { return; }
        foreach (NeurologicalEpisode episode in _brainEpisodes) { episode.Present = false; episode.ClinicalInjury = 0f; }
    }

    private NeurologicalEpisode Episode(BodyPartRecord part, bool hypoxic)
    {
        _brainEpisodes ??= [];
        for (int i = 0; i < _brainEpisodes.Count; i++)
        {
            NeurologicalEpisode episode = _brainEpisodes[i];
            if (episode.Part == part && episode.Hypoxic == hypoxic) { return episode; }
        }
        NeurologicalEpisode created = new() { Part = part, Hypoxic = hypoxic };
        _brainEpisodes.Add(created);
        return created;
    }

    private void GatherNeurologicalInjury(Hediff injury)
    {
        if (injury is not Hediff_Injury || injury.Part is not { } part
            || part.def.tags?.Contains(BodyPartTagDefOf.ConsciousnessSource) != true) { return; }
        bool hypoxic = injury.def == KnownHediffDefOf.BrainDamage_Hypoxia;
        NeurologicalEpisode episode = Episode(part, hypoxic);
        episode.Present = true;
        episode.ClinicalInjury += hypoxic ? CerebralInjury.ClinicalSeverity(injury) : injury.Severity / CerebralInjury.Scale(Patient, part);
        if (hypoxic && episode.Outcome is null) { episode.Profile = NeurologicalOutcomes.SourceProfile(injury); }
        else { episode.Profile ??= NeurologicalDefOf.MI_NeurologicalConfig.DefaultProfile; }
    }

    internal void RecordBrainTrauma(BodyPartRecord part, DamageDef damage)
    {
        if (Scribe.mode != LoadSaveMode.Inactive) { return; }
        NeurologicalEpisode episode = Episode(part, hypoxic: false);
        if (episode.Outcome is null) { episode.Profile = NeurologicalDefOf.MI_NeurologicalConfig.ForDamage(damage); }
        Invalidate();
    }

    private void ApplyNeurologicalMilestones(bool award)
    {
        if (_brainEpisodes is null) { return; }
        for (int i = _brainEpisodes.Count - 1; i >= 0; i--)
        {
            NeurologicalEpisode episode = _brainEpisodes[i];
            if (!episode.Present) { _brainEpisodes.RemoveAt(i); continue; }
            bool mayAward = award && CanUpdate(Patient) && MoreInjuriesMod.Settings.EnableNeuralDamage
                && (!episode.Hypoxic || !Patient.HasOxygenDeficiencyImmunity() && !_deathresting);
            float increment = episode.Consume(NeurologicalDefOf.MI_NeurologicalConfig.InjuryMilestones,
                CerebralInjury.Scale(Patient, episode.Part), mayAward);
            if (increment <= 0f || Patient.health.hediffSet.PartIsMissing(episode.Part)
                || Patient.health.hediffSet.GetPartHealth(episode.Part) <= 0f) { continue; }
            episode.Outcome ??= NeurologicalOutcomes.Choose(Patient, episode.Profile ?? NeurologicalDefOf.MI_NeurologicalConfig.DefaultProfile)?.HediffDef;
            if (episode.Outcome is { } outcome && NeurologicalOutcomes.Eligible(Patient, outcome)
                && HasMilestoneWeight(episode.Profile!, outcome))
            { NeurologicalOutcomes.Add(Patient, episode.Part, outcome, increment); }
        }
    }

    private static bool HasMilestoneWeight(NeurologicalProfileDef profile, HediffDef outcome)
    {
        for (int i = 0; i < profile.Outcomes.Count; i++)
        {
            if (profile.Outcomes[i].HediffDef == outcome && profile.Outcomes[i].Weight > 0f) { return true; }
        }
        return false;
    }
}
