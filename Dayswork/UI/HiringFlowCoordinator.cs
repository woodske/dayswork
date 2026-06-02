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
    private readonly IContractTermsBuilder _termsBuilder;
    private readonly ModConfigManager _configManager;
    private readonly IContractStore _contractStore;
    private readonly ChestResolver _chestResolver;
    private readonly IModHelper _helper;

    public HiringFlowCoordinator(
        IContractTermsBuilder termsBuilder,
        ModConfigManager configManager,
        IContractStore contractStore,
        ChestResolver chestResolver,
        IModHelper helper)
    {
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

    /// <summary>Entry point from the hiring building's tile action: manage an existing contract, else hire.</summary>
    public void OpenFromBuilding()
    {
        if (_contractStore.List().Any(c => c.Status is ContractStatus.Active or ContractStatus.Paused))
            OpenManageFlow();
        else
            OpenHiringFlow();
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
            onBack: d => ShowSchedule(d),
            onCycleTier: (d, direction) => CycleTier(d, direction),
            onMoveCategory: (d, category, direction) => MoveCategory(d, category, direction));
    }

    private void CycleTier(ContractDraft draft, int direction)
    {
        draft.CycleTier(direction);
        RefreshPreview(draft);
        ShowSummary(draft);
    }

    private void MoveCategory(ContractDraft draft, Core.Domain.TaskCategory category, int direction)
    {
        draft.MoveCategory(category, direction);
        RefreshViewModels(draft);
        ShowSummary(draft);
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
        draft.Greenhouses.Clear();
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
            draft.Tier,
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

        var builtContract = BuildContract(draft, proposedTerms);

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

    private static Contract BuildContract(
        ContractDraft draft,
        ContractTermsSnapshot proposedTerms)
    {
        return new Contract(
            Id: ContractId.New(),
            EnabledTasks: draft.EnabledTasks.ToHashSet(),
            TaskDestinations: draft.Destinations.Count > 0
                ? new Dictionary<TaskKind, DestinationKey>(draft.Destinations)
                : new Dictionary<TaskKind, DestinationKey>(),
            Schedule: draft.Schedule,
            Status: ContractStatus.Active,
            HireDate: new GameDate(
                Game1.Date.DayOfMonth,
                Enum.Parse<Dayswork.Core.Domain.Season>(Game1.currentSeason, ignoreCase: true),
                Game1.year),
            ScopeSelection: draft.ScopeSelection,
            TermsSnapshot: proposedTerms,
            Tier: draft.Tier,
            CategoryPriority: draft.CategoryPriority.ToList().AsReadOnly());
    }

    private void CloseFlow() => Game1.activeClickableMenu = null;
}
