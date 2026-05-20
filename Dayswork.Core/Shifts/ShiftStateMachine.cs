namespace Dayswork.Core.Shifts;

public sealed class ShiftStateMachine : IShiftStateMachine
{
    public ShiftPhase Phase { get; private set; } = ShiftPhase.WaitingForSpawn;
    public ShiftIntent? CurrentIntent { get; private set; }

    // Legal successors: each phase maps to the single phase that may follow it.
    // Done has no successor — any transition out of Done is illegal.
    private static readonly Dictionary<ShiftPhase, ShiftPhase> _successors = new()
    {
        [ShiftPhase.WaitingForSpawn] = ShiftPhase.Working,
        [ShiftPhase.Working]         = ShiftPhase.Depositing,
        [ShiftPhase.Depositing]      = ShiftPhase.Exiting,
        [ShiftPhase.Exiting]         = ShiftPhase.Done,
    };

    // Phases that must carry a non-null intent.
    private static readonly HashSet<ShiftPhase> _activePhases = new()
    {
        ShiftPhase.Working,
        ShiftPhase.Depositing,
        ShiftPhase.Exiting,
    };

    public void Transition(ShiftPhase newPhase, ShiftIntent? intent = null)
    {
        if (!_successors.TryGetValue(Phase, out var legal) || newPhase != legal)
            throw new InvalidOperationException(
                $"Illegal shift transition: {Phase} → {newPhase}.");

        if (_activePhases.Contains(newPhase) && intent is null)
            throw new ArgumentNullException(nameof(intent),
                $"Transition to {newPhase} requires a non-null intent.");

        if (!_activePhases.Contains(newPhase) && intent is not null)
            throw new ArgumentException(
                $"Transition to {newPhase} must not carry an intent.", nameof(intent));

        Phase = newPhase;
        CurrentIntent = intent;
    }

    public void SetIntent(ShiftIntent intent)
    {
        if (!_activePhases.Contains(Phase))
            throw new InvalidOperationException(
                $"Cannot set intent in phase {Phase}.");

        CurrentIntent = intent;
    }
}
