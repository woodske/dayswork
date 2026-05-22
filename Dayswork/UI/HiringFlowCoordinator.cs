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
    private readonly IConfigSnapshot    _config;
    private readonly IContractStore     _contractStore;
    private readonly ChestResolver      _chestResolver;
    private readonly IModHelper         _helper;

    public HiringFlowCoordinator(
        IRateCalculator    rateCalc,
        IDepositCalculator depositCalc,
        IConfigSnapshot    config,
        IContractStore     contractStore,
        ChestResolver      chestResolver,
        IModHelper         helper)
    {
        _rateCalc      = rateCalc;
        _depositCalc   = depositCalc;
        _config        = config;
        _contractStore = contractStore;
        _chestResolver = chestResolver;
        _helper        = helper;
    }

    public void OpenHiringFlow()
    {
        // DEV-U15-01 / BR-CTR-01: v1 supports one active contract at a time. Refuse a new hire while an
        // Active or Paused contract exists (the player edits/cancels the existing one instead).
        if (_contractStore.List().Any(c => c.Status is ContractStatus.Active or ContractStatus.Paused))
        {
            Game1.addHUDMessage(new HUDMessage(
                I18nHelper.Get("ui.error.one_contract"),
                HUDMessage.error_type));
            return;
        }

        var draft = new ContractDraft();
        ShowTaskSelection(draft);
    }

    public void OpenEditFlow(ContractId existing)
    {
        var contract = _contractStore.Get(existing);
        var draft = new ContractDraft { EditingId = existing, Schedule = contract.Schedule };
        draft.EnabledTasks.UnionWith(contract.EnabledTasks);
        draft.Zones.AddRange(contract.Zones);
        foreach (var kvp in contract.TaskDestinations)
            draft.Destinations[kvp.Key] = kvp.Value;
        ShowTaskSelection(draft);
    }

    public void OpenManageFlow()
    {
        Game1.activeClickableMenu = new ContractListMenu(_contractStore, _helper);
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
            onAdvance:       d => ShowSchedule(d),
            onBack:          d => ShowTaskSelection(d),
            onBeginZoneDraw: d => BeginZoneDraw(d));
    }

    private void ShowSchedule(ContractDraft draft)
    {
        Game1.activeClickableMenu = new ScheduleMenu(
            draft,
            onAdvance: d => ShowSummary(d),
            onBack:    d => ShowZoneAndChest(d));
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
            draft, _depositCalc, _config, WholeFarmZone,
            onConfirm: (d, deposit, rate) => ConfirmContract(d, deposit, rate),
            onBack:    d => ShowZoneAndChest(d));
    }

    private void ConfirmContract(ContractDraft draft, int deposit, int rate)
    {
        if (draft.EditingId.HasValue)
        {
            // Edit: update existing contract — no gold deduction (original deposit already paid)
            var original = _contractStore.Get(draft.EditingId.Value);
            var updated = BuildContract(draft, deposit, rate) with
            {
                Id       = draft.EditingId.Value,
                Status   = original.Status,
                HireDate = original.HireDate,
            };
            _contractStore.Update(draft.EditingId.Value, updated);
        }
        else
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
        }
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
