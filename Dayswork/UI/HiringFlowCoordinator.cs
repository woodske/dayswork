using Dayswork.Core.Config;
using Dayswork.Core.Crops;
using Dayswork.Core.Domain;
using Dayswork.Core.Machines;
using Dayswork.Core.Net;
using Dayswork.Core.Persistence;
using Dayswork.Core.Pricing;
using Dayswork.Core.Upgrades;
using Dayswork.Guards;
using Dayswork.Integration;
using Dayswork.Net;
using Dayswork.Orchestration;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.Objects;
using Season = Dayswork.Core.Domain.Season;
using SObject = StardewValley.Object;

namespace Dayswork.UI;

internal sealed class HiringFlowCoordinator
{
    private readonly ContractTermsBuilder _termsBuilder;
    private readonly ModConfigManager _configManager;
    private readonly OfficeContractStore _contractStore;
    private readonly FarmhandUpgradeStore _upgradeStore;
    private readonly ChestResolver _chestResolver;
    private readonly IModHelper _helper;

    // Live crop/fertilizer/shop catalog adapter, rebuilt per hiring-flow session so its per-season
    // memo is fresh. Created lazily when the Manage Crops page first opens.
    private CropCatalogProvider? _cropCatalog;

    public HiringFlowCoordinator(
        ContractTermsBuilder termsBuilder,
        ModConfigManager configManager,
        OfficeContractStore contractStore,
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

    /// <summary>Hire for one office. The guard is per office now — a second office is free to
    /// hire while the first has a farmhand out.</summary>
    public void OpenHiringFlow(Building office)
    {
        if (_contractStore.HasOpenContract(office.id.Value))
        {
            Game1.addHUDMessage(new HUDMessage(
                I18nHelper.Get("ui.error.one_contract"),
                HUDMessage.error_type));
            return;
        }

        _cropCatalog = null;
        var draft = new ContractDraft
        {
            OfficeId = office.id.Value,
            OwnerId = ContractRequestHandler.ResolveOwner(office),
        };
        RequestMenuSnapshot(draft.OfficeId);
        ShowHub(draft);
    }

    public void OpenEditFlow(Guid officeId)
    {
        var contract = _contractStore.ForOffice(officeId);
        if (contract is null)
            return;

        _cropCatalog = null;
        RequestMenuSnapshot(officeId);
        ShowHub(CreateEditDraft(contract));
    }

    public void OpenManageFlow(Guid officeId)
    {
        // Manage leads to Upgrades, which a remote client can only fill in from the host: without
        // this a fresh connection reads its own purchase history as empty and relocks Speed2 (R8).
        RequestMenuSnapshot(officeId, clearWorldSnapshot: false);
        Game1.activeClickableMenu = new ContractMenu(_contractStore, officeId);
    }

    /// <summary>
    /// Entry point from an office's tile action: manage that office's contract, else hire a
    /// farmhand for it — but only for the office's owner. Anyone else gets the same page in its
    /// read-only form (2.0 plan D4), which is <see cref="ContractMenu"/> with the actions their
    /// role allows, so there is no second card to keep in step.
    /// </summary>
    public void OpenFromBuilding(Building office)
    {
        if (_contractStore.HasOpenContract(office.id.Value) || !OfficeViewerRoles.Resolve(office).CanEdit())
            OpenManageFlow(office.id.Value);
        else
            OpenHiringFlow(office);
    }

    /// <summary>
    /// A client asks the host for the parts of its world it cannot see (expansion locations and
    /// their chests) as the flow opens, so the answer is usually there by the time the player
    /// reaches a picker that needs it. No-op on the host, which reads the world directly.
    /// </summary>
    private void RequestMenuSnapshot(Guid officeId, bool clearWorldSnapshot = true)
    {
        if (!Authority.IsRemoteClient)
            return;

        // The pickers must not offer the previous office's locations and chests while the new
        // answer is in flight. Upgrade state belongs to the player rather than the office, so it
        // is never cleared here — clearing it is what made a known purchase look unmade (R8).
        if (clearWorldSnapshot)
            Net.MenuSnapshotCache.ClearWorldSnapshot();

        ModEntry.Requests.RequestMenuSnapshot(officeId);
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

    public void ShowUpgradesFromManage(Guid officeId)
    {
        // Asked for again on the way in: the player may have sat on the Manage page long enough
        // for a purchase made on another screen to matter, and a first request may have been sent
        // while the host was still unverified.
        RequestMenuSnapshot(officeId, clearWorldSnapshot: false);
        Game1.activeClickableMenu = new UpgradesMenu(
            MyUpgradesOrNull,
            onPurchase: kind => PurchaseUpgrade(officeId, kind, () => ShowUpgradesFromManage(officeId)),
            onBack: () => OpenManageFlow(officeId));
    }

    private void ShowUpgrades(ContractDraft draft)
    {
        Game1.activeClickableMenu = new UpgradesMenu(
            MyUpgradesOrNull,
            onPurchase: kind => PurchaseUpgrade(draft.OfficeId, kind, () =>
            {
                RefreshPreview(draft);
                ShowUpgrades(draft);
            }, draft),
            onBack: () => ShowHub(draft));
    }

    /// <summary>
    /// This player's own upgrades. They are per owner (2.0 plan D9) and live in host save data, so
    /// a remote client reads them from the host's menu snapshot instead of the store.
    /// </summary>
    private FarmhandUpgradeState MyUpgrades() => MyUpgradesOrNull() ?? FarmhandUpgradeState.Empty;

    /// <summary>
    /// The same, but null on a remote client the host has not answered yet. Pricing has to assume
    /// something and assumes no upgrades — the host re-derives the real terms at commit — but the
    /// Upgrades page must not present that assumption as the player's purchase history (R8).
    /// </summary>
    private FarmhandUpgradeState? MyUpgradesOrNull() =>
        Authority.IsRemoteClient
            ? Net.MenuSnapshotCache.Upgrades
            : _upgradeStore.For(Game1.player.UniqueMultiplayerID);

    /// <summary>
    /// Buying is a host action: it moves money and writes host save data. The menu sends a request
    /// and redraws from the answer — on the host that is the same tick, so this reads exactly like
    /// the old direct purchase.
    /// </summary>
    private void PurchaseUpgrade(Guid officeId, FarmhandUpgradeKind kind, Action reopen, ContractDraft? editing = null)
    {
        // The two "you already know this" cases are answered locally from the state the page is
        // already drawing, so they keep their specific wording; everything else is the host's call.
        var current = MyUpgrades();
        if (current.IsPurchased(kind))
        {
            reopen();
            return;
        }

        if (kind == FarmhandUpgradeKind.Speed2 && !current.IsPurchased(FarmhandUpgradeKind.Speed))
        {
            Game1.addHUDMessage(new HUDMessage(
                I18nHelper.Get("ui.upgrades.prereq_required"),
                HUDMessage.error_type));
            reopen();
            return;
        }

        ModEntry.Requests.SubmitAction(officeId, ContractActionKind.PurchaseUpgrade, response =>
        {
            if (response.Accepted)
            {
                Game1.addHUDMessage(new HUDMessage(
                    I18nHelper.Get("notify.upgrade_purchased", new { name = UpgradeDisplayName(kind) }),
                    HUDMessage.newQuest_type));
            }
            else if (response.Code == ContractRejectionCode.CannotAfford)
            {
                Game1.addHUDMessage(new HUDMessage(
                    I18nHelper.Get("ui.upgrades.cant_afford"),
                    HUDMessage.error_type));
            }
            else
            {
                Game1.addHUDMessage(new HUDMessage(
                    ContractRejectionText.Describe(response.Code),
                    HUDMessage.error_type));
            }

            // The response carries the buyer's upgrade state, so a client's page is right without
            // a second round trip.
            Net.MenuSnapshotCache.ApplyUpgradeState(response);

            // The energy upgrade rewrites every open contract's terms, which bumps the revision an
            // edit in progress was authored against. Rebasing it here is safe and is not the silent
            // rebase R7 forbids: the change was this player's own purchase, and the only field it
            // touched is the terms snapshot this draft recomputes at Confirm anyway.
            if (response.Accepted && editing is { } draft && draft.IsEditing)
                draft.BaseRevision = _contractStore.ForOffice(draft.OfficeId)?.Revision ?? draft.BaseRevision;

            reopen();
        }, upgradeKind: kind.ToString());
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

        // Locations outside the farm are the host's to report: a client's game does not keep them
        // in sync, so it would judge every one of them unavailable. On a client the list is
        // whatever the host's menu snapshot said, and empty until that arrives (2.0 plan D3).
        if (Authority.IsRemoteClient)
        {
            // Until the host answers, say so rather than showing a list that is short for reasons
            // the player cannot see.
            if (Net.MenuSnapshotCache.IsWaiting)
            {
                options.Add(new CropGroupLocationOption(
                    "Dayswork_WaitingForHost",
                    I18nHelper.Get("ui.net.waiting_for_host"),
                    IsAvailable: false));
            }

            foreach (var location in Net.MenuSnapshotCache.ExpansionLocations)
            {
                options.Add(new CropGroupLocationOption(
                    location.LocationName,
                    location.DisplayName,
                    IsAvailable: location.IsAvailable));
            }
        }
        else if (ModEntry.ExpansionCompat is { } compat)
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

    // Managed-crop zones owned by OTHER offices' contracts at the given location. Surfaced in the
    // crop draw session as a light-purple overlay so the player can see where another farmhand
    // already grows crops — informational only; the tiles stay selectable (the per-day
    // WorkClaimRegistry arbitrates any tile two farmhands both claim). Excludes the contract being
    // edited, and Cancelled/Executed contracts, which have no live worker tending their crops.
    private IReadOnlyList<Zone> OtherContractCropZones(ContractDraft draft, string locationName) =>
        _contractStore.OpenContracts()
            .Where(contract => contract.OfficeId != draft.OfficeId)
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

        // Chests outside the farm come from the host's snapshot; before it arrives the picker shows
        // only the farm's own, which is worth saying out loud (2.0 plan D3).
        if (Net.MenuSnapshotCache.IsWaiting)
        {
            Game1.addHUDMessage(new HUDMessage(
                I18nHelper.Get("ui.net.waiting_for_host"),
                HUDMessage.newQuest_type));
        }

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

        // As with the crop-group location list, a client takes the eligible expansion locations
        // from the host's snapshot rather than judging availability itself. It still has to be able
        // to resolve the location to scan it for machines; one it cannot reach is simply not
        // offered, which is the restriction the plan asks for rather than a silent empty picker.
        if (Authority.IsRemoteClient)
        {
            foreach (var entry in Net.MenuSnapshotCache.ExpansionLocations)
            {
                if (!entry.IsAvailable)
                    continue;

                var location = Game1.getLocationFromName(entry.LocationName);
                if (location is not null)
                    yield return (location, entry.DisplayName);
            }

            yield break;
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

    /// <summary>
    /// The config the previewed contract would run under: the base snapshot plus the OFFICE
    /// OWNER's upgrades, which since 2.0 are per player rather than per farm (plan D9). The player
    /// authoring the draft is always its owner, so this is also their own upgrade state — the host
    /// re-derives it the same way when it prices the commit.
    /// <para>
    /// On a remote client the base snapshot is the HOST's, not this game's config file: the two can
    /// disagree, and it is the host's numbers that get charged, so quoting our own would show one
    /// price and bill another (R9). Until the host has answered we quote our own and the host
    /// requotes the commit rather than silently substituting terms.
    /// </para>
    /// </summary>
    private ConfigSnapshot EffectiveConfig() =>
        FarmhandUpgradeEffects.Apply(BaseConfig(), MyUpgrades());

    private ConfigSnapshot BaseConfig() =>
        Authority.IsRemoteClient
            ? PricingConfigTransfer.ApplyTo(_configManager.CurrentSnapshot, Net.MenuSnapshotCache.PricingConfig)
            : _configManager.CurrentSnapshot;

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

    /// <summary>
    /// Hands the finished draft to the host to commit. Money, pricing, and the world checks are all
    /// the host's (2.0 plan D3), so this side only submits and reacts: on acceptance the flow
    /// closes; on rejection the player is told why and the hub reopens <b>with the draft intact</b>
    /// so they can fix the one thing that was wrong rather than start again.
    /// </summary>
    private void ConfirmContract(ContractDraft draft) => ConfirmContract(draft, quotedTerms: null);

    /// <param name="quotedTerms">The host's own terms, when the player is accepting a requote. They
    /// are submitted verbatim so the host's freshly computed terms cannot disagree with them for the
    /// same reason twice (R9).</param>
    private void ConfirmContract(ContractDraft draft, ContractTermsSnapshot? quotedTerms)
    {
        var proposedTerms = quotedTerms ?? draft.PreviewState.Preview.ProposedTerms;
        if (!draft.PreviewState.ReviewModel.CanConfirm || proposedTerms is null)
            return;

        // Identity comes from the draft, not from a fresh read of the store: the store may have
        // moved on since the flow opened (another screen paused it, the host edited it), and the
        // revision submitted here has to be the one the player actually authored against, so the
        // host can tell us it is stale rather than let us overwrite the change unseen (R7).
        var isEdit = draft.EditingId.HasValue;
        var original = isEdit ? _contractStore.ForOffice(draft.OfficeId) : null;
        var submitted = BuildContract(draft, proposedTerms);
        if (original is not null)
        {
            submitted = submitted with
            {
                Id = original.Id,
                Status = original.Status,
                HireDate = original.HireDate,
                Revision = draft.BaseRevision,
            };
        }

        ModEntry.Requests.SubmitContract(
            draft.OfficeId,
            submitted,
            isEdit: isEdit,
            expectedRevision: draft.BaseRevision,
            response =>
            {
                if (response.Accepted)
                {
                    CloseFlow();
                    return;
                }

                if (response.Code == ContractRejectionCode.Stale)
                {
                    ShowStaleContractChoice(draft);
                    return;
                }

                if (response.Code == ContractRejectionCode.TermsChanged)
                {
                    ShowRequotedTermsChoice(draft, response.HostTerms);
                    return;
                }

                Game1.addHUDMessage(new HUDMessage(
                    response.Code == ContractRejectionCode.CannotAfford
                        ? I18nHelper.Get("ui.error.cant_afford")
                        : ContractRejectionText.Describe(response.Code),
                    HUDMessage.error_type));
                ShowHub(draft);
            });
    }

    /// <summary>
    /// The host refused the commit because the office moved on under the draft. The response has
    /// already corrected this screen's read cache, so the two versions are both in hand — and the
    /// player picks which one wins rather than losing their work or silently clobbering the other
    /// change (R7).
    /// </summary>
    private void ShowStaleContractChoice(ContractDraft draft)
    {
        var current = _contractStore.ForOffice(draft.OfficeId);

        // Nothing to reconcile against: the office was hired for by someone else while this was a
        // new hire, or its contract is gone. Either way the office's own page is the truth.
        if (!draft.IsEditing || current is null)
        {
            Game1.addHUDMessage(new HUDMessage(
                ContractRejectionText.Describe(ContractRejectionCode.Stale),
                HUDMessage.error_type));
            OpenManageFlow(draft.OfficeId);
            return;
        }

        Game1.activeClickableMenu = new ConfirmStaleContractMenu(
            onReviewTheirs: () => OpenEditFlow(draft.OfficeId),
            onKeepMine: () =>
            {
                draft.BaseRevision = current.Revision;
                ShowHub(draft);
            });
    }

    /// <summary>
    /// The host would have committed different terms from the ones the player reviewed — its
    /// pricing or energy configuration is not what this client quoted against (R9). Nothing has
    /// been charged: the player is shown the host's real figures and chooses whether to accept
    /// them. Accepting resubmits the host's own terms, so the second attempt is the price shown.
    /// </summary>
    private void ShowRequotedTermsChoice(ContractDraft draft, ContractTermsSnapshot? hostTerms)
    {
        // The host answered without terms it could stand behind (a draft it would not accept at
        // all). Nothing to offer, so the generic line and the hub, draft intact.
        if (hostTerms is null)
        {
            Game1.addHUDMessage(new HUDMessage(
                ContractRejectionText.Describe(ContractRejectionCode.TermsChanged),
                HUDMessage.error_type));
            ShowHub(draft);
            return;
        }

        // Read before refreshing: this is the figure the player actually agreed to, and the dialog
        // is only meaningful as the difference between it and the host's.
        var quotedPrice = draft.PreviewState.Preview.ProposedTerms?.Pricing.TotalPrice ?? 0;

        // The response also carried the host's pricing tables, so every preview from here on quotes
        // them; the hub behind the dialog is rebuilt with the corrected numbers.
        RefreshPreview(draft);

        Game1.activeClickableMenu = new ConfirmRequotedTermsMenu(
            quotedPrice,
            hostTerms,
            onAccept: () => ConfirmContract(draft, hostTerms),
            onDecline: () => ShowHub(draft));
    }

    private static Contract BuildContract(
        ContractDraft draft,
        ContractTermsSnapshot proposedTerms)
    {
        return new Contract(
            Id: ContractId.New(),
            OwnerId: draft.OwnerId,
            OfficeId: draft.OfficeId,
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

    internal static ContractDraft CreateEditDraft(Contract contract)
    {
        var draft = new ContractDraft
        {
            EditingId = contract.Id,
            OfficeId = contract.OfficeId,
            OwnerId = contract.OwnerId,
            Schedule = contract.Schedule,
            Tier = contract.Tier,
            BaseRevision = contract.Revision,
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
