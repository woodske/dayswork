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
    private readonly IRateCalculator _rateCalc;
    private readonly IDepositCalculator _depositCalc;
    private readonly IContractTermsBuilder _termsBuilder;
    private readonly ModConfigManager _configManager;
    private readonly IContractStore _contractStore;
    private readonly ChestResolver _chestResolver;
    private readonly IModHelper _helper;

    public HiringFlowCoordinator(
        IRateCalculator rateCalc,
        IDepositCalculator depositCalc,
        IContractTermsBuilder termsBuilder,
        ModConfigManager configManager,
        IContractStore contractStore,
        ChestResolver chestResolver,
        IModHelper helper)
    {
        _rateCalc = rateCalc;
        _depositCalc = depositCalc;
        _termsBuilder = termsBuilder;
        _configManager = configManager;
        _contractStore = contractStore;
        _chestResolver = chestResolver;
        _helper = helper;
    }

    public void OpenHiringFlow()
    {
        if (_contractStore.List().Any(c => c.Status is ContractStatus.Active or ContractStatus.Paused))
        {
            Game1.addHUDMessage(new HUDMessage(
                I18nHelper.Get("ui.error.one_contract"),
                HUDMessage.error_type));
            return;
        }

        var draft = new ContractDraft();
        RefreshPreview(draft);
        ShowTaskSelection(draft);
    }

    public void OpenEditFlow(ContractId existing)
    {
        var contract = _contractStore.Get(existing);
        var draft = new ContractDraft
        {
            EditingId = existing,
            Schedule = contract.Schedule,
        };

        draft.EnabledTasks.UnionWith(contract.EnabledTasks);
        foreach (var (task, destination) in contract.TaskDestinations)
            draft.Destinations[task] = destination;

        LegacyScopeBootstrapper.HydrateDraft(draft, contract);
        RefreshPreview(draft);
        ShowSummary(draft);
    }

    public void OpenManageFlow()
    {
        Game1.activeClickableMenu = new ContractListMenu(_contractStore, _helper);
    }

    private void ShowTaskSelection(ContractDraft draft)
    {
        Game1.activeClickableMenu = new TaskSelectionMenu(
            draft,
            onToggleTask: task => ToggleTask(draft, task),
            onAdvance: d => ShowZoneAndChest(d),
            onCancel: CloseFlow);
    }

    private void ShowZoneAndChest(ContractDraft draft)
    {
        Game1.activeClickableMenu = new ZoneAndChestMenu(
            draft,
            _chestResolver,
            onAdvance: d => ShowOutputDestinations(d),
            onBack: d => ShowTaskSelection(d),
            onBeginZoneDraw: d => BeginZoneDraw(d),
            onClearScope: d => ClearScope(d));
    }

    private void ShowOutputDestinations(ContractDraft draft)
    {
        Game1.activeClickableMenu = new OutputDestinationsMenu(
            draft,
            _chestResolver,
            onAdvance: d => ShowSchedule(d),
            onBack: d => ShowZoneAndChest(d));
    }

    private void ShowSchedule(ContractDraft draft)
    {
        Game1.activeClickableMenu = new ScheduleMenu(
            draft,
            onScheduleChanged: (d, schedule) => UpdateSchedule(d, schedule),
            onAdvance: d => ShowSummary(d),
            onBack: d => ShowOutputDestinations(d));
    }

    private void ShowSummary(ContractDraft draft)
    {
        RefreshViewModels(draft);
        Game1.activeClickableMenu = new SummaryMenu(
            draft,
            onConfirm: ConfirmContract,
            onBack: d => ShowSchedule(d));
    }

    private void BeginZoneDraw(ContractDraft draft)
    {
        var buildingOutlines = LegacyScopeBootstrapper.FilterSupportedBuildings(
            _chestResolver.GetBuildingOutlines(Game1.getFarm()));

        Game1.activeClickableMenu = new ZoneDrawMenu(
            draft,
            buildingOutlines.ToList(),
            _helper,
            onComplete: (zones, buildings) =>
            {
                draft.OutdoorZones.Clear();
                draft.OutdoorZones.AddRange(zones);
                LegacyScopeBootstrapper.TryApplySelectedBuildings(draft, buildings);
                RefreshPreview(draft);
                ShowZoneAndChest(draft);
            },
            onCancel: () => ShowZoneAndChest(draft));
    }

    private void ToggleTask(ContractDraft draft, TaskKind task)
    {
        if (!draft.EnabledTasks.Remove(task))
            draft.EnabledTasks.Add(task);

        RefreshPreview(draft);
    }

    private void ClearScope(ContractDraft draft)
    {
        draft.OutdoorZones.Clear();
        draft.AnimalBuildings.Clear();
        draft.Greenhouse = null;
        RefreshPreview(draft);
    }

    private void UpdateSchedule(ContractDraft draft, ContractSchedule schedule)
    {
        draft.Schedule = schedule;
        RefreshViewModels(draft);
    }

    private void RefreshPreview(ContractDraft draft)
    {
        var preview = _termsBuilder.BuildPreview(
            draft.ScopeSelection,
            draft.EnabledTasks,
            _configManager.CurrentSnapshot);

        draft.PreviewState = HiringFlowViewModelBuilder.Build(draft, preview);
    }

    private void RefreshViewModels(ContractDraft draft)
    {
        draft.PreviewState = HiringFlowViewModelBuilder.Build(draft, draft.PreviewState.Preview);
    }

    private void ConfirmContract(ContractDraft draft)
    {
        var proposedTerms = draft.PreviewState.Preview.ProposedTerms;
        if (!draft.PreviewState.ReviewModel.CanConfirm || proposedTerms is null)
            return;

        if (!draft.IsEditing && draft.Schedule == ContractSchedule.OneTime)
        {
            var totalPrice = proposedTerms.Pricing.TotalPrice;
            if (Game1.player.Money < totalPrice)
            {
                Game1.addHUDMessage(new HUDMessage(
                    I18nHelper.Get("ui.error.cant_afford"),
                    HUDMessage.error_type));
                return;
            }

            Game1.player.Money -= totalPrice;
        }

        var compatibilityFinancials = BuildCompatibilityFinancialBridge(draft);
        var builtContract = BuildContract(
            draft,
            proposedTerms,
            compatibilityFinancials.depositAmount,
            compatibilityFinancials.hourlyRate);

        if (draft.EditingId.HasValue)
        {
            var original = _contractStore.Get(draft.EditingId.Value);
            var updated = builtContract with
            {
                Id = draft.EditingId.Value,
                Status = original.Status,
                HireDate = original.HireDate,
            };

            _contractStore.Update(draft.EditingId.Value, updated);
        }
        else
        {
            _contractStore.Add(builtContract);
        }

        CloseFlow();
    }

    private (int depositAmount, int hourlyRate) BuildCompatibilityFinancialBridge(ContractDraft draft)
    {
        var compatibilityZones = LegacyScopeBootstrapper.ProjectCompatibilityZones(draft.ScopeSelection);
        // Transitional persistence fields still expect the legacy hourly model, but the inputs now
        // come only from internal compatibility defaults on the runtime snapshot, never from GMCM.
        var rate = _rateCalc.Calculate(draft.EnabledTasks, _configManager.CurrentSnapshot, isRaining: false);
        var hours = DepositHoursPolicy.EstimateBillableHours(
            compatibilityZones,
            draft.EnabledTasks.Count,
            _configManager.CurrentSnapshot);
        var deposit = _depositCalc.Calculate(hours, rate) is PositiveDeposit positive
            ? positive.Amount
            : 0;

        return (deposit, rate);
    }

    private static Contract BuildContract(
        ContractDraft draft,
        ContractTermsSnapshot proposedTerms,
        int compatibilityDeposit,
        int compatibilityHourlyRate)
    {
        var scopeSelection = draft.ScopeSelection;
        var compatibilityZones = LegacyScopeBootstrapper.ProjectCompatibilityZones(scopeSelection);

        return new Contract(
            Id: ContractId.New(),
            EnabledTasks: draft.EnabledTasks.ToHashSet(),
            Zones: compatibilityZones,
            TaskDestinations: draft.Destinations.Count > 0
                ? new Dictionary<TaskKind, DestinationKey>(draft.Destinations)
                : new Dictionary<TaskKind, DestinationKey>(),
            Schedule: draft.Schedule,
            Status: ContractStatus.Active,
            HireDate: new GameDate(
                Game1.Date.DayOfMonth,
                Enum.Parse<Dayswork.Core.Domain.Season>(Game1.currentSeason, ignoreCase: true),
                Game1.year),
            DepositAmount: compatibilityDeposit,
            HourlyRate: compatibilityHourlyRate,
            ScopeSelection: scopeSelection,
            TermsSnapshot: proposedTerms);
    }

    private void CloseFlow() => Game1.activeClickableMenu = null;
}
