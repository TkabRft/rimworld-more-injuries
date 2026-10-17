using MoreInjuries.Defs.WellKnown;
using MoreInjuries.Extensions;
using RimWorld;
using Verse;

namespace MoreInjuries.HealthConditions.HypovolemicShock;

public class HediffComp_Shock : HediffComp
{
    private bool _fixedNow = false;

    private HediffCompProperties_Shock Properties => (HediffCompProperties_Shock)props;

    public bool PastFixedPoint => parent.Severity > 0.55f;

    public bool FixedNow
    {
        get => _fixedNow;
        set => _fixedNow = value;
    }

    private Hediff? GetBloodLoss() => parent.pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);

    private static HediffInfo GetHediffInfo(Pawn pawn)
    {
        HediffInfo info = default;
        int hediffsFound = 0;
        for (int i = 0; i < pawn.health.hediffSet.hediffs.Count && hediffsFound < 2; ++i)
        {
            Hediff hediff = pawn.health.hediffSet.hediffs[i];
            if (hediff.def == KnownHediffDefOf.AdrenalineRush)
            {
                info.AdrenalineRush = hediff;
                hediffsFound++;
            }
            else if (hediff.def == HediffDefOf.BloodLoss)
            {
                info.BloodLoss = hediff;
                hediffsFound++;
            }
        }
        return info;
    }


    public override void CompTended(float quality, float maxQuality, int batchPosition = 0)
    {
        base.CompTended(quality, maxQuality, batchPosition);

        // ensure that blood IVs can be used to stabilize the patient
        float requiredQuality = Properties.BleedSeverityCurve.Evaluate(parent.Severity);
        if (GetBloodLoss()?.Severity is null or < 0.15f || quality >= requiredQuality)
        {
            FixedNow = true;
            Logger.LogVerbose($"Fixed hypovolemic shock for {parent.pawn.Name}");
        }
    }

    public override void CompExposeData()
    {
        base.CompExposeData();

        Scribe_Values.Look(ref _fixedNow, "fixedNow", false);
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        Pawn pawn = parent.pawn;

        if (pawn.HasOxygenDeficiencyImmunity())
        {
            pawn.health.RemoveHediff(parent);
            return;
        }

        if (!pawn.IsHashIntervalTick(ShockPerfusionModel.CYCLE_LENGTH))
        {
            return;
        }
        (Hediff? bloodLoss, Hediff? adrenaline) = GetHediffInfo(pawn);
        bool deathresting = ModsConfig.BiotechActive && pawn.health.hediffSet.HasHediff(HediffDefOf.Deathrest);
        bool tended = parent.IsTended();
        if (!tended && !deathresting)
        {
            // A previous successful tend does not confer permanent protection against later hemorrhage.
            FixedNow = false;
        }
        parent.Severity = ShockPerfusionModel.Advance(parent.Severity, bloodLoss?.Severity ?? 0f,
            adrenaline?.Severity ?? 0f, tended, FixedNow,
            deathresting || !MoreInjuriesMod.Settings.EnableHypovolemicShock);
    }

    private struct HediffInfo
    {
        internal Hediff? BloodLoss;
        internal Hediff? AdrenalineRush;

        public readonly void Deconstruct(out Hediff? bloodLoss, out Hediff? adrenaline)
        {
            bloodLoss = BloodLoss;
            adrenaline = AdrenalineRush;
        }
    }
}
