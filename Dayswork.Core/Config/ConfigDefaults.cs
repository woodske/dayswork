namespace Dayswork.Core.Config;

using System.Collections.ObjectModel;
using Dayswork.Core.Domain;
using Dayswork.Core.Energy;

public static class ConfigDefaults
{
    public static IConfigSnapshot Build()
    {
        var increments = new Dictionary<TaskKind, int>
        {
            [TaskKind.WaterCrops]            = 20,
            [TaskKind.HarvestCrops]          = 25,
            [TaskKind.CollectFruit]          = 15,
            [TaskKind.FeedAnimals]           = 20,
            [TaskKind.PetAnimals]            = 10,
            [TaskKind.CollectAnimalProducts] = 15,
            [TaskKind.CutTrees]              = 30,
            [TaskKind.ClearRocks]            = 20,
            [TaskKind.ClearWeeds]            = 20,
            [TaskKind.ClearGrass]            = 20,
        };

        var outdoorBandThresholds = new Dictionary<OutdoorBandSize, int>
        {
            [OutdoorBandSize.Small] = 64,
            [OutdoorBandSize.Medium] = 160,
            [OutdoorBandSize.Large] = 999999,
        };

        var outdoorPrices = new Dictionary<OutdoorPriceKey, int>();
        AddOutdoorPrices(TaskKind.WaterCrops, 150, 300, 500);
        AddOutdoorPrices(TaskKind.HarvestCrops, 200, 400, 650);
        AddOutdoorPrices(TaskKind.CollectFruit, 120, 240, 400);
        AddOutdoorPrices(TaskKind.CutTrees, 250, 450, 700);
        AddOutdoorPrices(TaskKind.ClearRocks, 200, 375, 600);
        AddOutdoorPrices(TaskKind.ClearWeeds, 125, 225, 375);
        AddOutdoorPrices(TaskKind.ClearGrass, 100, 200, 325);

        var animalPrices = new Dictionary<AnimalBuildingPriceKey, int>();
        AddAnimalPrices(TaskKind.FeedAnimals, 90, 120, 150, 120, 150, 180);
        AddAnimalPrices(TaskKind.PetAnimals, 70, 100, 130, 100, 130, 160);
        AddAnimalPrices(TaskKind.CollectAnimalProducts, 110, 150, 190, 150, 190, 230);

        var greenhousePrices = new Dictionary<GreenhousePriceKey, int>
        {
            [new GreenhousePriceKey(TaskKind.WaterCrops)] = 200,
            [new GreenhousePriceKey(TaskKind.HarvestCrops)] = 250,
            [new GreenhousePriceKey(TaskKind.CollectFruit)] = 225,
        };

        var workActionCosts = new Dictionary<WorkActionKind, int>
        {
            [WorkActionKind.WaterTile] = 2,
            [WorkActionKind.HarvestCrop] = 1,
            [WorkActionKind.HarvestFruit] = 1,
            [WorkActionKind.FeedAnimal] = 1,
            [WorkActionKind.PetAnimal] = 1,
            [WorkActionKind.CollectAnimalProduct] = 1,
            [WorkActionKind.AxeSwing] = 2,
            [WorkActionKind.PickaxeSwing] = 2,
            [WorkActionKind.ScytheSwing] = 1,
        };

        return ConfigSnapshotFactory.Create(
            baseRate: 50,
            taskIncrements: new ReadOnlyDictionary<TaskKind, int>(increments),
            averageSpeedConstant: 0.3,  // pricing-min per raw tile per task; see U-05 HoursEstimator
            hardCapTime: 2000,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10,
            outdoorBandThresholds: new ReadOnlyDictionary<OutdoorBandSize, int>(outdoorBandThresholds),
            outdoorServiceBandPrices: new ReadOnlyDictionary<OutdoorPriceKey, int>(outdoorPrices),
            animalBuildingPrices: new ReadOnlyDictionary<AnimalBuildingPriceKey, int>(animalPrices),
            greenhouseServicePrices: new ReadOnlyDictionary<GreenhousePriceKey, int>(greenhousePrices),
            workerDailyEnergyCapacity: 270,
            workActionCosts: new ReadOnlyDictionary<WorkActionKind, int>(workActionCosts));

        void AddOutdoorPrices(TaskKind service, int small, int medium, int large)
        {
            outdoorPrices[new OutdoorPriceKey(service, OutdoorBandSize.Small)] = small;
            outdoorPrices[new OutdoorPriceKey(service, OutdoorBandSize.Medium)] = medium;
            outdoorPrices[new OutdoorPriceKey(service, OutdoorBandSize.Large)] = large;
        }

        void AddAnimalPrices(
            TaskKind service,
            int coop,
            int bigCoop,
            int deluxeCoop,
            int barn,
            int bigBarn,
            int deluxeBarn)
        {
            animalPrices[new AnimalBuildingPriceKey(service, AnimalBuildingTier.Coop)] = coop;
            animalPrices[new AnimalBuildingPriceKey(service, AnimalBuildingTier.BigCoop)] = bigCoop;
            animalPrices[new AnimalBuildingPriceKey(service, AnimalBuildingTier.DeluxeCoop)] = deluxeCoop;
            animalPrices[new AnimalBuildingPriceKey(service, AnimalBuildingTier.Barn)] = barn;
            animalPrices[new AnimalBuildingPriceKey(service, AnimalBuildingTier.BigBarn)] = bigBarn;
            animalPrices[new AnimalBuildingPriceKey(service, AnimalBuildingTier.DeluxeBarn)] = deluxeBarn;
        }
    }
}
