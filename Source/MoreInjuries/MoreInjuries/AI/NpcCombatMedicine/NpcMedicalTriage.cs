using MoreInjuries.Defs.WellKnown;
using MoreInjuries.HealthConditions.HeavyBleeding;
using RimWorld;
using Verse;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static class NpcMedicalTriage
{
    internal static float Urgency(Pawn patient)
    {
        bool arrest = false, lifeThreatening = false;
        bool needsCare = patient.health.HasHediffsNeedingTend();
        float choking = 0f, shock = 0f, loss = 0f, dilution = 0f;
        foreach (Hediff hediff in patient.health.hediffSet.hediffs)
        {
            if (hediff.def == KnownHediffDefOf.CardiacArrest) { arrest = true; }
            if (hediff.def == KnownHediffDefOf.ChokingOnBlood) { choking = hediff.Severity; }
            if (hediff.def == KnownHediffDefOf.HypovolemicShock) { shock = hediff.Severity; }
            if (hediff.def == HediffDefOf.BloodLoss) { loss = hediff.Severity; }
            if (hediff.def == KnownHediffDefOf.Hemodilution) { dilution = hediff.Severity; }
            needsCare |= hediff.def == KnownHediffDefOf.Fracture || hediff.def == KnownHediffDefOf.TourniquetApplied;
            lifeThreatening |= hediff.IsCurrentlyLifeThreatening || hediff.def == KnownHediffDefOf.HeartAttack;
        }
        float breathing = patient.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);
        return NpcMedicalModel.Urgency(arrest, choking > 0f && breathing <= .2f, shock, loss,
            patient.health.hediffSet.BleedRateTotal, breathing, patient.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping),
            needsCare, patient.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness), choking,
            loss > BloodLossConstants.BLOOD_LOSS_THRESHOLD || dilution > BloodLossConstants.BLOOD_LOSS_THRESHOLD, lifeThreatening);
    }
}
