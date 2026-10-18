using System.Collections.Generic;
using Verse;

namespace MoreInjuries.HealthConditions.BrainDamage;

// Discrete awards for an injury episode; this stores no elapsed exposure or oxygen debt.
public sealed class NeurologicalEpisode : IExposable
{
    internal BodyPartRecord Part = null!;
    internal bool Hypoxic;
    internal HediffDef? Outcome;
    internal NeurologicalProfileDef? Profile;
    private int _processed;
    private float _scale;
    internal bool Present;
    internal float ClinicalInjury;

    internal float Consume(IReadOnlyList<NeurologicalMilestone> milestones, float scale, bool award)
    {
        int reached = 0;
        while (reached < milestones.Count && ClinicalInjury + 0.000001f >= milestones[reached].Injury) { reached++; }
        // Anatomical resizing cannot manufacture another disability award.
        if (_scale > 0f && _scale != scale) { award = false; }
        _scale = scale;
        if (reached <= _processed) { return 0f; }
        float previous = _processed > 0 ? milestones[System.Math.Min(_processed, milestones.Count) - 1].LastingSeverity : 0f;
        _processed = reached;
        return award ? milestones[reached - 1].LastingSeverity - previous : 0f;
    }

    public void ExposeData()
    {
        Scribe_BodyParts.Look(ref Part, "part");
        Scribe_Values.Look(ref Hypoxic, "hypoxic");
        Scribe_Defs.Look(ref Outcome, "outcome");
        Scribe_Defs.Look(ref Profile, "profile");
        Scribe_Values.Look(ref _processed, "processedMilestone");
        Scribe_Values.Look(ref _scale, "anatomyScale");
    }
}
