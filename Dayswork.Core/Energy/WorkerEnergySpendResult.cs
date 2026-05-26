namespace Dayswork.Core.Energy;

public sealed record WorkerEnergySpendResult(
    WorkerEnergyState State,
    WorkActionKind Action,
    int CostApplied,
    bool ReachedZeroOnThisBeat);
