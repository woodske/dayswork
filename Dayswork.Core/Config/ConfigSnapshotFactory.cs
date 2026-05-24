namespace Dayswork.Core.Config;

using System.Collections.ObjectModel;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

public static class ConfigSnapshotFactory
{
    public static ConfigSnapshot Create(
        int baseRate,
        IReadOnlyDictionary<TaskKind, int> taskIncrements,
        double averageSpeedConstant,
        int hardCapTime,
        int stuckInitialWaitMinutes,
        int stuckPostTeleportWaitMinutes,
        IReadOnlyDictionary<OutdoorBandSize, int> outdoorBandThresholds,
        IReadOnlyDictionary<OutdoorPriceKey, int> outdoorServiceBandPrices,
        IReadOnlyDictionary<AnimalBuildingPriceKey, int> animalBuildingPrices,
        IReadOnlyDictionary<GreenhousePriceKey, int> greenhouseServicePrices,
        int workerDailyEnergyCapacity,
        IReadOnlyDictionary<WorkActionKind, int> workActionCosts)
    {
        if (baseRate < 0)
            throw new ArgumentOutOfRangeException(nameof(baseRate), "BaseRate must be non-negative.");

        if (averageSpeedConstant <= 0)
            throw new ArgumentOutOfRangeException(nameof(averageSpeedConstant), "AverageSpeedConstant must be greater than zero.");

        if (hardCapTime < 1000 || hardCapTime > 2600)
            throw new ArgumentOutOfRangeException(nameof(hardCapTime), "HardCapTime must be in Stardew HHMM range [1000, 2600].");

        if (stuckInitialWaitMinutes < 1)
            throw new ArgumentOutOfRangeException(nameof(stuckInitialWaitMinutes), "StuckInitialWaitMinutes must be at least 1.");

        if (stuckPostTeleportWaitMinutes < 1)
            throw new ArgumentOutOfRangeException(nameof(stuckPostTeleportWaitMinutes), "StuckPostTeleportWaitMinutes must be at least 1.");

        if (workerDailyEnergyCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(workerDailyEnergyCapacity), "WorkerDailyEnergyCapacity must be greater than zero.");

        var normalizedIncrements = NormalizeExactNonNegativeDictionary(
            taskIncrements,
            Enum.GetValues<TaskKind>(),
            nameof(taskIncrements),
            kind => kind.ToString());

        var normalizedThresholds = NormalizeThresholdDictionary(outdoorBandThresholds);
        var normalizedOutdoorPrices = NormalizeExactNonNegativeDictionary(
            outdoorServiceBandPrices,
            ExpectedOutdoorPriceKeys(),
            nameof(outdoorServiceBandPrices),
            key => $"{key.Service}:{key.Band}");
        var normalizedAnimalPrices = NormalizeExactNonNegativeDictionary(
            animalBuildingPrices,
            ExpectedAnimalBuildingPriceKeys(),
            nameof(animalBuildingPrices),
            key => $"{key.Service}:{key.Tier}");
        var normalizedGreenhousePrices = NormalizeExactNonNegativeDictionary(
            greenhouseServicePrices,
            ExpectedGreenhousePriceKeys(),
            nameof(greenhouseServicePrices),
            key => key.Service.ToString());
        var normalizedActionCosts = NormalizeExactNonNegativeDictionary(
            workActionCosts,
            Enum.GetValues<WorkActionKind>(),
            nameof(workActionCosts),
            action => action.ToString());

        return new ConfigSnapshot(
            baseRate,
            new ReadOnlyDictionary<TaskKind, int>(normalizedIncrements),
            averageSpeedConstant,
            hardCapTime,
            stuckInitialWaitMinutes,
            stuckPostTeleportWaitMinutes,
            new ReadOnlyDictionary<OutdoorBandSize, int>(normalizedThresholds),
            new ReadOnlyDictionary<OutdoorPriceKey, int>(normalizedOutdoorPrices),
            new ReadOnlyDictionary<AnimalBuildingPriceKey, int>(normalizedAnimalPrices),
            new ReadOnlyDictionary<GreenhousePriceKey, int>(normalizedGreenhousePrices),
            workerDailyEnergyCapacity,
            new ReadOnlyDictionary<WorkActionKind, int>(normalizedActionCosts));
    }

    private static Dictionary<OutdoorBandSize, int> NormalizeThresholdDictionary(
        IReadOnlyDictionary<OutdoorBandSize, int> thresholds)
    {
        var normalized = NormalizeExactPositiveDictionary(
            thresholds,
            Enum.GetValues<OutdoorBandSize>(),
            nameof(thresholds),
            band => band.ToString());

        if (normalized[OutdoorBandSize.Small] > normalized[OutdoorBandSize.Medium])
            throw new ArgumentOutOfRangeException(nameof(thresholds), "Outdoor band thresholds must be non-decreasing from Small to Medium.");

        if (normalized[OutdoorBandSize.Medium] > normalized[OutdoorBandSize.Large])
            throw new ArgumentOutOfRangeException(nameof(thresholds), "Outdoor band thresholds must be non-decreasing from Medium to Large.");

        return normalized;
    }

    private static Dictionary<TKey, int> NormalizeExactNonNegativeDictionary<TKey>(
        IReadOnlyDictionary<TKey, int> source,
        IEnumerable<TKey> expectedKeys,
        string parameterName,
        Func<TKey, string> keyFormatter)
        where TKey : notnull
    {
        var normalized = new Dictionary<TKey, int>();
        foreach (var key in expectedKeys)
        {
            if (!source.TryGetValue(key, out var value))
                throw new InvalidOperationException($"Config snapshot is missing a {parameterName} entry for {keyFormatter(key)}.");

            if (value < 0)
                throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} for {keyFormatter(key)} must be non-negative.");

            normalized[key] = value;
        }

        return normalized;
    }

    private static Dictionary<TKey, int> NormalizeExactPositiveDictionary<TKey>(
        IReadOnlyDictionary<TKey, int> source,
        IEnumerable<TKey> expectedKeys,
        string parameterName,
        Func<TKey, string> keyFormatter)
        where TKey : notnull
    {
        var normalized = new Dictionary<TKey, int>();
        foreach (var key in expectedKeys)
        {
            if (!source.TryGetValue(key, out var value))
                throw new InvalidOperationException($"Config snapshot is missing a {parameterName} entry for {keyFormatter(key)}.");

            if (value <= 0)
                throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} for {keyFormatter(key)} must be greater than zero.");

            normalized[key] = value;
        }

        return normalized;
    }

    private static IReadOnlyList<OutdoorPriceKey> ExpectedOutdoorPriceKeys() =>
        (from service in TaskKindSets.OutdoorServices
         from band in Enum.GetValues<OutdoorBandSize>()
         select new OutdoorPriceKey(service, band)).ToList();

    private static IReadOnlyList<AnimalBuildingPriceKey> ExpectedAnimalBuildingPriceKeys() =>
        (from service in TaskKindSets.AnimalServices
         from tier in Enum.GetValues<AnimalBuildingTier>()
         select new AnimalBuildingPriceKey(service, tier)).ToList();

    private static IReadOnlyList<GreenhousePriceKey> ExpectedGreenhousePriceKeys() =>
        TaskKindSets.GreenhouseServices
            .Select(service => new GreenhousePriceKey(service))
            .ToList();
}
