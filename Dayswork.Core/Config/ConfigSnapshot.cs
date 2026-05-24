namespace Dayswork.Core.Config;

using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

public sealed record ConfigSnapshot(
    int BaseRate,
    IReadOnlyDictionary<TaskKind, int> TaskIncrements,
    double AverageSpeedConstant,
    int HardCapTime,
    int StuckInitialWaitMinutes,
    int StuckPostTeleportWaitMinutes,
    IReadOnlyDictionary<OutdoorBandSize, int> OutdoorBandThresholds,
    IReadOnlyDictionary<OutdoorPriceKey, int> OutdoorServiceBandPrices,
    IReadOnlyDictionary<AnimalBuildingPriceKey, int> AnimalBuildingPrices,
    IReadOnlyDictionary<GreenhousePriceKey, int> GreenhouseServicePrices,
    int WorkerDailyEnergyCapacity,
    IReadOnlyDictionary<WorkActionKind, int> WorkActionCosts
) : IConfigSnapshot
{
    // IReadOnlyDictionary uses reference equality, so override to get structural equality.
    public bool Equals(ConfigSnapshot? other) =>
        other is not null
        && BaseRate == other.BaseRate
        && AverageSpeedConstant == other.AverageSpeedConstant
        && HardCapTime == other.HardCapTime
        && StuckInitialWaitMinutes == other.StuckInitialWaitMinutes
        && StuckPostTeleportWaitMinutes == other.StuckPostTeleportWaitMinutes
        && WorkerDailyEnergyCapacity == other.WorkerDailyEnergyCapacity
        && DictionaryEquals(TaskIncrements, other.TaskIncrements)
        && DictionaryEquals(OutdoorBandThresholds, other.OutdoorBandThresholds)
        && DictionaryEquals(OutdoorServiceBandPrices, other.OutdoorServiceBandPrices)
        && DictionaryEquals(AnimalBuildingPrices, other.AnimalBuildingPrices)
        && DictionaryEquals(GreenhouseServicePrices, other.GreenhouseServicePrices)
        && DictionaryEquals(WorkActionCosts, other.WorkActionCosts);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BaseRate);
        hash.Add(AverageSpeedConstant);
        hash.Add(HardCapTime);
        hash.Add(StuckInitialWaitMinutes);
        hash.Add(StuckPostTeleportWaitMinutes);
        hash.Add(WorkerDailyEnergyCapacity);
        hash.Add(TaskIncrements.Count);
        hash.Add(OutdoorBandThresholds.Count);
        hash.Add(OutdoorServiceBandPrices.Count);
        hash.Add(AnimalBuildingPrices.Count);
        hash.Add(GreenhouseServicePrices.Count);
        hash.Add(WorkActionCosts.Count);
        return hash.ToHashCode();
    }

    private static bool DictionaryEquals<TKey>(
        IReadOnlyDictionary<TKey, int> left,
        IReadOnlyDictionary<TKey, int> right)
        where TKey : notnull
    {
        if (left.Count != right.Count)
            return false;

        return left.All(kvp => right.TryGetValue(kvp.Key, out var value) && value == kvp.Value);
    }
}
