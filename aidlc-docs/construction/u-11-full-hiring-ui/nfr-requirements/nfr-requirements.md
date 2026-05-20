# NFR Requirements — U-11 Full Hiring UI: Zones & Chests

## Unit
U-11 — Full Hiring UI: Zones & Chests (ZoneAndChestMenu, ZoneDrawOverlay, ChestResolver)

**Depth**: Minimal — all NFRs derived from approved requirements.md; no clarifying questions needed.

---

## Applicable NFRs

### NFR-PERF-01 — Frame Budget for draw()
**Requirement**: The hiring UI's per-frame hooks must not introduce visible frame drops. Stardew targets 60fps.

**How it applies to U-11**:
- `ZoneAndChestMenu.draw(SpriteBatch b)` runs every frame while the menu is open. It renders task-output destination rows, the building-chest dropdown panel, and navigation buttons.
- `ZoneDrawOverlay` hooks `Display.RenderedWorld`. Its handler runs every render frame while zone-draw mode is active, rendering the zone tile highlights on the farm map.
- **Rule**: No computation inside either `draw()` or the `RenderedWorld` handler. Zone rectangles, chest lists, dropdown items, and hover states are pre-computed when state changes (zone finalized, chest selected, dropdown opened) and stored as fields. Rendering reads those fields only.
- Chest dropdown population: `ChestResolver.GetAllChests(farm)` is called **once** when the menu opens (or when the player clicks "Select Building"), result cached in `_chestList`. Not called per frame.
- Zone tile highlight set: computed once when a zone drag completes (`_completedZones` list), stored as `List<Rectangle>`. The overlay iterates this list each frame but performs no Game1 queries.

**Enforcement**: `draw()` and `RenderedWorld` handlers must contain only sprite/text rendering calls reading from pre-computed fields. No `ChestResolver`, `IZoneGeometry`, or `Game1.getFarm()` calls in these methods.

---

### NFR-PERF-03 — Zone Overlay Rendering Responsiveness
**Requirement**: The hiring UI's zone overlay rendering must remain responsive for zones up to the size of the full Standard Farm map (~80×65 = 5,200 tiles).

**How it applies to U-11**:
- The `ZoneDrawOverlay.OnRenderedWorld` handler highlights all tiles in all completed zones plus the in-progress drag rectangle.
- For a full-farm zone (worst case), this is up to 5,200 tile rectangles drawn per frame.
- **Rule**: Render zone areas as filled rectangles in world space rather than iterating individual tile draws. Each `Zone` is stored as a `Rectangle` bounding box (`Zone.Bounds`). The overlay draws each zone as a single `SpriteBatch.Draw(pixel, screenRect, color)` call (semi-transparent fill). This makes rendering cost O(zone count) not O(tile count).
- Tile count is therefore irrelevant to rendering performance — only the number of distinct zones matters. The spec allows multiple zones per contract but in practice players will draw a handful.
- The in-progress drag preview (before mouse release) is also a single rectangle draw.
- **Coordinate transform**: World tile coordinates → screen pixel coordinates use `Game1.GlobalToLocal(Game1.viewport, tileRect)` or equivalent (`Game1.uiViewport` for UI-mode menus). The transform is applied once per zone rectangle, not per tile.

**Enforcement**: `ZoneDrawOverlay` must never iterate tile-by-tile in its render path. Zone areas are stored as `Rectangle` (bounding box) and drawn as a single filled sprite call each.

---

### NFR-UX-01 — Full Gamepad Navigation
**Requirement**: The hiring UI is fully navigable with mouse/keyboard and gamepad (FR-HIRE-03, Q24).

**How it applies to U-11**:
- `ZoneAndChestMenu` must override `receiveGamePadButton(Buttons b)` per SMAPI IClickableMenu contract.
- **Standard bindings**: `Buttons.B` → back to TaskSelectionMenu; `Buttons.A` → confirm current component; D-pad → move between clickable components.
- **Zone drawing with gamepad**: Stardew Valley renders a virtual gamepad cursor when the controller is in use (moved with the left analog stick). Zone drawing interacts with this cursor exactly as it does with the mouse — `receiveLeftClick` is called by SMAPI when the player presses A with the virtual cursor positioned on the farm map. Click-and-drag uses `receiveLeftClick` (start) and `leftClickHeld` (drag) — the gamepad cursor supports these. No separate gamepad code path is needed for zone drawing itself.
- **Dropdown navigation**: The building-chest dropdown panel must expose its items as `ClickableComponent` entries so D-pad snaps to each row.
- **Focus on open**: `currentlySnappedComponent` set to the first interactive element (Draw Zone button) when `ZoneAndChestMenu` opens.
- `populateClickableComponentList()` must be overridden to list all clickable buttons and dropdown items so `setCurrentlySnappedComponentTo` can restore focus after overlay returns.

**Enforcement**: Play-test with controller before marking U-11 Definition of Done. All interactive elements in ZoneAndChestMenu reachable without mouse.

---

### NFR-UX-02 — i18n Routing
**Requirement**: All user-visible strings routed through SMAPI's i18n system (FR-CFG-02, Q23).

**How it applies to U-11**:
- All text rendered in `ZoneAndChestMenu.draw()` must come from `I18nHelper.Get(key)`.
- New i18n keys added in this unit (added to `i18n/default.json`):
  - `ui.zone_chest.title` — "Zone & Output"
  - `ui.zone_chest.draw_zone_btn` — "Draw Zone"
  - `ui.zone_chest.clear_zones_btn` — "Clear Zones"
  - `ui.zone_chest.select_building_btn` — "Select Building"
  - `ui.zone_chest.set_output_btn` — "Set Output Chest"
  - `ui.zone_chest.zone_count_label` — "Zones: {count}"
  - `ui.zone_chest.no_zones_hint` — "Click and drag on the farm to draw a zone"
  - `ui.zone_chest.output_section_label` — "Output Destinations"
  - `ui.zone_chest.task_output_row` — "{taskName}: {chestName}"
  - `ui.zone_chest.no_chest_assigned` — "Mail next morning (no chest)"
  - `ui.zone_chest.chest_fallback_name` — "{buildingName} — Chest at {x}, {y}"
  - `ui.zone_chest.confirm_btn` — "Next"
  - `ui.zone_chest.back_btn` — "Back"
  - `ui.zone_chest.dropdown_title` — "{buildingName} chests"
  - `ui.zone_chest.shipping_bin_option` — "Shipping Bin"

**Enforcement**: No hardcoded user-visible strings in any Mod-layer file for this unit. U-16 i18n lint test will catch regressions.

---

### NFR-UX-03 — In-Place Zone Draw Mode
**Requirement**: Hiring UI does not require the player to leave the bulletin board to configure zones — zone draw mode overlays the farm map and returns to Screen 2 on completion (NFR-UX-03).

**How it applies to U-11**:
- When the player clicks "Draw Zone" in `ZoneAndChestMenu`, the menu does **not** close. Instead, `ZoneDrawOverlay` activates and renders on top of the farm map via the `Display.RenderedWorld` event.
- The bulletin board is still the active context in-game; the overlay is purely visual, rendered on top of the world.
- Mouse input during overlay mode: `ZoneAndChestMenu.receiveLeftClick` / `leftClickHeld` / `releaseLeftClick` are repurposed to handle zone drawing when `_inZoneDrawMode` is true. Input that doesn't hit the farm map is ignored.
- On drag release: the drawn rectangle is appended to `_completedZones`, `_inZoneDrawMode` flips back to false, and the ZoneAndChestMenu re-renders its zone count label.
- "Clear Zones" button resets `_completedZones` to empty.
- Building selection (FR-HIRE-05): clicking a building outline on the farm map (in a separate "_inBuildingSelectMode" that works the same way) adds the building's indoor location to the zone list. Building outlines are pre-fetched from `Game1.getFarm().buildings` once on menu open.

**Enforcement**: `ZoneAndChestMenu` must not call `Game1.activeClickableMenu = this` or push/pop menu stack when entering/exiting zone draw mode — the menu stays active throughout. The `ZoneDrawOverlay` is a stateless renderer attached to the event, activated/deactivated by toggling its subscription or a boolean flag.

---

### NFR-MAINT-03 — SMAPI Integration Separation
**Requirement**: Pure business-logic modules are separated from SMAPI/game-engine integration so they can be unit-tested without launching Stardew.

**How it applies to U-11**:
- `ChestResolver` lives in `Dayswork/Integration/` (Mod layer) — it may reference `Game1`, `GameLocation`, and `StardewValley.Objects.Chest`. This is correct: chest resolution is an integration concern, not a Core domain concern.
- `ZoneAndChestMenu` and `ZoneDrawOverlay` also live in the Mod layer and may reference SMAPI/SV types freely.
- `ZoneAndChestMenu` depends on `ChestResolver` injected via constructor. No `new ChestResolver()` inside the menu.
- `HiringFlowCoordinator` (extended this unit) constructs `ZoneAndChestMenu` and passes the injected `ChestResolver` instance.
- `Dayswork.Core` gains no new files from U-11 — the existing `Zone`, `ChestRef`, `IZoneGeometry` are reused as-is.

**Enforcement**: `Dayswork.Core.csproj` must still have zero SMAPI/SV references after U-11 (build-verified). `ChestResolver` is injected, not newed inside menus.

---

### NFR-ONBOARD-01 — Just-In-Time Docs
**Requirement**: C# / SMAPI concepts explained just-in-time during Construction, embedded in Code Generation plans.

**How it applies to U-11**:
The Code Generation plan for U-11 must include brief explanations of:

1. **Display.RenderedWorld**: `helper.Events.Display.RenderedWorld += OnRenderedWorld`. Fires after the world (farm map, characters, objects) is drawn but before UI overlays. Handler signature: `void OnRenderedWorld(object sender, RenderedWorldEventArgs e)` — `e.SpriteBatch` is the active SpriteBatch, already begun. You draw on it directly; no Begin/End needed.

2. **World vs. screen coordinates**: Farm tiles are in "world space" (`Vector2 * Game1.tileSize`). To draw on screen, convert: `Utility.ModifyCoordinatesForUIScale(new Vector2(tileX * 64, tileY * 64))` gives the screen-space pixel position accounting for zoom. Alternatively, access the screen rectangle directly via `Game1.GlobalToLocal(Game1.viewport, worldRect)`. For menu-mode (where the UI viewport differs from the world viewport), use `Game1.uiViewport` for overlays drawn during `RenderedWorld`.

3. **leftClickHeld and releaseLeftClick**: Beyond `receiveLeftClick`, `IClickableMenu` has `leftClickHeld(int x, int y)` (called each frame the mouse button is held after the initial click) and `releaseLeftClick(int x, int y)` (called when the mouse button is released). Override both to implement click-drag rectangle selection.

4. **ClickableComponent for chest dropdown items**: Each chest row in the dropdown is a `ClickableComponent` added to `allClickableComponents`. Set `myID`, `upNeighborID`, `downNeighborID` to form the D-pad navigation chain. `setCurrentlySnappedComponentTo(id)` restores focus after overlay mode exits.

5. **SMAPI `Display.RenderedWorld` subscription lifecycle**: Subscribe in the menu constructor; unsubscribe in `cleanupBeforeExit()` (called when the menu closes). Failing to unsubscribe leaves a dangling event handler that keeps rendering after the menu closes.

These are embedded in the Code Generation plan step comments, not as separate doc files.

---

## N/A NFRs

| NFR | Rationale |
|---|---|
| NFR-SAFE-01 | No items collected or moved in this unit (hire-time UI only) |
| NFR-SAFE-02 | No gold transactions in this unit |
| NFR-SAFE-03 | No new serialization; Zone/ChestRef types persist via existing ContractStore path |
| NFR-SAFE-04 | No item pickup by worker |
| NFR-PERF-02 | No tile scanning for task queue; tile scanning for hours estimation is SummaryMenu (U-09) |
| NFR-MAINT-01 | xUnit project established in U-02 |
| NFR-MAINT-02 | FsCheck established; no new PBT obligations this unit |
| NFR-MAINT-04 | No new Harmony patches |
| NFR-MAINT-05 | `dotnet format` applies always; no design decisions |
| NFR-COMPAT-01 | Compatibility docs — README concern |
| NFR-COMPAT-02 | Farm-type support — runtime concern; zone drawing works on any farm |
| NFR-COMPAT-03 | Multiplayer guard established in U-08 |
| NFR-COMPAT-04 | No new required dependencies |
| Security Baseline | Disabled project-wide (Q28) |

---

## PBT Extension Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-02 (round-trip) | N/A | No new serialized types; Zone and ChestRef round-trips covered in U-04/U-06 |
| PBT-03 (invariants) | N/A | No new domain invariants; zone geometry invariants covered in U-04 |
| PBT-07 (generator quality) | N/A | No new FsCheck generators needed |
| PBT-08 (shrinking/seed logging) | N/A | No PBT tests in this unit |
| PBT-09 (framework = FsCheck) | Already decided | No new framework decision |

**Note**: U-11 components (ZoneAndChestMenu, ZoneDrawOverlay, ChestResolver) require a running Stardew instance to test meaningfully — they are play-tested per the unit Definition of Done. The Core geometry (ZoneGeometry, Zone.Bounds intersection) tested in U-04 covers the domain logic invoked by zone drawing.
