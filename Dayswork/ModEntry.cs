using Dayswork.Core.Config;
using Dayswork.Core.Capabilities;
using Dayswork.Core.Energy;
using Dayswork.Core.Inventory;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Integration;
using Dayswork.Orchestration;
using Dayswork.UI;
using Dayswork.Worker;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork;

public sealed class ModEntry : Mod
{
    // Exposed as internal static so static patch classes can emit log messages and
    // invoke coordinator methods without constructor injection.
    internal static IMonitor ModMonitor { get; private set; } = null!;
    internal static HiringFlowCoordinator Coordinator { get; private set; } = null!;
    internal static ShiftOrchestrator Orchestrator { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        ModMonitor = this.Monitor;
        I18nHelper.Init(helper);

        // ── Core singletons (dependency order) ──────────────────────────────
        var logWarning  = (string msg) => this.Monitor.Log(msg, LogLevel.Warn);
        var configManager = new ModConfigManager(helper, msg => this.Monitor.Log(msg, LogLevel.Warn));
        var config      = configManager.CurrentSnapshot;
        var configResolver = new ConfigValueResolver();
        var rateCalc    = new RateCalculator();
        var depositCalc = new DepositCalculator();
        var workScopeClassifier = new WorkScopeClassifier();
        var contractTermsBuilder = new ContractTermsBuilder(
            workScopeClassifier,
            new OutdoorServiceBandClassifier(configResolver),
            new ContractPriceCalculator(configResolver),
            new PriceBreakdownBuilder(configResolver),
            new WorkerEnergyProfileBuilder(configResolver));
        var recurringDecisionEngine = new RecurringDayStartDecisionEngine(contractTermsBuilder);
        var store       = new ContractStore(logWarning);
        var serializer  = new SaveDataSerializer(logWarning);

        // ── Mod singletons ───────────────────────────────────────────────────
        var chestResolver = new ChestResolver(Helper);
        Coordinator = new HiringFlowCoordinator(rateCalc, depositCalc, contractTermsBuilder, configManager, store, chestResolver, Helper);
        var persistAdapter  = new ContractPersistenceAdapter(
            store, serializer, helper.Data, this.ModManifest.Version.ToString());
        var toolReader      = new ToolLevelReader();
        var toolAnimator    = new ToolSwapAnimator();
        var movementDriver  = new WorkerMovementDriver();
        var workAreaScanner = new WorkAreaScanner(new CapabilityEvaluator());
        var indoorScanner   = new IndoorWorkScanner(workAreaScanner);
        var animalHandler   = new AnimalTaskHandler(this.Monitor);
        var buildingNavigator = new BuildingWorkNavigator(this.Monitor, movementDriver);
        var depositPlanner  = new DepositPlanner();
        // MFM is a required dependency (manifest). Mod-provided APIs must be fetched after all mods
        // initialize (GameLaunched), NOT in Entry() — so construct the dispatcher now and inject the
        // API on GameLaunched below. If it's ever null, MailDispatcher logs and falls back so no
        // items are lost (REL-U14-05).
        var mailDispatcher  = new MailDispatcher(helper.Data);
        var orchestrator    = new ShiftOrchestrator(
            toolReader,
            config,
            workScopeClassifier,
            toolAnimator,
            movementDriver,
            workAreaScanner,
            indoorScanner,
            animalHandler,
            buildingNavigator,
            chestResolver,
            depositPlanner,
            mailDispatcher);
        Orchestrator = orchestrator;
        var calendarHandlers = new CalendarHandlers(orchestrator);
        var scheduler       = new RecurringContractScheduler(
            store, orchestrator, calendarHandlers, recurringDecisionEngine, configManager, mailDispatcher);
        var gmcmRegistrar = new GMCMRegistrar(helper, this.ModManifest, configManager);

        // ── Event registrations ──────────────────────────────────────────────
        // Fetch the MFM API after all mods are initialised (never in Entry()).
        helper.Events.GameLoop.GameLaunched += (_, _) =>
        {
            mailDispatcher.SetApi(helper.ModRegistry.GetApi("DIGUS.MailFrameworkMod"));
            gmcmRegistrar.RegisterIfAvailable();
        };
        helper.Events.GameLoop.SaveLoaded   += persistAdapter.OnSaveLoaded;
        helper.Events.GameLoop.SaveLoaded   += mailDispatcher.OnSaveLoaded;
        // Stop and settle any in-flight shift (sleep-stop + mailed refund) BEFORE contracts persist and
        // before the day rolls over — handler order is authoritative (Pattern S / REL-U15-02).
        helper.Events.GameLoop.Saving       += calendarHandlers.OnSavingHook;
        helper.Events.GameLoop.Saving       += persistAdapter.OnSaving;
        helper.Events.GameLoop.DayStarted   += scheduler.OnDayStarted;
        helper.Events.GameLoop.UpdateTicked += orchestrator.OnUpdateTicked;
        helper.Events.GameLoop.TimeChanged  += orchestrator.OnTimeChanged;
        helper.Events.Content.AssetRequested += OnAssetRequested;

        // ── Harmony patches ──────────────────────────────────────────────────
        new Harmony(this.ModManifest.UniqueID).PatchAll();

        // TODO: REMOVE before release — debug command for verifying save/load persistence (task #1 play-test)
        RegisterDebugCommands(helper, store);

        this.Monitor.Log($"Dayswork loaded ({this.ModManifest.Version}) build=U24-Step19", LogLevel.Info);
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        // Redirect the placeholder NPC portrait to Marnie's existing texture.
        // Custom farmhand art is post-v1 (FR-NPC-01).
        if (e.NameWithoutLocale.IsEquivalentTo($"Portraits/{FarmhandNpc.InternalName}"))
            e.LoadFrom(
                () => Game1.content.Load<Texture2D>(FarmhandNpc.PlaceholderPortraitPath),
                AssetLoadPriority.Medium);
    }

    // TODO: REMOVE before release — see RegisterDebugCommands call above
    private void RegisterDebugCommands(IModHelper helper, ContractStore store)
    {
        helper.ConsoleCommands.Add(
            "dayswork_list",
            "Lists all contracts currently in memory. Used to verify save/load persistence.",
            (_, _) =>
            {
                var contracts = store.List();
                if (contracts.Count == 0)
                {
                    this.Monitor.Log("No contracts in store.", LogLevel.Info);
                    return;
                }
                foreach (var c in contracts)
                {
                    this.Monitor.Log(
                        $"[{c.Id.Value}] status={c.Status} tasks={string.Join(",", c.EnabledTasks)} " +
                        $"hired={c.HireDate.Day} {c.HireDate.Season} Y{c.HireDate.Year} " +
                        $"deposit={c.DepositAmount}g rate={c.HourlyRate}g/hr",
                        LogLevel.Info);
                }
            });

        helper.ConsoleCommands.Add(
            "dayswork_end_shift",
            "Ends the current worker shift immediately. Worker deposits buffered items and exits.",
            (_, _) => Orchestrator.EndShiftEarly());

        helper.ConsoleCommands.Add(
            "dayswork_debug_buildings",
            "Logs Dayswork's current building-resolution candidates for the farm.",
            (_, args) =>
            {
                var requested = args.Length > 0 ? string.Join(" ", args) : "Greenhouse";
                this.Monitor.Log(BuildingLocationResolver.DescribeResolutionState(Game1.getFarm(), requested), LogLevel.Info);
            });
    }
}
