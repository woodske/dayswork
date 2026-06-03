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
        ShowHub(draft);
    }

    public void OpenEditFlow(ContractId existing)
    {
        var contract = _contractStore.Get(existing);
        var draft = CreateEditDraft(existing, contract);
        ShowHub(draft);
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

    // Hub-and-spoke navigation: the hub is the home page and every spoke returns to it. RefreshPreview
    // here keeps the hub's per-section status and the Confirm gate current after any change.
    private void ShowHub(ContractDraft draft)
    {
        RefreshPreview(draft);
        Game1.activeClickableMenu = new HubMenu(
            draft,
            onTaskSelection: ShowTaskSelection,
            onWorkScope: ShowZoneAndChest,
            onOutput: ShowOutputDestinations,
            onPriority: ShowTaskPriority,
            onEnergy: ShowEnergy,
            onRecurrence: ShowSchedule,
            onSummary: ShowSummary,
            onConfirm: ConfirmContract,
            onCancel: CloseFlow);
    }

    private void ShowTaskSelection(ContractDraft draft)
    {
        Game1.activeClickableMenu = new TaskSelectionMenu(
            draft,
            onToggleTask: task => ToggleTask(draft, task),
            onBack: ShowHub);
    }

    private void ShowZoneAndChest(ContractDraft draft)
    {
        Game1.activeClickableMenu = new ZoneAndChestMenu(
            draft,
            _chestResolver,
            onBack: ShowHub,
            onBeginZoneDraw: BeginZoneDraw,
            onClearScope: ClearScope);
    }

    private void ShowOutputDestinations(ContractDraft draft)
    {
        Game1.activeClickableMenu = new OutputDestinationsMenu(
            draft,
            _chestResolver,
            onBack: ShowHub);
    }

    private void ShowSchedule(ContractDraft draft)
    {
        Game1.activeClickableMenu = new ScheduleMenu(
            draft,
            onScheduleChanged: UpdateSchedule,
            onBack: ShowHub);
    }

    private void ShowEnergy(ContractDraft draft)
    {
        Game1.activeClickableMenu = new EnergyMenu(
            draft,
            BuildEnergyOptions(draft),
            onSelectTier: SelectTier,
            onBack: ShowHub);
    }

    private void ShowTaskPriority(ContractDraft draft)
    {
        Game1.activeClickableMenu = new TaskPriorityMenu(
            draft,
            onChanged: RefreshViewModels,
            onBack: ShowHub);
    }

    private void ShowSummary(ContractDraft draft)
    {
        RefreshViewModels(draft);
        Game1.activeClickableMenu = new SummaryMenu(draft, onBack: ShowHub);
    }

    private void SelectTier(ContractDraft draft, EnergyTier tier)
    {
        draft.Tier = tier;
        RefreshPreview(draft);
        ShowEnergy(draft);
    }

    // Prices each energy tier against the current scope/tasks so the Energy page can show energy + cost
    // per option. Tiers with no chargeable scope yet have null terms (the card shows just the name).
    private IReadOnlyList<EnergyTierOption> BuildEnergyOptions(ContractDraft draft)
    {
        var options = new List<EnergyTierOption>();
        foreach (var tier in Enum.GetValues<EnergyTier>())
        {
            var preview = _termsBuilder.BuildPreview(
                draft.ScopeSelection,
                draft.EnabledTasks,
                tier,
                _configManager.CurrentSnapshot);
            var terms = preview.ProposedTerms;
            options.Add(new EnergyTierOption(tier, terms?.Energy.DailyCapacity, terms?.Pricing.TotalPrice));
        }

        return options;
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

    internal static ContractDraft CreateEditDraft(ContractId existing, Contract contract)
    {
        var draft = new ContractDraft
        {
            EditingId = existing,
            Schedule = contract.Schedule,
            Tier = contract.Tier,
        };

        draft.EnabledTasks.UnionWith(contract.EnabledTasks);
        foreach (var (task, destination) in contract.TaskDestinations)
            draft.Destinations[task] = destination;

        draft.CategoryPriority.Clear();
        draft.CategoryPriority.AddRange(contract.CategoryPriority);

        LegacyScopeBootstrapper.HydrateDraft(draft, contract);
        return draft;
    }

    private void CloseFlow() => Game1.activeClickableMenu = null;
}
