using Dayswork.Core.Domain;
using Dayswork.Integration;
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
    private readonly Action<List<Zone>, List<BuildingOutline>> _onComplete;
    private readonly Action                _onCancel;

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
    private readonly string _targetLocationName;
    private readonly List<Zone> _protectedZones = new();

    IReadOnlyList<Zone>            IZoneDrawSource.CompletedZones    => _completedZones;
    IReadOnlyList<Zone>            IZoneDrawSource.ProtectedZones    => _protectedZones;
    IReadOnlyList<BuildingOutline> IZoneDrawSource.SelectedBuildings => _selectedBuildings;
    bool       IZoneDrawSource.IsInZoneDrawMode => true;          // grid always visible during the session
    TileCoord? IZoneDrawSource.DragStart        => _dragStart;
    TileCoord  IZoneDrawSource.DragCurrent      => _dragCurrent;
    Color      IZoneDrawSource.ZoneFillColor    => _zoneFillColor;
    Color      IZoneDrawSource.ProtectedZoneFillColor => _protectedZoneFillColor;

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

        // Restore prior selections (for the active layer only) so navigating back preserves work.
        _completedZones.AddRange((initialZones ?? draft.OutdoorZones)
            .Where(zone => string.Equals(zone.LocationName, _targetLocationName, StringComparison.Ordinal)));

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
        _doneBtn.bounds.Contains(x, y);

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
            _completedZones.Clear();
            _selectedBuildings.Clear();
            Game1.playSound("trashcan");
            return;
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
                Game1.playSound("coin");
            }

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
        var zones     = new List<Zone>(_completedZones);
        var buildings = new List<BuildingOutline>(_selectedBuildings);
        exitThisMenu(false);
        _onComplete(zones, buildings);
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
        string hint = I18nHelper.Get("ui.zone_chest.session_hint");
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

        int total = _completedZones.Count + _selectedBuildings.Count;
        DrawButton(b, _cancelBtn,  enabled: true);
        DrawButton(b, _clearBtn,   enabled: total > 0);
        DrawButton(b, _zoomOutBtn, enabled: true);
        DrawButton(b, _zoomInBtn,  enabled: true);
        DrawButton(b, _panBtn,     enabled: true, active: _isPanMode);
        DrawButton(b, _doneBtn,    enabled: true);

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
