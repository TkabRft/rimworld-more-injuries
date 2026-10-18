using System;
using RimWorld;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

// Local forecast state; none of these values are written back to a pawn or save.
internal struct PrognosisFailures
{
    private float _respiratory, _liver, _renal, _filtration, _metabolic, _coma, _arrest;
    internal bool HasActive => _respiratory > 0f || _liver > 0f || _renal > 0f || _filtration > 0f
        || _metabolic > 0f || _coma > 0f || _arrest > 0f;

    internal float Get(int index) => index switch
    {
        0 => _respiratory, 1 => _liver, 2 => _renal, 3 => _filtration, 4 => _metabolic, 5 => _coma, _ => _arrest
    };

    internal void Set(int index, float severity)
    {
        switch (index)
        {
            case 0: _respiratory = severity; break;
            case 1: _liver = severity; break;
            case 2: _renal = severity; break;
            case 3: _filtration = severity; break;
            case 4: _metabolic = severity; break;
            case 5: _coma = severity; break;
            default: _arrest = severity; break;
        }
    }

    internal int Advance(in PhysiologySnapshot source, int ticks)
    {
        float breathing = PrognosisCapacity.Level(in source, 0, ticks), pumping = PrognosisCapacity.Level(in source, 1, ticks);
        float filtration = PrognosisCapacity.Level(in source, 3, ticks);
        for (int i = 0; i < 7; i++)
        {
            bool failed = i switch
            {
                0 => !source.OxygenProtected && breathing <= VitalFailureModel.ZERO_FUNCTION,
                1 => source.LiverFailed, 2 => source.RenalFailed,
                3 => filtration <= VitalFailureModel.ZERO_FUNCTION && !source.LiverFailed && !source.RenalFailed,
                4 => PrognosisCapacity.Level(in source, 4, ticks) <= VitalFailureModel.ZERO_FUNCTION,
                5 => !source.OxygenProtected && source.CanHaveBrain && PrognosisCapacity.Level(in source, 2, ticks) <= VitalFailureModel.ZERO_FUNCTION,
                _ => !source.OxygenProtected && (source.PersistentArrest || pumping <= VitalFailureModel.ZERO_FUNCTION)
            };
            float severity = failed ? Math.Max(VitalFailureModel.INITIAL_SEVERITY, Get(i))
                + source.FailureRates![i] * VitalFailureModel.INTERVAL / GenDate.TicksPerDay : 0f;
            Set(i, severity);
            if (severity >= 1f && (!source.Protected || ticks >= source.ProtectionTicks)) { return i; }
        }
        return -1;
    }
}
