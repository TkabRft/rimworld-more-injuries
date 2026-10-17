using System.Runtime.CompilerServices;

namespace MoreInjuries.HealthConditions.Hemodilution;

/// <summary>
/// Implements the mathematical model for calculating hemodilution levels based on blood loss and the addition of blood products or saline.
/// </summary>
// this class uses System.Math instead of UnityEngine.Mathf to allow unit testing without Unity dependencies
public static class HemodilutionEvaluator
{
    internal static float FluidVolumeFromBloodLossReduction(float severityReduction) => severityReduction * 0.5f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float BloodLossSeverityToMissingVolume(float bloodLoss)
    {
        // Blood loss severity means 0 blood loss = healthy patient, 1 = dead patient.
        // Since you die way before losing 100% of your blood volume, we re-map blood loss severity to lost volume of blood
        // such that the patient dies at 50% of total blood volume lost.
        // As such, 100% blood loss severity means 50% of total blood volume lost.
        return FluidVolumeFromBloodLossReduction(bloodLoss);
    }

    /// <summary>
    /// Calculates the change in hemodilution after the specified <paramref name="addedVolume"/> of either blood products or saline 
    /// (determined by <paramref name="dilutionFactor"/>) is added to the current hemodilution level.
    /// </summary>
    /// <param name="hemodilution">The current hemodilution severity level, where 0 is no hemodilution and 1 is pure saline.</param>
    /// <param name="bloodLoss">The current blood loss severity level, used to determine the missing fluid volume.</param>
    /// <param name="addedVolume">The volume of fluid added, which can be either blood products or saline.</param>
    /// <param name="dilutionFactor">The dilution factor determines how the added volume affects hemodilution: <c>+1.0</c> for saline, <c>-1.0</c> for blood products. Any other value is disallowed.</param>
    /// <returns>The change in hemodilution level after the addition of the specified volume.</returns>
    public static float CalculateHemodilutionDelta(float hemodilution, float bloodLoss, float addedVolume, float dilutionFactor)
    {
        if (dilutionFactor is not (-1f or 1f))
        {
            throw new ArgumentOutOfRangeException(nameof(dilutionFactor));
        }
        // volume calculations
        float missingVolume = BloodLossSeverityToMissingVolume(bloodLoss);
        float currentVolume = 1f - missingVolume;
        float replacedVolume = Math.Min(addedVolume, missingVolume);
        float overflowVolume = addedVolume - replacedVolume;

        // Only saline (dilution_factor = +1) contributes to dilution in the replaced_volume
        float dilutionContribution = Math.Max(0f, dilutionFactor) * replacedVolume;
        float newDilution = ((hemodilution * currentVolume) + dilutionContribution) / (currentVolume + replacedVolume);
        // Any overflow is fully diluted or fully concentrated depending on infusion type
        newDilution += overflowVolume * dilutionFactor;
        return newDilution - hemodilution;
    }

    public static float CalculateMaximumSafeAddedSalineVolume(float hemodilution, float bloodLoss, float hemodilutionThreshold)
    {
        if (hemodilution >= hemodilutionThreshold)
        {
            return 0f;
        }
        float missingVolume = BloodLossSeverityToMissingVolume(bloodLoss);
        float currentVolume = 1f - missingVolume;
        float volumeBeforeFull = currentVolume * (hemodilutionThreshold - hemodilution) / (1f - hemodilutionThreshold);
        if (volumeBeforeFull <= missingVolume)
        {
            return Math.Max(0f, volumeBeforeFull);
        }
        float dilutionWhenFull = (hemodilution * currentVolume + missingVolume) / (currentVolume + missingVolume);
        return missingVolume + Math.Max(0f, hemodilutionThreshold - dilutionWhenFull);
    }

    public static int CalculateMaximumSafeSalineTransfusions(float hemodilution, float bloodLoss, float hemodilutionThreshold, float salineBagVolume)
    {
        if (salineBagVolume <= 0f)
        {
            return 0;
        }
        float maxAllowedAddedVolume = CalculateMaximumSafeAddedSalineVolume(hemodilution, bloodLoss, hemodilutionThreshold);
        int count = Math.Max(0, (int)Math.Floor(maxAllowedAddedVolume / salineBagVolume));
        // Correct integer-boundary roundoff against the actual outcome, without a per-bag loop.
        if (count > 0 && ResultingDilution(hemodilution, bloodLoss, count * salineBagVolume, 1f) > hemodilutionThreshold)
        {
            count--;
        }
        if (ResultingDilution(hemodilution, bloodLoss, (count + 1) * salineBagVolume, 1f) <= hemodilutionThreshold)
        {
            count++;
        }
        return count;
    }

    public static float CalculateMaxNeededWholeBloodVolumeToTreatHemodilution(float hemodilution, float bloodLoss)
    {
        float missingVolume = BloodLossSeverityToMissingVolume(bloodLoss);
        float currentVolume = 1f - missingVolume;
        float minRequiredAddedVolume = missingVolume + (hemodilution * currentVolume / (currentVolume + missingVolume));
        return minRequiredAddedVolume;
    }

    public static int CalculateMaxNeededBloodTransfusionsToTreatHemodilution(float hemodilution, float bloodLoss, float bloodBagVolume)
    {
        float minRequiredAddedVolume = CalculateMaxNeededWholeBloodVolumeToTreatHemodilution(hemodilution, bloodLoss);
        return RequiredBloodCount(hemodilution, bloodLoss, minRequiredAddedVolume, bloodBagVolume, 0f, restoreVolume: true);
    }

    public static float CalculateMinimumRequiredWholeBloodVolumeToTreatHemodilution(float hemodilution, float bloodLoss, float hemodilutionThreshold)
    {
        if (hemodilution <= hemodilutionThreshold)
        {
            return 0f;
        }
        if (hemodilutionThreshold <= 0f)
        {
            return CalculateMaxNeededWholeBloodVolumeToTreatHemodilution(hemodilution, bloodLoss);
        }
        float missingVolume = BloodLossSeverityToMissingVolume(bloodLoss);
        float currentVolume = 1f - missingVolume;
        // valid for 0 <= x <= missingVolume
        float x = (hemodilution * currentVolume / hemodilutionThreshold) - currentVolume;
        if (0 <= x && x <= missingVolume)
        {
            return x;
        }
        // x > missingVolume (overflow required)
        x = missingVolume + (hemodilution * currentVolume / (currentVolume + missingVolume)) - hemodilutionThreshold;
        return x;
    }

    public static int CalculateMinimumRequiredBloodTransfusionsToTreatHemodilution(float hemodilution, float bloodLoss, float hemodilutionThreshold, float bloodBagVolume)
    {
        float minRequiredAddedVolume = CalculateMinimumRequiredWholeBloodVolumeToTreatHemodilution(hemodilution, bloodLoss, hemodilutionThreshold);
        return RequiredBloodCount(hemodilution, bloodLoss, minRequiredAddedVolume, bloodBagVolume, hemodilutionThreshold);
    }

    private static float ResultingDilution(float hemodilution, float bloodLoss, float volume, float factor)
        => hemodilution + CalculateHemodilutionDelta(hemodilution, bloodLoss, volume, factor);

    private static int RequiredBloodCount(float hemodilution, float bloodLoss, float requiredVolume, float bagVolume, float threshold, bool restoreVolume = false)
    {
        if (bagVolume <= 0f)
        {
            return 0;
        }
        int count = Math.Max(0, (int)Math.Ceiling(requiredVolume / bagVolume));
        float minimumVolume = restoreVolume ? BloodLossSeverityToMissingVolume(bloodLoss) : 0f;
        if (count > 0 && (count - 1) * bagVolume >= minimumVolume
            && ResultingDilution(hemodilution, bloodLoss, (count - 1) * bagVolume, -1f) <= threshold) { count--; }
        if (count * bagVolume < minimumVolume || ResultingDilution(hemodilution, bloodLoss, count * bagVolume, -1f) > threshold)
        {
            count++;
        }
        return count;
    }
}
