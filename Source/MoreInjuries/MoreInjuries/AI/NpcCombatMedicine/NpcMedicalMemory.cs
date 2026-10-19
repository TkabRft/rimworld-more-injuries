using System.Collections.Generic;
using System.Linq;
using Verse;

namespace MoreInjuries.AI.NpcCombatMedicine;

// Medic roles and actual dose history only. No equipment is generated during load.
public sealed class NpcMedicalMemory : GameComponent
{
    private List<NpcMedicalDoseRecord> _records = [];
    public NpcMedicalMemory(Game game) { }
    internal static void Remember(Pawn pawn)
    {
        NpcMedicalMemory? memory = Current.Game?.GetComponent<NpcMedicalMemory>();
        if (memory is null || memory._records.Any(record => record.Pawn == pawn)) { return; }
        memory._records.Add(new NpcMedicalDoseRecord { Pawn = pawn });
    }
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _records.RemoveAll(record => record.Pawn is null || record.Pawn.Dead);
            foreach (NpcMedicalDoseRecord record in _records) { record.Capture(); }
        }
        Scribe_Collections.Look(ref _records, "npcMedicalDoseHistory", LookMode.Deep);
        _records ??= [];
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        { foreach (NpcMedicalDoseRecord record in _records) { record.Restore(); } }
    }
}

public sealed class NpcMedicalDoseRecord : IExposable
{
    internal Pawn? Pawn;
    private int _threat = -1, _performance = -1, _clinical = -1, _doses;
    private bool _rolled, _allowed, _medic;
    internal void Capture()
    {
        NpcDrugBudget budget = NpcMedicalAi.State(Pawn!).Drugs;
        _threat = budget.LastThreat; _performance = budget.LastPerformance; _clinical = budget.LastClinical;
        _doses = budget.Doses; _rolled = budget.OpportunityRolled; _allowed = budget.OpportunityAllowed;
        _medic = NpcMedicalAi.State(Pawn!).Medic;
    }
    internal void Restore()
    {
        if (Pawn is null) { return; }
        NpcDrugBudget budget = NpcMedicalAi.State(Pawn).Drugs;
        budget.LastThreat = _threat; budget.LastPerformance = _performance; budget.LastClinical = _clinical;
        budget.Doses = _doses; budget.OpportunityRolled = _rolled; budget.OpportunityAllowed = _allowed;
        NpcMedicalAi.State(Pawn).Medic = _medic;
    }
    public void ExposeData()
    {
        Scribe_References.Look(ref Pawn, "pawn");
        Scribe_Values.Look(ref _threat, "lastThreat", -1);
        Scribe_Values.Look(ref _performance, "lastPerformance", -1);
        Scribe_Values.Look(ref _clinical, "lastClinical", -1);
        Scribe_Values.Look(ref _doses, "performanceDoses");
        Scribe_Values.Look(ref _rolled, "opportunityRolled");
        Scribe_Values.Look(ref _allowed, "opportunityAllowed");
        Scribe_Values.Look(ref _medic, "medic");
    }
}
