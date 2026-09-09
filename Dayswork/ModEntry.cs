using Dayswork.Compat;
using Dayswork.Core.Compat;
using Dayswork.Core.Config;
using Dayswork.Core.Capabilities;
using Dayswork.Core.Energy;
using Dayswork.Core.Inventory;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Core.Upgrades;
using Dayswork.Diagnostics;
using Dayswork.Integration;
using Dayswork.Net;
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
    /// <summary>Every live shift — one per office with a farmhand out today.</summary>
    internal static ShiftFleet Fleet { get; private set; } = null!;
    /// <summary>The one way a contract changes: menus submit here and the host decides. On the
    /// host's own computer the answer is synchronous, so single-player reads exactly as before.</summary>
    internal static ContractRequestClient Requests { get; private set; } = null!;
    /// <summary>Whether Dayswork is standing down for an incompatible peer, and (on a client)
    /// whether the host can run it at all.</summary>
    internal static DaysworkSuspension Suspension { get; private set; } = null!;
    // Expansion-compatibility seam. Vanilla-by-default; active profile resolved at GameLaunched.
    internal static ExpansionCompatService ExpansionCompat { get; private set; } = null!;

    public override void Entry(IModHelper helper)
    {
        ModMonitor = this.Monitor;
        I18nHelper.Init(helper);

        // Tree trunk drops are emitted later by Tree.tickUpdate, outside the worker's guarded axe
        // beat. This one verified Harmony boundary attributes those drops to the exact tree/shift;
        // if registration fails, only standing-tree work is disabled and the rest of the mod runs.
        TreeDropAttribution.Install(this.ModManifest.UniqueID);

        // ── Core singletons (dependency order) ──────────────────────────────
        var logWarning  = (string msg) => this.Monitor.Log(msg, DevLog.WarnLevel);
        var configManager = new ModConfigManager(helper, msg => this.Monitor.Log(msg, DevLog.WarnLevel));
        var config      = configManager.CurrentSnapshot;
        var configResolver = new ConfigValueResolver();
        var workScopeClassifier = new WorkScopeClassifier();
        var contractTermsBuilder = new ContractTermsBuilder(
            workScopeClassifier,
            new WorkerEnergyProfileBuilder(configResolver),
            configResolver);
        var recurringDecisionEngine = new RecurringDayStartDecisionEngine(contractTermsBuilder);
        var store       = new OfficeContractStore(logWarning);
        var serializer  = new SaveDataSerializer(logWarning);
        var upgradeStore = new FarmhandUpgradeStore();
        var upgradeSerializer = new FarmhandUpgradeSaveDataSerializer(logWarning);

        // ── Mod singletons ───────────────────────────────────────────────────
        var chestResolver = new ChestResolver(Helper);
        var officeChestService = new OfficeChestService();
        Coordinator = new HiringFlowCoordinator(contractTermsBuilder, configManager, store, upgradeStore, chestResolver, Helper);
        var buildingOverlay = new HiringBuildingOverlayRenderer();
        // Missed/overflow items are deposited into the shift's own office chest; notices go to the
        // contract's owner through OwnerNotifier — no Mail Framework Mod, no mailbox delivery.
        var shiftOutcomeDispatcher = new ShiftOutcomeDispatcher();
        var contractPersistence = new OfficeContractPersistence(
            store, serializer, helper.Data, this.ModManifest.Version.ToString(), shiftOutcomeDispatcher);
        // The interaction re-reads contracts from the world before opening a menu, so a client's
        // card reflects the host's latest rather than what it loaded with.
        var buildingInteraction = new HiringBuildingInteraction(helper, contractPersistence);
        var upgradePersistAdapter = new FarmhandUpgradePersistenceAdapter(
            upgradeStore, upgradeSerializer, helper.Data);
        var toolReader      = new ToolLevelReader();
        var workAreaScanner = new WorkAreaScanner();
        var indoorScanner   = new IndoorWorkScanner(workAreaScanner);
        var animalHandler   = new AnimalTaskHandler(this.Monitor);
        var buildingNavigator = new BuildingWorkNavigator(this.Monitor);
        var depositPlanner  = new DepositPlanner();

        // One orchestrator per live shift, each with its own movement driver, tool animator, and
        // travel runner — those hold per-worker state and cannot be shared. Everything above is
        // stateless (or shared by design) and is captured once.
        var fleet = new ShiftFleet(() => new ShiftOrchestrator(
            toolReader,
            config,
            workScopeClassifier,
            new ToolSwapAnimator(),
            new WorkerMovementDriver(),
            workAreaScanner,
            indoorScanner,
            animalHandler,
            buildingNavigator,
            chestResolver,
            depositPlanner,
            shiftOutcomeDispatcher));
        Fleet = fleet;
        var officeDemolition = new OfficeDemolitionHandler(store, fleet, shiftOutcomeDispatcher);
        var sessionResetHandler = new SessionResetHandler(fleet);
        var calendarHandlers = new CalendarHandlers(fleet);

        // ── Multiplayer (2.0 Phase 4) ────────────────────────────────────────
        // Host-authoritative: the engine above runs on the host alone, and every peer's menus reach
        // it through one request path. On the host's own computer that path is a direct call, so
        // single-player behaves exactly as it did before there was a protocol.
        var suspension = new DaysworkSuspension();
        var netChannel = new NetChannel(helper.Multiplayer);
        var requestHandler = new ContractRequestHandler(
            store, serializer, contractTermsBuilder, configManager, upgradeStore,
            new ContractReferenceResolver(chestResolver), chestResolver, fleet, suspension);
        var requestClient = new ContractRequestClient(
            netChannel, requestHandler, serializer, this.ModManifest.Version.ToString(), suspension);
        var network = new DaysworkNetwork(
            helper, netChannel, requestHandler, requestClient, suspension, configManager, store, fleet,
            this.ModManifest.Version.ToString());
        // A request the host never answered. The player's draft is still on screen (the flow only
        // closes on an acceptance), and the host may in fact have committed — reopening the office
        // reads its modData and shows the truth — so this says "not saved", not "lost".
        requestClient.TimedOut = () => Game1.addHUDMessage(
            new HUDMessage(I18nHelper.Get("ui.net.no_response"), HUDMessage.error_type));
        Requests = requestClient;
        Suspension = suspension;

        var scheduler       = new RecurringContractScheduler(
            store, fleet, calendarHandlers, recurringDecisionEngine, configManager, upgradeStore,
            shiftOutcomeDispatcher, suspension);
        var gmcmRegistrar = new GMCMRegistrar(helper, this.ModManifest, configManager);

        // ── Expansion compatibility ───────────────────────────────
        // Vanilla-by-default seam; the active profile is resolved at GameLaunched (after all mods
        // load) and assigned below.
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
        helper.Events.GameLoop.ReturnedToTitle += network.OnReturnedToTitle;
        helper.Events.GameLoop.SaveLoaded   += sessionResetHandler.OnSaveLoaded;
        // The peer picture is re-derived per save: whether this process is the host, and which
        // already-connected peers can run Dayswork, are both only knowable once a save is loaded.
        helper.Events.GameLoop.SaveLoaded   += network.OnSaveLoaded;
        helper.Events.GameLoop.SaveLoaded   += officeChestService.OnSaveLoaded;
        // Office chests are ensured first, so a contract hydrated from an office always finds
        // its porch chests in place.
        helper.Events.GameLoop.SaveLoaded   += contractPersistence.OnSaveLoaded;
        helper.Events.GameLoop.SaveLoaded   += upgradePersistAdapter.OnSaveLoaded;
        // Stop and settle every in-flight shift (sleep-stop + overflow delivery) BEFORE the day
        // rolls over — handler order is authoritative. Contracts need no save hook of their own:
        // they live on their office building, written the moment they change. Refund settlement is
        // not part of this path.
        helper.Events.GameLoop.Saving       += calendarHandlers.OnSavingHook;
        helper.Events.GameLoop.Saving       += upgradePersistAdapter.OnSaving;
        // The fleet's day reset (work claims, shopping ledgers, HUD dedup) must run before the
        // scheduler starts today's shifts.
        helper.Events.GameLoop.DayStarted   += fleet.OnDayStarted;
        helper.Events.GameLoop.DayStarted   += scheduler.OnDayStarted;
        helper.Events.GameLoop.DayStarted   += officeChestService.OnDayStarted;
        helper.Events.GameLoop.UpdateTicked += fleet.OnUpdateTicked;
        helper.Events.GameLoop.TimeChanged  += fleet.OnTimeChanged;
        // A remote client's outstanding requests time out here; on the host this never has anything
        // to do, because its own requests are answered synchronously.
        helper.Events.GameLoop.UpdateTicked += requestClient.OnUpdateTicked;
        helper.Events.GameLoop.UpdateTicked += network.OnUpdateTicked;
        // Keep each shift's passability cache in step with world changes (all no-op when no shift
        // is active). Worker-cleared resource clumps have no event and are invalidated at the clear
        // site; everything else rides these.
        helper.Events.World.ObjectListChanged         += fleet.OnObjectListChanged;
        helper.Events.World.TerrainFeatureListChanged += fleet.OnTerrainFeatureListChanged;
        helper.Events.World.FurnitureListChanged      += fleet.OnFurnitureListChanged;
        helper.Events.World.BuildingListChanged       += fleet.OnBuildingListChanged;
        // Demolishing an office ends its shift and takes its contract with it.
        helper.Events.World.BuildingListChanged       += officeDemolition.OnBuildingListChanged;
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Input.ButtonPressed += buildingInteraction.OnButtonPressed;
        // Evening lit-windows + chimney smoke once the worker has finished for the day.
        helper.Events.Display.RenderedWorld += buildingOverlay.OnRenderedWorld;
        // The "Dayswork is paused" banner, drawn over the HUD for as long as it lasts.
        helper.Events.Display.Rendered += suspension.OnRendered;

        // Multiplayer: the handshake, the incompatible-peer policy, and the request protocol.
        // Registered unconditionally — which side of each handler runs is decided inside, since
        // Context.IsMainPlayer is only meaningful once a save is loaded.
        helper.Events.Multiplayer.PeerContextReceived += network.OnPeerContextReceived;
        helper.Events.Multiplayer.PeerConnected       += network.OnPeerConnected;
        helper.Events.Multiplayer.PeerDisconnected    += network.OnPeerDisconnected;
        helper.Events.Multiplayer.ModMessageReceived  += network.OnModMessageReceived;

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
        // The worker's sprite sheet, its paint mask, every painted colour variant, and the portrait.
        FarmhandAppearance.OnAssetRequested(e);

        // The hiring building's texture + Data/Buildings entry.
        HiringBuilding.OnAssetRequested(e, this.Helper);
    }

    // Dev-only debug commands — registered from Entry only when DevLog.Enabled (off for release).
    private void RegisterDebugCommands(IModHelper helper, OfficeContractStore store, PlayerTileStepLogger playerTileStepLogger)
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
                        $"[{c.Id.Value}] office={c.OfficeId:N} owner={c.OwnerId} rev={c.Revision} " +
                        $"status={c.Status} tasks={string.Join(",", c.EnabledTasks)} " +
                        $"hired={c.HireDate.Day} {c.HireDate.Season} Y{c.HireDate.Year} " +
                        $"price={c.TermsSnapshot.Pricing.TotalPrice}g",
                        LogLevel.Info);
                }
            });

        helper.ConsoleCommands.Add(
            "dayswork_end_shift",
            "Ends worker shifts immediately (deposit buffered items and exit). With no argument every live shift ends; pass a contract-id prefix to end just one.",
            (_, args) => Fleet.EndShiftEarly(args.Length > 0 ? args[0] : null));

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

        helper.ConsoleCommands.Add(
            "dayswork_debug_machines",
            "Lists Data/Machines objects in the player's current location with their ready-state, held output, and reload eligibility (verifies the machine reader + per-entry data in-world).",
            (_, _) => LogMachinesInCurrentLocation());

        helper.ConsoleCommands.Add(
            "dayswork_debug_leaks",
            "Reports the current shift's worker-action leak audit: item-debris vanilla mis-routed into the player's location, how much was recovered, and how much was stranded (stranded > 0 means loot escaped the recovery sweep).",
            (_, _) => Fleet.LogLeakAudit(LogLevel.Info));
    }

    private void LogMachinesInCurrentLocation()
    {
        var location = Game1.currentLocation;
        if (location is null)
        {
            this.Monitor.Log("[Dayswork][machines] No current location.", LogLevel.Info);
            return;
        }

        var reader = new Orchestration.MachineReader();
        var count = 0;
        foreach (var (tile, machine, data) in reader.EnumerateMachines(location))
        {
            count++;
            var state = reader.Classify(machine);
            var held = machine.heldObject.Value;
            var reloadable = Orchestration.MachineReader.IsReloadable(data);
            this.Monitor.Log(
                $"[Dayswork][machines] {location.NameOrUniqueName} ({tile.X},{tile.Y}) {machine.QualifiedItemId} '{machine.Name}' " +
                $"state={state} held={(held is null ? "none" : $"{held.QualifiedItemId} x{held.Stack}")} " +
                $"minutesUntilReady={machine.MinutesUntilReady} reloadable={reloadable} " +
                $"allowLoadWhenFull={data.AllowLoadWhenFull} isIncubator={data.IsIncubator}.",
                LogLevel.Info);
        }

        this.Monitor.Log(
            $"[Dayswork][machines] {count} machine(s) in {location.NameOrUniqueName}.",
            LogLevel.Info);
    }
}
