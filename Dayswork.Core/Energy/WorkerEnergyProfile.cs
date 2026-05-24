namespace Dayswork.Core.Energy;

public sealed record WorkerEnergyProfile(
    int DailyCapacity,
    IReadOnlyDictionary<WorkActionKind, int> ActionCosts);
