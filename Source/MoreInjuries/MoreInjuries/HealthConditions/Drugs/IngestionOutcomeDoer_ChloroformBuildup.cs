using MoreInjuries.Extensions;
using MoreInjuries.Roslyn.SourceGen.XmlBinding.Attributes;
using RimWorld;
using Verse;

namespace MoreInjuries.HealthConditions.Drugs;

[XmlBindable]
public sealed partial class IngestionOutcomeDoer_ChloroformBuildup : IngestionOutcomeDoer_GiveHediff
{
    [XmlBinding("minSeverityOffsetByDoctorSkill")]
    private partial SimpleCurve MinSeverityOffsetByDoctorSkill { get; }

    [XmlBinding("maxSeverityOffsetByDoctorSkill")]
    private partial SimpleCurve MaxSeverityOffsetByDoctorSkill { get; }

    protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
    {
        float offset = severity;
        if (MedicalDrugAdministration.GetDoctor(ingested) is { } doctor)
        {
            float skill = doctor.GetMedicalSkillLevelOrDefault();
            offset = Rand.Range(MinSeverityOffsetByDoctorSkill.Evaluate(skill), MaxSeverityOffsetByDoctorSkill.Evaluate(skill));
        }
        HealthUtility.AdjustSeverity(pawn, hediffDef, offset);
    }
}
