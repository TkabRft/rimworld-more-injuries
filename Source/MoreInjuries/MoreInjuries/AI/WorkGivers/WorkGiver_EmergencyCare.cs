using Verse;
using Verse.AI;

namespace MoreInjuries.AI.WorkGivers;

// Existing laying-status, ownership, medical-care and reservation rules remain in the base scanner.
public sealed class WorkGiver_EmergencyCare : WorkGiver_MoreInjuriesTreatmentBase
{
    private Pawn? _doctor, _patient;
    private Job? _candidate;
    private int _tick;

    protected override bool CanTreat(Pawn doctor, Pawn patient)
    {
        _doctor = doctor; _patient = patient; _tick = Find.TickManager.TicksGame;
        _candidate = EmergencyCare.CreateJob(doctor, patient, aftercare: false);
        return _candidate is not null;
    }

    protected override Job CreateJob(Pawn doctor, Pawn patient)
    {
        Job? result = doctor == _doctor && patient == _patient && Find.TickManager.TicksGame == _tick
            ? _candidate : EmergencyCare.CreateJob(doctor, patient, aftercare: false);
        _candidate = null;
        if (result is not null) { result.endAfterTendedOnce = true; }
        return result!; // Native work selection accepts a null job when eligibility changed.
    }
}
