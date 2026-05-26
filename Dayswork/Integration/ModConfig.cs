using Dayswork.Core.Config;
using Dayswork.Core.Domain;

namespace Dayswork.Integration;

public sealed class ModConfig
{
    private static readonly IConfigSnapshot DefaultSnapshot = ConfigDefaults.Build();

    public int BaseRate { get; set; } = DefaultSnapshot.BaseRate;
    public int WaterCropsRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.WaterCrops];
    public int HarvestCropsRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.HarvestCrops];
    public int CollectFruitRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.CollectFruit];
    public int FeedAnimalsRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.FeedAnimals];
    public int PetAnimalsRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.PetAnimals];
    public int CollectAnimalProductsRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.CollectAnimalProducts];
    public int CutTreesRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.CutTrees];
    public int ClearRocksRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.ClearRocks];
    public int ClearWeedsRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.ClearWeeds];
    public int ClearGrassRate { get; set; } = DefaultSnapshot.TaskIncrements[TaskKind.ClearGrass];
    public double AverageSpeedConstant { get; set; } = DefaultSnapshot.AverageSpeedConstant;
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

    public int GetTaskRate(TaskKind kind) => kind switch
    {
        TaskKind.WaterCrops            => WaterCropsRate,
        TaskKind.HarvestCrops          => HarvestCropsRate,
        TaskKind.CollectFruit          => CollectFruitRate,
        TaskKind.FeedAnimals           => FeedAnimalsRate,
        TaskKind.PetAnimals            => PetAnimalsRate,
        TaskKind.CollectAnimalProducts => CollectAnimalProductsRate,
        TaskKind.CutTrees              => CutTreesRate,
        TaskKind.ClearRocks            => ClearRocksRate,
        TaskKind.ClearWeeds            => ClearWeedsRate,
        TaskKind.ClearGrass            => ClearGrassRate,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public void SetTaskRate(TaskKind kind, int value)
    {
        switch (kind)
        {
            case TaskKind.WaterCrops:
                WaterCropsRate = value;
                break;
            case TaskKind.HarvestCrops:
                HarvestCropsRate = value;
                break;
            case TaskKind.CollectFruit:
                CollectFruitRate = value;
                break;
            case TaskKind.FeedAnimals:
                FeedAnimalsRate = value;
                break;
            case TaskKind.PetAnimals:
                PetAnimalsRate = value;
                break;
            case TaskKind.CollectAnimalProducts:
                CollectAnimalProductsRate = value;
                break;
            case TaskKind.CutTrees:
                CutTreesRate = value;
                break;
            case TaskKind.ClearRocks:
                ClearRocksRate = value;
                break;
            case TaskKind.ClearWeeds:
                ClearWeedsRate = value;
                break;
            case TaskKind.ClearGrass:
                ClearGrassRate = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

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
