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
    // Whole-farm default used when no zones have been drawn (U-09 thin slice).
    // Replaced by actual drawn zones in U-11.
    private static readonly Zone WholeFarmZone =
        new("Farm", new TileCoord(0, 0), new TileCoord(79, 63));

    private readonly IRateCalculator _rateCalc;
    private readonly IDepositCalculator _depositCalc;
    private readonly IHoursEstimator _hoursEst;
    private readonly IConfigSnapshot _config;
    private readonly IContractStore _contractStore;

    public HiringFlowCoordinator(
        IRateCalculator rateCalc,
        IDepositCalculator depositCalc,
        IHoursEstimator hoursEst,
        IConfigSnapshot config,
        IContractStore contractStore)
    {
        _rateCalc     = rateCalc;
        _depositCalc  = depositCalc;
        _hoursEst     = hoursEst;
        _config       = config;
        _contractStore = contractStore;
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
            onAdvance: d => ShowSummary(d),
            onCancel:  CloseFlow);
    }

    private void ShowSummary(ContractDraft draft)
    {
        Game1.activeClickableMenu = new SummaryMenu(
            draft, _hoursEst, _depositCalc, _config, WholeFarmZone,
            onConfirm: (d, deposit, rate) => ConfirmContract(d, deposit, rate),
            onBack:    d => ShowTaskSelection(d));
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
            Id:             ContractId.New(),
            EnabledTasks:   draft.EnabledTasks.ToHashSet(),
            Zones:          zones,
            TaskDestinations: destinations,
            Schedule:       draft.Schedule,
            Status:         ContractStatus.Active,
            HireDate:       new GameDate(
                                Game1.Date.DayOfMonth,
                                Enum.Parse<Dayswork.Core.Domain.Season>(Game1.currentSeason, ignoreCase: true),
                                Game1.year),
            DepositAmount:  deposit,
            HourlyRate:     rate);
    }
}
