using System.Collections.Generic;
using MoreInjuries.Defs.WellKnown;
using RimWorld;
using Verse.AI;

namespace MoreInjuries.AI.NpcCombatMedicine;

// Retain native toil layouts and serialization; only MI-owned automatic work yields.
public sealed class JobDriver_NpcMove : JobDriver_Goto
{
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => !job.playerForced && NpcMedicalAi.ActorEligible(pawn)
            && (NpcMedicalSafety.ExplosiveDanger(pawn) || !MoreInjuriesMod.Settings.EnableNpcCombatMedicine
                || MoreInjuriesMod.Settings.NpcMedicalAiMode < (job.def == KnownJobDefOf.MI_NpcWithdraw ? 2 : 1)));
        foreach (Toil toil in base.MakeNewToils()) { yield return toil; }
    }
}

public sealed class JobDriver_NpcPerformanceDrug : JobDriver_Ingest
{
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => NpcMedicalSafety.YieldToExplosive(pawn, job));
        foreach (Toil toil in base.MakeNewToils()) { yield return toil; }
    }
}
