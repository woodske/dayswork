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

        // INV-CFG-03: every defined TaskKind must have a rate entry or the factory fails fast.
        foreach (TaskKind kind in Enum.GetValues<TaskKind>())
        {
            if (!increments.ContainsKey(kind))
                throw new InvalidOperationException(
                    $"ConfigDefaults.Build is missing a TaskIncrement entry for {kind}.");
        }

        return new ConfigSnapshot(
            BaseRate: 50,
            TaskIncrements: new ReadOnlyDictionary<TaskKind, int>(increments),
            AverageSpeedConstant: 5.0,
            HardCapTime: 2000,
            StuckInitialWaitMinutes: 10,
            StuckPostTeleportWaitMinutes: 10
        );
    }
}
