using Dayswork.Core.Config;
using Dayswork.Core.Domain;

namespace Dayswork.Integration;

internal static class RuntimeConfigSnapshotMapper
{
    public static ModConfig Normalize(ModConfig config, Action<string>? logWarning = null)
    {
        var defaults = ModConfig.CreateDefaults();
        var normalizedThresholds = NormalizePositiveDictionary(
            config.OutdoorBandThresholds,
            defaults.OutdoorBandThresholds,
            nameof(ModConfig.OutdoorBandThresholds),
            logWarning);
        if (normalizedThresholds[ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(OutdoorBandSize.Medium)]
            < normalizedThresholds[ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(OutdoorBandSize.Small)])
        {
            logWarning?.Invoke("ConfigFallback_OutdoorBandThresholds_Medium");
            normalizedThresholds[ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(OutdoorBandSize.Medium)] =
                defaults.OutdoorBandThresholds[ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(OutdoorBandSize.Medium)];
        }

        if (normalizedThresholds[ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(OutdoorBandSize.Large)]
            < normalizedThresholds[ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(OutdoorBandSize.Medium)])
        {
            logWarning?.Invoke("ConfigFallback_OutdoorBandThresholds_Large");
            normalizedThresholds[ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(OutdoorBandSize.Large)] =
                defaults.OutdoorBandThresholds[ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(OutdoorBandSize.Large)];
        }

        return new ModConfig
        {
            BaseRate = Math.Max(0, config.BaseRate),
            WaterCropsRate = Math.Max(0, config.WaterCropsRate),
            HarvestCropsRate = Math.Max(0, config.HarvestCropsRate),
            CollectFruitRate = Math.Max(0, config.CollectFruitRate),
            FeedAnimalsRate = Math.Max(0, config.FeedAnimalsRate),
            PetAnimalsRate = Math.Max(0, config.PetAnimalsRate),
            CollectAnimalProductsRate = Math.Max(0, config.CollectAnimalProductsRate),
            CutTreesRate = Math.Max(0, config.CutTreesRate),
            ClearRocksRate = Math.Max(0, config.ClearRocksRate),
            ClearWeedsRate = Math.Max(0, config.ClearWeedsRate),
            ClearGrassRate = Math.Max(0, config.ClearGrassRate),
            AverageSpeedConstant = config.AverageSpeedConstant > 0
                ? config.AverageSpeedConstant
                : defaults.AverageSpeedConstant,
            HardCapTime = Math.Clamp(config.HardCapTime, 1000, 2600),
            StuckInitialWaitMinutes = Math.Max(1, config.StuckInitialWaitMinutes),
            StuckPostTeleportWaitMinutes = Math.Max(1, config.StuckPostTeleportWaitMinutes),
            OutdoorBandThresholds = normalizedThresholds,
            OutdoorServiceBandPrices = NormalizeNonNegativeDictionary(
                config.OutdoorServiceBandPrices,
                defaults.OutdoorServiceBandPrices,
                nameof(ModConfig.OutdoorServiceBandPrices),
                logWarning),
            AnimalBuildingPrices = NormalizeNonNegativeDictionary(
                config.AnimalBuildingPrices,
                defaults.AnimalBuildingPrices,
                nameof(ModConfig.AnimalBuildingPrices),
                logWarning),
            GreenhouseServicePrices = NormalizeNonNegativeDictionary(
                config.GreenhouseServicePrices,
                defaults.GreenhouseServicePrices,
                nameof(ModConfig.GreenhouseServicePrices),
                logWarning),
            WorkerDailyEnergyCapacity = config.WorkerDailyEnergyCapacity > 0
                ? config.WorkerDailyEnergyCapacity
                : defaults.WorkerDailyEnergyCapacity,
            WorkActionCosts = NormalizeNonNegativeDictionary(
                config.WorkActionCosts,
                defaults.WorkActionCosts,
                nameof(ModConfig.WorkActionCosts),
                logWarning),
        };
    }

    public static ConfigSnapshot BuildSnapshot(ModConfig config)
    {
        var normalized = Normalize(config);
        var increments = new Dictionary<TaskKind, int>
        {
            [TaskKind.WaterCrops] = normalized.WaterCropsRate,
            [TaskKind.HarvestCrops] = normalized.HarvestCropsRate,
            [TaskKind.CollectFruit] = normalized.CollectFruitRate,
            [TaskKind.FeedAnimals] = normalized.FeedAnimalsRate,
            [TaskKind.PetAnimals] = normalized.PetAnimalsRate,
            [TaskKind.CollectAnimalProducts] = normalized.CollectAnimalProductsRate,
            [TaskKind.CutTrees] = normalized.CutTreesRate,
            [TaskKind.ClearRocks] = normalized.ClearRocksRate,
            [TaskKind.ClearWeeds] = normalized.ClearWeedsRate,
            [TaskKind.ClearGrass] = normalized.ClearGrassRate,
        };

        var outdoorBandThresholds = DefaultSnapshot.OutdoorBandThresholds.Keys.ToDictionary(
            key => key,
            key => normalized.OutdoorBandThresholds[ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(key)]);

        var outdoorServiceBandPrices = DefaultSnapshot.OutdoorServiceBandPrices.Keys.ToDictionary(
            key => key,
            key => normalized.OutdoorServiceBandPrices[ContractTermsConfigKeyCodec.EncodeOutdoorPriceKey(key)]);

        var animalBuildingPrices = DefaultSnapshot.AnimalBuildingPrices.Keys.ToDictionary(
            key => key,
            key => normalized.AnimalBuildingPrices[ContractTermsConfigKeyCodec.EncodeAnimalBuildingPriceKey(key)]);

        var greenhouseServicePrices = DefaultSnapshot.GreenhouseServicePrices.Keys.ToDictionary(
            key => key,
            key => normalized.GreenhouseServicePrices[ContractTermsConfigKeyCodec.EncodeGreenhousePriceKey(key)]);

        var workActionCosts = DefaultSnapshot.WorkActionCosts.Keys.ToDictionary(
            key => key,
            key => normalized.WorkActionCosts[ContractTermsConfigKeyCodec.EncodeWorkActionKey(key)]);

        return ConfigSnapshotFactory.Create(
            normalized.BaseRate,
            increments,
            normalized.AverageSpeedConstant,
            normalized.HardCapTime,
            normalized.StuckInitialWaitMinutes,
            normalized.StuckPostTeleportWaitMinutes,
            outdoorBandThresholds,
            outdoorServiceBandPrices,
            animalBuildingPrices,
            greenhouseServicePrices,
            normalized.WorkerDailyEnergyCapacity,
            workActionCosts);
    }

    private static readonly IConfigSnapshot DefaultSnapshot = ConfigDefaults.Build();

    private static Dictionary<string, int> NormalizeNonNegativeDictionary(
        Dictionary<string, int>? actual,
        Dictionary<string, int> defaults,
        string propertyName,
        Action<string>? logWarning)
    {
        var source = actual ?? new Dictionary<string, int>();
        var normalized = new Dictionary<string, int>();
        foreach (var defaultEntry in defaults)
        {
            if (source.TryGetValue(defaultEntry.Key, out var value) && value >= 0)
            {
                normalized[defaultEntry.Key] = value;
                continue;
            }

            logWarning?.Invoke(BuildFallbackCode(propertyName, defaultEntry.Key, defaultEntry.Value));
            normalized[defaultEntry.Key] = defaultEntry.Value;
        }

        return normalized;
    }

    private static Dictionary<string, int> NormalizePositiveDictionary(
        Dictionary<string, int>? actual,
        Dictionary<string, int> defaults,
        string propertyName,
        Action<string>? logWarning)
    {
        var source = actual ?? new Dictionary<string, int>();
        var normalized = new Dictionary<string, int>();
        foreach (var defaultEntry in defaults)
        {
            if (source.TryGetValue(defaultEntry.Key, out var value) && value > 0)
            {
                normalized[defaultEntry.Key] = value;
                continue;
            }

            logWarning?.Invoke(BuildFallbackCode(propertyName, defaultEntry.Key, defaultEntry.Value));
            normalized[defaultEntry.Key] = defaultEntry.Value;
        }

        return normalized;
    }

    private static string BuildFallbackCode(string propertyName, string key, int defaultValue)
    {
        var sanitizedKey = new string(key.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        return $"ConfigFallback_{propertyName}_{sanitizedKey}_Default{defaultValue}";
    }
}
