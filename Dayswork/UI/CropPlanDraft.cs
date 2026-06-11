using Dayswork.Core.Crops;
using Dayswork.Core.Domain;

namespace Dayswork.UI;

/// <summary>Whether a Manage Crops season slot is editable or occupied by a multi-season crop.</summary>
internal enum SeasonLockState
{
    Open,
    MultiSeasonLocked,
}

/// <summary>One season's in-progress authoring state on a crop group.</summary>
internal sealed class SeasonSlotDraft
{
    /// <summary>The chosen crop (fertilizer-free catalog descriptor), or null when unconfigured.</summary>
    public CropDescriptor? Crop { get; set; }

    public string CropDisplayName { get; set; } = string.Empty;
    public string? FertilizerItemId { get; set; }
    public string FertilizerDisplayName { get; set; } = string.Empty;
    public bool HasCrop => Crop is not null;
}

/// <summary>
/// A single managed-crop group: one seasonal plan, one optional output chest, and one or more
/// drawn zones. It projects into one persisted <see cref="CropZoneAssignment"/> per zone.
/// </summary>
internal sealed class CropGroupDraft
{
    private static readonly SeasonAssignmentResolver Resolver = new();

    // A sentinel zone used only to drive the resolver while consolidating per-season choices; it is
    // replaced by real drawn zones at materialization time.
    private static readonly Zone TemplateZone =
        new("__dayswork_crop_group_draft__", new TileCoord(0, 0), new TileCoord(0, 0));

    private readonly Dictionary<Season, SeasonSlotDraft> _slots = new();
    private SeasonSlotDraft? _yearRoundSlot;

    public CropGroupDraft(string id)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException(null, nameof(id)) : id;
    }

    public string Id { get; }
    public string LocationName { get; private set; } = "Farm";
    public CropAssignmentMode Mode { get; private set; } = CropAssignmentMode.Seasonal;
    public ChestRef? OutputChest { get; set; }
    public List<Zone> Zones { get; } = new();

    public bool HasAnyConfiguredSeason => IsSeasonAgnostic
        ? _yearRoundSlot?.HasCrop == true
        : _slots.Values.Any(slot => slot.HasCrop);
    public bool HasAnyAssignment => HasAnyConfiguredSeason && Zones.Count > 0;
    public bool IsSeasonAgnostic => Mode == CropAssignmentMode.SeasonAgnostic;
    public SeasonSlotDraft YearRoundSlot => _yearRoundSlot ?? new SeasonSlotDraft();

    public SeasonSlotDraft Slot(Season season) =>
        _slots.TryGetValue(season, out var slot) ? slot : new SeasonSlotDraft();

    public bool IsConfigured(Season season) =>
        _slots.TryGetValue(season, out var slot) && slot.HasCrop;

    public void SetLocation(string locationName)
    {
        var normalized = string.IsNullOrWhiteSpace(locationName) ? "Farm" : locationName.Trim();
        if (string.Equals(LocationName, normalized, StringComparison.Ordinal))
            return;

        LocationName = normalized;
        Mode = ModeForLocation(normalized);
        Zones.Clear();
    }

    /// <summary>Maps each multi-season-locked season to the origin season whose crop occupies it.</summary>
    public IReadOnlyDictionary<Season, Season> LockedSeasons()
    {
        var locked = new Dictionary<Season, Season>();
        foreach (var origin in CropPlanDraft.AllSeasons)
        {
            if (!_slots.TryGetValue(origin, out var slot) || slot.Crop is not { IsMultiSeason: true } crop)
                continue;

            foreach (var season in crop.Seasons.Where(season => season != origin))
                locked[season] = origin;
        }

        return locked;
    }

    public SeasonLockState LockState(Season season) =>
        LockedSeasons().ContainsKey(season) ? SeasonLockState.MultiSeasonLocked : SeasonLockState.Open;

    public Season? LockOrigin(Season season) =>
        LockedSeasons().TryGetValue(season, out var origin) ? origin : null;

    /// <summary>The crop display name shown for a season (its own, or its lock origin's crop).</summary>
    public string DisplayCropName(Season season)
    {
        if (_slots.TryGetValue(season, out var slot) && slot.HasCrop)
            return slot.CropDisplayName;

        if (LockOrigin(season) is { } origin && _slots.TryGetValue(origin, out var originSlot))
            return originSlot.CropDisplayName;

        return string.Empty;
    }

    /// <summary>
    /// Attempts to configure <paramref name="crop"/> for <paramref name="season"/>. Rejects when the
    /// season is locked, or when a multi-season crop would collide with another configured/locked
    /// season. On success the prior fertilizer for that slot is cleared.
    /// </summary>
    public bool TrySetCrop(Season season, CropDescriptor crop, string displayName, out string? rejectReason)
    {
        var locked = LockedSeasons();
        if (locked.ContainsKey(season))
        {
            rejectReason = nameof(SeasonLockState.MultiSeasonLocked);
            return false;
        }

        if (crop.IsMultiSeason)
        {
            foreach (var covered in crop.Seasons.Where(s => s != season))
            {
                var directlyConfigured = _slots.TryGetValue(covered, out var coveredSlot) && coveredSlot.HasCrop;
                var lockedByOther = locked.TryGetValue(covered, out var origin) && origin != season;
                if (directlyConfigured || lockedByOther)
                {
                    rejectReason = covered.ToString();
                    return false;
                }
            }
        }

        var slot = GetOrCreate(season);
        slot.Crop = crop;
        slot.CropDisplayName = displayName;
        slot.FertilizerItemId = null;
        slot.FertilizerDisplayName = string.Empty;
        rejectReason = null;
        return true;
    }

    public void SetYearRoundCrop(CropDescriptor crop, string displayName)
    {
        var slot = GetOrCreateYearRound();
        slot.Crop = crop;
        slot.CropDisplayName = displayName;
        slot.FertilizerItemId = null;
        slot.FertilizerDisplayName = string.Empty;
    }

    /// <summary>Clears a directly-configured season (releasing any seasons its crop locked). No-op for locked seasons.</summary>
    public void ClearSeason(Season season)
    {
        if (LockedSeasons().ContainsKey(season))
            return;

        _slots.Remove(season);
    }

    public void ClearYearRound() => _yearRoundSlot = null;

    public void SetFertilizer(Season season, string? itemId, string displayName)
    {
        if (!_slots.TryGetValue(season, out var slot) || !slot.HasCrop)
            return;

        slot.FertilizerItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        slot.FertilizerDisplayName = slot.FertilizerItemId is null ? string.Empty : displayName;
    }

    public void SetYearRoundFertilizer(string? itemId, string displayName)
    {
        if (_yearRoundSlot is not { HasCrop: true } slot)
            return;

        slot.FertilizerItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        slot.FertilizerDisplayName = slot.FertilizerItemId is null ? string.Empty : displayName;
    }

    /// <summary>
    /// Consolidates the configured season slots into the persisted per-zone choice set, expanding
    /// and locking multi-season crops via the pure resolver. Fertilizer is folded onto each crop.
    /// </summary>
    public IReadOnlyList<SeasonCropChoice> BuildAssignmentChoices()
    {
        if (IsSeasonAgnostic)
        {
            if (_yearRoundSlot?.Crop is null)
                return Array.Empty<SeasonCropChoice>();

            var crop = _yearRoundSlot.Crop.WithFertilizer(_yearRoundSlot.FertilizerItemId);
            return new[]
            {
                new SeasonCropChoice(Season.Spring, crop),
            };
        }

        var template = new CropZoneAssignment(
            TemplateZone,
            CropAssignmentMode.Seasonal,
            Array.Empty<SeasonCropChoice>(),
            OutputChest,
            Id);

        foreach (var season in CropPlanDraft.AllSeasons)
        {
            if (!_slots.TryGetValue(season, out var slot) || slot.Crop is null)
                continue;

            var crop = slot.Crop.WithFertilizer(slot.FertilizerItemId);
            template = Resolver.ApplyChoice(
                template,
                new SeasonCropChoice(season, crop));
        }

        return template.Choices;
    }

    public IReadOnlyList<CropZoneAssignment> BuildAssignments()
    {
        var choices = BuildAssignmentChoices();
        if (choices.Count == 0 || Zones.Count == 0)
            return Array.Empty<CropZoneAssignment>();

        return Zones
            .Select(zone => new Zone(LocationName, zone.TopLeft, zone.BottomRight))
            .Select(zone => new CropZoneAssignment(zone, Mode, choices, OutputChest, Id))
            .ToList()
            .AsReadOnly();
    }

    internal void HydrateSlotsFrom(CropZoneAssignment assignment)
    {
        _slots.Clear();
        _yearRoundSlot = null;
        LocationName = string.IsNullOrWhiteSpace(assignment.Zone.LocationName)
            ? "Farm"
            : assignment.Zone.LocationName;
        Mode = assignment.Mode;
        OutputChest = assignment.OutputChest;

        foreach (var choice in assignment.Choices.Where(choice => !choice.IsLocked))
        {
            var slot = IsSeasonAgnostic
                ? GetOrCreateYearRound()
                : GetOrCreate(choice.Season);
            slot.Crop = new CropDescriptor(
                choice.Crop.CropItemId,
                choice.Crop.SeedItemId,
                null,
                choice.Crop.DaysToFirstHarvest,
                choice.Crop.FertilizedDaysToFirstHarvest,
                choice.Crop.RegrowDays,
                choice.Crop.Seasons);
            slot.CropDisplayName = choice.Crop.SeedItemId;
            slot.FertilizerItemId = choice.Crop.FertilizerItemId;
            slot.FertilizerDisplayName = choice.Crop.FertilizerItemId ?? string.Empty;
        }
    }

    internal void EnrichDisplayNames(
        IReadOnlyList<CropCatalogEntry> crops,
        IReadOnlyList<FertilizerOption> fertilizers)
    {
        if (_yearRoundSlot is { } yearRoundSlot)
            EnrichSlot(yearRoundSlot, crops, fertilizers);

        foreach (var slot in _slots.Values)
            EnrichSlot(slot, crops, fertilizers);
    }

    private static void EnrichSlot(
        SeasonSlotDraft slot,
        IReadOnlyList<CropCatalogEntry> crops,
        IReadOnlyList<FertilizerOption> fertilizers)
    {
        if (slot.Crop is { } crop)
        {
            var match = crops.FirstOrDefault(entry =>
                string.Equals(entry.Crop.SeedItemId, crop.SeedItemId, StringComparison.Ordinal));
            if (match is not null)
                slot.CropDisplayName = match.DisplayName;
        }

        if (slot.FertilizerItemId is { } fertilizerId)
        {
            var match = fertilizers.FirstOrDefault(option =>
                string.Equals(option.ItemId, fertilizerId, StringComparison.Ordinal));
            if (match is not null)
                slot.FertilizerDisplayName = match.DisplayName;
        }
    }

    private SeasonSlotDraft GetOrCreate(Season season)
    {
        if (_slots.TryGetValue(season, out var slot))
            return slot;

        slot = new SeasonSlotDraft();
        _slots[season] = slot;
        return slot;
    }

    private SeasonSlotDraft GetOrCreateYearRound()
    {
        _yearRoundSlot ??= new SeasonSlotDraft();
        return _yearRoundSlot;
    }

    private static CropAssignmentMode ModeForLocation(string locationName) =>
        string.Equals(locationName, "Farm", StringComparison.Ordinal)
            ? CropAssignmentMode.Seasonal
            : CropAssignmentMode.SeasonAgnostic;
}

/// <summary>
/// Transient, mutable in-progress crop plan the Manage Crops pages edit. Owns multiple crop
/// groups, then projects them into the persisted <see cref="CropPlan"/> assignment list.
/// </summary>
internal sealed class CropPlanDraft
{
    internal static readonly Season[] AllSeasons =
        { Season.Spring, Season.Summer, Season.Fall, Season.Winter };

    private readonly List<CropGroupDraft> _groups = new();
    private int _nextGroupNumber = 1;

    public IReadOnlyList<CropGroupDraft> Groups => _groups;

    public bool HasAnyAssignment => _groups.Any(group => group.HasAnyAssignment);

    public CropGroupDraft AddGroup()
    {
        var group = new CropGroupDraft(NextGroupId());
        _groups.Add(group);
        return group;
    }

    public CropGroupDraft GetGroup(string groupId) =>
        _groups.First(group => string.Equals(group.Id, groupId, StringComparison.Ordinal));

    public bool TryGetGroup(string groupId, out CropGroupDraft group)
    {
        group = _groups.FirstOrDefault(g => string.Equals(g.Id, groupId, StringComparison.Ordinal))!;
        return group is not null;
    }

    public void DeleteGroup(string groupId)
    {
        _groups.RemoveAll(group => string.Equals(group.Id, groupId, StringComparison.Ordinal));
    }

    public void SetGroupZones(string groupId, IEnumerable<Zone> zones)
    {
        if (!TryGetGroup(groupId, out var group))
            return;

        group.Zones.Clear();
        group.Zones.AddRange(zones.Select(zone => new Zone(group.LocationName, zone.TopLeft, zone.BottomRight)));
    }

    public IReadOnlyList<Zone> ProtectedZones(string activeGroupId)
    {
        var locationName = TryGetGroup(activeGroupId, out var group) ? group.LocationName : "Farm";
        return ProtectedZones(activeGroupId, locationName);
    }

    public IReadOnlyList<Zone> ProtectedZones(string activeGroupId, string locationName) =>
        _groups
            .Where(group => !string.Equals(group.Id, activeGroupId, StringComparison.Ordinal))
            .SelectMany(group => group.Zones)
            .Where(zone => string.Equals(zone.LocationName, locationName, StringComparison.Ordinal))
            .ToList()
            .AsReadOnly();

    /// <summary>Builds the immutable crop plan attached to the contract on confirm.</summary>
    public CropPlan BuildCropPlan() =>
        new(_groups.SelectMany(group => group.BuildAssignments()).ToList());

    /// <summary>Seeds this draft from an existing contract's crop plan for the edit flow (draft isolation).</summary>
    public void HydrateFrom(CropPlan plan)
    {
        _groups.Clear();
        _nextGroupNumber = 1;

        if (!plan.IsEnabled)
            return;

        var legacyIndex = 1;
        var groups = plan.Assignments
            .Select(assignment => new
            {
                Assignment = assignment,
                GroupId = assignment.GroupId ?? $"legacy-{legacyIndex++}",
            })
            .GroupBy(item => item.GroupId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (var grouped in groups)
        {
            var first = grouped.First().Assignment;
            var group = new CropGroupDraft(grouped.Key);
            group.HydrateSlotsFrom(first);
            group.Zones.AddRange(grouped.Select(item => item.Assignment.Zone));
            _groups.Add(group);
        }
    }

    /// <summary>
    /// Fills in human-readable crop/fertilizer names on configured slots by matching ids against the
    /// freshly-built catalog (used after edit-flow hydration, since names are not persisted).
    /// </summary>
    public void EnrichDisplayNames(
        IReadOnlyList<CropCatalogEntry> crops,
        IReadOnlyList<FertilizerOption> fertilizers)
    {
        foreach (var group in _groups)
            group.EnrichDisplayNames(crops, fertilizers);
    }

    private string NextGroupId()
    {
        string id;
        do
        {
            id = $"group-{_nextGroupNumber++}";
        }
        while (_groups.Any(group => string.Equals(group.Id, id, StringComparison.Ordinal)));

        return id;
    }
}
