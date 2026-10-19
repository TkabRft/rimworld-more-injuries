using System;

namespace MoreInjuries.AI.NpcCombatMedicine;

// Engine-independent policy shared by generation and regression tests.
internal static class NpcMedicalModel
{
    internal const int TRAVEL_TIMEOUT = 2500;
    internal const int REPOSITION_TIMEOUT = 600;
    internal static bool TravelExpired(int now, int started) => started >= 0 && now - started >= TRAVEL_TIMEOUT;
    internal static float Clamp(float value) => Math.Max(0f, Math.Min(1f, value));
    internal static float Strength(float points) => Clamp((points - 400f) / 2600f);
    internal static float Interpolate(float low, float high, float strength) => low + (high - low) * Clamp(strength);
    internal static int Quota(int population, float chance, float multiplier = 1f)
        => Math.Min(population, Math.Max(0, (int)Math.Floor(population * Clamp(chance * multiplier) + .5f)));
    internal static int Quantity(int count, float multiplier, float strength)
        => Math.Max(0, (int)Math.Floor(count * Math.Max(0f, multiplier) * (1f + .5f * Clamp(strength)) + .5f));
    internal static bool Saline(float roll) => roll < .75f;
    internal static int CareRank(int relation, float urgency, bool own, bool friendly, bool neutral, bool player)
        => relation switch
        {
            0 => 0, // Self care does not depend on the permission to treat other group members.
            1 => own ? 0 : -1,
            2 => friendly && urgency >= 70f ? 1 : -1,
            3 => neutral && urgency >= 70f ? 2 : -1,
            4 => player && urgency >= 70f ? 1 : -1,
            _ => -1
        };
    internal static float Urgency(bool arrest, bool criticalAirway, float shock, float loss, float bleeding,
        float breathing, float pumping, bool needsCare, float consciousness = 1f)
        => arrest ? 100f : criticalAirway ? 90f
            : shock >= .5f || breathing <= .4f || pumping <= .4f || consciousness <= .4f || loss >= .45f && bleeding > 0f ? 70f
            : bleeding > 0f ? 30f + Math.Min(30f, bleeding * 20f) : needsCare ? 10f : 0f;
    internal static bool Survival(bool enabled, int mode, bool managed, bool available, bool scripted,
        bool prisoner, bool prisonBreak) => enabled && mode >= 2 && managed && available && !scripted && (!prisoner || prisonBreak);
}

internal sealed class NpcDrugBudget
{
    internal int LastThreat = -1, LastPerformance = -1, LastClinical = -1, Doses;
    internal bool OpportunityRolled, OpportunityAllowed;

    internal void Threat(int now)
    {
        if (LastThreat < 0 || now - LastThreat >= 2500)
        {
            Doses = 0;
            OpportunityRolled = false;
            OpportunityAllowed = false;
        }
        LastThreat = now;
    }

    internal bool PerformanceAllowed(int now, int maximum, int interval)
        => Doses < maximum && (LastPerformance < 0 || now - LastPerformance >= interval);
    internal bool ClinicalAllowed(int now, int interval) => LastClinical < 0 || now - LastClinical >= interval;
    internal void PerformanceTaken(int now) { LastPerformance = now; Doses++; }
    internal void ClinicalTaken(int now) => LastClinical = now;
}
