using MoreInjuries.HealthConditions.Drugs;
using Verse;

namespace MoreInjuries.AI.Jobs.Outcomes;

public sealed class JobOutcomeDoer_IngestDrug : JobOutcomeDoer
{
    protected override bool DoOutcome(Pawn doctor, Pawn patient, Thing? device)
    {
        if (device is not { Destroyed: false, IngestibleNow: true, stackCount: 1 })
        {
            Logger.ConfigError("Medical drug ingestion requires one live, ingestible dose.");
            return false;
        }
        MedicalDrugAdministration.Administer(doctor, patient, device);
        return true;
    }

    public override string ToString() => nameof(JobOutcomeDoer_IngestDrug);
}
