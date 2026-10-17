using Verse;

namespace MoreInjuries.HealthConditions.Drugs;

internal static class MedicalDrugAdministration
{
    // Synchronous, main-thread context for clinician-dependent ingestion outcomes.
    // Restore the previous doctor even if an outcome throws or administers another drug.
    private static Pawn? s_doctor;
    private static Thing? s_dose;

    internal static Pawn? GetDoctor(Thing dose) => ReferenceEquals(dose, s_dose) ? s_doctor : null;

    internal static void Administer(Pawn doctor, Pawn patient, Thing dose)
    {
        Pawn? previousDoctor = s_doctor;
        Thing? previousDose = s_dose;
        try
        {
            s_doctor = doctor;
            s_dose = dose;
            dose.Ingested(patient, nutritionWanted: 0f);
        }
        finally
        {
            s_doctor = previousDoctor;
            s_dose = previousDose;
        }
    }
}
