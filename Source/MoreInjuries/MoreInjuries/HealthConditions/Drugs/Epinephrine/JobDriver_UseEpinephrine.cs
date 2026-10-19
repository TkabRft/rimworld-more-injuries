using MoreInjuries.AI.Jobs;
using MoreInjuries.Defs.WellKnown;
using MoreInjuries.AI.NpcCombatMedicine;
using Verse;
using Verse.AI;

namespace MoreInjuries.HealthConditions.Drugs.Epinephrine;

public sealed class JobDriver_UseEpinephrine : JobDriver_UseMedicalDrug
{
    public const string JOB_LABEL_KEY = "MI_UseEpinephrine";
    public const float SAFETY_THRESHOLD = 0.25f;

    protected override ThingDef DeviceDef => KnownThingDefOf.Epinephrine;

    protected override bool ApplyDevice(Pawn doctor, Pawn patient, Thing? device)
    {
        if (NpcMedicalAi.Managed(doctor)
            && (!NpcMedicalAi.State(patient).Drugs.ClinicalAllowed(Find.TickManager.TicksGame, MoreInjuriesMod.Settings.NpcClinicalInterval)
                || NpcMedicalAi.Severity(patient, "AdrenalineRush") >= SAFETY_THRESHOLD))
        {
            EndJobWith(JobCondition.Incompletable);
            return false;
        }
        return base.ApplyDevice(doctor, patient, device);
    }

    public static IJobDescriptor GetDispatcher(Pawn doctor, Pawn patient, Thing device) =>
        new JobDescriptor(KnownJobDefOf.UseEpinephrine, doctor, patient, device);
}
