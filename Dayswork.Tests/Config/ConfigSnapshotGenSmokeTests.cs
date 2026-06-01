namespace Dayswork.Tests.Config;

using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using Dayswork.Tests.Generators;
using FsCheck;
using FsCheck.Xunit;

public class ConfigSnapshotGenSmokeTests
{
    [Property(Arbitrary = new[] { typeof(ConfigSnapshotGen) })]
    public bool Generated_snapshots_satisfy_all_INV_CFG_invariants(IConfigSnapshot s)
    {
        return s.HardCapTime >= 1000
            && s.HardCapTime <= 2600
            && s.StuckInitialWaitMinutes >= 1
            && s.StuckPostTeleportWaitMinutes >= 1
            && s.WorkerWalkPixelsPerTick > 0
            && s.WorkerActionAnimationMs >= 1
            && s.WorkerEntranceHoldTicks >= 0
            && s.OutdoorBandThresholds.Count == Enum.GetValues<OutdoorBandSize>().Length
            && s.OutdoorBandThresholds[OutdoorBandSize.Small] > 0
            && s.OutdoorBandThresholds[OutdoorBandSize.Small] <= s.OutdoorBandThresholds[OutdoorBandSize.Medium]
            && s.OutdoorBandThresholds[OutdoorBandSize.Medium] <= s.OutdoorBandThresholds[OutdoorBandSize.Large]
            && s.OutdoorServiceBandPrices.Count == TaskKindSets.OutdoorServices.Count * Enum.GetValues<OutdoorBandSize>().Length
            && s.AnimalBuildingPrices.Count == TaskKindSets.AnimalServices.Count * Enum.GetValues<AnimalBuildingTier>().Length
            && s.GreenhouseServicePrices.Count == TaskKindSets.GreenhouseServices.Count
            && s.WorkerDailyEnergyCapacity > 0
            && s.WorkActionCosts.Count == Enum.GetValues<WorkActionKind>().Length
            && s.WorkActionCosts.Values.All(v => v >= 0);
    }
}
