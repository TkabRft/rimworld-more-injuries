using MoreInjuries.Defs.WellKnown;
using UnityEngine;
using Verse;

namespace MoreInjuries.HealthConditions.BrainDamage;

internal static class CerebralInjury
{
    internal static float Scale(Pawn pawn, BodyPartRecord? part) => part is null ? 1f
        : Mathf.Max(0.001f, part.def.GetMaxHealth(pawn) / 10f);

    internal static float ClinicalSeverity(Hediff hediff) => hediff.def == KnownHediffDefOf.BrainDamage_Hypoxia
        ? hediff.Severity / (hediff.TryGetComp<HediffComp_CerebralScale>()?.Scale ?? Scale(hediff.pawn, hediff.Part))
        : hediff.Severity;
}

public sealed class HediffCompProperties_CerebralScale : HediffCompProperties
{
    public HediffCompProperties_CerebralScale() => compClass = typeof(HediffComp_CerebralScale);
}

public sealed class HediffComp_CerebralScale : HediffComp
{
    private float _scale;
    internal float Scale => _scale > 0f ? _scale : CerebralInjury.Scale(Pawn, parent.Part);

    public override void CompPostPostAdd(DamageInfo? dinfo) => _scale = CerebralInjury.Scale(Pawn, parent.Part);

    internal void Rebase()
    {
        float current = CerebralInjury.Scale(Pawn, parent.Part);
        if (current != _scale)
        {
            float previous = _scale > 0f ? _scale : 1f;
            _scale = current;
            parent.Severity *= current / previous;
        }
    }

    public override void CompExposeData()
    {
        Scribe_Values.Look(ref _scale, "cerebralAnatomyScale", 0f);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            Rebase();
        }
    }
}
