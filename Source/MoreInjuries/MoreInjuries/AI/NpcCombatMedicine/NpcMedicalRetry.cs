namespace MoreInjuries.AI.NpcCombatMedicine;

// No Thing identity, clock, RNG or persistence in this policy. Equivalent floor
// stacks share a deadline. Material context changes permit reconsideration.
internal sealed class NpcMedicalRetry
{
    private string? _context;
    private int _failures;
    internal int Deadline { get; private set; }

    internal bool Allowed(int now, string context)
    {
        if (_context != context) { _context = context; _failures = 0; Deadline = 0; }
        return now >= Deadline;
    }

    internal void Complete(int now, string context, bool progress)
    {
        Allowed(now, context);
        if (progress) { _failures = 0; Deadline = 0; return; }
        _failures++;
        int delay = _failures switch { 1 => 150, 2 => 300, 3 => 600, 4 => 1200, _ => 2500 };
        Deadline = now + delay;
    }
}
