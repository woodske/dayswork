namespace Dayswork.Core.Config;

using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

public interface IConfigSnapshot
{
    // Compatibility-only hourly/deposit-era pricing values retained behind internal seams.
    int BaseRate { get; }
    IReadOnlyDictionary<TaskKind, int> TaskIncrements { get; }
    double AverageSpeedConstant { get; }
    int HardCapTime { get; }
    int StuckInitialWaitMinutes { get; }
    int StuckPostTeleportWaitMinutes { get; }
    float WorkerWalkPixelsPerTick { get; }
    int WorkerActionAnimationMs { get; }
    int WorkerEntranceHoldTicks { get; }
    IReadOnlyDictionary<OutdoorBandSize, int> OutdoorBandThresholds { get; }
    IReadOnlyDictionary<OutdoorPriceKey, int> OutdoorServiceBandPrices { get; }
    IReadOnlyDictionary<AnimalBuildingPriceKey, int> AnimalBuildingPrices { get; }
    IReadOnlyDictionary<GreenhousePriceKey, int> GreenhouseServicePrices { get; }
    int WorkerDailyEnergyCapacity { get; }
    IReadOnlyDictionary<WorkActionKind, int> WorkActionCosts { get; }
}
