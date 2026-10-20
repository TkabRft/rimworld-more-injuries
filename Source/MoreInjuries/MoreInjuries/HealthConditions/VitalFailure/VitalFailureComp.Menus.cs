using MoreInjuries.HealthConditions.CardiacArrest;
using MoreInjuries.HealthConditions.Choking;
using System.Collections.Generic;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

public sealed partial class VitalFailureComp
{
    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selectedPawn)
    {
        // Float-menu callbacks can reach a corpse-owned copy of the comp.
        // Vital care applies to a living pawn, never the corpse's inner pawn.
        if (parent is not Pawn patient || !EnabledFor(patient) || patient.HasComp<MoreInjuryComp>() || !VitalPhysiology.CanReceiveCare(patient)
            || !patient.RaceProps.Humanlike && patient.Faction != selectedPawn.Faction)
        { return []; }
        UIBuilder<FloatMenuOption> builder = new(Keys: [], Options: []);
        CardiacArrestWorker.AddEmergencyOptions(patient, builder, selectedPawn);
        if (MoreInjuriesMod.Settings.EnableChoking) { ChokingWorker.AddEmergencyOptions(patient, builder, selectedPawn); }
        return builder.Options;
    }
}
