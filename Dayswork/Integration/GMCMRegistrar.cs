using Dayswork.Core.Domain;
using Dayswork.Core.Energy;
using StardewModdingAPI;

namespace Dayswork.Integration;

internal sealed class GMCMRegistrar
{
    private const string GmcmUniqueId = "spacechase0.GenericModConfigMenu";

    private static readonly IReadOnlyList<EnergyTier> EnergyTierOrder = Enum.GetValues<EnergyTier>();
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

        RegisterBehaviorOptions(api);
        RegisterStaminaOptions(api);
        RegisterPricingOptions(api);
    }

    private void RegisterPricingOptions(IGenericModConfigMenuApi api)
    {
        api.AddSectionTitle(
            _manifest,
            () => I18nHelper.Get("gmcm.section.pricing.name"),
            () => I18nHelper.Get("gmcm.section.pricing.tooltip"));

        foreach (var tier in EnergyTierOrder)
        {
            var encodedKey = ContractTermsConfigKeyCodec.EncodeEnergyTierKey(tier);

            RegisterIntOption(
                api,
                new IntOptionSpec(
                    () => _config.Editable.EnergyTierEnergy[encodedKey],
                    value => _config.Editable.EnergyTierEnergy[encodedKey] = value,
                    () => I18nHelper.Get("gmcm.pricing.tier_energy.name", new { tier = EnergyTierLabel(tier) }),
                    () => I18nHelper.Get("gmcm.pricing.tier_energy.tooltip", new { tier = EnergyTierLabel(tier) }),
                    1,
                    1000,
                    10,
                    $"pricing-tier-energy-{EnergyTierKey(tier)}"));

            RegisterIntOption(
                api,
                new IntOptionSpec(
                    () => _config.Editable.EnergyTierPrice[encodedKey],
                    value => _config.Editable.EnergyTierPrice[encodedKey] = value,
                    () => I18nHelper.Get("gmcm.pricing.tier_price.name", new { tier = EnergyTierLabel(tier) }),
                    () => I18nHelper.Get("gmcm.pricing.tier_price.tooltip", new { tier = EnergyTierLabel(tier) }),
                    0,
                    100000,
                    10,
                    $"pricing-tier-price-{EnergyTierKey(tier)}"));
        }
    }

    private void RegisterStaminaOptions(IGenericModConfigMenuApi api)
    {
        api.AddSectionTitle(
            _manifest,
            () => I18nHelper.Get("gmcm.section.stamina.name"),
            () => I18nHelper.Get("gmcm.section.stamina.tooltip"));

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

        api.AddBoolOption(
            _manifest,
            () => _config.Editable.WorkOnHolidays,
            value => _config.Editable.WorkOnHolidays = value,
            () => I18nHelper.Get("gmcm.worker.work_on_holidays.name"),
            () => I18nHelper.Get("gmcm.worker.work_on_holidays.tooltip"),
            fieldId: "worker-work-on-holidays");

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

    private static string EnergyTierLabel(EnergyTier tier) => I18nHelper.Get($"gmcm.common.tier.{EnergyTierKey(tier)}");

    private static string WorkActionLabel(WorkActionKind action) => I18nHelper.Get($"gmcm.common.action.{WorkActionKey(action)}");

    private static string EnergyTierKey(EnergyTier tier) => tier switch
    {
        EnergyTier.HalfDay => "half_day",
        EnergyTier.FullDay => "full_day",
        EnergyTier.Overtime => "overtime",
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
        WorkActionKind.HoeSwing => "hoe_swing",
        WorkActionKind.PlantSeed => "plant_seed",
        WorkActionKind.ApplyFertilizer => "apply_fertilizer",
        WorkActionKind.CollectMachine => "collect_machine",
        WorkActionKind.LoadMachine => "load_machine",
        WorkActionKind.CollectFishPond => "collect_fish_pond",
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
