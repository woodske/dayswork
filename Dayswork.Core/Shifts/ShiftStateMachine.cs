namespace Dayswork.Core.Shifts;

public sealed class ShiftStateMachine : IShiftStateMachine
{
    public ShiftPhase Phase { get; private set; } = ShiftPhase.WaitingForSpawn;
    public ShiftIntent? CurrentIntent { get; private set; }
    public ShiftStopReason StopReason { get; private set; } = ShiftStopReason.None;

    // Legal successors: each phase maps to the set of phases that may follow it.
    // Done maps to the empty set — any transition out of Done is illegal.
    private static readonly Dictionary<ShiftPhase, HashSet<ShiftPhase>> _successors = new()
    {
        [ShiftPhase.WaitingForSpawn] = new() { ShiftPhase.Working },
        [ShiftPhase.Working]         = new() { ShiftPhase.Depositing, ShiftPhase.Stuck },
        [ShiftPhase.Stuck]           = new() { ShiftPhase.Recovering },
        [ShiftPhase.Recovering]      = new() { ShiftPhase.Working, ShiftPhase.Depositing },
        [ShiftPhase.Depositing]      = new() { ShiftPhase.Exiting },
        [ShiftPhase.Exiting]         = new() { ShiftPhase.Done },
        [ShiftPhase.Done]            = new() { },
    };

    // Phases that must carry a non-null intent.
    private static readonly HashSet<ShiftPhase> _activePhases = new()
    {
        ShiftPhase.Working,
        ShiftPhase.Stuck,
        ShiftPhase.Recovering,
        ShiftPhase.Depositing,
        ShiftPhase.Exiting,
    };

    public void Transition(ShiftPhase newPhase, ShiftIntent? intent = null)
    {
        if (!_successors.TryGetValue(Phase, out var legalSet) || !legalSet.Contains(newPhase))
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

    public void BeginWrapUp(ShiftIntent intent, ShiftStopReason stopReason)
    {
        if (stopReason == ShiftStopReason.None)
            throw new ArgumentOutOfRangeException(nameof(stopReason), "Wrap-up requires a concrete stop reason.");

        RegisterStopReason(stopReason);
        Transition(ShiftPhase.Depositing, intent);
    }

    public void RegisterStopReason(ShiftStopReason stopReason)
    {
        if (stopReason == ShiftStopReason.None || StopReason != ShiftStopReason.None)
            return;

        StopReason = stopReason;
    }

    public void SetIntent(ShiftIntent intent)
    {
        if (!_activePhases.Contains(Phase))
            throw new InvalidOperationException(
                $"Cannot set intent in phase {Phase}.");

        CurrentIntent = intent;
    }
}
