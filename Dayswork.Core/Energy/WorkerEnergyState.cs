using System.Collections.ObjectModel;

namespace Dayswork.Core.Energy;

public sealed record WorkerEnergyState(
    int Capacity,
    int RemainingEnergy,
    bool CanStartNewWorkUnit,
    IReadOnlyDictionary<WorkActionKind, int> ActionCosts)
{
    public static WorkerEnergyState FromProfile(WorkerEnergyProfile profile) =>
        new(
            Capacity: profile.DailyCapacity,
            RemainingEnergy: profile.DailyCapacity,
            CanStartNewWorkUnit: profile.DailyCapacity > 0,
            ActionCosts: new ReadOnlyDictionary<WorkActionKind, int>(
                new Dictionary<WorkActionKind, int>(profile.ActionCosts)));
}
