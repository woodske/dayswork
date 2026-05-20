# NFR Design Patterns — U-11 Full Hiring UI: Zones & Chests

## Pattern 1: Pre-Compute on State Change
**Addresses**: NFR-PERF-01 (draw() frame budget)

**Problem**: `ZoneAndChestMenu.draw()` and `ZoneDrawOverlay.OnRenderedWorld` run every frame. Computing chest lists, zone screen rectangles, or output assignment strings in the render path would waste CPU and could cause frame drops.

**Solution**: All data needed for rendering is computed once when state changes and stored in fields. The render path reads fields only — no game queries.

**State change triggers and their cached fields**:

| Trigger | Action | Fields Updated |
|---|---|---|
| Menu opens | Enumerate farm chests + building outlines | `_chestList`, `_buildingOutlines` |
| Zone drag completes | Convert tile rectangle to Zone; compute screen bounds | `_completedZones`, `_zoneScreenRects` |
| Chest assigned to task | Update output map, format display string | `_outputAssignments`, `_outputDisplayLines` |
| Building selected | Mark building as included, add its location to zones | `_selectedBuildingLocations` |
| "Clear Zones" pressed | Reset zone lists | `_completedZones`, `_zoneScreenRects` |

**Invariant**: `draw()` and `OnRenderedWorld` contain zero calls to `ChestResolver`, `IZoneGeometry`, `Game1.getFarm()`, or any game-state query.

---

## Pattern 2: Rectangle-Fill Overlay
**Addresses**: NFR-PERF-03 (zone overlay rendering responsiveness at full farm scale)

**Problem**: Highlighting all tiles in a large zone tile-by-tile means up to 5,200 individual `SpriteBatch.Draw` calls per frame for a full-farm zone.

**Solution**: Each `Zone` is stored as a bounding `Rectangle` in world space. The overlay renders one filled rectangle per zone using a shared 1×1 white pixel texture. Cost = O(zone count), independent of tile count.

**Implementation**:
```csharp
// ZoneDrawOverlay holds a 1×1 pixel texture created once:
private Texture2D _pixelTexture;

// ZoneDrawOverlay constructor:
_pixelTexture = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
_pixelTexture.SetData(new[] { Color.White });

// OnRenderedWorld handler — per completed zone:
foreach (var zone in _menu.CompletedZones)
{
    var worldRect = new Rectangle(
        zone.Bounds.X * Game1.tileSize,
        zone.Bounds.Y * Game1.tileSize,
        zone.Bounds.Width * Game1.tileSize,
        zone.Bounds.Height * Game1.tileSize);
    var screenRect = Game1.GlobalToLocal(Game1.viewport, worldRect);
    e.SpriteBatch.Draw(_pixelTexture, screenRect, Color.LightBlue * 0.4f);
}

// In-progress drag preview (before mouse release):
if (_menu.IsInZoneDrawMode && _menu.DragStart.HasValue)
{
    var previewRect = BuildDragRect(_menu.DragStart.Value, _menu.DragCurrent);
    e.SpriteBatch.Draw(_pixelTexture, previewRect, Color.White * 0.25f);
}
```

**`Zone.Bounds`** is the existing `Microsoft.Xna.Framework.Rectangle` on the `Zone` record (established in U-04). No new types needed.

---

## Pattern 3: Modal Draw Mode
**Addresses**: NFR-UX-03 (in-place zone draw mode without leaving the bulletin board)

**Problem**: Zone drawing is a spatial, full-farm interaction. Naive approach would push a new full-screen menu (losing ZoneAndChestMenu state). This forces the player to leave the bulletin board context.

**Solution**: `ZoneAndChestMenu` uses a boolean modal flag (`_inZoneDrawMode`) to repurpose its own input handlers while keeping the menu on the stack.

**States within ZoneAndChestMenu**:

```
Normal → [Draw Zone button] → ZoneDrawMode → [mouse release] → Normal
Normal → [Select Building] → BuildingSelectMode → [building click] → Normal
```

**Input routing in ZoneDrawMode**:
- `receiveLeftClick(x, y)`: Records `_dragStart = ScreenToTile(x, y)`
- `leftClickHeld(x, y)`: Records `_dragCurrent = ScreenToTile(x, y)` (drives preview)
- `releaseLeftClick(x, y)`: Calls `FinalizeZone(_dragStart, ScreenToTile(x, y))`; sets `_inZoneDrawMode = false`
- All other menu button clicks are ignored while in zone draw mode
- `ZoneDrawOverlay.OnRenderedWorld` checks `_menu.IsInZoneDrawMode` — shows preview rectangle only in that mode

**FinalizeZone logic**:
```csharp
private void FinalizeZone(TileCoord start, TileCoord end)
{
    // Normalize: ensure top-left / bottom-right order
    var bounds = new Rectangle(
        Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
        Math.Abs(end.X - start.X) + 1, Math.Abs(end.Y - start.Y) + 1);
    if (bounds.Width == 0 || bounds.Height == 0) return; // ignore zero-size drag
    _completedZones.Add(new Zone(bounds));
    // Invalidate cached screen rects → recomputed on next draw
}
```

**Building select mode**: similar pattern. `receiveLeftClick` checks which building tile was clicked, adds the building's `indoors` location name to `_selectedBuildingLocations`, returns to Normal mode.

---

## Pattern 4: Event Subscription Lifecycle
**Addresses**: NFR-ONBOARD-01 (prevent dangling render handlers), NFR-PERF-01

**Problem**: `Display.RenderedWorld` is a global event. If not unsubscribed, `ZoneDrawOverlay.OnRenderedWorld` keeps running after the menu closes — causing incorrect rendering and potential null-ref errors.

**Solution**: Subscribe in constructor; unsubscribe in `cleanupBeforeExit()`.

```csharp
// ZoneAndChestMenu constructor:
helper.Events.Display.RenderedWorld += _overlay.OnRenderedWorld;

// ZoneAndChestMenu.cleanupBeforeExit():
protected override void cleanupBeforeExit()
{
    ModEntry.Instance.Helper.Events.Display.RenderedWorld -= _overlay.OnRenderedWorld;
    base.cleanupBeforeExit();
}
```

`cleanupBeforeExit()` is called by SMAPI's menu system when `Game1.activeClickableMenu` is replaced (including when HiringFlowCoordinator advances to the next screen). This guarantees exactly one subscribe/unsubscribe pair per menu instance lifetime.

**Note**: The `_pixelTexture` created in `ZoneDrawOverlay` should be disposed in `cleanupBeforeExit()` as well:
```csharp
_overlay.Dispose(); // calls _pixelTexture?.Dispose()
```

---

## Pattern 5: Constructor Injection
**Addresses**: NFR-MAINT-03 (SMAPI integration separation)

**Problem**: `ZoneAndChestMenu` needs `ChestResolver` (a game-API-dependent integration) to populate its chest list. Instantiating it inside the menu couples the menu to the concrete class, breaking testability and violating the Core separation rule.

**Solution**: Inject `ChestResolver` via constructor from `HiringFlowCoordinator`.

```csharp
// ModEntry wires singletons (extended this unit):
_chestResolver = new ChestResolver(Helper);
_coordinator = new HiringFlowCoordinator(..., _chestResolver);

// HiringFlowCoordinator constructs ZoneAndChestMenu:
var zoneMenu = new ZoneAndChestMenu(Helper, _chestResolver, _contractDraft);
```

**`ChestResolver` is a singleton** — constructed once in `ModEntry`, reused across hiring flows. It holds no mutable state; its methods query the current game state fresh on each call.

---

## Pattern 6: Gamepad Virtual Cursor Passthrough
**Addresses**: NFR-UX-01 (full gamepad navigation)

**Problem**: Zone drawing is inherently spatial (click-and-drag). Mapping D-pad navigation to tile-by-tile cursor movement would require a custom cursor system.

**Solution**: Leverage Stardew's existing gamepad virtual cursor. When the player uses a controller, the game renders a software cursor moved by the left analog stick. SMAPI translates A-button presses with this cursor into `receiveLeftClick(x, y)` calls. Zone drawing therefore works identically on gamepad with no extra code.

**Required gamepad wiring for ZoneAndChestMenu buttons**:
```csharp
public override void populateClickableComponentList()
{
    allClickableComponents.Clear();
    allClickableComponents.Add(_drawZoneBtn);       // myID = 100
    allClickableComponents.Add(_clearZonesBtn);     // myID = 101
    allClickableComponents.Add(_selectBuildingBtn); // myID = 102
    // Per output-task row:
    foreach (var row in _outputRows)
        allClickableComponents.Add(row.SetOutputBtn); // myID = 200+index
    allClickableComponents.Add(_confirmBtn);        // myID = 998
    allClickableComponents.Add(_backBtn);           // myID = 999
}

public override void snapToDefaultClickableComponent()
{
    currentlySnappedComponent = getComponentWithID(100); // Draw Zone btn
    snapCursorToCurrentSnappedComponent();
}
```

D-pad navigation chain (upNeighborID / downNeighborID) wired to form a logical top-to-bottom flow: DrawZone → ClearZones → SelectBuilding → output row 0 → … → output row N → Confirm / Back.

---

## Resilience Assessment

| Concern | Handling |
|---|---|
| `OnRenderedWorld` exception | SMAPI catches + logs; overlay stops rendering; menu stays open — acceptable for v1 |
| `ChestResolver.GetAllChests` returns empty | Menu shows empty dropdown ("no chests available on farm"); valid state |
| `ChestResolver.ResolveChest` returns null (orphaned chest) | Recorded as `ChestRef?` null in `_outputAssignments`; downstream U-14 handles mail fallback |
| Zero-size drag (click without drag) | `FinalizeZone` guards `Width/Height == 0`; zone not added |
| Building with no interior chests | `ChestResolver.GetAllChests` omits buildings with empty chest lists; building-select mode skips them |

## Scalability Assessment
N/A — single-player SMAPI mod, no concurrency, no distributed state.

## Security Assessment
N/A — Security Baseline disabled (Q28).
