using Dayswork.Compat;
using Dayswork.Core.Compat;
using Dayswork.Core.Config;
using Dayswork.Core.Capabilities;
using Dayswork.Core.Energy;
using Dayswork.Core.Inventory;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Diagnostics;
using Dayswork.Integration;
using Dayswork.Orchestration;
using Dayswork.UI;
using Dayswork.Worker;
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
    // Expansion-compatibility seam (U-SVE-01). Vanilla-by-default; active profile resolved at GameLaunched.
    internal static ExpansionCompatService ExpansionCompat { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        ModMonitor = this.Monitor;
        I18nHelper.Init(helper);

        // ── Core singletons (dependency order) ──────────────────────────────
        var logWarning  = (string msg) => this.Monitor.Log(msg, LogLevel.Warn);
        var configManager = new ModConfigManager(helper, msg => this.Monitor.Log(msg, LogLevel.Warn));
        var config      = configManager.CurrentSnapshot;
        var configResolver = new ConfigValueResolver();
        var workScopeClassifier = new WorkScopeClassifier();
        var contractTermsBuilder = new ContractTermsBuilder(
            workScopeClassifier,
            new WorkerEnergyProfileBuilder(configResolver),
            configResolver);
        var recurringDecisionEngine = new RecurringDayStartDecisionEngine(contractTermsBuilder);
        var store       = new ContractStore(logWarning);
        var serializer  = new SaveDataSerializer(logWarning);

        // ── Mod singletons ───────────────────────────────────────────────────
        var chestResolver = new ChestResolver(Helper);
        Coordinator = new HiringFlowCoordinator(contractTermsBuilder, configManager, store, chestResolver, Helper);
        var buildingInteraction = new HiringBuildingInteraction(helper);
        var buildingOverlay = new HiringBuildingOverlayRenderer();
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
        // U-25 WS2: missed/overflow items are deposited into the hiring building's static chest and
        // notices are shown as HUD messages — no Mail Framework Mod, no mailbox delivery.
        var shiftOutcomeDispatcher = new ShiftOutcomeDispatcher();
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
            shiftOutcomeDispatcher);
        Orchestrator = orchestrator;
        var sessionResetHandler = new SessionResetHandler(orchestrator);
        var calendarHandlers = new CalendarHandlers(orchestrator);
        var scheduler       = new RecurringContractScheduler(
            store, orchestrator, calendarHandlers, recurringDecisionEngine, configManager, shiftOutcomeDispatcher);
        var gmcmRegistrar = new GMCMRegistrar(helper, this.ModManifest, configManager);

        // ── Expansion compatibility (U-SVE-01) ───────────────────────────────
        // Vanilla-by-default seam; the active profile is resolved at GameLaunched (after all mods
        // load) and assigned below. Consumers are wired in U-SVE-02..04 — this unit only detects,
        // so behavior is unchanged for vanilla and (with empty SVE tables) for SVE too.
        var vanillaProfile    = new VanillaExpansionProfile();
        var sveProfile        = new SveExpansionProfile();
        var expansionSelector = new ExpansionProfileSelector(new IExpansionProfile[] { sveProfile, vanillaProfile });
        var expansionDetector = new ExpansionDetector(helper.ModRegistry, expansionSelector, vanillaProfile, this.Monitor);
        var expansionCompat   = new ExpansionCompatService(vanillaProfile, new AnimalBuildingCapacityPolicy());
        ExpansionCompat = expansionCompat;

        // ── Event registrations ──────────────────────────────────────────────
        // Register optional integrations after all mods are initialised.
        helper.Events.GameLoop.GameLaunched += (_, _) =>
        {
            gmcmRegistrar.RegisterIfAvailable();
            expansionCompat.SetActiveProfile(expansionDetector.ResolveActiveProfile());
        };
        helper.Events.GameLoop.ReturnedToTitle += sessionResetHandler.OnReturnedToTitle;
        helper.Events.GameLoop.SaveLoaded   += sessionResetHandler.OnSaveLoaded;
        helper.Events.GameLoop.SaveLoaded   += persistAdapter.OnSaveLoaded;
        // Stop and settle any in-flight shift (sleep-stop + overflow delivery) BEFORE contracts
        // persist and before the day rolls over — handler order is authoritative (Pattern S /
        // REL-U15-02). U-21 BR-END-03 / BR-SLEEP-02 removed refund settlement from this path.
        helper.Events.GameLoop.Saving       += calendarHandlers.OnSavingHook;
        helper.Events.GameLoop.Saving       += persistAdapter.OnSaving;
        helper.Events.GameLoop.DayStarted   += scheduler.OnDayStarted;
        // Reset the "worker done for the day" animation flag each morning (office goes dark again).
        helper.Events.GameLoop.DayStarted   += (_, _) => HiringBuilding.WorkCompletedToday = false;
        helper.Events.GameLoop.UpdateTicked += orchestrator.OnUpdateTicked;
        helper.Events.GameLoop.TimeChanged  += orchestrator.OnTimeChanged;
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Input.ButtonPressed += buildingInteraction.OnButtonPressed;
        // Evening lit-windows + chimney smoke once the worker has finished for the day.
        helper.Events.Display.RenderedWorld += buildingOverlay.OnRenderedWorld;

        // Dev tooling (verbose diagnostics + debug console commands) — gated by DevLog.Enabled, which
        // is off for release. One switch keeps the whole dev kit out of shipped builds.
        if (DevLog.Enabled)
        {
            var playerTileStepLogger = new PlayerTileStepLogger(this.Monitor);
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => playerTileStepLogger.Reset();
            helper.Events.GameLoop.SaveLoaded      += (_, _) => playerTileStepLogger.Reset();
            helper.Events.GameLoop.UpdateTicked    += playerTileStepLogger.OnUpdateTicked;
            RegisterDebugCommands(helper, store, playerTileStepLogger);
        }

        this.Monitor.Log($"Dayswork loaded ({this.ModManifest.Version})", LogLevel.Trace);
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        // Redirect the placeholder NPC portrait to Marnie's existing texture.
        // Custom farmhand art is post-v1 (FR-NPC-01).
        if (e.NameWithoutLocale.IsEquivalentTo($"Portraits/{FarmhandNpc.InternalName}"))
            e.LoadFrom(
                () => Game1.content.Load<Texture2D>(FarmhandNpc.PlaceholderPortraitPath),
                AssetLoadPriority.Medium);

        // The hiring building's texture + Data/Buildings entry (U-25 WS2).
        HiringBuilding.OnAssetRequested(e, this.Helper);
    }

    // Dev-only debug commands — registered from Entry only when DevLog.Enabled (off for release).
    private void RegisterDebugCommands(IModHelper helper, ContractStore store, PlayerTileStepLogger playerTileStepLogger)
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
                        $"price={c.TermsSnapshot.Pricing.TotalPrice}g",
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

        helper.ConsoleCommands.Add(
            "dayswork_debug_player_tile",
            "Toggles player tile step logging to SMAPI debug logs. Usage: dayswork_debug_player_tile [on|off|toggle|status]",
            (_, args) =>
            {
                var mode = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "toggle";
                switch (mode)
                {
                    case "on":
                        playerTileStepLogger.SetEnabled(true);
                        break;

                    case "off":
                        playerTileStepLogger.SetEnabled(false);
                        break;

                    case "toggle":
                        playerTileStepLogger.Toggle();
                        break;

                    case "status":
                        this.Monitor.Log(
                            $"[Dayswork][debug] Player tile step logger is currently {(playerTileStepLogger.IsEnabled ? "enabled" : "disabled")}.",
                            LogLevel.Info);
                        break;

                    default:
                        this.Monitor.Log(
                            "[Dayswork][debug] Usage: dayswork_debug_player_tile [on|off|toggle|status]",
                            LogLevel.Info);
                        break;
                }
            });
    }
}
