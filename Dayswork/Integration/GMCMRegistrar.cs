using Dayswork.Core.Domain;
using StardewModdingAPI;

namespace Dayswork.Integration;

internal sealed class GMCMRegistrar
{
    private const string GmcmUniqueId = "spacechase0.GenericModConfigMenu";

    // Declared before RateOptions so the static initializer can reference it without null.
    private static readonly TaskKind[] TaskKindOrder =
    {
        TaskKind.WaterCrops,
        TaskKind.HarvestCrops,
        TaskKind.CollectFruit,
        TaskKind.FeedAnimals,
        TaskKind.PetAnimals,
        TaskKind.CollectAnimalProducts,
        TaskKind.CutTrees,
        TaskKind.ClearRocks,
        TaskKind.ClearWeeds,
        TaskKind.ClearGrass,
    };

    private static readonly IReadOnlyList<IntOptionSpec> RateOptions =
        new IntOptionSpec[]
        {
            new(
                "gmcm.base_rate",
                static config => config.BaseRate,
                static (config, value) => config.BaseRate = value,
                0,
                1000,
                5),
        }.Concat(TaskKindOrder.Select(static task => new IntOptionSpec(
            $"gmcm.rate.{TaskKey(task)}",
            config => config.GetTaskRate(task),
            (config, value) => config.SetTaskRate(task, value),
            0,
            1000,
            5,
            "gmcm.rate.task.tooltip")))
        .ToArray();

    private static readonly IReadOnlyList<FloatOptionSpec> WorkerFloatOptions =
        new FloatOptionSpec[]
        {
            new(
                "gmcm.average_speed_constant",
                static config => (float)config.AverageSpeedConstant,
                static (config, value) => config.AverageSpeedConstant = value,
                0.05f,
                10f,
                0.05f),
        };

    private static readonly IReadOnlyList<IntOptionSpec> WorkerIntOptions =
        new IntOptionSpec[]
        {
            new(
                "gmcm.hard_cap_time",
                static config => config.HardCapTime,
                static (config, value) => config.HardCapTime = value,
                1000,
                2600,
                10),
            new(
                "gmcm.stuck_initial_wait",
                static config => config.StuckInitialWaitMinutes,
                static (config, value) => config.StuckInitialWaitMinutes = value,
                1,
                120,
                1),
            new(
                "gmcm.stuck_post_teleport_wait",
                static config => config.StuckPostTeleportWaitMinutes,
                static (config, value) => config.StuckPostTeleportWaitMinutes = value,
                1,
                120,
                1),
        };

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

        api.AddSectionTitle(
            _manifest,
            () => I18nHelper.Get("gmcm.section.rates.name"),
            () => I18nHelper.Get("gmcm.section.rates.tooltip"));

        foreach (var option in RateOptions)
            RegisterIntOption(api, option);

        api.AddSectionTitle(
            _manifest,
            () => I18nHelper.Get("gmcm.section.worker.name"),
            () => I18nHelper.Get("gmcm.section.worker.tooltip"));

        foreach (var option in WorkerFloatOptions)
            RegisterFloatOption(api, option);

        foreach (var option in WorkerIntOptions)
            RegisterIntOption(api, option);
    }

    private void RegisterIntOption(IGenericModConfigMenuApi api, IntOptionSpec option)
    {
        api.AddNumberOption(
            _manifest,
            () => option.Getter(_config.Editable),
            value => option.Setter(_config.Editable, value),
            () => I18nHelper.Get($"{option.KeyRoot}.name"),
            () => I18nHelper.Get(option.TooltipKey ?? $"{option.KeyRoot}.tooltip"),
            option.Min,
            option.Max,
            option.Interval);
    }

    private void RegisterFloatOption(IGenericModConfigMenuApi api, FloatOptionSpec option)
    {
        api.AddNumberOption(
            _manifest,
            () => option.Getter(_config.Editable),
            value => option.Setter(_config.Editable, value),
            () => I18nHelper.Get($"{option.KeyRoot}.name"),
            () => I18nHelper.Get($"{option.KeyRoot}.tooltip"),
            option.Min,
            option.Max,
            option.Interval);
    }

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

    private sealed record IntOptionSpec(
        string KeyRoot,
        Func<ModConfig, int> Getter,
        Action<ModConfig, int> Setter,
        int Min,
        int Max,
        int Interval,
        string? TooltipKey = null);

    private sealed record FloatOptionSpec(
        string KeyRoot,
        Func<ModConfig, float> Getter,
        Action<ModConfig, float> Setter,
        float Min,
        float Max,
        float Interval);
}
