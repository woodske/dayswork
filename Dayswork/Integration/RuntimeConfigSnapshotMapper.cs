using Dayswork.Core.Config;
using Dayswork.Core.Domain;

namespace Dayswork.Integration;

internal static class RuntimeConfigSnapshotMapper
{
    public static ModConfig Normalize(ModConfig config, Action<string>? logWarning = null)
    {
        var defaults = ModConfig.CreateDefaults();

        return new ModConfig
        {
            HardCapTime = Math.Clamp(config.HardCapTime, 1000, 2600),
            StuckInitialWaitMinutes = Math.Max(1, config.StuckInitialWaitMinutes),
            StuckPostTeleportWaitMinutes = Math.Max(1, config.StuckPostTeleportWaitMinutes),
            WorkerWalkPixelsPerTick = config.WorkerWalkPixelsPerTick > 0
                ? config.WorkerWalkPixelsPerTick
                : defaults.WorkerWalkPixelsPerTick,
            WorkerActionAnimationMs = Math.Max(1, config.WorkerActionAnimationMs),
            WorkerEntranceHoldTicks = Math.Max(0, config.WorkerEntranceHoldTicks),
            WorkOnHolidays = config.WorkOnHolidays,
            PreferredCropStore = NormalizePreferredCropStore(config.PreferredCropStore),
            EnergyTierEnergy = NormalizePositiveDictionary(
                config.EnergyTierEnergy,
                defaults.EnergyTierEnergy,
                nameof(ModConfig.EnergyTierEnergy),
                logWarning),
            EnergyTierPrice = NormalizeNonNegativeDictionary(
                config.EnergyTierPrice,
                defaults.EnergyTierPrice,
                nameof(ModConfig.EnergyTierPrice),
                logWarning),
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

        var energyTierEnergy = DefaultSnapshot.EnergyTierEnergy.Keys.ToDictionary(
            key => key,
            key => normalized.EnergyTierEnergy[ContractTermsConfigKeyCodec.EncodeEnergyTierKey(key)]);

        var energyTierPrice = DefaultSnapshot.EnergyTierPrice.Keys.ToDictionary(
            key => key,
            key => normalized.EnergyTierPrice[ContractTermsConfigKeyCodec.EncodeEnergyTierKey(key)]);

        var workActionCosts = DefaultSnapshot.WorkActionCosts.Keys.ToDictionary(
            key => key,
            key => normalized.WorkActionCosts[ContractTermsConfigKeyCodec.EncodeWorkActionKey(key)]);

        return ConfigSnapshotFactory.Create(
            normalized.HardCapTime,
            normalized.StuckInitialWaitMinutes,
            normalized.StuckPostTeleportWaitMinutes,
            normalized.WorkerWalkPixelsPerTick,
            normalized.WorkerActionAnimationMs,
            normalized.WorkerEntranceHoldTicks,
            normalized.WorkOnHolidays,
            energyTierEnergy,
            energyTierPrice,
            workActionCosts);
    }

    private static readonly ConfigSnapshot DefaultSnapshot = ConfigDefaults.Build();

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

    private static string NormalizePreferredCropStore(string? value) => value switch
    {
        "Pierre" => "Pierre",
        "Joja" => "Joja",
        _ => "Either",
    };

    private static string BuildFallbackCode(string propertyName, string key, int defaultValue)
    {
        var sanitizedKey = new string(key.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        return $"ConfigFallback_{propertyName}_{sanitizedKey}_Default{defaultValue}";
    }
}
