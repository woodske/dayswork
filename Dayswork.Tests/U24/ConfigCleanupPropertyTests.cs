namespace Dayswork.Tests.U24;

using Dayswork.Core.Config;
using Dayswork.Integration;
using FsCheck.Xunit;

public sealed class ConfigCleanupPropertyTests
{
    [Property(Arbitrary = new[] { typeof(U24PropertyGenerators) }, MaxTest = 250)]
    public bool Normalize_is_idempotent_for_redesign_config(ModConfig config)
    {
        var once = RuntimeConfigSnapshotMapper.Normalize(config);
        var twice = RuntimeConfigSnapshotMapper.Normalize(once);
        return Equivalent(once, twice);
    }

    [Property(Arbitrary = new[] { typeof(U24PropertyGenerators) }, MaxTest = 250)]
    public bool BuildSnapshot_is_deterministic_for_equivalent_redesign_inputs(ModConfig config)
    {
        var first = RuntimeConfigSnapshotMapper.BuildSnapshot(config);
        var second = RuntimeConfigSnapshotMapper.BuildSnapshot(config);
        return first.Equals(second);
    }

    private static bool Equivalent(ModConfig left, ModConfig right) =>
        left.HardCapTime == right.HardCapTime
        && left.StuckInitialWaitMinutes == right.StuckInitialWaitMinutes
        && left.StuckPostTeleportWaitMinutes == right.StuckPostTeleportWaitMinutes
        && left.WorkerWalkPixelsPerTick.Equals(right.WorkerWalkPixelsPerTick)
        && left.WorkerActionAnimationMs == right.WorkerActionAnimationMs
        && left.WorkerEntranceHoldTicks == right.WorkerEntranceHoldTicks
        && left.WorkerDailyEnergyCapacity == right.WorkerDailyEnergyCapacity
        && DictionaryEquals(left.OutdoorBandThresholds, right.OutdoorBandThresholds)
        && DictionaryEquals(left.OutdoorServiceBandPrices, right.OutdoorServiceBandPrices)
        && DictionaryEquals(left.AnimalBuildingPrices, right.AnimalBuildingPrices)
        && DictionaryEquals(left.GreenhouseServicePrices, right.GreenhouseServicePrices)
        && DictionaryEquals(left.WorkActionCosts, right.WorkActionCosts);

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
