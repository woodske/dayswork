using Dayswork.Core.Config;
using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using Season = Dayswork.Core.Domain.Season;

namespace Dayswork.UI;

internal sealed class HiringFlowCoordinator
{
    private readonly ContractTermsBuilder _termsBuilder;
    private readonly ModConfigManager _configManager;
    private readonly ContractStore _contractStore;
    private readonly ChestResolver _chestResolver;
    private readonly IModHelper _helper;

    // Live crop/fertilizer/shop catalog adapter, rebuilt per hiring-flow session so its per-season
    // memo is fresh. Created lazily when the Manage Crops page first opens.
    private CropCatalogProvider? _cropCatalog;

    public HiringFlowCoordinator(
        ContractTermsBuilder termsBuilder,
        ModConfigManager configManager,
        ContractStore contractStore,
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

        _cropCatalog = null;
        var draft = new ContractDraft();
        ShowHub(draft);
    }

    public void OpenEditFlow(ContractId existing)
    {
        _cropCatalog = null;
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
            onManageCrops: ShowManageCrops,
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

    // ── Manage Crops authoring ─────────────────────────────────────
    private void ShowManageCrops(ContractDraft draft)
    {
        var catalog = EnsureCropCatalog();

        // Names are not persisted; refresh hydrated/edit-flow slot labels from the full catalog.
        draft.CropPlan.EnrichDisplayNames(catalog.GetCatalog(null, greenhouse: true), catalog.GetFertilizers());

        Game1.activeClickableMenu = new ManageCropsMenu(
            draft,
            onBack: ShowHub,
            onAddGroup: AddCropGroup,
            onEditGroup: ShowCropGroupEditor,
            onDeleteGroup: DeleteCropGroup);
    }

    private void AddCropGroup(ContractDraft draft)
    {
        var group = draft.CropPlan.AddGroup();
        ShowCropGroupEditor(draft, group.Id);
    }

    private void ShowCropGroupEditor(ContractDraft draft, string groupId)
    {
        if (!draft.CropPlan.TryGetGroup(groupId, out var group))
        {
            ShowManageCrops(draft);
            return;
        }

        var catalog = EnsureCropCatalog();
        group.EnrichDisplayNames(catalog.GetCatalog(null, greenhouse: true), catalog.GetFertilizers());

        Game1.activeClickableMenu = new CropGroupEditorMenu(
            draft,
            group,
            BuildCropGroupLocationOptions(),
            onBack: ShowManageCrops,
            onPickCrop: (id, season) => ShowCropPicker(draft, id, season),
            onPickFertilizer: (id, season) => ShowFertilizerPicker(draft, id, season),
            onPickChest: id => ShowCropOutputChestPicker(draft, id),
            onBeginDraw: id => BeginCropZoneDraw(draft, id),
            onSetLocation: (id, locationName) => SetCropGroupLocation(draft, id, locationName));
    }

    private void DeleteCropGroup(ContractDraft draft, string groupId)
    {
        draft.CropPlan.DeleteGroup(groupId);
        RefreshPreview(draft);
    }

    private void ShowCropPicker(ContractDraft draft, string groupId, Season season)
    {
        if (!draft.CropPlan.TryGetGroup(groupId, out var currentGroup))
        {
            ShowManageCrops(draft);
            return;
        }

        var isSeasonAgnostic = currentGroup.IsSeasonAgnostic;
        var entries = EnsureCropCatalog().GetCatalog(
            isSeasonAgnostic ? null : season,
            greenhouse: isSeasonAgnostic);

        var rows = new List<PickerRow> { new(I18nHelper.Get("ui.manage_crops.picker_none"), null) };
        rows.AddRange(entries.Select(entry => new PickerRow(entry.DisplayName, SupplyTagLabel(entry.Supply))));

        Game1.activeClickableMenu = new CropListPickerMenu(
            I18nHelper.Get("ui.manage_crops.picker_crop_title"),
            rows,
            selectedIndex: 0,
            onSelect: index =>
            {
                if (!draft.CropPlan.TryGetGroup(groupId, out var group))
                {
                    ShowManageCrops(draft);
                    return;
                }

                if (index == 0)
                {
                    if (group.IsSeasonAgnostic)
                        group.ClearYearRound();
                    else
                        group.ClearSeason(season);
                }
                else
                {
                    var entry = entries[index - 1];
                    if (group.IsSeasonAgnostic)
                    {
                        group.SetYearRoundCrop(entry.Crop, entry.DisplayName);
                    }
                    else if (!group.TrySetCrop(season, entry.Crop, entry.DisplayName, out _))
                    {
                        Game1.addHUDMessage(new HUDMessage(I18nHelper.Get("ui.manage_crops.lock_conflict"), HUDMessage.error_type));
                    }
                }

                ShowCropGroupEditor(draft, groupId);
            },
            onCancel: () => ShowCropGroupEditor(draft, groupId));
    }

    private void ShowFertilizerPicker(ContractDraft draft, string groupId, Season season)
    {
        var fertilizers = EnsureCropCatalog().GetFertilizers();

        var rows = new List<PickerRow> { new(I18nHelper.Get("ui.manage_crops.fertilizer_none"), null) };
        rows.AddRange(fertilizers.Select(option => new PickerRow(option.DisplayName, SupplyTagLabel(option.Supply))));

        Game1.activeClickableMenu = new CropListPickerMenu(
            I18nHelper.Get("ui.manage_crops.picker_fertilizer_title"),
            rows,
            selectedIndex: 0,
            onSelect: index =>
            {
                if (!draft.CropPlan.TryGetGroup(groupId, out var group))
                {
                    ShowManageCrops(draft);
                    return;
                }

                if (index == 0)
                {
                    if (group.IsSeasonAgnostic)
                        group.SetYearRoundFertilizer(null, string.Empty);
                    else
                        group.SetFertilizer(season, null, string.Empty);
                }
                else
                {
                    var option = fertilizers[index - 1];
                    if (group.IsSeasonAgnostic)
                        group.SetYearRoundFertilizer(option.ItemId, option.DisplayName);
                    else
                        group.SetFertilizer(season, option.ItemId, option.DisplayName);
                }

                ShowCropGroupEditor(draft, groupId);
            },
            onCancel: () => ShowCropGroupEditor(draft, groupId));
    }

    private void SetCropGroupLocation(ContractDraft draft, string groupId, string locationName)
    {
        if (draft.CropPlan.TryGetGroup(groupId, out var group))
            group.SetLocation(locationName);

        RefreshPreview(draft);
        ShowCropGroupEditor(draft, groupId);
    }

    private IReadOnlyList<CropGroupLocationOption> BuildCropGroupLocationOptions()
    {
        var options = new List<CropGroupLocationOption>
        {
            new("Farm", I18nHelper.Get("ui.manage_crops.location_farm")),
            new("Greenhouse", I18nHelper.Get("ui.manage_crops.location_greenhouse")),
        };

        if (ModEntry.ExpansionCompat is { } compat)
        {
            foreach (var descriptor in compat.GetExpansionLocationDescriptors())
            {
                if (!descriptor.IsWorkScopeEligible)
                    continue;

                options.Add(new CropGroupLocationOption(descriptor.LocationName, descriptor.DisplayName));
            }
        }

        return options
            .GroupBy(option => option.LocationName, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList()
            .AsReadOnly();
    }

    private void ShowCropOutputChestPicker(ContractDraft draft, string groupId)
    {
        var chests = _chestResolver.GetAllChests(Game1.getFarm(), draft.Greenhouses);

        var rows = new List<PickerRow> { new(I18nHelper.Get("ui.manage_crops.output_automatic"), null) };
        rows.AddRange(chests.Select(chest => new PickerRow(chest.DisplayName, chest.GroupLabel)));

        Game1.activeClickableMenu = new CropListPickerMenu(
            I18nHelper.Get("ui.manage_crops.picker_chest_title"),
            rows,
            selectedIndex: 0,
            onSelect: index =>
            {
                if (draft.CropPlan.TryGetGroup(groupId, out var group))
                    group.OutputChest = index == 0 ? null : chests[index - 1].Ref;

                ShowCropGroupEditor(draft, groupId);
            },
            onCancel: () => ShowCropGroupEditor(draft, groupId));
    }

    private void BeginCropZoneDraw(ContractDraft draft, string groupId)
    {
        if (!draft.CropPlan.TryGetGroup(groupId, out var group))
        {
            ShowManageCrops(draft);
            return;
        }

        // Managed crops are their own draw layer: seed only from existing crop zones (not the general
        // work scope), render green, ignore buildings, and let overlapping draws toggle active tiles off.
        // Other crop groups are protected so one tile can only belong to one group.
        Game1.activeClickableMenu = new ZoneDrawMenu(
            draft,
            new List<BuildingOutline>(),
            _helper,
            onComplete: (zones, _) =>
            {
                draft.CropPlan.SetGroupZones(groupId, zones);
                RefreshPreview(draft);
                ShowCropGroupEditor(draft, groupId);
            },
            onCancel: () => ShowCropGroupEditor(draft, groupId),
            initialZones: group.Zones,
            allowBuildingSelection: false,
            overlapTogglesSelection: true,
            protectedZones: draft.CropPlan.ProtectedZones(groupId, group.LocationName),
            zoneFillColor: Color.LimeGreen * 0.5f,
            targetLocationName: group.LocationName);
    }

    private CropCatalogProvider EnsureCropCatalog() =>
        _cropCatalog ??= new CropCatalogProvider(ModEntry.ModMonitor);

    private static string SupplyTagLabel(CropSupplyTag tag) =>
        I18nHelper.Get(tag == CropSupplyTag.AutoBuyable
            ? "ui.manage_crops.tag_auto_buyable"
            : "ui.manage_crops.tag_chest_only");

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
            CategoryPriority: draft.CategoryPriority.ToList().AsReadOnly(),
            CropPlan: draft.CropPlan.BuildCropPlan());
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
        draft.CropPlan.HydrateFrom(contract.CropPlan);
        return draft;
    }

    private void CloseFlow() => Game1.activeClickableMenu = null;
}
