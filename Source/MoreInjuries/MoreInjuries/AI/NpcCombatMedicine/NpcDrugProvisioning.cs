using System;

namespace MoreInjuries.AI.NpcCombatMedicine;

internal static class NpcDrugProvisioning
{
    internal static int RestorativeShare(int total, bool medic, bool replaceSingle) =>
        !medic && total == 1 ? (replaceSingle ? 1 : 0) : total / 2;

    internal static int MissingBoosters(int target, int existingBoosters, int existingMorphine, int clinicalMorphine)
        => Math.Max(0, target - (existingBoosters - Math.Min(existingMorphine, clinicalMorphine)));
}
