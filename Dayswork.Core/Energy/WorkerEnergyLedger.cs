namespace Dayswork.Core.Energy;

public sealed class WorkerEnergyLedger
{
    public WorkerEnergyState StartShift(WorkerEnergyProfile profile) =>
        WorkerEnergyState.FromProfile(profile);

    public WorkerEnergySpendResult ApplyActionCost(WorkerEnergyState state, WorkActionKind action)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));

        if (!state.ActionCosts.TryGetValue(action, out var configuredCost))
            throw new InvalidOperationException($"Missing energy cost for action {action}.");

        var costApplied = Math.Max(0, configuredCost);
        var remaining = Math.Max(0, state.RemainingEnergy - costApplied);
        var canStartNewUnit = state.CanStartNewWorkUnit && remaining > 0;
        var reachedZero = state.RemainingEnergy > 0 && remaining == 0;

        return new WorkerEnergySpendResult(
            state with
            {
                RemainingEnergy = remaining,
                CanStartNewWorkUnit = canStartNewUnit,
            },
            action,
            costApplied,
            reachedZero);
    }
}
