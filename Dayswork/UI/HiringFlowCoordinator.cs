using Dayswork.Core.Config;
using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Machines;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Core.Upgrades;
using Dayswork.Integration;
using Dayswork.Orchestration;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using Season = Dayswork.Core.Domain.Season;
using SObject = StardewValley.Object;

namespace Dayswork.UI;

internal sealed class HiringFlowCoordinator
{
    private readonly ContractTermsBuilder _termsBuilder;
    private readonly ModConfigManager _configManager;
    private readonly ContractStore _contractStore;
    private readonly FarmhandUpgradeStore _upgradeStore;
    private readonly ChestResolver _chestResolver;
    private readonly IModHelper _helper;

    // Live crop/fertilizer/shop catalog adapter, rebuilt per hiring-flow session so its per-season
    // memo is fresh. Created lazily when the Manage Crops page first opens.
    private CropCatalogProvider? _cropCatalog;

    public HiringFlowCoordinator(
        ContractTermsBuilder termsBuilder,
        ModConfigManager configManager,
        ContractStore contractStore,
        FarmhandUpgradeStore upgradeStore,
        ChestResolver chestResolver,
        IModHelper helper)
    {
        _termsBuilder = termsBuilder;
        _configManager = configManager;
        _contractStore = contractStore;
        _upgradeStore = upgradeStore;
        _chestResolver = chestResolver;
        _helper = helper;
    }

    // How many contracts may be Active/Paused at once — one worker per contract, all working the
    // same day. Placeholder value; the final capacity progression (office upgrades / pricing) is
    // a deferred design decision.
    internal const int MaxActiveContracts = 3;

    public void OpenHiringFlow()
    {
        var activeCount = _contractStore.List().Count(c => c.Status is ContractStatus.Active or ContractStatus.Paused);
        if (activeCount >= MaxActiveContracts)
        {
            Game1.addHUDMessage(new HUDMessage(
                I18nHelper.Get("ui.error.max_contracts", new { max = MaxActiveContracts }),
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
            onManageMachines: ShowManageMachines,
            onManageFishPonds: ShowManageFishPonds,
            onUpgrades: ShowUpgrades,
            onOutput: ShowOutputDestinations,
            onPriority: ShowTaskPriority,
            onPreferences: ShowPreferences,
            onEnergy: ShowEnergy,
            onRecurrence: ShowSchedule,
            onSummary: ShowSummary,
            onConfirm: ConfirmContract,
            onCancel: () => MaybeCloseFlow(draft));
    }

    private void ShowPreferences(ContractDraft draft)
    {
        Game1.activeClickableMenu = new PreferencesMenu(draft, onBack: ShowHub);
    }

    public void ShowUpgradesFromManage()
    {
        Game1.activeClickableMenu = new UpgradesMenu(
            _upgradeStore.State,
            onPurchase: PurchaseUpgradeFromManage,
            onBack: OpenManageFlow);
    }

    private void ShowUpgrades(ContractDraft draft)
    {
        Game1.activeClickableMenu = new UpgradesMenu(
            _upgradeStore.State,
            onPurchase: kind => PurchaseUpgradeFromDraft(draft, kind),
            onBack: () => ShowHub(draft));
    }

    private void PurchaseUpgradeFromDraft(ContractDraft draft, FarmhandUpgradeKind kind)
    {
        if (TryPurchaseUpgrade(kind))
            RefreshPreview(draft);

        ShowUpgrades(draft);
    }

    private void PurchaseUpgradeFromManage(FarmhandUpgradeKind kind)
    {
        TryPurchaseUpgrade(kind);
        ShowUpgradesFromManage();
    }

    private bool TryPurchaseUpgrade(FarmhandUpgradeKind kind)
    {
        var result = FarmhandUpgradePurchaser.TryPurchase(kind, _upgradeStore.State, Game1.player.Money);
        switch (result.Status)
        {
            case FarmhandUpgradePurchaseStatus.Purchased:
                Game1.player.Money = result.RemainingGold;
                _upgradeStore.Replace(result.State);
                if (kind == FarmhandUpgradeKind.Energy)
                    ApplyEnergyUpgradeToOpenContracts();

                Game1.addHUDMessage(new HUDMessage(
                    I18nHelper.Get("notify.upgrade_purchased", new { name = UpgradeDisplayName(kind) }),
                    HUDMessage.newQuest_type));
                return true;

            case FarmhandUpgradePurchaseStatus.InsufficientFunds:
                Game1.addHUDMessage(new HUDMessage(
                    I18nHelper.Get("ui.upgrades.cant_afford"),
                    HUDMessage.error_type));
                return false;

            case FarmhandUpgradePurchaseStatus.PrerequisiteNotMet:
                Game1.addHUDMessage(new HUDMessage(
                    I18nHelper.Get("ui.upgrades.prereq_required"),
                    HUDMessage.error_type));
                return false;

            case FarmhandUpgradePurchaseStatus.AlreadyPurchased:
                return false;

            default:
                throw new ArgumentOutOfRangeException(nameof(result.Status), result.Status, null);
        }
    }

    private void ApplyEnergyUpgradeToOpenContracts()
    {
        foreach (var contract in _contractStore.List()
                     .Where(c => c.Status is ContractStatus.Active or ContractStatus.Paused))
        {
            _contractStore.Update(
                contract.Id,
                contract with
                {
                    TermsSnapshot = FarmhandUpgradeEffects.AddEnergyBonus(contract.TermsSnapshot),
                });
        }
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
            _helper,
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
        draft.MarkDirty();
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
        draft.MarkDirty();
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

                draft.MarkDirty();
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

                draft.MarkDirty();
                ShowCropGroupEditor(draft, groupId);
            },
            onCancel: () => ShowCropGroupEditor(draft, groupId));
    }

    private void SetCropGroupLocation(ContractDraft draft, string groupId, string locationName)
    {
        if (draft.CropPlan.TryGetGroup(groupId, out var group))
            group.SetLocation(locationName);

        draft.MarkDirty();
        RefreshPreview(draft);
        ShowCropGroupEditor(draft, groupId);
    }

    private IReadOnlyList<CropGroupLocationOption> BuildCropGroupLocationOptions()
    {
        var options = new List<CropGroupLocationOption>
        {
            new("Farm", I18nHelper.Get("ui.manage_crops.location_farm")),
            new("Greenhouse", I18nHelper.Get("ui.manage_crops.location_greenhouse"),
                IsAvailable: Game1.getFarm().greenhouseUnlocked.Value),
        };

        if (ModEntry.ExpansionCompat is { } compat)
        {
            foreach (var descriptor in compat.GetExpansionLocationDescriptors())
            {
                if (!descriptor.IsWorkScopeEligible)
                    continue;

                options.Add(new CropGroupLocationOption(
                    descriptor.LocationName,
                    descriptor.DisplayName,
                    IsAvailable: compat.IsExpansionLocationAvailable(descriptor.LocationName)));
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
        var locations = _chestResolver.BuildChestMapLocations(Game1.getFarm(), draft.Greenhouses);
        draft.CropPlan.TryGetGroup(groupId, out var current);

        Game1.activeClickableMenu = new ZoneDrawMenu(
            _helper,
            locations,
            initial: current?.OutputDestination,
            options: new ChestPickerOptions(ShowAutomatic: true, ShowShippingBin: true, ShowNone: false),
            onComplete: destination =>
            {
                if (draft.CropPlan.TryGetGroup(groupId, out var group))
                    // Crop groups treat "automatic" as a null destination.
                    group.OutputDestination = destination is AutomaticOutputDestination ? null : destination;

                draft.MarkDirty();
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
                draft.MarkDirty();
                RefreshPreview(draft);
                ShowCropGroupEditor(draft, groupId);
            },
            onCancel: () => ShowCropGroupEditor(draft, groupId),
            initialZones: group.Zones,
            allowBuildingSelection: false,
            overlapTogglesSelection: true,
            protectedZones: draft.CropPlan.ProtectedZones(groupId, group.LocationName),
            otherWorkerZones: OtherContractCropZones(draft, group.LocationName),
            zoneFillColor: Color.LimeGreen * 0.5f,
            targetLocationName: group.LocationName);
    }

    // Managed-crop zones owned by OTHER live contracts (other farmhands) at the given location. Surfaced
    // in the crop draw session as a light-purple overlay so the player can see where another farmhand
    // already grows crops — informational only; the tiles stay selectable (the per-day WorkClaimRegistry
    // arbitrates any tile two farmhands both claim). Excludes the contract currently being edited, and
    // Cancelled/Executed contracts (no live worker tending their crops).
    private IReadOnlyList<Zone> OtherContractCropZones(ContractDraft draft, string locationName) =>
        _contractStore.List()
            .Where(contract => contract.Status is ContractStatus.Active or ContractStatus.Paused)
            .Where(contract => draft.EditingId != contract.Id)
            .SelectMany(contract => contract.CropPlan.Assignments)
            .Select(assignment => assignment.Zone)
            .Where(zone => string.Equals(zone.LocationName, locationName, StringComparison.Ordinal))
            .ToList()
            .AsReadOnly();

    private CropCatalogProvider EnsureCropCatalog() =>
        _cropCatalog ??= new CropCatalogProvider(ModEntry.ModMonitor);

    private static string SupplyTagLabel(CropSupplyTag tag) =>
        I18nHelper.Get(tag == CropSupplyTag.AutoBuyable
            ? "ui.manage_crops.tag_auto_buyable"
            : "ui.manage_crops.tag_chest_only");

    // ── Manage Machines authoring ─────────────────────────────────────
    private readonly MachineReader _machineReader = new();
    private readonly FishPondReader _fishPondReader = new();

    private void ShowManageMachines(ContractDraft draft)
    {
        Game1.activeClickableMenu = new ManageMachinesMenu(
            draft,
            onBack: ShowHub,
            onAddGroup: AddMachineGroup,
            onEditGroup: ShowMachineGroupEditor,
            onDeleteGroup: DeleteMachineGroup);
    }

    private void AddMachineGroup(ContractDraft draft)
    {
        var group = draft.MachinePlan.AddGroup();
        draft.MarkDirty();
        ShowMachineGroupEditor(draft, group.Id);
    }

    private void ShowMachineGroupEditor(ContractDraft draft, string groupId)
    {
        if (!draft.MachinePlan.TryGetGroup(groupId, out var group))
        {
            ShowManageMachines(draft);
            return;
        }

        Game1.activeClickableMenu = new MachineGroupEditorMenu(
            draft,
            group,
            onBack: ShowManageMachines,
            onPickType: id => ShowMachineTypePicker(draft, id),
            onPickInputChest: id => ShowMachineInputChestPicker(draft, id),
            onPickInputFilter: id => ShowMachineInputFilterPicker(draft, id),
            onPickOutput: id => ShowMachineOutputPicker(draft, id),
            onSelectMachines: id => BeginMachineSelection(draft, id));
    }

    private void ShowMachineTypePicker(ContractDraft draft, string groupId)
    {
        if (!draft.MachinePlan.TryGetGroup(groupId, out var group))
        {
            ShowManageMachines(draft);
            return;
        }

        // Machines claimed by other groups don't count toward an available type's tally.
        var claimed = draft.MachinePlan.ProtectedMachines(groupId)
            .GroupBy(machine => machine.LocationName, StringComparer.Ordinal)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.Select(machine => machine.Tile).ToHashSet(), StringComparer.Ordinal);

        var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (location, _) in EnumerateCandidateLocations())
        {
            claimed.TryGetValue(location.NameOrUniqueName, out var claimedTiles);
            foreach (var (tile, machine, _) in _machineReader.EnumerateMachines(location))
            {
                if (claimedTiles is not null && claimedTiles.Contains(tile))
                    continue;
                typeCounts[machine.QualifiedItemId] = typeCounts.GetValueOrDefault(machine.QualifiedItemId) + 1;
            }
        }

        if (typeCounts.Count == 0)
        {
            Game1.addHUDMessage(new HUDMessage(I18nHelper.Get("ui.manage_machines.no_machines_found"), HUDMessage.error_type));
            ShowMachineGroupEditor(draft, groupId);
            return;
        }

        var types = typeCounts
            .Select(kvp => (Id: kvp.Key, Name: ItemRegistry.GetData(kvp.Key)?.DisplayName ?? kvp.Key, Count: kvp.Value))
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = types
            .Select(type => new PickerRow(
                type.Name,
                string.Equals(type.Id, group.MachineType, StringComparison.Ordinal)
                    ? I18nHelper.Get("ui.manage_machines.filter_active")
                    : I18nHelper.Get("ui.manage_machines.type_count", new { count = type.Count })))
            .ToList();

        Game1.activeClickableMenu = new CropListPickerMenu(
            I18nHelper.Get("ui.manage_machines.picker_type_title"),
            rows,
            selectedIndex: 0,
            onSelect: index =>
            {
                if (index >= 0 && index < types.Count && draft.MachinePlan.TryGetGroup(groupId, out var target))
                {
                    target.SetMachineType(types[index].Id);   // clears machines + filter if the type changed
                    draft.MarkDirty();
                    RefreshPreview(draft);
                }

                ShowMachineGroupEditor(draft, groupId);
            },
            onCancel: () => ShowMachineGroupEditor(draft, groupId));
    }

    private void DeleteMachineGroup(ContractDraft draft, string groupId)
    {
        draft.MachinePlan.DeleteGroup(groupId);
        draft.MarkDirty();
        RefreshPreview(draft);
    }

    private void ShowMachineInputChestPicker(ContractDraft draft, string groupId)
    {
        // Auto-grabbers are valid input sources here only — a grabber's collected animal products can
        // feed reload machines. Output/deposit pickers stay grabber-free (includeAutoGrabbers default).
        var locations = _chestResolver.BuildChestMapLocations(Game1.getFarm(), draft.Greenhouses, includeAutoGrabbers: true);
        draft.MachinePlan.TryGetGroup(groupId, out var current);

        Game1.activeClickableMenu = new ZoneDrawMenu(
            _helper,
            locations,
            initial: current?.InputChest is { } chestRef ? new ChestDestination(chestRef) : null,
            options: new ChestPickerOptions(ShowAutomatic: false, ShowShippingBin: false, ShowNone: true),
            onComplete: destination =>
            {
                if (draft.MachinePlan.TryGetGroup(groupId, out var group))
                    group.InputChest = destination is ChestDestination chest ? chest.Ref : null;

                draft.MarkDirty();
                RefreshPreview(draft);
                ShowMachineGroupEditor(draft, groupId);
            },
            onCancel: () => ShowMachineGroupEditor(draft, groupId));
    }

    private void ShowMachineInputFilterPicker(ContractDraft draft, string groupId)
    {
        if (!draft.MachinePlan.TryGetGroup(groupId, out var group) || !group.HasType)
        {
            ShowMachineGroupEditor(draft, groupId);
            return;
        }

        // Inputs are derived from what the machine type accepts, read off a live instance of it.
        var sample = ResolveGroupMachineSample(group);
        if (sample is null)
        {
            Game1.addHUDMessage(new HUDMessage(I18nHelper.Get("ui.manage_machines.inputs_unavailable"), HUDMessage.error_type));
            ShowMachineGroupEditor(draft, groupId);
            return;
        }

        var (machine, location) = sample.Value;
        var data = machine.GetMachineData()!;
        var accepted = _machineReader.EnumerateAcceptedInputs(machine, data, Game1.player, location);
        var companions = MachineReader.EnumerateRequiredCompanions(data);

        // "Any <category>" shortcuts for categories with several accepted members (e.g. a keg's fruits).
        var categories = accepted
            .Where(input => !string.IsNullOrWhiteSpace(input.CategoryName))
            .GroupBy(input => input.Category)
            .Where(grouping => grouping.Count() >= 2)
            .Select(grouping => (Name: grouping.First().CategoryName, Ids: grouping.Select(input => input.QualifiedId).ToList()))
            .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<PickerRow>();
        var actions = new List<Action>();

        rows.Add(new PickerRow(
            I18nHelper.Get("ui.manage_machines.input_any"),
            group.IsAnyInput ? I18nHelper.Get("ui.manage_machines.filter_active") : null));
        actions.Add(() => group.ClearInputFilter());

        // Required companions (coal, …): auto-included and locked — the load engine consumes them
        // regardless of the filter, so this is informational ("stock coal in the input chest").
        foreach (var companion in companions)
        {
            rows.Add(new PickerRow(
                I18nHelper.Get("ui.manage_machines.input_required", new { name = companion.DisplayName }),
                I18nHelper.Get("ui.manage_machines.input_required_tag"),
                Locked: true));
            actions.Add(() => { });
        }

        foreach (var category in categories)
        {
            var ids = category.Ids;
            rows.Add(new PickerRow(
                I18nHelper.Get("ui.manage_machines.input_any_category", new { name = category.Name }),
                ids.All(group.AllowedInputIds.Contains) ? I18nHelper.Get("ui.manage_machines.filter_active") : null));
            actions.Add(() =>
            {
                var fullySelected = ids.All(group.AllowedInputIds.Contains);
                foreach (var id in ids)
                {
                    if (fullySelected)
                        group.AllowedInputIds.Remove(id);
                    else if (!group.AllowedInputIds.Contains(id))
                        group.AllowedInputIds.Add(id);
                }
            });
        }

        foreach (var input in accepted)
        {
            var id = input.QualifiedId;
            rows.Add(new PickerRow(
                input.DisplayName,
                group.AllowedInputIds.Contains(id) ? I18nHelper.Get("ui.manage_machines.filter_active") : null));
            actions.Add(() => group.ToggleAllowedInput(id));
        }

        Game1.activeClickableMenu = new CropListPickerMenu(
            I18nHelper.Get("ui.manage_machines.picker_filter_title"),
            rows,
            selectedIndex: 0,
            onSelect: index =>
            {
                if (index >= 0 && index < actions.Count)
                {
                    actions[index]();
                    draft.MarkDirty();
                }

                ShowMachineInputFilterPicker(draft, groupId);   // reopen so the player can toggle several
            },
            onCancel: () => ShowMachineGroupEditor(draft, groupId));
    }

    private void ShowMachineOutputPicker(ContractDraft draft, string groupId)
    {
        var locations = _chestResolver.BuildChestMapLocations(Game1.getFarm(), draft.Greenhouses);
        draft.MachinePlan.TryGetGroup(groupId, out var current);

        Game1.activeClickableMenu = new ZoneDrawMenu(
            _helper,
            locations,
            initial: current?.OutputDestination,
            options: new ChestPickerOptions(ShowAutomatic: true, ShowShippingBin: true, ShowNone: false),
            onComplete: destination =>
            {
                if (draft.MachinePlan.TryGetGroup(groupId, out var group))
                    // Machine groups treat "automatic" as a null destination.
                    group.OutputDestination = destination is AutomaticOutputDestination ? null : destination;

                draft.MarkDirty();
                ShowMachineGroupEditor(draft, groupId);
            },
            onCancel: () => ShowMachineGroupEditor(draft, groupId));
    }

    // ── Manage Fish Ponds authoring ───────────────────────────────────
    private void ShowManageFishPonds(ContractDraft draft)
    {
        Game1.activeClickableMenu = new ManageFishPondsMenu(
            draft,
            onBack: ShowHub,
            onSelectPonds: BeginFishPondSelection,
            onPickOutput: ShowFishPondOutputPicker);
    }

    private void BeginFishPondSelection(ContractDraft draft)
    {
        var locations = BuildFishPondMapLocations();
        if (locations.Count == 0)
        {
            Game1.addHUDMessage(new HUDMessage(
                I18nHelper.Get("ui.manage_fish_ponds.no_ponds_found"),
                HUDMessage.error_type));
            ShowManageFishPonds(draft);
            return;
        }

        Game1.activeClickableMenu = new ZoneDrawMenu(
            _helper,
            locations,
            initialSelected: draft.FishPondPlan.Ponds,
            onComplete: ponds =>
            {
                draft.FishPondPlan.SetPonds(ponds);
                draft.MarkDirty();
                RefreshPreview(draft);
                ShowManageFishPonds(draft);
            },
            onCancel: () => ShowManageFishPonds(draft));
    }

    private void ShowFishPondOutputPicker(ContractDraft draft)
    {
        var locations = _chestResolver.BuildChestMapLocations(Game1.getFarm(), draft.Greenhouses);

        Game1.activeClickableMenu = new ZoneDrawMenu(
            _helper,
            locations,
            initial: draft.FishPondPlan.OutputDestination,
            options: new ChestPickerOptions(ShowAutomatic: true, ShowShippingBin: true, ShowNone: false),
            onComplete: destination =>
            {
                // Treat "automatic" as a null destination (BuildScope coalesces null → Automatic).
                draft.FishPondPlan.OutputDestination = destination is AutomaticOutputDestination ? null : destination;
                draft.MarkDirty();
                RefreshPreview(draft);
                ShowManageFishPonds(draft);
            },
            onCancel: () => ShowManageFishPonds(draft));
    }

    private IReadOnlyList<FishPondMapLocation> BuildFishPondMapLocations()
    {
        var result = new List<FishPondMapLocation>();
        foreach (var (location, displayName) in EnumerateCandidateLocations())
        {
            var ponds = _fishPondReader.EnumerateFishPonds(location)
                .Select(entry => new FishPondFootprint(
                    entry.Tile,
                    Math.Max(1, entry.Pond.tilesWide.Value),
                    Math.Max(1, entry.Pond.tilesHigh.Value)))
                .ToList();
            if (ponds.Count == 0)
                continue;

            result.Add(new FishPondMapLocation(location.NameOrUniqueName, displayName, ponds));
        }

        return result
            .GroupBy(location => location.LocationName, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private void BeginMachineSelection(ContractDraft draft, string groupId)
    {
        if (!draft.MachinePlan.TryGetGroup(groupId, out var group))
        {
            ShowManageMachines(draft);
            return;
        }

        if (!group.HasType)
        {
            Game1.addHUDMessage(new HUDMessage(
                I18nHelper.Get("ui.manage_machines.choose_type_first"),
                HUDMessage.error_type));
            ShowMachineGroupEditor(draft, groupId);
            return;
        }

        var locations = BuildMachineMapLocations();
        if (locations.Count == 0)
        {
            Game1.addHUDMessage(new HUDMessage(
                I18nHelper.Get("ui.manage_machines.no_machines_found"),
                HUDMessage.error_type));
            ShowMachineGroupEditor(draft, groupId);
            return;
        }

        Game1.activeClickableMenu = new ZoneDrawMenu(
            _helper,
            locations,
            initialSelected: group.Machines,
            protectedMachines: draft.MachinePlan.ProtectedMachines(groupId),
            machineTypeFilter: group.MachineType,
            onComplete: machines =>
            {
                draft.MachinePlan.SetGroupMachines(groupId, machines);
                draft.MarkDirty();
                RefreshPreview(draft);
                ShowMachineGroupEditor(draft, groupId);
            },
            onCancel: () => ShowMachineGroupEditor(draft, groupId));
    }

    /// <summary>
    /// A live, still-present machine in the group plus its location, used to read the type's accepted
    /// inputs/companions. Null when none of the group's selected machines resolve anymore.
    /// </summary>
    private (SObject Machine, GameLocation Location)? ResolveGroupMachineSample(MachineGroupDraft group)
    {
        foreach (var machineRef in group.Machines)
        {
            var location = ResolveGameLocation(machineRef.LocationName);
            if (location is null)
                continue;

            var machine = _machineReader.Resolve(location, machineRef.Tile, machineRef.ExpectedQualifiedId);
            if (machine is not null && machine.GetMachineData() is not null)
                return (machine, location);
        }

        return null;
    }

    private static GameLocation? ResolveGameLocation(string locationName) =>
        EnumerateCandidateLocations()
            .FirstOrDefault(candidate => string.Equals(candidate.Location.NameOrUniqueName, locationName, StringComparison.Ordinal))
            .Location;

    private IReadOnlyList<MachineMapLocation> BuildMachineMapLocations()
    {
        var result = new List<MachineMapLocation>();
        foreach (var (location, displayName) in EnumerateCandidateLocations())
        {
            var candidates = _machineReader.EnumerateMachines(location)
                .GroupBy(machine => machine.Tile)
                .ToDictionary(group => group.Key, group => group.First().Machine.QualifiedItemId);
            if (candidates.Count == 0)
                continue;

            result.Add(new MachineMapLocation(location.NameOrUniqueName, displayName, candidates));
        }

        return result
            .GroupBy(location => location.LocationName, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    /// <summary>
    /// Every location the farmhand can reach that may hold machines (farm, building interiors,
    /// greenhouse, eligible expansions), with a friendly label. Single source of truth for the map
    /// session, the type picker, and machine-sample resolution.
    /// </summary>
    private static IEnumerable<(GameLocation Location, string DisplayName)> EnumerateCandidateLocations()
    {
        var farm = Game1.getFarm();
        yield return (farm, I18nHelper.Get("ui.manage_machines.location_farm"));

        foreach (var building in farm.buildings)
        {
            if (BuildingLocationResolver.TryGetInteriorForBuilding(building, out var indoors))
                yield return (indoors, building.buildingType.Value);
        }

        if (farm.greenhouseUnlocked.Value)
        {
            var greenhouse = Game1.getLocationFromName("Greenhouse");
            if (greenhouse is not null)
                yield return (greenhouse, I18nHelper.Get("ui.manage_machines.location_greenhouse"));
        }

        if (ModEntry.ExpansionCompat is { } compat)
        {
            foreach (var descriptor in compat.GetExpansionLocationDescriptors())
            {
                if (!descriptor.IsWorkScopeEligible || !compat.IsExpansionLocationAvailable(descriptor.LocationName))
                    continue;

                var location = Game1.getLocationFromName(descriptor.LocationName);
                if (location is not null)
                    yield return (location, descriptor.DisplayName);
            }
        }
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
        var catalog = EnsureCropCatalog();
        draft.CropPlan.EnrichDisplayNames(catalog.GetCatalog(null, greenhouse: true), catalog.GetFertilizers());
        RefreshViewModels(draft);
        Game1.activeClickableMenu = new SummaryMenu(draft, onBack: ShowHub);
    }

    private void SelectTier(ContractDraft draft, EnergyTier tier)
    {
        draft.Tier = tier;
        draft.MarkDirty();
        RefreshPreview(draft);
        ShowEnergy(draft);
    }

    private ConfigSnapshot EffectiveConfig() =>
        FarmhandUpgradeEffects.Apply(_configManager.CurrentSnapshot, _upgradeStore.State);

    private static string UpgradeDisplayName(FarmhandUpgradeKind kind) =>
        I18nHelper.Get(kind switch
        {
            FarmhandUpgradeKind.Speed => "ui.upgrades.speed.name",
            FarmhandUpgradeKind.Speed2 => "ui.upgrades.speed2.name",
            _ => "ui.upgrades.energy.name",
        });

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
                EffectiveConfig(),
                draft.CropPlan.BuildCropPlan(),
                draft.MachinePlan.BuildScope(),
                draft.FishPondPlan.BuildScope());
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
                draft.MarkDirty();
                RefreshPreview(draft);
                ShowZoneAndChest(draft);
            },
            onCancel: () => ShowZoneAndChest(draft),
            overlapTogglesSelection: true);
    }

    private void ToggleTask(ContractDraft draft, TaskKind task)
    {
        if (!draft.EnabledTasks.Remove(task))
            draft.EnabledTasks.Add(task);

        draft.MarkDirty();
        RefreshPreview(draft);
    }

    private void ClearScope(ContractDraft draft)
    {
        draft.OutdoorZones.Clear();
        draft.AnimalBuildings.Clear();
        draft.Greenhouses.Clear();
        draft.MarkDirty();
        RefreshPreview(draft);
    }

    private void UpdateSchedule(ContractDraft draft, ContractSchedule schedule)
    {
        draft.Schedule = schedule;
        draft.MarkDirty();
        RefreshViewModels(draft);
        ShowSchedule(draft);
    }

    private void RefreshPreview(ContractDraft draft)
    {
        var preview = _termsBuilder.BuildPreview(
            draft.ScopeSelection,
            draft.EnabledTasks,
            draft.Tier,
            EffectiveConfig(),
            draft.CropPlan.BuildCropPlan(),
            draft.MachinePlan.BuildScope(),
            draft.FishPondPlan.BuildScope());

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
            CropPlan: draft.CropPlan.BuildCropPlan(),
            MachineScope: draft.MachinePlan.BuildScope(),
            FishPondScope: draft.FishPondPlan.BuildScope(),
            Preferences: draft.Preferences);
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
        draft.MachinePlan.HydrateFrom(contract.MachineScope);
        draft.FishPondPlan.HydrateFrom(contract.FishPondScope);
        draft.Preferences = contract.Preferences;
        return draft;
    }

    private void MaybeCloseFlow(ContractDraft draft)
    {
        if (draft.IsDirty)
            ShowConfirmDiscard(draft);
        else
            CloseFlow();
    }

    private void ShowConfirmDiscard(ContractDraft draft)
    {
        Game1.activeClickableMenu = new ConfirmDiscardMenu(
            onGoBack: () => ShowHub(draft),
            onCloseAnyway: CloseFlow);
    }

    private void CloseFlow() => Game1.activeClickableMenu = null;
}
