using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using StardewModdingAPI;

namespace Dayswork.Integration;

internal sealed class GMCMRegistrar
{
    private const string GmcmUniqueId = "spacechase0.GenericModConfigMenu";

    private static readonly IReadOnlyList<OutdoorBandSize> OutdoorBandOrder = Enum.GetValues<OutdoorBandSize>();
    private static readonly IReadOnlyList<AnimalBuildingTier> AnimalTierOrder = Enum.GetValues<AnimalBuildingTier>();
    private static readonly IReadOnlyList<WorkActionKind> WorkActionOrder = Enum.GetValues<WorkActionKind>();

    private readonly IModHelper _helper;
    private readonly IManifest _manifest;
    private readonly ModConfigManager _config;

    public GMCMRegistrar(IModHelper helper, IManifest manifest, ModConfigManager config)
    {
        _helper = helper;
        _manifest = manifest;
        _config = config;
    }

    public void RegisterIfAvailable()
    {
        var api = _helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GmcmUniqueId);
        if (api is null)
            return;

        api.Register(_manifest, _config.ResetToDefaults, _config.SaveAndPublish);

        RegisterPricingOptions(api);
        RegisterStaminaOptions(api);
        RegisterBehaviorOptions(api);
    }

    private void RegisterPricingOptions(IGenericModConfigMenuApi api)
    {
        api.AddSectionTitle(
            _manifest,
            () => I18nHelper.Get("gmcm.section.pricing.name"),
            () => I18nHelper.Get("gmcm.section.pricing.tooltip"));

        foreach (var band in OutdoorBandOrder)
        {
            var encodedKey = ContractTermsConfigKeyCodec.EncodeOutdoorBandKey(band);
            RegisterIntOption(
                api,
                new IntOptionSpec(
                    () => _config.Editable.OutdoorBandThresholds[encodedKey],
                    value => _config.Editable.OutdoorBandThresholds[encodedKey] = value,
                    () => I18nHelper.Get("gmcm.pricing.outdoor_threshold.name", new { band = OutdoorBandLabel(band) }),
                    () => I18nHelper.Get("gmcm.pricing.outdoor_threshold.tooltip", new { band = OutdoorBandLabel(band) }),
                    1,
                    999999,
                    1,
                    $"pricing-outdoor-threshold-{BandKey(band)}"));
        }

        foreach (var service in TaskKindSets.OutdoorServices)
        {
            foreach (var band in OutdoorBandOrder)
            {
                var key = new OutdoorPriceKey(service, band);
                var encodedKey = ContractTermsConfigKeyCodec.EncodeOutdoorPriceKey(key);
                RegisterIntOption(
                    api,
                    new IntOptionSpec(
                        () => _config.Editable.OutdoorServiceBandPrices[encodedKey],
                        value => _config.Editable.OutdoorServiceBandPrices[encodedKey] = value,
                        () => I18nHelper.Get("gmcm.pricing.outdoor_price.name", new
                        {
                            service = TaskLabel(service),
                            band = OutdoorBandLabel(band),
                        }),
                        () => I18nHelper.Get("gmcm.pricing.outdoor_price.tooltip", new
                        {
                            service = TaskLabel(service),
                            band = OutdoorBandLabel(band),
                        }),
                        0,
                        100000,
                        5,
                        $"pricing-outdoor-{TaskKey(service)}-{BandKey(band)}"));
            }
        }

        foreach (var service in TaskKindSets.AnimalServices)
        {
            foreach (var tier in AnimalTierOrder)
            {
                var key = new AnimalBuildingPriceKey(service, tier);
                var encodedKey = ContractTermsConfigKeyCodec.EncodeAnimalBuildingPriceKey(key);
                RegisterIntOption(
                    api,
                    new IntOptionSpec(
                        () => _config.Editable.AnimalBuildingPrices[encodedKey],
                        value => _config.Editable.AnimalBuildingPrices[encodedKey] = value,
                        () => I18nHelper.Get("gmcm.pricing.animal_price.name", new
                        {
                            service = TaskLabel(service),
                            tier = AnimalTierLabel(tier),
                        }),
                        () => I18nHelper.Get("gmcm.pricing.animal_price.tooltip", new
                        {
                            service = TaskLabel(service),
                            tier = AnimalTierLabel(tier),
                        }),
                        0,
                        100000,
                        5,
                        $"pricing-animal-{TaskKey(service)}-{AnimalTierKey(tier)}"));
            }
        }

        foreach (var service in TaskKindSets.GreenhouseServices)
        {
            var key = new GreenhousePriceKey(service);
            var encodedKey = ContractTermsConfigKeyCodec.EncodeGreenhousePriceKey(key);
            RegisterIntOption(
                api,
                new IntOptionSpec(
                    () => _config.Editable.GreenhouseServicePrices[encodedKey],
                    value => _config.Editable.GreenhouseServicePrices[encodedKey] = value,
                    () => I18nHelper.Get("gmcm.pricing.greenhouse_price.name", new { service = TaskLabel(service) }),
                    () => I18nHelper.Get("gmcm.pricing.greenhouse_price.tooltip", new { service = TaskLabel(service) }),
                    0,
                    100000,
                    5,
                    $"pricing-greenhouse-{TaskKey(service)}"));
        }
    }

    private void RegisterStaminaOptions(IGenericModConfigMenuApi api)
    {
        api.AddSectionTitle(
            _manifest,
            () => I18nHelper.Get("gmcm.section.stamina.name"),
            () => I18nHelper.Get("gmcm.section.stamina.tooltip"));

        RegisterIntOption(
            api,
            new IntOptionSpec(
                () => _config.Editable.WorkerDailyEnergyCapacity,
                value => _config.Editable.WorkerDailyEnergyCapacity = value,
                () => I18nHelper.Get("gmcm.stamina.daily_capacity.name"),
                () => I18nHelper.Get("gmcm.stamina.daily_capacity.tooltip"),
                1,
                1000,
                5,
                "stamina-daily-capacity"));

        foreach (var action in WorkActionOrder)
        {
            var encodedKey = ContractTermsConfigKeyCodec.EncodeWorkActionKey(action);
            RegisterIntOption(
                api,
                new IntOptionSpec(
                    () => _config.Editable.WorkActionCosts[encodedKey],
                    value => _config.Editable.WorkActionCosts[encodedKey] = value,
                    () => I18nHelper.Get("gmcm.stamina.action_cost.name", new { action = WorkActionLabel(action) }),
                    () => I18nHelper.Get("gmcm.stamina.action_cost.tooltip", new { action = WorkActionLabel(action) }),
                    0,
                    100,
                    1,
                    $"stamina-action-cost-{WorkActionKey(action)}"));
        }
    }

    private void RegisterBehaviorOptions(IGenericModConfigMenuApi api)
    {
        api.AddSectionTitle(
            _manifest,
            () => I18nHelper.Get("gmcm.section.worker_behavior.name"),
            () => I18nHelper.Get("gmcm.section.worker_behavior.tooltip"));

        RegisterFloatOption(
            api,
            new FloatOptionSpec(
                () => _config.Editable.WorkerWalkPixelsPerTick,
                value => _config.Editable.WorkerWalkPixelsPerTick = value,
                () => I18nHelper.Get("gmcm.worker.walk_pixels_per_tick.name"),
                () => I18nHelper.Get("gmcm.worker.walk_pixels_per_tick.tooltip"),
                0.5f,
                6f,
                0.1f,
                "worker-walk-pixels-per-tick"));

        RegisterIntOption(
            api,
            new IntOptionSpec(
                () => _config.Editable.WorkerActionAnimationMs,
                value => _config.Editable.WorkerActionAnimationMs = value,
                () => I18nHelper.Get("gmcm.worker.action_animation_ms.name"),
                () => I18nHelper.Get("gmcm.worker.action_animation_ms.tooltip"),
                1,
                2000,
                10,
                "worker-action-animation-ms"));

        RegisterIntOption(
            api,
            new IntOptionSpec(
                () => _config.Editable.WorkerEntranceHoldTicks,
                value => _config.Editable.WorkerEntranceHoldTicks = value,
                () => I18nHelper.Get("gmcm.worker.entrance_hold_ticks.name"),
                () => I18nHelper.Get("gmcm.worker.entrance_hold_ticks.tooltip"),
                0,
                600,
                10,
                "worker-entrance-hold-ticks"));

        RegisterIntOption(
            api,
            new IntOptionSpec(
                () => _config.Editable.HardCapTime,
                value => _config.Editable.HardCapTime = value,
                () => I18nHelper.Get("gmcm.worker.hard_cap_time.name"),
                () => I18nHelper.Get("gmcm.worker.hard_cap_time.tooltip"),
                1000,
                2600,
                10,
                "worker-hard-cap-time"));

        RegisterIntOption(
            api,
            new IntOptionSpec(
                () => _config.Editable.StuckInitialWaitMinutes,
                value => _config.Editable.StuckInitialWaitMinutes = value,
                () => I18nHelper.Get("gmcm.worker.stuck_initial_wait.name"),
                () => I18nHelper.Get("gmcm.worker.stuck_initial_wait.tooltip"),
                1,
                120,
                1,
                "worker-stuck-initial-wait"));

        RegisterIntOption(
            api,
            new IntOptionSpec(
                () => _config.Editable.StuckPostTeleportWaitMinutes,
                value => _config.Editable.StuckPostTeleportWaitMinutes = value,
                () => I18nHelper.Get("gmcm.worker.stuck_post_teleport_wait.name"),
                () => I18nHelper.Get("gmcm.worker.stuck_post_teleport_wait.tooltip"),
                1,
                120,
                1,
                "worker-stuck-post-teleport-wait"));
    }

    private void RegisterIntOption(IGenericModConfigMenuApi api, IntOptionSpec option)
    {
        api.AddNumberOption(
            _manifest,
            option.Getter,
            option.Setter,
            option.Name,
            option.Tooltip,
            option.Min,
            option.Max,
            option.Interval,
            fieldId: option.FieldId);
    }

    private void RegisterFloatOption(IGenericModConfigMenuApi api, FloatOptionSpec option)
    {
        api.AddNumberOption(
            _manifest,
            option.Getter,
            option.Setter,
            option.Name,
            option.Tooltip,
            option.Min,
            option.Max,
            option.Interval,
            fieldId: option.FieldId);
    }

    private static string TaskLabel(TaskKind task) => I18nHelper.Get($"ui.task_selection.{TaskKey(task)}");

    private static string OutdoorBandLabel(OutdoorBandSize band) => I18nHelper.Get($"gmcm.common.band.{BandKey(band)}");

    private static string AnimalTierLabel(AnimalBuildingTier tier) => I18nHelper.Get($"gmcm.common.tier.{AnimalTierKey(tier)}");

    private static string WorkActionLabel(WorkActionKind action) => I18nHelper.Get($"gmcm.common.action.{WorkActionKey(action)}");

    private static string TaskKey(TaskKind task) => task switch
    {
        TaskKind.WaterCrops => "water_crops",
        TaskKind.HarvestCrops => "harvest_crops",
        TaskKind.CollectFruit => "collect_fruit",
        TaskKind.FeedAnimals => "feed_animals",
        TaskKind.PetAnimals => "pet_animals",
        TaskKind.CollectAnimalProducts => "collect_animal_products",
        TaskKind.CutTrees => "cut_trees",
        TaskKind.ClearRocks => "clear_rocks",
        TaskKind.ClearWeeds => "clear_weeds",
        TaskKind.ClearGrass => "clear_grass",
        _ => throw new ArgumentOutOfRangeException(nameof(task), task, null),
    };

    private static string BandKey(OutdoorBandSize band) => band switch
    {
        OutdoorBandSize.Small => "small",
        OutdoorBandSize.Medium => "medium",
        OutdoorBandSize.Large => "large",
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, null),
    };

    private static string AnimalTierKey(AnimalBuildingTier tier) => tier switch
    {
        AnimalBuildingTier.Coop => "coop",
        AnimalBuildingTier.BigCoop => "big_coop",
        AnimalBuildingTier.DeluxeCoop => "deluxe_coop",
        AnimalBuildingTier.Barn => "barn",
        AnimalBuildingTier.BigBarn => "big_barn",
        AnimalBuildingTier.DeluxeBarn => "deluxe_barn",
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null),
    };

    private static string WorkActionKey(WorkActionKind action) => action switch
    {
        WorkActionKind.WaterTile => "water_tile",
        WorkActionKind.HarvestCrop => "harvest_crop",
        WorkActionKind.HarvestFruit => "harvest_fruit",
        WorkActionKind.FeedAnimal => "feed_animal",
        WorkActionKind.PetAnimal => "pet_animal",
        WorkActionKind.CollectAnimalProduct => "collect_animal_product",
        WorkActionKind.AxeSwing => "axe_swing",
        WorkActionKind.PickaxeSwing => "pickaxe_swing",
        WorkActionKind.ScytheSwing => "scythe_swing",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    private sealed record IntOptionSpec(
        Func<int> Getter,
        Action<int> Setter,
        Func<string> Name,
        Func<string> Tooltip,
        int Min,
        int Max,
        int Interval,
        string FieldId);

    private sealed record FloatOptionSpec(
        Func<float> Getter,
        Action<float> Setter,
        Func<string> Name,
        Func<string> Tooltip,
        float Min,
        float Max,
        float Interval,
        string FieldId);
}
