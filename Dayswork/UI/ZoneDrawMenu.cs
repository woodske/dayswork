using Dayswork.Core.Domain;
using Dayswork.Core.Geometry;
using Dayswork.Core.Machines;
using Dayswork.Integration;
using Dayswork.Orchestration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Dayswork.UI;

// Full-farm zone-drawing session (Robin building-placement UX pattern).
// Does NOT warp the player: it swaps the *displayed* location to the farm and pans
// the camera (like CarpenterMenu). The player character stays where it is.
internal sealed class ZoneDrawMenu : IClickableMenu, IZoneDrawSource
{
    private const int PanMargin = 64;   // screen-edge band that triggers panning
    private const int PanSpeed  = 12;   // world px per frame

    private readonly List<BuildingOutline> _buildingOutlines;
    private readonly IModHelper            _helper;
    private readonly Action<List<Zone>, List<BuildingOutline>>? _onComplete;
    private readonly Action                _onCancel;

    // ── Machine-selection mode (Manage Machines) ─────────────────────────────
    // When set, the session selects individual machines (1×1) across multiple locations instead of
    // drawing task/crop zones. Selected machines render as green fills via the existing overlay;
    // other groups' machines render protected (red). A location switcher cycles the displayed map.
    private readonly bool _machineMode;
    private readonly Action<List<MachineRef>>? _onMachinesComplete;
    private readonly IReadOnlyList<MachineMapLocation> _machineLocations = Array.Empty<MachineMapLocation>();
    private readonly string? _machineTypeFilter;   // only machines of this qualified id are selectable
    private readonly Dictionary<string, HashSet<TileCoord>> _selectedMachines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<TileCoord>> _protectedMachines = new(StringComparer.Ordinal);
    private int _locationIndex;   // shared by machine + chest + fish-pond location switchers
    private ClickableComponent? _locationBtn;

    // ── Fish-pond-selection mode (Manage Fish Ponds) ─────────────────────────
    // When set, the session selects whole fish ponds across multiple locations. A click anywhere on a
    // pond's footprint toggles that pond; selected ponds render as green footprint fills. Reuses the
    // _selectedMachines tile-set keyed by each pond's anchor tile. Collect-only — no type filter or
    // protected sets (a pond belongs to the single fish-pond plan).
    private readonly bool _fishPondMode;
    private readonly Action<List<Dayswork.Core.FishPonds.FishPondRef>>? _onFishPondsComplete;
    private readonly IReadOnlyList<FishPondMapLocation> _fishPondLocations = Array.Empty<FishPondMapLocation>();

    // ── Chest-selection mode (output/input chest pickers) ────────────────────
    // When set, the session selects a SINGLE chest (1×1) across multiple locations, plus the
    // non-chest options (Automatic / Shipping Bin / None) as on-screen buttons. The selected chest
    // renders green via the existing overlay; the location switcher cycles maps that contain a chest.
    private readonly bool _chestMode;
    private readonly Action<DestinationKey?>? _onChestComplete;
    private readonly IReadOnlyList<ChestMapLocation> _chestLocations = Array.Empty<ChestMapLocation>();
    private readonly ChestPickerOptions _chestOptions;
    private ChestRef? _selectedChest;
    private ChestSpecialKind? _selectedSpecial;          // mutually exclusive with _selectedChest
    private readonly List<(ClickableComponent Btn, ChestSpecialKind Kind)> _specialBtns = new();

    private enum ChestSpecialKind { Automatic, ShippingBin, None }

    // Zone session state — implements IZoneDrawSource
    private readonly List<Zone>            _completedZones    = new();
    private readonly List<BuildingOutline> _selectedBuildings = new();
    private TileCoord? _dragStart;
    private TileCoord  _dragCurrent;

    // Layer configuration (general task scope vs managed-crop zones).
    private readonly bool _allowBuildingSelection;
    private readonly bool _overlapToggles;
    private readonly Color _zoneFillColor;
    private readonly Color _protectedZoneFillColor;
    private string _targetLocationName;
    private readonly List<Zone> _protectedZones = new();

    // Managed-crop zones owned by OTHER contracts (other farmhands) at the target location. Rendered as
    // an informational light-purple wash but left out of the overlap/protection checks so they stay
    // selectable. Only populated by the managed-crop draw path; empty everywhere else.
    private readonly List<Zone> _otherWorkerZones = new();

    // Managed-crop draw layer: zones are flattened to the fewest rectangles and the count of valid
    // (tillable/plantable, non-sprinkler) tiles is shown on screen. Note _overlapToggles is also true
    // in machine mode, so crop mode must exclude the special modes.
    private bool CropMode => _overlapToggles && !_machineMode && !_fishPondMode && !_chestMode;
    private int _validCropTileCount;
    private GameLocation? _cropLocation;   // cached live map for the per-tile plantable test
    private GameLocation CropLocation => _cropLocation ??= ResolveDrawLocation(_targetLocationName);

    IReadOnlyList<Zone>            IZoneDrawSource.CompletedZones    =>
        _fishPondMode ? FishPondZones() : _machineMode ? MachineZones(_selectedMachines) : _chestMode ? SelectedChestZones() : _completedZones;
    IReadOnlyList<Zone>            IZoneDrawSource.ProtectedZones    =>
        _machineMode ? MachineZones(_protectedMachines) : _chestMode ? Array.Empty<Zone>() : _protectedZones;
    IReadOnlyList<Zone>            IZoneDrawSource.OtherWorkerZones  => _otherWorkerZones;
    IReadOnlyList<BuildingOutline> IZoneDrawSource.SelectedBuildings => _selectedBuildings;
    bool       IZoneDrawSource.IsInZoneDrawMode => true;          // grid always visible during the session
    TileCoord? IZoneDrawSource.DragStart        => _dragStart;
    TileCoord  IZoneDrawSource.DragCurrent      => _dragCurrent;
    Color      IZoneDrawSource.ZoneFillColor    => _zoneFillColor;
    Color      IZoneDrawSource.ProtectedZoneFillColor => _protectedZoneFillColor;
    bool       IZoneDrawSource.HighlightValidTilesOnly => CropMode;
    bool       IZoneDrawSource.IsHighlightableTile(TileCoord tile) =>
        ManagedCropFieldReader.IsPlantableGroundTile(CropLocation, tile.X, tile.Y);

    private readonly ZoneDrawOverlay _overlay;

    // Saved world state — restored on exit
    private readonly GameLocation _returnLocation;

    private bool _resultDelivered;

    // Corner toolbar buttons
    private ClickableComponent _cancelBtn  = null!;
    private ClickableComponent _clearBtn   = null!;
    private ClickableComponent _zoomOutBtn = null!;
    private ClickableComponent _zoomInBtn  = null!;
    private ClickableComponent _panBtn     = null!;
    private ClickableComponent _doneBtn    = null!;

    // Pan mode: drag moves the viewport instead of drawing a zone.
    private bool _isPanMode;
    private Point _lastDragPx;

    // Zoom (pan mode only). Extended range beyond the game's own slider (0.75–2.0).
    private const float ZoomStep = 0.25f;   // per scroll notch / button press
    private const float MinZoom  = 0.4f;    // below Options.minZoom (0.75) for a full-farm overview
    private const float MaxZoom  = Options.maxZoom;   // 2.0
    private float _savedZoom;                // player's zoom, restored on exit
    // On Android, desiredBaseZoomLevel's getter returns the hardware DPI zoom regardless of what
    // the setter writes, so the game's update loop reverts baseZoomLevel every frame. We track
    // the intended zoom here and force baseZoomLevel directly in update() after each revert.
    private float _effectiveZoom;
    private bool  _pinchActive;              // two fingers currently down (Android)
    private float _lastPinchDist;            // previous frame's finger distance

    public ZoneDrawMenu(
        ContractDraft draft,
        List<BuildingOutline> buildingOutlines,
        IModHelper helper,
        Action<List<Zone>, List<BuildingOutline>> onComplete,
        Action onCancel,
        IReadOnlyList<Zone>? initialZones = null,
        bool allowBuildingSelection = true,
        bool overlapTogglesSelection = false,
        IReadOnlyList<Zone>? protectedZones = null,
        IReadOnlyList<Zone>? otherWorkerZones = null,
        Color? zoneFillColor = null,
        string targetLocationName = "Farm")
        : base(0, 0, 0, 0)
    {
        var drawLocation = ResolveDrawLocation(targetLocationName);
        _buildingOutlines = buildingOutlines;
        _helper           = helper;
        _onComplete       = onComplete;
        _onCancel         = onCancel;
        _allowBuildingSelection = allowBuildingSelection;
        _overlapToggles   = overlapTogglesSelection;
        _zoneFillColor    = zoneFillColor ?? Color.LightBlue * 0.4f;
        _protectedZoneFillColor = Color.Red * 0.35f;
        _targetLocationName = drawLocation.NameOrUniqueName;
        _protectedZones.AddRange((protectedZones ?? Array.Empty<Zone>())
            .Where(zone => string.Equals(zone.LocationName, _targetLocationName, StringComparison.Ordinal)));
        _otherWorkerZones.AddRange((otherWorkerZones ?? Array.Empty<Zone>())
            .Where(zone => string.Equals(zone.LocationName, _targetLocationName, StringComparison.Ordinal)));

        // Restore prior selections (for the active layer only) so navigating back preserves work.
        _completedZones.AddRange((initialZones ?? draft.OutdoorZones)
            .Where(zone => string.Equals(zone.LocationName, _targetLocationName, StringComparison.Ordinal)));

        if (CropMode)
        {
            FlattenCropZones();
            RecomputeCropTileCount();
        }

        if (_allowBuildingSelection)
        {
            foreach (var animalBuilding in draft.AnimalBuildings)
            {
                var normalizedName = BuildingLocationResolver.NormalizeLocationName(Game1.getFarm(), animalBuilding.LocationName);
                var match = buildingOutlines.FirstOrDefault(outline =>
                    outline.LocationName == animalBuilding.LocationName
                    || outline.LocationName == normalizedName);
                if (match is not null && !_selectedBuildings.Contains(match))
                    _selectedBuildings.Add(match);
            }

            foreach (var greenhouse in draft.Greenhouses)
            {
                var normalizedName = BuildingLocationResolver.NormalizeLocationName(Game1.getFarm(), greenhouse.LocationName);
                var greenhouseMatch = buildingOutlines.FirstOrDefault(outline =>
                    outline.LocationName == greenhouse.LocationName
                    || outline.LocationName == normalizedName);
                if (greenhouseMatch is not null && !_selectedBuildings.Contains(greenhouseMatch))
                    _selectedBuildings.Add(greenhouseMatch);
            }
        }

        // Swap displayed location to the target map (no warp) and freeze the camera so we control it.
        _returnLocation = Game1.currentLocation;
        Game1.currentLocation = drawLocation;
        Game1.viewportFreeze  = true;
        Game1.displayHUD      = false;
        // baseZoomLevel is always accurate (desiredBaseZoomLevel getter is hardware-derived on Android).
        _savedZoom     = Game1.options.baseZoomLevel;
        _effectiveZoom = _savedZoom;
        CenterViewport(drawLocation);

        _overlay = new ZoneDrawOverlay(this, Game1.game1.GraphicsDevice);
        helper.Events.Display.RenderedWorld += _overlay.OnRenderedWorld;

        BuildComponents();
        populateClickableComponentList();
    }

    /// <summary>
    /// Machine-selection session: pick individual machines (1×1) across the candidate
    /// <paramref name="locations"/>, switching the displayed map with the location switcher.
    /// </summary>
    public ZoneDrawMenu(
        IModHelper helper,
        IReadOnlyList<MachineMapLocation> locations,
        IReadOnlyList<MachineRef> initialSelected,
        IReadOnlyList<MachineRef> protectedMachines,
        Action<List<MachineRef>> onComplete,
        Action onCancel,
        string? machineTypeFilter = null)
        : base(0, 0, 0, 0)
    {
        _machineMode        = true;
        _machineLocations   = locations;
        _machineTypeFilter  = machineTypeFilter;
        _helper             = helper;
        _onMachinesComplete = onComplete;
        _onCancel           = onCancel;
        _onComplete         = null;
        _buildingOutlines   = new List<BuildingOutline>();
        _allowBuildingSelection = false;
        _overlapToggles     = true;
        _zoneFillColor      = Color.LimeGreen * 0.5f;
        _protectedZoneFillColor = Color.Red * 0.35f;

        foreach (var machine in initialSelected)
            Selected(machine.LocationName).Add(machine.Tile);
        foreach (var machine in protectedMachines)
            Protected(machine.LocationName).Add(machine.Tile);

        // Start on the first selected machine's location, else the first candidate location.
        _locationIndex = 0;
        if (initialSelected.Count > 0)
        {
            var idx = IndexOfLocation(initialSelected[0].LocationName);
            if (idx >= 0)
                _locationIndex = idx;
        }

        _targetLocationName = _machineLocations[_locationIndex].LocationName;
        var drawLocation = ResolveDrawLocation(_targetLocationName);

        _returnLocation = Game1.currentLocation;
        Game1.currentLocation = drawLocation;
        Game1.viewportFreeze  = true;
        Game1.displayHUD      = false;
        _savedZoom     = Game1.options.baseZoomLevel;
        _effectiveZoom = _savedZoom;
        CenterViewport(drawLocation);

        _overlay = new ZoneDrawOverlay(this, Game1.game1.GraphicsDevice);
        helper.Events.Display.RenderedWorld += _overlay.OnRenderedWorld;

        BuildComponents();
        populateClickableComponentList();
    }

    /// <summary>
    /// Fish-pond-selection session: pick whole fish ponds across the candidate
    /// <paramref name="locations"/> (switching the displayed map with the location switcher). A click
    /// anywhere on a pond toggles it. Returns the selected <see cref="Dayswork.Core.FishPonds.FishPondRef"/>s.
    /// </summary>
    public ZoneDrawMenu(
        IModHelper helper,
        IReadOnlyList<FishPondMapLocation> locations,
        IReadOnlyList<Dayswork.Core.FishPonds.FishPondRef> initialSelected,
        Action<List<Dayswork.Core.FishPonds.FishPondRef>> onComplete,
        Action onCancel)
        : base(0, 0, 0, 0)
    {
        _fishPondMode        = true;
        _fishPondLocations   = locations;
        _helper              = helper;
        _onFishPondsComplete = onComplete;
        _onCancel            = onCancel;
        _onComplete          = null;
        _buildingOutlines    = new List<BuildingOutline>();
        _allowBuildingSelection = false;
        _overlapToggles      = true;
        _zoneFillColor       = Color.LimeGreen * 0.5f;
        _protectedZoneFillColor = Color.Red * 0.35f;

        foreach (var pond in initialSelected)
            Selected(pond.LocationName).Add(pond.Tile);

        _locationIndex = 0;
        if (initialSelected.Count > 0)
        {
            var idx = IndexOfLocation(initialSelected[0].LocationName);
            if (idx >= 0)
                _locationIndex = idx;
        }

        _targetLocationName = _fishPondLocations[_locationIndex].LocationName;
        var drawLocation = ResolveDrawLocation(_targetLocationName);

        _returnLocation = Game1.currentLocation;
        Game1.currentLocation = drawLocation;
        Game1.viewportFreeze  = true;
        Game1.displayHUD      = false;
        _savedZoom     = Game1.options.baseZoomLevel;
        _effectiveZoom = _savedZoom;
        CenterViewport(drawLocation);

        _overlay = new ZoneDrawOverlay(this, Game1.game1.GraphicsDevice);
        helper.Events.Display.RenderedWorld += _overlay.OnRenderedWorld;

        BuildComponents();
        populateClickableComponentList();
    }

    /// <summary>
    /// Chest-selection session: pick a SINGLE chest across the candidate <paramref name="locations"/>
    /// (switching the displayed map with the location switcher), plus the enabled non-chest options.
    /// Returns the chosen <see cref="DestinationKey"/> (null = "None" / nothing chosen).
    /// </summary>
    public ZoneDrawMenu(
        IModHelper helper,
        IReadOnlyList<ChestMapLocation> locations,
        DestinationKey? initial,
        ChestPickerOptions options,
        Action<DestinationKey?> onComplete,
        Action onCancel)
        : base(0, 0, 0, 0)
    {
        _chestMode        = true;
        _chestLocations   = locations;
        _chestOptions     = options;
        _helper           = helper;
        _onChestComplete  = onComplete;
        _onCancel         = onCancel;
        _onComplete       = null;
        _buildingOutlines = new List<BuildingOutline>();
        _allowBuildingSelection = false;
        _zoneFillColor    = Color.LimeGreen * 0.5f;
        _protectedZoneFillColor = Color.Red * 0.35f;

        // Seed the selection from the current value.
        switch (initial)
        {
            case ChestDestination chestDest:
                _selectedChest = chestDest.Ref;
                break;
            case ShippingBinDestination:
                _selectedSpecial = ChestSpecialKind.ShippingBin;
                break;
            case AutomaticOutputDestination:
                _selectedSpecial = ChestSpecialKind.Automatic;
                break;
            default:
                // null means "automatic" for output pickers, "no chest" for input pickers.
                _selectedSpecial = options.ShowNone ? ChestSpecialKind.None
                    : options.ShowAutomatic ? ChestSpecialKind.Automatic
                    : null;
                break;
        }

        // Start on the selected chest's location, else the first candidate location.
        _locationIndex = 0;
        if (_selectedChest is { } sel)
        {
            var idx = IndexOfChestLocation(sel.LocationName);
            if (idx >= 0)
                _locationIndex = idx;
        }

        _targetLocationName = _chestLocations.Count > 0
            ? _chestLocations[_locationIndex].LocationName
            : "Farm";
        var drawLocation = ResolveDrawLocation(_targetLocationName);

        _returnLocation = Game1.currentLocation;
        Game1.currentLocation = drawLocation;
        Game1.viewportFreeze  = true;
        Game1.displayHUD      = false;
        _savedZoom     = Game1.options.baseZoomLevel;
        _effectiveZoom = _savedZoom;
        CenterViewport(drawLocation);

        _overlay = new ZoneDrawOverlay(this, Game1.game1.GraphicsDevice);
        helper.Events.Display.RenderedWorld += _overlay.OnRenderedWorld;

        BuildComponents();
        populateClickableComponentList();
    }

    // ── Chest-mode helpers ───────────────────────────────────────────────────

    private ChestMapLocation CurrentChestLocation => _chestLocations[_locationIndex];

    private int IndexOfChestLocation(string locationName)
    {
        for (var i = 0; i < _chestLocations.Count; i++)
            if (string.Equals(_chestLocations[i].LocationName, locationName, StringComparison.Ordinal))
                return i;
        return -1;
    }

    // The selected chest as a 1×1 zone the overlay renders, but only when it's on the current map.
    private IReadOnlyList<Zone> SelectedChestZones() =>
        _selectedChest is { } chest && string.Equals(chest.LocationName, _targetLocationName, StringComparison.Ordinal)
            ? new List<Zone> { new Zone(_targetLocationName, chest.Tile, chest.Tile) }
            : Array.Empty<Zone>();

    private void HandleChestSelection(TileCoord tile)
    {
        if (_chestLocations.Count == 0)
        {
            Game1.playSound("cancel");
            return;
        }

        if (!CurrentChestLocation.Candidates.TryGetValue(tile, out var chestRef))
        {
            Game1.playSound("cancel");
            return;
        }

        if (_selectedChest == chestRef)
        {
            _selectedChest = null;          // re-click the same chest clears it
            Game1.playSound("bigDeSelect");
            return;
        }

        _selectedChest = chestRef;
        _selectedSpecial = null;
        Game1.playSound("coin");
    }

    private void SelectSpecial(ChestSpecialKind kind)
    {
        _selectedSpecial = kind;
        _selectedChest = null;
        Game1.playSound("smallSelect");
    }

    private DestinationKey? ChestResult() =>
        _selectedChest is { } chest
            ? new ChestDestination(chest)
            : _selectedSpecial switch
            {
                ChestSpecialKind.Automatic => AutomaticOutputDestination.Instance,
                ChestSpecialKind.ShippingBin => ShippingBinDestination.Instance,
                _ => null,   // None or nothing chosen
            };

    private static string SpecialLabel(ChestSpecialKind kind) => kind switch
    {
        ChestSpecialKind.Automatic => I18nHelper.Get("ui.zone_chest.picker_automatic_output_option"),
        ChestSpecialKind.ShippingBin => I18nHelper.Get("ui.zone_chest.shipping_bin_option"),
        _ => I18nHelper.Get("ui.zone_chest.no_chest_option"),
    };

    // ── Machine-mode helpers ─────────────────────────────────────────────────

    private MachineMapLocation CurrentMachineLocation => _machineLocations[_locationIndex];

    private HashSet<TileCoord> Selected(string locationName)
    {
        if (!_selectedMachines.TryGetValue(locationName, out var set))
            _selectedMachines[locationName] = set = new HashSet<TileCoord>();
        return set;
    }

    private HashSet<TileCoord> Protected(string locationName)
    {
        if (!_protectedMachines.TryGetValue(locationName, out var set))
            _protectedMachines[locationName] = set = new HashSet<TileCoord>();
        return set;
    }

    private int IndexOfLocation(string locationName)
    {
        if (_fishPondMode)
        {
            for (var i = 0; i < _fishPondLocations.Count; i++)
                if (string.Equals(_fishPondLocations[i].LocationName, locationName, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        for (var i = 0; i < _machineLocations.Count; i++)
            if (string.Equals(_machineLocations[i].LocationName, locationName, StringComparison.Ordinal))
                return i;
        return -1;
    }

    // ── Fish-pond-mode helpers ───────────────────────────────────────────────

    private FishPondMapLocation CurrentFishPondLocation => _fishPondLocations[_locationIndex];

    // Selected ponds for the CURRENT display location as full-footprint zones for the overlay.
    private IReadOnlyList<Zone> FishPondZones()
    {
        if (!_selectedMachines.TryGetValue(_targetLocationName, out var anchors) || anchors.Count == 0)
            return Array.Empty<Zone>();

        var ponds = CurrentFishPondLocation.Ponds;
        return anchors
            .Select(anchor => ponds.FirstOrDefault(p => p.Anchor.Equals(anchor)))
            .Where(pond => pond is not null)
            .Select(pond => pond!.ToZone(_targetLocationName))
            .ToList();
    }

    private void HandleFishPondSelection(TileCoord topLeft, TileCoord bottomRight, bool singleTile)
    {
        var selected = Selected(_targetLocationName);
        var ponds = CurrentFishPondLocation.Ponds;

        if (singleTile)
        {
            var pond = ponds.FirstOrDefault(p => p.Contains(topLeft));
            if (pond is null)
            {
                Game1.playSound("cancel");
                return;
            }

            if (selected.Remove(pond.Anchor))
                Game1.playSound("bigDeSelect");
            else
            {
                selected.Add(pond.Anchor);
                Game1.playSound("coin");
            }

            return;
        }

        // Drag rectangle: select every pond whose footprint overlaps the rectangle.
        var added = 0;
        foreach (var pond in ponds)
        {
            var overlaps = pond.Anchor.X + pond.Width - 1 >= topLeft.X && pond.Anchor.X <= bottomRight.X
                && pond.Anchor.Y + pond.Height - 1 >= topLeft.Y && pond.Anchor.Y <= bottomRight.Y;
            if (overlaps && selected.Add(pond.Anchor))
                added++;
        }

        Game1.playSound(added > 0 ? "coin" : "cancel");
    }

    private List<Dayswork.Core.FishPonds.FishPondRef> CollectSelectedFishPonds()
    {
        var result = new List<Dayswork.Core.FishPonds.FishPondRef>();
        foreach (var location in _fishPondLocations)
        {
            if (!_selectedMachines.TryGetValue(location.LocationName, out var anchors))
                continue;

            foreach (var pond in location.Ponds)
                if (anchors.Contains(pond.Anchor))
                    result.Add(new Dayswork.Core.FishPonds.FishPondRef(location.LocationName, pond.Anchor));
        }

        return result;
    }

    private int SelectedFishPondCount => _selectedMachines.Values.Sum(set => set.Count);

    // Selected/protected machines for the CURRENT display location only, as 1×1 zones the overlay
    // renders in the active viewport. (Selections for other locations persist off-screen.)
    private IReadOnlyList<Zone> MachineZones(Dictionary<string, HashSet<TileCoord>> source) =>
        source.TryGetValue(_targetLocationName, out var tiles) && tiles.Count > 0
            ? tiles.Select(tile => new Zone(_targetLocationName, tile, tile)).ToList()
            : Array.Empty<Zone>();

    private int LocationCount =>
        _fishPondMode ? _fishPondLocations.Count : _chestMode ? _chestLocations.Count : _machineLocations.Count;

    private string CurrentLocationDisplayName =>
        _fishPondMode ? CurrentFishPondLocation.DisplayName
        : _chestMode ? CurrentChestLocation.DisplayName
        : CurrentMachineLocation.DisplayName;

    private string LocationButtonLabel() =>
        I18nHelper.Get("ui.manage_machines.viewing", new { name = CurrentLocationDisplayName });

    private void SwitchLocation(int direction)
    {
        if (LocationCount <= 1)
        {
            Game1.playSound("cancel");
            return;
        }

        _locationIndex = ((_locationIndex + direction) % LocationCount + LocationCount) % LocationCount;
        _targetLocationName = _fishPondMode
            ? _fishPondLocations[_locationIndex].LocationName
            : _chestMode
                ? _chestLocations[_locationIndex].LocationName
                : _machineLocations[_locationIndex].LocationName;
        var loc = ResolveDrawLocation(_targetLocationName);
        Game1.currentLocation = loc;
        CenterViewport(loc);
        _dragStart = null;
        Game1.playSound("smallSelect");
    }

    private void HandleMachineSelection(TileCoord topLeft, TileCoord bottomRight, bool singleTile)
    {
        var selected = Selected(_targetLocationName);
        var protectedSet = Protected(_targetLocationName);
        var candidates = CurrentMachineLocation.Candidates;

        if (singleTile)
        {
            var tile = topLeft;
            if (!candidates.TryGetValue(tile, out var candidateId) || !MatchesTypeFilter(candidateId))
            {
                Game1.playSound("cancel");
                return;
            }

            if (protectedSet.Contains(tile))
            {
                Game1.addHUDMessage(new HUDMessage(
                    I18nHelper.Get("ui.manage_machines.machine_claimed"),
                    HUDMessage.error_type));
                Game1.playSound("cancel");
                return;
            }

            if (selected.Remove(tile))
                Game1.playSound("bigDeSelect");
            else
            {
                selected.Add(tile);
                Game1.playSound("coin");
            }

            return;
        }

        // Drag rectangle: add every candidate machine of the chosen type inside it that isn't claimed.
        var added = 0;
        foreach (var (tile, candidateId) in candidates)
        {
            if (tile.X < topLeft.X || tile.X > bottomRight.X || tile.Y < topLeft.Y || tile.Y > bottomRight.Y)
                continue;
            if (!MatchesTypeFilter(candidateId) || protectedSet.Contains(tile) || !selected.Add(tile))
                continue;
            added++;
        }

        Game1.playSound(added > 0 ? "coin" : "cancel");
    }

    private bool MatchesTypeFilter(string candidateId) =>
        _machineTypeFilter is null || string.Equals(candidateId, _machineTypeFilter, StringComparison.Ordinal);

    private List<MachineRef> CollectSelectedMachines()
    {
        var result = new List<MachineRef>();
        foreach (var location in _machineLocations)
        {
            if (!_selectedMachines.TryGetValue(location.LocationName, out var tiles))
                continue;

            foreach (var tile in tiles)
                if (location.Candidates.TryGetValue(tile, out var id))
                    result.Add(new MachineRef(location.LocationName, tile, id));
        }

        return result;
    }

    private int SelectedMachineCount => _selectedMachines.Values.Sum(set => set.Count);

    // ── Component construction ───────────────────────────────────────────────

    private void BuildComponents()
    {
        int w = Game1.uiViewport.Width;
        int h = Game1.uiViewport.Height;
        int bh = 60, pad = 30;
        int by = h - pad - bh;

        // Size every button to fit its label with consistent horizontal padding.
        string cancelLabel = I18nHelper.Get("ui.zone_chest.back_btn");
        string clearLabel  = I18nHelper.Get("ui.zone_chest.clear_zones_btn");
        string panLabel    = I18nHelper.Get("ui.zone_chest.pan_btn");
        string doneLabel   = I18nHelper.Get("ui.zone_chest.done_drawing_btn");
        int bw = (int)Math.Ceiling(Math.Max(
            Math.Max(Math.Max(Game1.smallFont.MeasureString(cancelLabel).X,
                              Game1.smallFont.MeasureString(clearLabel).X),
                     Game1.smallFont.MeasureString(panLabel).X),
            Game1.smallFont.MeasureString(doneLabel).X)) + 40;

        _cancelBtn = new ClickableComponent(
            new Rectangle(pad, by, bw, bh),
            "Cancel", cancelLabel)
        { myID = 201, rightNeighborID = 202 };

        _clearBtn = new ClickableComponent(
            new Rectangle(pad + bw + 16, by, bw, bh),
            "Clear", clearLabel)
        { myID = 202, leftNeighborID = 201, rightNeighborID = 204 };

        // Compact +/- zoom buttons (pan mode only), grouped just left of the Pan button.
        int zbw = bh, zgap = 12;
        int panX = w - pad - 2 * bw - 16;
        int zoomInX  = panX - zgap - zbw;
        int zoomOutX = zoomInX - zgap - zbw;

        _zoomOutBtn = new ClickableComponent(
            new Rectangle(zoomOutX, by, zbw, bh),
            "ZoomOut", "-")
        { myID = 204, leftNeighborID = 202, rightNeighborID = 205 };

        _zoomInBtn = new ClickableComponent(
            new Rectangle(zoomInX, by, zbw, bh),
            "ZoomIn", "+")
        { myID = 205, leftNeighborID = 204, rightNeighborID = 203 };

        _panBtn = new ClickableComponent(
            new Rectangle(panX, by, bw, bh),
            "Pan", panLabel)
        { myID = 203, leftNeighborID = 205, rightNeighborID = 200 };

        _doneBtn = new ClickableComponent(
            new Rectangle(w - pad - bw, by, bw, bh),
            "Done", doneLabel)
        { myID = 200, leftNeighborID = 203 };

        // Machine/chest mode: a top-left location switcher cycles the displayed map among candidate
        // locations (farm + interiors/greenhouse with ≥1 machine/chest). Chest mode may have no
        // candidate locations (no selectable chests yet) — then there's nothing to switch.
        _locationBtn = _machineMode || _fishPondMode || (_chestMode && _chestLocations.Count > 0)
            ? new ClickableComponent(new Rectangle(pad, 80, 320, bh), "Location", LocationButtonLabel()) { myID = 210 }
            : null;

        // Chest mode: stack the enabled non-chest options (Automatic / Shipping Bin / None) under
        // the location switcher as toggle buttons.
        _specialBtns.Clear();
        if (_chestMode)
        {
            var kinds = new List<ChestSpecialKind>();
            if (_chestOptions.ShowAutomatic)   kinds.Add(ChestSpecialKind.Automatic);
            if (_chestOptions.ShowShippingBin) kinds.Add(ChestSpecialKind.ShippingBin);
            if (_chestOptions.ShowNone)        kinds.Add(ChestSpecialKind.None);

            int sw = Math.Max(320, kinds.Count == 0 ? 0 :
                (int)Math.Ceiling(kinds.Max(k => Game1.smallFont.MeasureString(SpecialLabel(k)).X)) + 40);
            int sy = 80 + bh + 8;
            for (var i = 0; i < kinds.Count; i++)
            {
                _specialBtns.Add((
                    new ClickableComponent(new Rectangle(pad, sy + i * (bh + 8), sw, bh),
                        SpecialLabel(kinds[i]), SpecialLabel(kinds[i])) { myID = 220 + i },
                    kinds[i]));
            }
        }
    }

    private static void CenterViewport(GameLocation loc)
    {
        Game1.viewport.X = 0;
        Game1.viewport.Y = 0;
        int cx = loc.map.DisplayWidth  / 2 - Game1.viewport.Width  / 2;
        int cy = loc.map.DisplayHeight / 2 - Game1.viewport.Height / 2;
        PanViewport(cx, cy);
    }

    private static void PanViewport(int dx, int dy)
    {
        var map  = Game1.currentLocation.map;
        int maxX = Math.Max(0, map.DisplayWidth  - Game1.viewport.Width);
        int maxY = Math.Max(0, map.DisplayHeight - Game1.viewport.Height);
        Game1.viewport.X = Math.Clamp(Game1.viewport.X + dx, 0, maxX);
        Game1.viewport.Y = Math.Clamp(Game1.viewport.Y + dy, 0, maxY);
    }

    protected override void cleanupBeforeExit()
    {
        // Restore world state regardless of how we exit
        Game1.currentLocation = _returnLocation;
        Game1.viewportFreeze  = false;
        Game1.displayHUD      = true;
        Game1.options.desiredBaseZoomLevel = _savedZoom;
        Game1.options.baseZoomLevel        = _savedZoom;   // direct restore for Android
        Game1.game1.refreshWindowSettings();

        _helper.Events.Display.RenderedWorld -= _overlay.OnRenderedWorld;
        _overlay.Dispose();
        base.cleanupBeforeExit();

        // Safety net: if the menu was force-closed, still hand control back to the flow
        if (!_resultDelivered)
        {
            _resultDelivered = true;
            _onCancel();
        }
    }

    // Block ESC / menu-button close so we always exit through Done/Cancel
    public override bool readyToClose() => false;

    // ── Camera panning ───────────────────────────────────────────────────────

    public override void update(GameTime time)
    {
        base.update(time);

        int mx = Game1.getMouseX(true);
        int my = Game1.getMouseY(true);

        if (!IsOverButton(mx, my))
        {
            int w = Game1.uiViewport.Width;
            int h = Game1.uiViewport.Height;
            int dx = 0, dy = 0;
            if (mx < PanMargin)          dx = -PanSpeed;
            else if (mx > w - PanMargin) dx =  PanSpeed;
            if (my < PanMargin)          dy = -PanSpeed;
            else if (my > h - PanMargin) dy =  PanSpeed;
            if (dx != 0 || dy != 0) PanViewport(dx, dy);
        }

        if (Game1.options.gamepadControls)
        {
            var rs = Game1.input.GetGamePadState().ThumbSticks.Right;
            if (rs.LengthSquared() > 0.02f)
                PanViewport((int)(rs.X * PanSpeed * 1.5f), (int)(-rs.Y * PanSpeed * 1.5f));
        }

        // Android: game.update() reverts baseZoomLevel to the hardware DPI zoom each frame because
        // desiredBaseZoomLevel's getter ignores what we write. Force our value back here, after the
        // revert, so the draw phase sees the correct zoom. Skips the call when no zoom change is active.
        if (Constants.TargetPlatform == GamePlatform.Android &&
            Math.Abs(Game1.options.baseZoomLevel - _effectiveZoom) > 0.001f)
        {
            Game1.options.baseZoomLevel = _effectiveZoom;
            Game1.game1.refreshWindowSettings();
        }

        UpdatePinchZoom();
    }

    private bool IsOverButton(int x, int y) =>
        _cancelBtn.bounds.Contains(x, y)  ||
        _clearBtn.bounds.Contains(x, y)   ||
        _zoomOutBtn.bounds.Contains(x, y) ||
        _zoomInBtn.bounds.Contains(x, y)  ||
        _panBtn.bounds.Contains(x, y)     ||
        _doneBtn.bounds.Contains(x, y)    ||
        (_locationBtn is not null && _locationBtn.bounds.Contains(x, y)) ||
        _specialBtns.Any(special => special.Btn.bounds.Contains(x, y));

    // ── Zoom ────────────────────────────────────────────────────────────────────
    // On PC: writing desiredBaseZoomLevel works; game.update() applies it and recomputes the viewport.
    // On Android: desiredBaseZoomLevel getter returns the hardware DPI zoom regardless of writes, so
    // the game reverts baseZoomLevel every frame. We track _effectiveZoom separately and force
    // baseZoomLevel directly in update() after each revert (see Android force block there).
    private void SetZoom(float target)
    {
        float clamped = Math.Clamp(target, MinZoom, MaxZoom);
        if (Math.Abs(clamped - _effectiveZoom) < 0.001f) return;
        _effectiveZoom = clamped;
        Game1.options.desiredBaseZoomLevel = clamped;   // effective on PC; ignored on Android
    }

    // Discrete zoom (scroll notch / button) — plays a sound when the level actually changes.
    private void AdjustZoom(float delta)
    {
        float before = _effectiveZoom;
        SetZoom(before + delta);
        if (Math.Abs(_effectiveZoom - before) > 0.001f)
            Game1.playSound("smallSelect");
    }

    // Two-finger pinch zoom (Android). On PC TouchPanel returns no points, so this is inert.
    // Works in both draw and pan mode; cancels any active zone draw to prevent stray zones.
    // Kept silent (no sound) to avoid per-frame spam while the fingers move.
    private void UpdatePinchZoom()
    {
        var touches = TouchPanel.GetState();
        if (touches.Count >= 2)
        {
            if (!_pinchActive)
                _dragStart = null;   // cancel any in-progress zone draw before first pinch frame

            float dist = Vector2.Distance(touches[0].Position, touches[1].Position);
            if (_pinchActive && _lastPinchDist > 0f)
                SetZoom(_effectiveZoom * (dist / _lastPinchDist));   // _effectiveZoom accumulates correctly across frames
            _pinchActive   = true;
            _lastPinchDist = dist;
            return;
        }

        _pinchActive   = false;
        _lastPinchDist = 0f;
    }

    // ── Input ────────────────────────────────────────────────────────────────

    public override void receiveScrollWheelAction(int direction)
    {
        // Android has no scroll wheel; gate here so nothing fires if SMAPI ever routes scroll events.
        if (Constants.TargetPlatform == GamePlatform.Android) return;
        AdjustZoom(direction > 0 ? ZoomStep : -ZoomStep);   // wheel up = zoom in
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (_doneBtn.bounds.Contains(x, y))   { DoComplete(); return; }
        if (_cancelBtn.bounds.Contains(x, y)) { DoCancel();   return; }
        if (_clearBtn.bounds.Contains(x, y))
        {
            if (_machineMode || _fishPondMode)
            {
                _selectedMachines.Clear();
            }
            else if (_chestMode)
            {
                _selectedChest = null;
                _selectedSpecial = null;
            }
            else
            {
                _completedZones.Clear();
                _selectedBuildings.Clear();
                RecomputeCropTileCount();
            }
            Game1.playSound("trashcan");
            return;
        }
        if ((_machineMode || _fishPondMode || _chestMode) && _locationBtn is not null && _locationBtn.bounds.Contains(x, y))
        {
            SwitchLocation(1);
            return;
        }
        if (_chestMode)
        {
            foreach (var (btn, kind) in _specialBtns)
            {
                if (!btn.bounds.Contains(x, y)) continue;
                SelectSpecial(kind);
                return;
            }
        }
        if (_panBtn.bounds.Contains(x, y))
        {
            _isPanMode = !_isPanMode;
            Game1.playSound("smallSelect");
            return;
        }

        if (_zoomInBtn.bounds.Contains(x, y))  { AdjustZoom(ZoomStep);  return; }
        if (_zoomOutBtn.bounds.Contains(x, y)) { AdjustZoom(-ZoomStep); return; }

        if (_isPanMode)
        {
            _lastDragPx = new Point(x, y);
            return;
        }

        // Anywhere on the farm: begin a drag (a single-tile click on a building toggles it on release)
        _dragStart   = CursorTile();
        _dragCurrent = _dragStart.Value;
    }

    public override void leftClickHeld(int x, int y)
    {
        if (_isPanMode)
        {
            // While pinching, the primary finger still fires here — don't pan; keep the anchor
            // fresh so lifting the second finger doesn't cause a jump.
            if (_pinchActive)
            {
                _lastDragPx = new Point(x, y);
                return;
            }

            var current = new Point(x, y);
            PanViewport(-(current.X - _lastDragPx.X), -(current.Y - _lastDragPx.Y));
            _lastDragPx = current;
            return;
        }

        if (_dragStart.HasValue)
            _dragCurrent = CursorTile();
    }

    public override void releaseLeftClick(int x, int y)
    {
        if (_isPanMode) return;
        if (!_dragStart.HasValue) return;

        var start = _dragStart.Value;
        var end   = CursorTile();
        _dragStart = null;

        var topLeft     = new TileCoord(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y));
        var bottomRight = new TileCoord(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));

        var singleTile = topLeft.X == bottomRight.X && topLeft.Y == bottomRight.Y;

        if (_chestMode)
        {
            HandleChestSelection(topLeft);   // single-select; drags resolve to the click's start tile
            return;
        }

        if (_machineMode)
        {
            HandleMachineSelection(topLeft, bottomRight, singleTile);
            return;
        }

        if (_fishPondMode)
        {
            HandleFishPondSelection(topLeft, bottomRight, singleTile);
            return;
        }

        if (ZoneOverlapPolicy.OverlapsAny(_protectedZones, topLeft, bottomRight))
        {
            Game1.addHUDMessage(new HUDMessage(
                I18nHelper.Get("ui.manage_crops.zone_overlap_protected"),
                HUDMessage.error_type));
            Game1.playSound("cancel");
            return;
        }

        // Single-tile click on a building toggles it (general scope only).
        if (singleTile && _allowBuildingSelection)
        {
            foreach (var outline in _buildingOutlines)
            {
                if (!outline.TileBounds.Contains(topLeft.X, topLeft.Y)) continue;
                ToggleBuilding(outline);
                Game1.playSound("smallSelect");
                return;
            }
        }

        // Overlap-toggle layers (managed crops): a tile can be selected once. Drawing over any
        // already-selected tile removes the overlapping zone(s) instead of stacking another on top.
        if (_overlapToggles)
        {
            var overlapping = _completedZones.Where(zone => ZoneOverlapPolicy.ZonesOverlap(zone, topLeft, bottomRight)).ToList();
            if (overlapping.Count > 0)
            {
                foreach (var zone in overlapping)
                    _completedZones.Remove(zone);
                Game1.playSound("bigDeSelect");
            }
            else
            {
                _completedZones.Add(new Zone(_targetLocationName, topLeft, bottomRight));
                FlattenCropZones();   // collapse the new zone into any touching zones
                Game1.playSound("coin");
            }

            RecomputeCropTileCount();
            return;
        }

        // General scope: a bare single-tile click selects nothing; drags add a zone (overlap allowed).
        if (singleTile)
            return;

        _completedZones.Add(new Zone(_targetLocationName, topLeft, bottomRight));
        Game1.playSound("coin");
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (b == Buttons.B) { DoCancel(); return; }
        base.receiveGamePadButton(b);
    }

    private void ToggleBuilding(BuildingOutline outline)
    {
        if (_selectedBuildings.Contains(outline))
            _selectedBuildings.Remove(outline);
        else
            _selectedBuildings.Add(outline);
    }

    private void DoComplete()
    {
        _resultDelivered = true;

        if (_chestMode)
        {
            var result = ChestResult();
            exitThisMenu(false);
            _onChestComplete!(result);
            return;
        }

        if (_machineMode)
        {
            var machines = CollectSelectedMachines();
            exitThisMenu(false);
            _onMachinesComplete!(machines);
            return;
        }

        if (_fishPondMode)
        {
            var ponds = CollectSelectedFishPonds();
            exitThisMenu(false);
            _onFishPondsComplete!(ponds);
            return;
        }

        var zones     = new List<Zone>(_completedZones);
        var buildings = new List<BuildingOutline>(_selectedBuildings);
        exitThisMenu(false);
        _onComplete!(zones, buildings);
    }

    private void DoCancel()
    {
        _resultDelivered = true;
        exitThisMenu(false);
        _onCancel();
    }

    // ── Coordinate helpers ───────────────────────────────────────────────────

    // World tile under the cursor (zoom-space mouse + world viewport — robust to UI scale).
    private static TileCoord CursorTile() =>
        new TileCoord(
            (Game1.viewport.X + Game1.getMouseX(false)) / Game1.tileSize,
            (Game1.viewport.Y + Game1.getMouseY(false)) / Game1.tileSize);

    private static GameLocation ResolveDrawLocation(string locationName)
    {
        if (string.Equals(locationName, "Farm", StringComparison.Ordinal))
            return Game1.getFarm();

        return Game1.getLocationFromName(locationName) ?? Game1.getFarm();
    }

    // ── Managed-crop zone flattening + valid-tile count ──────────────────────

    // Collapse touching/co-aligned crop rectangles into the fewest zones (crop mode only).
    private void FlattenCropZones()
    {
        if (!CropMode) return;
        var flattened = ZoneFlattener.Flatten(_completedZones);
        _completedZones.Clear();
        _completedZones.AddRange(flattened);
    }

    // Count tiles inside the drawn zones that are tillable/plantable and not occupied by a sprinkler.
    private void RecomputeCropTileCount() =>
        _validCropTileCount = CropMode ? ManagedCropFieldReader.CountPlantableTiles(_completedZones) : 0;

    // ── Gamepad snapping ─────────────────────────────────────────────────────

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= new List<ClickableComponent>();
        allClickableComponents.Clear();
        allClickableComponents.Add(_cancelBtn);
        allClickableComponents.Add(_clearBtn);
        allClickableComponents.Add(_zoomOutBtn);
        allClickableComponents.Add(_zoomInBtn);
        allClickableComponents.Add(_panBtn);
        allClickableComponents.Add(_doneBtn);
        if (_locationBtn is not null)
            allClickableComponents.Add(_locationBtn);
        foreach (var (btn, _) in _specialBtns)
            allClickableComponents.Add(btn);
    }

    public override void setCurrentlySnappedComponentTo(int id)
    {
        currentlySnappedComponent = getComponentWithID(id);
        snapCursorToCurrentSnappedComponent();
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = _doneBtn;
        snapCursorToCurrentSnappedComponent();
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    public override void draw(SpriteBatch b)
    {
        int w = Game1.uiViewport.Width;

        // Instruction text at top-center with a subtle backdrop for readability
        string hint = I18nHelper.Get(
            _chestMode ? "ui.zone_chest.chest_session_hint"
            : _machineMode ? "ui.manage_machines.draw_hint"
            : _fishPondMode ? "ui.manage_fish_ponds.draw_hint"
            : "ui.zone_chest.session_hint");
        var size = Game1.smallFont.MeasureString(hint);
        var box  = new Rectangle((int)((w - size.X) / 2) - 16, 24, (int)size.X + 32, (int)size.Y + 14);
        b.Draw(Game1.staminaRect, box, Color.Black * 0.55f);
        Utility.drawTextWithShadow(b, hint, Game1.smallFont,
            new Vector2((w - size.X) / 2f, 31), Color.White);

        string scroll = I18nHelper.Get("ui.zone_chest.scroll_hint");
        var scrollSize = Game1.smallFont.MeasureString(scroll);
        Utility.drawTextWithShadow(b, scroll, Game1.smallFont,
            new Vector2((w - scrollSize.X) / 2f, 24 + box.Height + 6), Color.Wheat);

        string zoomHint = I18nHelper.Get("ui.zone_chest.zoom_hint");
        var zoomHintSize = Game1.smallFont.MeasureString(zoomHint);
        Utility.drawTextWithShadow(b, zoomHint, Game1.smallFont,
            new Vector2((w - zoomHintSize.X) / 2f, 24 + box.Height + 6 + scrollSize.Y + 4), Color.Wheat);

        int total = _machineMode
            ? SelectedMachineCount
            : _fishPondMode
                ? SelectedFishPondCount
                : _chestMode
                    ? (_selectedChest is not null || _selectedSpecial is not null ? 1 : 0)
                    : _completedZones.Count + _selectedBuildings.Count;
        DrawButton(b, _cancelBtn,  enabled: true);
        DrawButton(b, _clearBtn,   enabled: total > 0);
        DrawButton(b, _zoomOutBtn, enabled: true);
        DrawButton(b, _zoomInBtn,  enabled: true);
        DrawButton(b, _panBtn,     enabled: true, active: _isPanMode);
        DrawButton(b, _doneBtn,    enabled: true);

        if ((_machineMode || _fishPondMode || _chestMode) && _locationBtn is not null)
        {
            _locationBtn.label = LocationButtonLabel();
            DrawButton(b, _locationBtn, enabled: LocationCount > 1);
        }

        if (_machineMode || _fishPondMode)
        {
            var countText = I18nHelper.Get(
                _fishPondMode ? "ui.manage_fish_ponds.selected_count" : "ui.manage_machines.selected_count",
                new { count = total });
            var countBox = new Rectangle(_locationBtn!.bounds.X, _locationBtn.bounds.Bottom + 8,
                (int)Game1.smallFont.MeasureString(countText).X + 24, 36);
            b.Draw(Game1.staminaRect, countBox, Color.Black * 0.55f);
            Utility.drawTextWithShadow(b, countText, Game1.smallFont,
                new Vector2(countBox.X + 12, countBox.Y + 6), Color.White);
        }
        else if (CropMode)
        {
            // No location switcher in crop mode — anchor the valid-tile count above the toolbar,
            // right-aligned to the Done button's right edge so it never runs off the screen. Reserve
            // width for a 3-digit count so the box doesn't shift as the number grows.
            var countText = I18nHelper.Get("ui.manage_crops.selected_count", new { count = _validCropTileCount });
            var reserveText = I18nHelper.Get("ui.manage_crops.selected_count", new { count = 888 });
            int boxW = (int)Game1.smallFont.MeasureString(reserveText).X + 24;
            int boxX = _doneBtn.bounds.Right - boxW;
            var countBox = new Rectangle(boxX, _doneBtn.bounds.Y - 44, boxW, 36);
            b.Draw(Game1.staminaRect, countBox, Color.Black * 0.55f);
            Utility.drawTextWithShadow(b, countText, Game1.smallFont,
                new Vector2(countBox.X + 12, countBox.Y + 6), Color.White);
        }

        if (_chestMode)
        {
            foreach (var (btn, kind) in _specialBtns)
                DrawButton(b, btn, enabled: true, active: _selectedChest is null && _selectedSpecial == kind);
        }

        drawMouse(b);
    }

    private static void DrawButton(SpriteBatch b, ClickableComponent btn, bool enabled, bool active = false)
    {
        var tint     = active ? Color.LightGreen : (enabled ? Color.White : Color.Gray);
        var textTint = enabled ? Game1.textColor : Color.Gray;
        drawTextureBox(b, btn.bounds.X, btn.bounds.Y, btn.bounds.Width, btn.bounds.Height, tint);
        var textSize = Game1.smallFont.MeasureString(btn.label);
        Utility.drawTextWithShadow(b, btn.label, Game1.smallFont,
            new Vector2(
                btn.bounds.X + (btn.bounds.Width  - (int)textSize.X) / 2,
                btn.bounds.Y + (btn.bounds.Height - (int)textSize.Y) / 2),
            textTint);
    }
}
