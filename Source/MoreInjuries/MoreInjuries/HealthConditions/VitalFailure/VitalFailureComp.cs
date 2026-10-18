using MoreInjuries.Defs.WellKnown;
using MoreInjuries.Extensions;
using MoreInjuries.HealthConditions.BrainDamage;
using MoreInjuries.HealthConditions.HypovolemicShock;
using MoreInjuries.HealthConditions.Secondary;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace MoreInjuries.HealthConditions.VitalFailure;

public sealed partial class VitalFailureComp : ThingComp
{
    private bool _dirty = true;
    private bool _active;
    private bool _evaluating;
    private int _lastProgressTick = -1;
    private readonly Hediff?[] _failures = new Hediff?[6];
    private Hediff? _arrest;
    private Hediff? _heartAttack;
    private Hediff? _brainInjury;
    private bool _oxygenProtected;
    private bool _deathresting;
    private float _underlyingPumping;
    private float _deprivation;
    private readonly List<Hediff> _neurologicalConditions = new(6);
    private readonly HashSet<BodyPartRecord> _injuredParts = [];
    private float _regeneration;
    private int _settingsStamp = -1;

    private Pawn Patient => (Pawn)parent;
    internal bool ReadingUnderlying { get; private set; }
    internal bool CreatingCirculatoryFailure { get; set; }
    internal float Deprivation => _deprivation;

    internal static bool EnabledFor(Pawn pawn) => MoreInjuriesMod.Settings.EnableDelayedVitalFailure && CanUpdate(pawn);

    private static bool CanUpdate(Pawn pawn) => !pawn.Dead && !pawn.Suspended && VitalPhysiology.Supports(pawn)
        && (MoreInjuriesMod.Settings.AnomalyEnableConditionsForShamblers || !pawn.IsShambler);

    internal static VitalFailureComp? For(Pawn pawn) => pawn.TryGetComp<VitalFailureComp>();

    internal void Invalidate()
    {
        _dirty = true;
        InvalidatePrognosis();
    }

    partial void InvalidatePrognosis();

    internal void Resume()
    {
        _lastProgressTick = Find.TickManager.TicksGame;
        Invalidate();
    }

    internal static bool OwnsOxygenProgression(Pawn pawn) => EnabledFor(pawn) && For(pawn) is not null;

    internal static bool OwnsSystemicCerebralSource(Pawn pawn, HediffDef source, HediffDef target)
        => target == KnownHediffDefOf.BrainDamage_Hypoxia
            && (source == KnownHediffDefOf.HypovolemicShock || source == KnownHediffDefOf.CardiacArrest
                || source == KnownHediffDefOf.Hemodilution) && OwnsOxygenProgression(pawn);

    internal static bool ForcesPumping(Pawn pawn)
    {
        if (!EnabledFor(pawn) || For(pawn) is not { ReadingUnderlying: false })
        {
            return false;
        }
        foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
        {
            if (hediff.def == KnownHediffDefOf.CardiacArrest || IsTerminalHeartAttack(hediff))
            {
                return true;
            }
        }
        return false;
    }

    internal static bool IsTerminalHeartAttack(Hediff hediff) => hediff.def == KnownHediffDefOf.HeartAttack
        && hediff.Severity >= hediff.def.lethalSeverity && hediff.def.lethalSeverity > 0f;

    internal bool MayDelay(PawnCapacityDef capacity)
    {
        if (!EnabledFor(Patient) || VitalPhysiology.HasNativeCapacityDeath(Patient)
            || VitalAnatomy.For(Patient.RaceProps.body).BrainsDestroyed(Patient.health.hediffSet))
        {
            return false;
        }
        return capacity == PawnCapacityDefOf.Consciousness || capacity == PawnCapacityDefOf.Breathing
            || capacity == PawnCapacityDefOf.BloodPumping || capacity == PawnCapacityDefOf.BloodFiltration
            || capacity == VitalFailureDefOf.Metabolism;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref _lastProgressTick, "lastVitalProgressTick", -1);
        ExposeNeurologicalEpisodes();
        _dirty = true;
    }

    internal void TickHealth(int delta)
    {
        Pawn pawn = Patient;
        if (!pawn.IsHashIntervalTick(VitalFailureModel.INTERVAL, delta))
        {
            return;
        }
        int now = Find.TickManager.TicksGame;
        if (_lastProgressTick == now)
        {
            return;
        }
        int stamp = (MoreInjuriesMod.Settings.EnableDelayedVitalFailure ? 1 : 0)
            | (MoreInjuriesMod.Settings.AnomalyEnableConditionsForShamblers ? 2 : 0)
            | (MoreInjuriesMod.Settings.AnomalyRegenerationTreatsStructuralDamage ? 4 : 0)
            | (MoreInjuriesMod.Settings.EnableNeuralDamage ? 8 : 0);
        if (stamp != _settingsStamp) { _settingsStamp = stamp; _dirty = true; }
        if (_active)
        {
            _dirty = true;
        }
        Refresh();
        bool recovery = CanUpdate(pawn) && MoreInjuriesMod.Settings.AnomalyRegenerationTreatsStructuralDamage
            && _regeneration > 0f && _neurologicalConditions.Count > 0;
        if ((!EnabledFor(pawn) || !_active) && !recovery && !HasNeurologicalEpisodes || _lastProgressTick == now) { return; }
        _lastProgressTick = now;
        if (recovery) { NeurologicalOutcomes.Regenerate(pawn, _neurologicalConditions, _injuredParts, _regeneration); }
        _evaluating = true;
        try
        {
            ApplyNeurologicalMilestones(award: true);
            if (!EnabledFor(pawn)) { return; }
            float factor = Rand.Range(0.8f, 1.2f);
            float dayFraction = (float)VitalFailureModel.INTERVAL / GenDate.TicksPerDay;
            if (_arrest is not null && !_oxygenProtected && !_deathresting)
            {
                Advance(_arrest, ((HediffCompProperties_SeverityPerDay)_arrest.TryGetComp<HediffComp_VitalArrest>()!.props).severityPerDay,
                    factor, dayFraction);
                if (pawn.Dead)
                {
                    return;
                }
            }
            for (int i = 0; i < _failures.Length; i++)
            {
                if (_failures[i] is { } failure)
                {
                    Advance(failure, failure.def.GetModExtension<VitalFailureProperties>().SeverityPerDay, factor, dayFraction);
                    if (pawn.Dead)
                    {
                        return;
                    }
                }
            }
            if (_deprivation > 0f && !_oxygenProtected && !_deathresting)
            {
                if (_brainInjury is null && VitalAnatomy.For(pawn.RaceProps.body).SurvivingBrain(pawn.health.hediffSet) is { } brain)
                {
                    _brainInjury = pawn.health.AddHediff(KnownHediffDefOf.BrainDamage_Hypoxia, brain);
                    _brainInjury.Severity = VitalFailureModel.INITIAL_SEVERITY * CerebralInjury.Scale(pawn, brain);
                    GatherNeurologicalInjury(_brainInjury);
                }
                if (_brainInjury is not null)
                {
                    _brainInjury.TryGetComp<HediffComp_NeurologicalSource>()?.SetProfile(_arrest?.TryGetComp<HediffComp_NeurologicalSource>()?.Profile ?? NeurologicalDefOf.MI_NeurologicalConfig.HypoxiaProfile);
                    float previous = CerebralInjury.ClinicalSeverity(_brainInjury);
                    Advance(_brainInjury, VitalFailureDefOf.MI_VitalFailureConfig.CerebralSeverityPerDay * _deprivation
                        * CerebralInjury.Scale(pawn, _brainInjury.Part) * NeurologicalOutcomes.HypoxiaMultiplier(pawn),
                        factor, dayFraction);
                    if (_brainInjury.Part is { } part)
                    {
                        NeurologicalEpisode episode = Episode(part, hypoxic: true);
                        if (episode.Outcome is null) { episode.Profile = NeurologicalOutcomes.SourceProfile(_brainInjury); }
                        episode.ClinicalInjury += CerebralInjury.ClinicalSeverity(_brainInjury) - previous;
                    }
                }
            }
        }
        finally
        {
            try { ApplyNeurologicalMilestones(award: true); }
            finally { _evaluating = false; _dirty = true; }
        }
    }

    private static void Advance(Hediff hediff, float rate, float factor, float dayFraction)
        => hediff.Severity = VitalFailureModel.Advance(hediff.Severity, rate, factor, dayFraction);

    // Called before state checks, never from a capacity getter. Reentrant refreshes do no work.
    internal void Refresh()
    {
        if (_evaluating || !_dirty || Scribe.mode is LoadSaveMode.LoadingVars or LoadSaveMode.ResolvingCrossRefs)
        {
            return;
        }
        _evaluating = true;
        try
        {
            RefreshCore();
        }
        finally
        {
            _evaluating = false;
            _dirty = false;
        }
    }

    private void RefreshCore()
    {
        Pawn pawn = Patient;
        HediffSet set = pawn.health.hediffSet;
        _arrest = _heartAttack = _brainInjury = null;
        Hediff? shock = null, choking = null;
        float bloodLoss = 0f, dilution = 0f, adrenaline = 0f, hypothermia = 0f;
        _deathresting = false;
        _neurologicalConditions.Clear();
        _injuredParts.Clear();
        _regeneration = 0f;
        BeginNeurologicalScan();
        for (int i = 0; i < _failures.Length; i++)
        {
            _failures[i] = null;
        }
        foreach (Hediff hediff in set.hediffs)
        {
            if (ModsConfig.AnomalyActive && MoreInjuriesMod.Settings.AnomalyRegenerationTreatsStructuralDamage)
            {
                _regeneration += hediff.CurStage?.regeneration ?? 0f;
                if (hediff.def.GetModExtension<NeurologicalConditionProperties>()?.Regenerative == true)
                { _neurologicalConditions.Add(hediff); }
                if (hediff is Hediff_Injury && hediff.Part is { } injured) { _injuredParts.Add(injured); }
            }
            if (hediff.def == KnownHediffDefOf.CardiacArrest)
            {
                _arrest = hediff;
            }
            else if (hediff.def == KnownHediffDefOf.HeartAttack)
            {
                _heartAttack = hediff;
            }
            else if (hediff.def == KnownHediffDefOf.BrainDamage_Hypoxia)
            {
                _brainInjury = hediff;
                _brainInjury.TryGetComp<HediffComp_CerebralScale>()?.Rebase();
            }
            else if (hediff.def == KnownHediffDefOf.ChokingOnBlood)
            {
                choking = hediff;
            }
            else if (hediff.def == KnownHediffDefOf.HypovolemicShock)
            {
                shock = hediff;
            }
            else if (hediff.def == HediffDefOf.BloodLoss)
            {
                bloodLoss = hediff.Severity;
            }
            else if (hediff.def == KnownHediffDefOf.Hemodilution)
            {
                dilution = hediff.Severity;
            }
            else if (hediff.def == KnownHediffDefOf.AdrenalineRush)
            {
                adrenaline = hediff.Severity;
            }
            else if (hediff.def == HediffDefOf.Hypothermia)
            {
                hypothermia = hediff.Severity;
            }
            else if (ModsConfig.BiotechActive && hediff.def == HediffDefOf.Deathrest)
            {
                _deathresting = true;
            }
            else
            {
                for (int i = 0; i < _failures.Length; i++)
                {
                    if (hediff.def == FailureDef(i))
                    {
                        _failures[i] = hediff;
                        break;
                    }
                }
            }
            GatherNeurologicalInjury(hediff);
        }
        if (pawn.Dead || pawn.Suspended)
        {
            _deprivation = 0f;
            _active = false;
            return;
        }
        if (!EnabledFor(pawn))
        {
            _deprivation = 0f;
            _active = HasNeurologicalEpisodes || _regeneration > 0f && _neurologicalConditions.Count > 0;
            for (int i = 0; i < _failures.Length; i++)
            {
                Remove(ref _failures[i]);
            }
            return;
        }
        VitalAnatomy anatomy = VitalAnatomy.For(pawn.RaceProps.body);
        _oxygenProtected = pawn.HasOxygenDeficiencyImmunity();
        ReadingUnderlying = true;
        float breathing, consciousness, filtration, metabolism;
        try
        {
            _underlyingPumping = Capacity(pawn, PawnCapacityDefOf.BloodPumping);
            breathing = VitalPhysiology.BreathesAir(pawn) ? Capacity(pawn, PawnCapacityDefOf.Breathing) : 1f;
            consciousness = Capacity(pawn, PawnCapacityDefOf.Consciousness);
            filtration = Capacity(pawn, PawnCapacityDefOf.BloodFiltration);
            metabolism = Capacity(pawn, VitalFailureDefOf.Metabolism);
        }
        finally { ReadingUnderlying = false; }
        bool terminalHeartAttack = _heartAttack is not null && IsTerminalHeartAttack(_heartAttack);
        if (!_oxygenProtected && !_deathresting && (_underlyingPumping <= VitalFailureModel.ZERO_FUNCTION || terminalHeartAttack) && _arrest is null)
        {
            CreatingCirculatoryFailure = true;
            try { _arrest = pawn.health.AddHediff(KnownHediffDefOf.CardiacArrest); }
            finally { CreatingCirculatoryFailure = false; }
            _arrest.Severity = VitalFailureModel.INITIAL_SEVERITY;
            if (_arrest.TryGetComp<HediffComp_VitalArrest>() is { } arrestComp)
            {
                arrestComp.MechanicalFailure = !terminalHeartAttack;
            }
        }
        else if (_arrest is not null && (_oxygenProtected || _deathresting
            || _arrest.TryGetComp<HediffComp_VitalArrest>() is { MechanicalFailure: true } && _underlyingPumping > VitalFailureModel.ZERO_FUNCTION))
        {
            Remove(ref _arrest);
        }
        Sync(0, !_oxygenProtected && !_deathresting && breathing <= VitalFailureModel.ZERO_FUNCTION);
        bool liver = VitalAnatomy.AllFailed(set, anatomy.Livers);
        bool renal = VitalAnatomy.AllFailed(set, anatomy.Kidneys);
        Sync(1, liver);
        Sync(2, renal);
        Sync(3, filtration <= VitalFailureModel.ZERO_FUNCTION && !liver && !renal);
        Sync(4, metabolism <= VitalFailureModel.ZERO_FUNCTION);
        Sync(5, consciousness <= VitalFailureModel.ZERO_FUNCTION && !anatomy.BrainsDestroyed(set) && !_deathresting && !_oxygenProtected);

        VitalFailureConfigDef config = VitalFailureDefOf.MI_VitalFailureConfig;
        float shockSource = shock is not null && MoreInjuriesMod.Settings.EnableHypovolemicShock
            ? config.ShockDeprivationCurve.Evaluate(ShockPerfusionModel.EffectiveShock(shock.Severity, bloodLoss))
                * ShockPerfusionModel.GrowthMultiplier(ShockTreatment.Quality(shock)) / ShockPerfusionModel.Support(adrenaline) : 0f;
        _deprivation = _oxygenProtected || _deathresting ? 0f
            : VitalFailureModel.Deprivation(shockSource, breathing, _arrest is null ? _underlyingPumping : 0f,
                config.DilutionDeprivationCurve.Evaluate(dilution)) * config.HypothermiaProtectionCurve.Evaluate(hypothermia);
        _active = HasNeurologicalEpisodes || _deprivation > 0f || _arrest is not null || _regeneration > 0f && _neurologicalConditions.Count > 0;
        for (int i = 0; i < _failures.Length; i++)
        {
            _active |= _failures[i] is not null;
        }
        if (_brainInjury is not null && _deprivation > 0f)
        {
            HediffComp_CausedBy? causes = _brainInjury.TryGetComp<HediffComp_CausedBy>();
            if (_arrest is not null)
            {
                causes?.AddCause(_arrest);
            }
            if (shockSource > 0f && shock is not null)
            {
                causes?.AddCause(shock);
            }
            if (_failures[0] is { } respiratory)
            {
                causes?.AddCause(respiratory);
                if (choking is not null) { causes?.AddCause(choking); }
            }
        }
    }

    private static float Capacity(Pawn pawn, PawnCapacityDef def) => def.Worker.CanHaveCapacity(pawn.RaceProps.body)
        ? PawnCapacityUtility.CalculateCapacityLevel(pawn.health.hediffSet, def) : 1f;

    private static HediffDef FailureDef(int index) => index switch
    {
        0 => VitalFailureDefOf.MI_RespiratoryFailure,
        1 => VitalFailureDefOf.MI_LiverFailure,
        2 => VitalFailureDefOf.MI_RenalFailure,
        3 => VitalFailureDefOf.MI_FiltrationFailure,
        4 => VitalFailureDefOf.MI_MetabolicFailure,
        _ => VitalFailureDefOf.MI_Coma
    };

    private void Sync(int index, bool failed)
    {
        if (failed && _failures[index] is null)
        {
            Hediff failure = Patient.health.AddHediff(FailureDef(index));
            failure.Severity = VitalFailureModel.INITIAL_SEVERITY;
            _failures[index] = failure;
        }
        else if (!failed)
        {
            Remove(ref _failures[index]);
        }
    }

    private void Remove(ref Hediff? hediff)
    {
        if (hediff is not null)
        {
            Patient.health.RemoveHediff(hediff);
            hediff = null;
        }
    }

    internal bool CanRestoreCirculation()
    {
        bool previous = ReadingUnderlying;
        ReadingUnderlying = true;
        try
        {
            return Capacity(Patient, PawnCapacityDefOf.BloodPumping) > VitalFailureModel.ZERO_FUNCTION;
        }
        finally
        {
            ReadingUnderlying = previous;
        }
    }

    internal void RestoreCirculation()
    {
        Invalidate();
        Refresh();
        if (!CanRestoreCirculation())
        {
            return;
        }
        _evaluating = true;
        try
        {
            if (_heartAttack is not null && IsTerminalHeartAttack(_heartAttack))
            {
                Remove(ref _heartAttack);
            }
            Remove(ref _arrest);
        }
        finally { _evaluating = false; _dirty = true; }
    }
}
