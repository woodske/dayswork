# Code Generation Plan — U-11 Full Hiring UI: Zones & Chests

## Unit Context

**Unit**: U-11 — Full Hiring UI: Zones & Chests  
**Stories**: S-03 (full zone drawing + building selection + unreachable-tile silent skip), S-04 (full chest assignment including building-interior dropdown + orphaned-chest fallback)  
**Components owned**: M-05 ZoneAndChestMenu, M-08 ZoneDrawOverlay, M-20 ChestResolver  
**Components extended**: M-03 HiringFlowCoordinator (insert ZoneAndChestMenu in flow), M-01 ModEntry (wire ChestResolver singleton)

**Dependencies (all satisfied)**:
- `Zone`, `TileCoord`, `ChestRef`, `TaskKind`, `DestinationKey` hierarchy — U-04, U-04, U-04, U-04, U-04
- `ContractDraft.Zones`, `ContractDraft.Destinations` stubs — U-09 (already declared, populated here)
- `IModHelper`, SMAPI `Display.RenderedWorld` event — U-01
- `I18nHelper` — U-08

---

## JIT Docs (C# / SMAPI concepts introduced in this unit)

### 1. `Display.RenderedWorld` event
Fires after the world (farm map, objects, NPCs) is drawn but before the UI layer. The event's `SpriteBatch` is already begun by the game; call `Draw` on it directly — do **not** call `Begin`/`End`. Subscribe in the menu constructor; **unsubscribe in `cleanupBeforeExit()`** to prevent dangling handlers after the menu closes.

### 2. World → screen coordinate transform
`Game1.viewport` is the camera's top-left corner in **world pixels** (`xTile.Dimensions.Rectangle`). Farm tiles are 64 world pixels each (`Game1.tileSize`). To convert a farm tile position to a screen rectangle for `SpriteBatch.Draw`:
```csharp
int screenX = tileX * Game1.tileSize - Game1.viewport.X;
int screenY = tileY * Game1.tileSize - Game1.viewport.Y;
```
No manual zoom correction is needed in `RenderedWorld` — the SpriteBatch matrix handles zoom automatically during world rendering.

### 3. Screen → tile coordinate conversion (for `receiveLeftClick`)
`IClickableMenu.receiveLeftClick(x, y)` passes coordinates in the menu system's virtual coordinate space. During zone-draw mode these need converting to farm tiles:
```csharp
int tileX = (x + Game1.viewport.X) / Game1.tileSize;
int tileY = (y + Game1.viewport.Y) / Game1.tileSize;
```
**Play-test note**: If drawn zone corners feel off by a tile, double-check whether zoom scale needs applying: `(int)(x / Game1.options.zoomLevel + Game1.viewport.X / Game1.tileSize)`.

### 4. `leftClickHeld` and `releaseLeftClick`
Beyond `receiveLeftClick`, `IClickableMenu` has `leftClickHeld(int x, int y)` (called every frame the mouse button is held after the initial click) and `releaseLeftClick(int x, int y)` (called when released). Override both for click-drag rectangle selection.

### 5. `cleanupBeforeExit()`
Called automatically by SMAPI when `Game1.activeClickableMenu` is replaced. The override **must** call `base.cleanupBeforeExit()`. This is the correct place to unsubscribe events and dispose unmanaged resources (e.g., `Texture2D`).

### 6. `Game1.getFarm()` returns `Farm`
`Farm` extends `GameLocation`. `Farm.buildings` is a `NetList<Building>`. `building.indoors.Value` is the indoor `GameLocation` (null for decorative buildings). `Farm.Objects` and `GameLocation.Objects` are `OverlaidDictionary<Vector2, SObject>` — iterate with `.Pairs`.

---

## Steps

### Step 1 — UI DTOs: ChestEntry + BuildingOutline records
- [x] Create `Dayswork/UI/ChestEntry.cs`

```csharp
using Dayswork.Core.Domain;
using Microsoft.Xna.Framework;

namespace Dayswork.UI;

// UI-only DTO: a resolved chest entry for display in ZoneAndChestMenu.
// Never persisted — the ChestRef inside is what goes into ContractDraft.Destinations.
internal sealed record ChestEntry(ChestRef Ref, string DisplayName, string GroupLabel);

// UI-only DTO: a building's tile footprint on the farm, used by ZoneDrawOverlay + building-select mode.
internal sealed record BuildingOutline(string LocationName, Rectangle TileBounds, string DisplayName);
```

---

### Step 2 — M-20: ChestResolver
- [x] Create `Dayswork/Integration/ChestResolver.cs`

Key implementation notes:
- Constructor takes `IModHelper helper` (stored for future use; current methods are stateless queries against live `Game1` state).
- `GetAllChests(GameLocation farm)`: iterates `farm.Objects.Pairs` for open-farm `Chest` objects; if `farm` is `Farm`, also iterates `farm.buildings` → `building.indoors.Value?.Objects.Pairs` for building-interior chests. Returns `List<ChestEntry>` grouped by "Farm" (open chests) or `building.buildingType.Value` (building name).
- `ResolveChest(ChestRef chestRef)`: calls `Game1.getLocationFromName(chestRef.LocationName)` then checks `location.Objects` at tile coordinate; returns null if not found or not a `Chest`.
- `GetDisplayName(Chest chest, GameLocation location, int tileX, int tileY)`: returns `chest.Name` if non-null, non-empty, and not the default `"Chest"` string; otherwise returns `I18nHelper.Get("ui.zone_chest.chest_fallback_name", new { buildingName = location.Name, x = tileX, y = tileY })`.
- `GetBuildingOutlines(Farm farm)`: iterates `farm.buildings`; for each where `building.indoors.Value != null`, creates `BuildingOutline(indoors.Name, new Rectangle(building.tileX.Value, building.tileY.Value, building.tilesWide.Value, building.tilesHigh.Value), building.buildingType.Value)`.

Usings: `Dayswork.Core.Domain`, `Dayswork.UI`, `Microsoft.Xna.Framework`, `StardewModdingAPI`, `StardewValley`, `StardewValley.Objects`.

---

### Step 3 — M-08: ZoneDrawOverlay
- [x] Create `Dayswork/UI/ZoneDrawOverlay.cs`

Key implementation notes:
- Constructor: `ZoneDrawOverlay(ZoneAndChestMenu menu, GraphicsDevice graphicsDevice)`. Creates a 1×1 white `Texture2D _pixelTexture` with `SetData(new[] { Color.White })`.
- `OnRenderedWorld(object? sender, RenderedWorldEventArgs e)`:
  - For each zone in `_menu.CompletedZones`: compute screen rect (`tileX * 64 - viewport.X`, `tileY * 64 - viewport.Y`, width in pixels, height in pixels); draw with `e.SpriteBatch.Draw(_pixelTexture, screenRect, Color.LightBlue * 0.4f)`.
  - For each outline in `_menu.SelectedBuildings`: draw yellow fill `Color.Yellow * 0.35f` over the building footprint.
  - If `_menu.IsInZoneDrawMode && _menu.DragStart.HasValue`: compute a preview rect from `DragStart` + `DragCurrent`; draw `Color.White * 0.25f`.
- `Dispose()`: dispose `_pixelTexture`. Guard against double-dispose.
- Helper `DrawZoneFill(SpriteBatch sb, TileCoord topLeft, TileCoord bottomRight, Color color)`: computes the `Rectangle` and calls `sb.Draw`.

Usings: `Dayswork.Core.Domain`, `Microsoft.Xna.Framework`, `Microsoft.Xna.Framework.Graphics`, `StardewModdingAPI.Events`, `StardewValley`.

---

### Step 4 — M-05: ZoneAndChestMenu
- [x] Create `Dayswork/UI/ZoneAndChestMenu.cs`

This is the main new file. Key structure:

**Constants / static data**:
```csharp
private const int MenuWidth = 700, MenuHeight = 700;

// Tasks that produce assignable output (excludes WaterCrops, FeedAnimals, PetAnimals, ClearGrass)
private static readonly TaskKind[] OutputTasks = {
    TaskKind.HarvestCrops, TaskKind.CollectFruit, TaskKind.CollectAnimalProducts,
    TaskKind.CutTrees, TaskKind.ClearRocks, TaskKind.ClearWeeds,
};
// These three can also target the Shipping Bin (FR-TASK-02)
private static readonly HashSet<TaskKind> ShippingBinEligible = new() {
    TaskKind.HarvestCrops, TaskKind.CollectFruit, TaskKind.CollectAnimalProducts,
};
```

**Constructor `ZoneAndChestMenu(ContractDraft draft, ChestResolver chestResolver, IModHelper helper, Action<ContractDraft> onAdvance, Action<ContractDraft> onBack)`**:
- Centers menu via `Utility.getTopLeftPositionForCenteringOnScreen`.
- Calls `chestResolver.GetAllChests(Game1.getFarm())` → `_chestList`.
- Calls `chestResolver.GetBuildingOutlines(Game1.getFarm())` → `_buildingOutlines`.
- Initializes `_completedZones` from `draft.Zones` (restores state on back-navigation).
- Initializes `_outputAssignments` from `draft.Destinations`.
- Creates `_overlay = new ZoneDrawOverlay(this, Game1.graphics.GraphicsDevice)`.
- Subscribes `helper.Events.Display.RenderedWorld += _overlay.OnRenderedWorld`.
- Calls `BuildComponents()` and `populateClickableComponentList()`.

**Internal state exposed to ZoneDrawOverlay** (all readonly properties):
```csharp
internal IReadOnlyList<Zone> CompletedZones => _completedZones;
internal IReadOnlyList<BuildingOutline> SelectedBuildings => _selectedBuildings;
internal bool IsInZoneDrawMode => _inZoneDrawMode;
internal TileCoord? DragStart => _inZoneDrawMode ? _dragStart : null;
internal TileCoord DragCurrent => _dragCurrent;
```

**BuildComponents()**: Creates `ClickableComponent` instances for:
- `_drawZoneBtn` (myID=100), `_clearZonesBtn` (myID=101), `_selectBuildingBtn` (myID=102)
- `_setOutputBtns` (myID=200+i) — one per enabled output task in `OutputTasks` order
- `_confirmBtn` (myID=900), `_backBtn` (myID=901)
- `_exitDrawModeBtn` — rendered in mini-banner during draw/building-select modes; positioned at bottom center of screen

**cleanupBeforeExit()**:
```csharp
protected override void cleanupBeforeExit()
{
    _helper.Events.Display.RenderedWorld -= _overlay.OnRenderedWorld;
    _overlay.Dispose();
    base.cleanupBeforeExit();
}
```

**receiveLeftClick(int x, int y, bool playSound)**:
- If `_showingPicker`: check picker option components; if click in picker, apply selection and close.
- If `_inZoneDrawMode`: record `_dragStart = ScreenToTile(x, y)`.
- If `_inBuildingSelectMode`: detect clicked building outline (iterate `_buildingOutlines`, check tile-click hit); toggle building in `_selectedBuildings`, exit select mode.
- Normal mode: check `_drawZoneBtn`, `_clearZonesBtn`, `_selectBuildingBtn`, each `_setOutputBtns[i]`, `_confirmBtn`, `_backBtn`, `_exitDrawModeBtn`.

**leftClickHeld(int x, int y)**: if `_inZoneDrawMode`, update `_dragCurrent = ScreenToTile(x, y)`.

**releaseLeftClick(int x, int y)**: if `_inZoneDrawMode && _dragStart.HasValue`, call `FinalizeZone(_dragStart.Value, ScreenToTile(x, y))`; set `_inZoneDrawMode = false`; `_dragStart = null`.

**FinalizeZone(TileCoord start, TileCoord end)**:
- Compute normalized bounds: `topLeft = (Min(start.X, end.X), Min(start.Y, end.Y))`, `bottomRight = (Max(start.X, end.X), Max(start.Y, end.Y))`.
- Guard: if `topLeft == bottomRight`, zero-size click — skip.
- Add `new Zone("Farm", topLeft, bottomRight)` to `_completedZones`.
- `draft.Zones.Clear(); draft.Zones.AddRange(_completedZones)` — keep draft in sync.

**OpenPicker(TaskKind task)**:
- Set `_showingPicker = true`, `_pickerTask = task`.
- Build `_pickerOptions`: start with `("Mail (no chest)", null)` for MailDestination fallback; if ShippingBinEligible: add `("Shipping Bin", ShippingBinDestination.Instance)`; then for each entry in `_chestList`: add `(entry.DisplayName, new ChestDestination(entry.Ref))` with group labels as separators.
- Build `_pickerOptionComponents` as ClickableComponent list (myID=600+i, positioned in a 300×400 popup panel anchored near the Set button).

**ApplyPickerSelection(DestinationKey? dest)**:
- `_outputAssignments[_pickerTask] = dest`;
- `draft.Destinations[_pickerTask] = dest ?? MailDestination.Instance`;
- `_showingPicker = false`.

**ConfirmAndAdvance()**:
- Sync all building zones: for each entry in `_selectedBuildings`, if not already in `_completedZones`, add `new Zone(outline.LocationName, TileCoord(0,0), TileCoord(int.MaxValue, int.MaxValue))` as an "entire building" zone.
  - **Simpler approach**: just add building zones as-is; shift orchestrator ignores unreachable tiles anyway.
- `draft.Zones.Clear(); draft.Zones.AddRange(_completedZones);` (already synced in FinalizeZone, but ensure building zones added).
- `_onAdvance(_draft)`.

**ScreenToTile(int x, int y)** (private static helper):
```csharp
private static TileCoord ScreenToTile(int x, int y) =>
    new TileCoord(
        (x + Game1.viewport.X) / Game1.tileSize,
        (y + Game1.viewport.Y) / Game1.tileSize);
```

**draw(SpriteBatch b)**:
- If `_inZoneDrawMode || _inBuildingSelectMode`: render mini-banner at bottom center (semi-transparent dark rectangle, instruction text, "Done Drawing" / "Click a building" text, `_exitDrawModeBtn`); return early.
- If `_showingPicker`: render full panel first, then render picker popup panel on top (drawn as a raised texture box with picker options).
- Normal mode: render full panel.
  - Title: `I18nHelper.Get("ui.zone_chest.title")`
  - Zone section: zone count label, `_drawZoneBtn`, `_clearZonesBtn`, `_selectBuildingBtn`
  - Separator line
  - Output section title: `I18nHelper.Get("ui.zone_chest.output_section_label")`
  - If no enabled output tasks: `I18nHelper.Get("ui.zone_chest.no_output_tasks")`
  - Else: one row per enabled output task showing task name + current destination label + Set button
  - Bottom: `_backBtn`, `_confirmBtn`
- Always: `drawMouse(b)`.

**Destination label helper**: maps `DestinationKey?` → display string:
- `null` or `MailDestination`: `I18nHelper.Get("ui.zone_chest.no_chest_assigned")`
- `ShippingBinDestination`: `I18nHelper.Get("ui.zone_chest.shipping_bin_option")`
- `ChestDestination(chestRef)`: look up `_chestList.FirstOrDefault(e => e.Ref == chestRef)?.DisplayName ?? chestRef.ToString()`

**populateClickableComponentList()**: adds `_drawZoneBtn`, `_clearZonesBtn`, `_selectBuildingBtn`, all `_setOutputBtns`, `_confirmBtn`, `_backBtn`.

**setCurrentlySnappedComponentTo(int id)**: standard implementation (same as TaskSelectionMenu).

**receiveGamePadButton(Buttons b)**: `Buttons.B` → `_onBack(_draft)` (if not in draw/building mode, where it should exit draw mode instead).

---

### Step 5 — Extend M-03: HiringFlowCoordinator
- [x] Modify `Dayswork/UI/HiringFlowCoordinator.cs`

Changes:
1. Add `private readonly ChestResolver _chestResolver;` field.
2. Add `ChestResolver chestResolver` parameter to constructor; assign `_chestResolver = chestResolver;`.
3. Add `private readonly IModHelper _helper;` field + `IModHelper helper` constructor parameter.
4. Change `ShowTaskSelection`'s `onAdvance` from `d => ShowSummary(d)` to `d => ShowZoneAndChest(d)`.
5. Add new private method `ShowZoneAndChest(ContractDraft draft)`:
```csharp
private void ShowZoneAndChest(ContractDraft draft)
{
    Game1.activeClickableMenu = new ZoneAndChestMenu(
        draft, _chestResolver, _helper,
        onAdvance: d => ShowSummary(d),
        onBack:    d => ShowTaskSelection(d));
}
```
6. Update `ShowSummary`'s `onBack` from `d => ShowTaskSelection(d)` to `d => ShowZoneAndChest(d)`.
7. Remove the `WholeFarmZone` comment that says "Replaced by actual drawn zones in U-11" — keep the field as the BuildContract fallback.
8. Add `using StardewModdingAPI;` if not already present.

---

### Step 6 — Extend M-01: ModEntry
- [x] Modify `Dayswork/ModEntry.cs`

Changes:
1. Add `var chestResolver = new ChestResolver(Helper);` after the `store` / `serializer` lines.
2. Update `Coordinator` construction: `Coordinator = new HiringFlowCoordinator(rateCalc, depositCalc, hoursEst, config, store, chestResolver, Helper);`
3. Add `using Dayswork.Integration;` if not already present (it is already; just confirming).

---

### Step 7 — i18n keys
- [x] Modify `Dayswork/i18n/default.json`

Add after the `ui.error.cant_afford` entry:
```json
"ui.zone_chest.title": "Zone & Output",
"ui.zone_chest.draw_zone_btn": "Draw Zone",
"ui.zone_chest.clear_zones_btn": "Clear Zones",
"ui.zone_chest.select_building_btn": "Select Building",
"ui.zone_chest.done_drawing_btn": "Done Drawing",
"ui.zone_chest.zone_count_label": "Zones drawn: {{count}}",
"ui.zone_chest.no_zones_hint": "(no zones — whole farm default)",
"ui.zone_chest.output_section_label": "Output Destinations",
"ui.zone_chest.no_output_tasks": "(no output tasks selected)",
"ui.zone_chest.set_output_btn": "Set",
"ui.zone_chest.no_chest_assigned": "Mail next morning",
"ui.zone_chest.shipping_bin_option": "Shipping Bin",
"ui.zone_chest.chest_fallback_name": "{{buildingName}} — Chest at {{x}}, {{y}}",
"ui.zone_chest.confirm_btn": "Next",
"ui.zone_chest.back_btn": "Back",
"ui.zone_chest.group_farm": "Farm",
"ui.zone_chest.picker_mail_option": "Mail (no chest)",
"ui.zone_chest.draw_mode_hint": "Click and drag on the farm to draw a zone",
"ui.zone_chest.building_select_hint": "Click a building to include it"
```

---

### Step 8 — dotnet build + fix errors
- [x] Run `dotnet build` from `C:\Users\kwood\Repos\dayswork`
- [x] Fix all errors (common expected issues listed below); re-run until 0 errors 0 warnings

**Common expected build issues**:
- `ChestResolver` namespace/using missing in HiringFlowCoordinator → add `using Dayswork.Integration;` (already present in ModEntry; ensure coordinator has it)
- `Farm` cast required in `GetAllChests`: parameter is `GameLocation`; cast to `Farm` with `if (farm is Farm f)` for buildings
- `building.tileX.Value` vs `building.tileX` — in SV 1.6, `tileX` is a `NetInt`, use `.Value`
- `DestinationKey?` nullable in `_outputAssignments`: ensure `Dictionary<TaskKind, DestinationKey?>` uses nullable reference type syntax or non-nullable `DestinationKey` (use `MailDestination.Instance` instead of `null` if nullable causes issues)
- `RenderedWorldEventArgs` — in SMAPI 4.x, verify exact namespace: `StardewModdingAPI.Events`
- `Game1.graphics.GraphicsDevice` — if null reference at menu construction time, use `Game1.game1.GraphicsDevice` instead
- `chest.Name` default: in SV 1.6, unnamed chests return `"Chest"` not empty string — the `GetDisplayName` fallback check must handle this
- `Utility.getTopLeftPositionForCenteringOnScreen` returns `Vector2` — cast to `int` for `xPositionOnScreen` / `yPositionOnScreen`
- `allClickableComponents` may be null at BuildComponents time — guard with `??= new List<>()`

---

### Step 9 — Code summary + state update
- [x] Create `aidlc-docs/construction/u-11-full-hiring-ui/code/code-summary.md` (see format in u-10 summary)
- [x] Update `aidlc-docs/aidlc-state.md` current stage to "U-11 Code Generation Complete"
- [x] Update `aidlc-docs/audit.md` with completion entry

---

## Story Traceability

| Story | Step(s) | Status |
|---|---|---|
| S-03 (zone drawing + building selection + unreachable-tile silent skip) | Steps 2, 3, 4, 5 | [x] |
| S-04 (chest assignment + orphaned-chest fallback) | Steps 1, 2, 3, 4, 5 | [x] |

## Definition of Done
Hire a one-time contract: draw 2 zones, select a chest for Harvest Crops, leave Weeds on mail. Next morning the worker harvests crops within the drawn zones and deposits them in the designated chest; weed fiber arrives by mail next morning.
