using Dayswork.Core.Config;
using Dayswork.Core.Domain;

namespace Dayswork.Integration;

public sealed class ModConfig
{
    private static readonly IConfigSnapshot DefaultSnapshot = ConfigDefaults.Build();

    public int HardCapTime { get; set; } = DefaultSnapshot.HardCapTime;
    public int StuckInitialWaitMinutes { get; set; } = DefaultSnapshot.StuckInitialWaitMinutes;
    public int StuckPostTeleportWaitMinutes { get; set; } = DefaultSnapshot.StuckPostTeleportWaitMinutes;
    public float WorkerWalkPixelsPerTick { get; set; } = DefaultSnapshot.WorkerWalkPixelsPerTick;
    public int WorkerActionAnimationMs { get; set; } = DefaultSnapshot.WorkerActionAnimationMs;
    public int WorkerEntranceHoldTicks { get; set; } = DefaultSnapshot.WorkerEntranceHoldTicks;
    public Dictionary<string, int> OutdoorBandThresholds { get; set; } = CreateOutdoorBandThresholdDefaults();
    public Dictionary<string, int> OutdoorServiceBandPrices { get; set; } = CreateOutdoorServiceBandPriceDefaults();
    public Dictionary<string, int> AnimalBuildingPrices { get; set; } = CreateAnimalBuildingPriceDefaults();
    public Dictionary<string, int> GreenhouseServicePrices { get; set; } = CreateGreenhouseServicePriceDefaults();
    public int WorkerDailyEnergyCapacity { get; set; } = DefaultSnapshot.WorkerDailyEnergyCapacity;
    public Dictionary<string, int> WorkActionCosts { get; set; } = CreateWorkActionCostDefaults();

    public static ModConfig CreateDefaults() => new();

    private static Dictionary<string, int> CreateOutdoorBandThresholdDefaults() =>
        DefaultSnapshot.OutdoorBandThresholds.ToDictionary(
            kvp => ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(kvp.Key),
            kvp => kvp.Value);

    private static Dictionary<string, int> CreateOutdoorServiceBandPriceDefaults() =>
        DefaultSnapshot.OutdoorServiceBandPrices.ToDictionary(
            kvp => ContractTermsConfigKeyCodec.EncodeOutdoorPriceKey(kvp.Key),
            kvp => kvp.Value);

    private static Dictionary<string, int> CreateAnimalBuildingPriceDefaults() =>
        DefaultSnapshot.AnimalBuildingPrices.ToDictionary(
            kvp => ContractTermsConfigKeyCodec.EncodeAnimalBuildingPriceKey(kvp.Key),
            kvp => kvp.Value);

    private static Dictionary<string, int> CreateGreenhouseServicePriceDefaults() =>
        DefaultSnapshot.GreenhouseServicePrices.ToDictionary(
            kvp => ContractTermsConfigKeyCodec.EncodeGreenhousePriceKey(kvp.Key),
            kvp => kvp.Value);

    private static Dictionary<string, int> CreateWorkActionCostDefaults() =>
        DefaultSnapshot.WorkActionCosts.ToDictionary(
            kvp => ContractTermsConfigKeyCodec.EncodeWorkActionKey(kvp.Key),
            kvp => kvp.Value);
}
