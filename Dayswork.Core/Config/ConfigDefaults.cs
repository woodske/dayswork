namespace Dayswork.Core.Config;

using System.Collections.ObjectModel;
using Dayswork.Core.Domain;

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

        return ConfigSnapshotFactory.Create(
            baseRate: 50,
            taskIncrements: new ReadOnlyDictionary<TaskKind, int>(increments),
            averageSpeedConstant: 0.3,  // pricing-min per raw tile per task; see U-05 HoursEstimator
            hardCapTime: 2000,
            stuckInitialWaitMinutes: 10,
            stuckPostTeleportWaitMinutes: 10);
    }
}
