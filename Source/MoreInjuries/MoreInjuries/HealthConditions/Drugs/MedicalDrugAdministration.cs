using Verse;

namespace MoreInjuries.HealthConditions.Drugs;

internal static class MedicalDrugAdministration
{
    // Synchronous, main-thread context for clinician-dependent ingestion outcomes.
    // Restore the previous doctor even if an outcome throws or administers another drug.
    private static Pawn? s_doctor;
    private static Thing? s_dose;
    private static bool s_clinicalPain;

    internal static Pawn? GetDoctor(Thing dose) => ReferenceEquals(dose, s_dose) ? s_doctor : null;
    internal static bool IsClinicalPain(Thing dose) => ReferenceEquals(dose, s_dose) && s_clinicalPain;

    internal static void Administer(Pawn doctor, Pawn patient, Thing dose, bool clinicalPain = false)
    {
        Pawn? previousDoctor = s_doctor;
        Thing? previousDose = s_dose;
        bool previousClinicalPain = s_clinicalPain;
        try
        {
            s_doctor = doctor;
            s_dose = dose;
            s_clinicalPain = clinicalPain;
            dose.Ingested(patient, nutritionWanted: 0f);
        }
        finally
        {
            s_doctor = previousDoctor;
            s_dose = previousDose;
            s_clinicalPain = previousClinicalPain;
        }
    }
}
