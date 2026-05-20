using Dayswork.Core.Config;
using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Integration;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

internal sealed class HiringFlowCoordinator
{
    // Whole-farm fallback used by BuildContract when no zones have been drawn.
    private static readonly Zone WholeFarmZone =
        new("Farm", new TileCoord(0, 0), new TileCoord(79, 63));

    private readonly IRateCalculator    _rateCalc;
    private readonly IDepositCalculator _depositCalc;
    private readonly IHoursEstimator    _hoursEst;
    private readonly IConfigSnapshot    _config;
    private readonly IContractStore     _contractStore;
    private readonly ChestResolver      _chestResolver;
    private readonly IModHelper         _helper;

    public HiringFlowCoordinator(
        IRateCalculator    rateCalc,
        IDepositCalculator depositCalc,
        IHoursEstimator    hoursEst,
        IConfigSnapshot    config,
        IContractStore     contractStore,
        ChestResolver      chestResolver,
        IModHelper         helper)
    {
        _rateCalc      = rateCalc;
        _depositCalc   = depositCalc;
        _hoursEst      = hoursEst;
        _config        = config;
        _contractStore = contractStore;
        _chestResolver = chestResolver;
        _helper        = helper;
    }

    public void OpenHiringFlow()
    {
        var draft = new ContractDraft();
        ShowTaskSelection(draft);
    }

    // Stub — full edit flow implemented in U-12
    public void OpenEditFlow(ContractId existing)
    {
        ModEntry.ModMonitor.Log("[Dayswork] Edit flow not yet implemented.", LogLevel.Info);
    }

    private void ShowTaskSelection(ContractDraft draft)
    {
        Game1.activeClickableMenu = new TaskSelectionMenu(
            draft, _rateCalc, _config,
            onAdvance: d => ShowZoneAndChest(d),
            onCancel:  CloseFlow);
    }

    private void ShowZoneAndChest(ContractDraft draft)
    {
        Game1.activeClickableMenu = new ZoneAndChestMenu(
            draft, _chestResolver,
            onAdvance:       d => ShowSummary(d),
            onBack:          d => ShowTaskSelection(d),
            onBeginZoneDraw: d => BeginZoneDraw(d));
    }

    // ── Zone-draw session (Robin building-placement UX) ──────────────────────

    // Called when the player clicks "Draw Zone" / "Select Building" in ZoneAndChestMenu.
    // Opens ZoneDrawMenu, which swaps the displayed location to the farm (no warp).
    private void BeginZoneDraw(ContractDraft draft)
    {
        var buildingOutlines = _chestResolver.GetBuildingOutlines(Game1.getFarm());

        Game1.activeClickableMenu = new ZoneDrawMenu(
            draft,
            buildingOutlines,
            _helper,
            onComplete: (zones, buildings) =>
            {
                draft.Zones.Clear();
                draft.Zones.AddRange(zones);
                foreach (var b in buildings)
                    draft.Zones.Add(new Zone(b.LocationName,
                        new TileCoord(0, 0), new TileCoord(999, 999)));
                ShowZoneAndChest(draft);
            },
            onCancel: () => ShowZoneAndChest(draft));
    }

    // ── Summary / confirm ────────────────────────────────────────────────────

    private void ShowSummary(ContractDraft draft)
    {
        Game1.activeClickableMenu = new SummaryMenu(
            draft, _hoursEst, _depositCalc, _config, WholeFarmZone,
            onConfirm: (d, deposit, rate) => ConfirmContract(d, deposit, rate),
            onBack:    d => ShowZoneAndChest(d));
    }

    private void ConfirmContract(ContractDraft draft, int deposit, int rate)
    {
        if (Game1.player.Money < deposit)
        {
            Game1.addHUDMessage(new HUDMessage(
                I18nHelper.Get("ui.error.cant_afford"),
                HUDMessage.error_type));
            return;
        }

        Game1.player.Money -= deposit;
        _contractStore.Add(BuildContract(draft, deposit, rate));
        CloseFlow();
    }

    private void CloseFlow() => Game1.activeClickableMenu = null;

    private static Contract BuildContract(ContractDraft draft, int deposit, int rate)
    {
        IReadOnlyList<Zone> zones = draft.Zones.Count > 0
            ? draft.Zones.AsReadOnly()
            : new[] { WholeFarmZone };

        IReadOnlyDictionary<TaskKind, DestinationKey> destinations =
            draft.Destinations.Count > 0
                ? draft.Destinations
                : new Dictionary<TaskKind, DestinationKey>();

        return new Contract(
            Id:               ContractId.New(),
            EnabledTasks:     draft.EnabledTasks.ToHashSet(),
            Zones:            zones,
            TaskDestinations: destinations,
            Schedule:         draft.Schedule,
            Status:           ContractStatus.Active,
            HireDate:         new GameDate(
                                  Game1.Date.DayOfMonth,
                                  Enum.Parse<Dayswork.Core.Domain.Season>(Game1.currentSeason, ignoreCase: true),
                                  Game1.year),
            DepositAmount:    deposit,
            HourlyRate:       rate);
    }
}
