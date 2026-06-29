namespace Dayswork.Core.Config;

using System.Collections.ObjectModel;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

public static class ConfigSnapshotFactory
{
    public static ConfigSnapshot Create(
        int hardCapTime,
        int stuckInitialWaitMinutes,
        int stuckPostTeleportWaitMinutes,
        float workerWalkPixelsPerTick,
        int workerActionAnimationMs,
        int workerEntranceHoldTicks,
        bool workOnHolidays,
        bool eagerChestDeposits,
        IReadOnlyDictionary<EnergyTier, int> energyTierEnergy,
        IReadOnlyDictionary<EnergyTier, int> energyTierPrice,
        IReadOnlyDictionary<WorkActionKind, int> workActionCosts)
    {
        if (hardCapTime < 1000 || hardCapTime > 2600)
            throw new ArgumentOutOfRangeException(nameof(hardCapTime), "HardCapTime must be in Stardew HHMM range [1000, 2600].");

        if (stuckInitialWaitMinutes < 1)
            throw new ArgumentOutOfRangeException(nameof(stuckInitialWaitMinutes), "StuckInitialWaitMinutes must be at least 1.");

        if (stuckPostTeleportWaitMinutes < 1)
            throw new ArgumentOutOfRangeException(nameof(stuckPostTeleportWaitMinutes), "StuckPostTeleportWaitMinutes must be at least 1.");

        if (workerWalkPixelsPerTick <= 0)
            throw new ArgumentOutOfRangeException(nameof(workerWalkPixelsPerTick), "WorkerWalkPixelsPerTick must be greater than zero.");

        if (workerActionAnimationMs < 1)
            throw new ArgumentOutOfRangeException(nameof(workerActionAnimationMs), "WorkerActionAnimationMs must be at least 1.");

        if (workerEntranceHoldTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(workerEntranceHoldTicks), "WorkerEntranceHoldTicks must be non-negative.");

        var normalizedTierEnergy = NormalizeExactPositiveDictionary(
            energyTierEnergy,
            Enum.GetValues<EnergyTier>(),
            nameof(energyTierEnergy),
            tier => tier.ToString());
        var normalizedTierPrice = NormalizeExactNonNegativeDictionary(
            energyTierPrice,
            Enum.GetValues<EnergyTier>(),
            nameof(energyTierPrice),
            tier => tier.ToString());
        var normalizedActionCosts = NormalizeExactNonNegativeDictionary(
            workActionCosts,
            Enum.GetValues<WorkActionKind>(),
            nameof(workActionCosts),
            action => action.ToString());

        return new ConfigSnapshot(
            hardCapTime,
            stuckInitialWaitMinutes,
            stuckPostTeleportWaitMinutes,
            workerWalkPixelsPerTick,
            workerActionAnimationMs,
            workerEntranceHoldTicks,
            workOnHolidays,
            eagerChestDeposits,
            new ReadOnlyDictionary<EnergyTier, int>(normalizedTierEnergy),
            new ReadOnlyDictionary<EnergyTier, int>(normalizedTierPrice),
            new ReadOnlyDictionary<WorkActionKind, int>(normalizedActionCosts));
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
}
