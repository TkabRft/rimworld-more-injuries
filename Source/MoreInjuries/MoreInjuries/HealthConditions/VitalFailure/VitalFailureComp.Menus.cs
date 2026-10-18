using MoreInjuries.HealthConditions.CardiacArrest;
using MoreInjuries.HealthConditions.Choking;
using System.Collections.Generic;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

public sealed partial class VitalFailureComp
{
    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selectedPawn)
    {
        Pawn patient = Patient;
        if (!EnabledFor(patient) || patient.HasComp<MoreInjuryComp>() || !VitalPhysiology.CanReceiveCare(patient)
            || !patient.RaceProps.Humanlike && patient.Faction != selectedPawn.Faction)
        { return []; }
        UIBuilder<FloatMenuOption> builder = new(Keys: [], Options: []);
        CardiacArrestWorker.AddEmergencyOptions(patient, builder, selectedPawn);
        if (MoreInjuriesMod.Settings.EnableChoking) { ChokingWorker.AddEmergencyOptions(patient, builder, selectedPawn); }
        return builder.Options;
    }
}
