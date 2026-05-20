# Logical Components — U-11 Full Hiring UI: Zones & Chests

## Overview

U-11 introduces 3 new Mod-layer components and extends 2 existing ones. All new components live in the `Dayswork` assembly. `Dayswork.Core` gains no new files — U-04's `Zone`, `ChestRef`, `TileCoord`, and `IZoneGeometry` are reused as-is.

---

## New Components

### M-05: ZoneAndChestMenu
**Layer**: Mod (`Dayswork/UI/ZoneAndChestMenu.cs`)  
**Pattern**: Pre-Compute on State Change + Modal Draw Mode + Constructor Injection

**Responsibility**: Screen 2 of the hiring flow. Presents zone drawing controls, building-chest dropdowns, per-task output assignment rows, and navigation buttons. Manages `_inZoneDrawMode` and `_inBuildingSelectMode` flag transitions.

**Constructor**:
```
ZoneAndChestMenu(IModHelper helper, ChestResolver chestResolver, ContractDraft draft)
```

**Internal state**:
```
_completedZones: List<Zone>           — finalized zones (bounding rectangles)
_selectedBuildingLocations: List<string> — interior location names of selected buildings
_outputAssignments: Dictionary<TaskKind, ChestRef?> — null = mail fallback
_chestList: List<ChestEntry>          — populated once on menu open
_buildingOutlines: List<BuildingOutline> — populated once on menu open
_outputDisplayLines: List<string>     — pre-formatted strings for draw()
_inZoneDrawMode: bool
_inBuildingSelectMode: bool
_dragStart: TileCoord?
_dragCurrent: TileCoord
_overlay: ZoneDrawOverlay             — owned instance
```

**Key methods**:
- `draw(SpriteBatch b)` — reads pre-computed fields only; renders zone count, output rows, buttons
- `receiveLeftClick(int x, int y, bool playSound)` — routes to modal or button handling
- `leftClickHeld(int x, int y)` — updates drag preview in zone draw mode
- `releaseLeftClick(int x, int y)` — finalizes zone or building selection
- `cleanupBeforeExit()` — unsubscribes overlay; disposes pixel texture
- `populateClickableComponentList()` — wires all buttons + output rows for gamepad
- `snapToDefaultClickableComponent()` — focuses Draw Zone button on open
- `GetZones()` — exposes completed zones to HiringFlowCoordinator
- `GetOutputAssignments()` — exposes task→chest map to HiringFlowCoordinator

**Outbound dependencies**: `ChestResolver` (injected), `ZoneDrawOverlay` (owned), `IModHelper.Events.Display`

---

### M-08: ZoneDrawOverlay
**Layer**: Mod (`Dayswork/UI/ZoneDrawOverlay.cs`)  
**Pattern**: Rectangle-Fill Overlay + Event Subscription Lifecycle

**Responsibility**: Pure renderer. Draws completed zone fills, in-progress drag previews, and building highlight outlines on the farm map via `Display.RenderedWorld`. Holds no mutable state — reads its data from references passed by `ZoneAndChestMenu`.

**Constructor**:
```
ZoneDrawOverlay(ZoneAndChestMenu menu)
```
Stores reference to `menu` for reading `CompletedZones`, `IsInZoneDrawMode`, `DragStart`, `DragCurrent`, `SelectedBuildingOutlines`.

**Internal state**:
```
_menu: ZoneAndChestMenu       — reference to owning menu (read-only access)
_pixelTexture: Texture2D      — 1×1 white pixel, created once, disposed in Dispose()
```

**Key methods**:
- `OnRenderedWorld(object sender, RenderedWorldEventArgs e)` — renders completed zones (blue fill), drag preview (white outline), selected buildings (yellow outline)
- `Dispose()` — disposes `_pixelTexture`

**Rendering color scheme**:
- Completed zone fill: `Color.LightBlue * 0.4f`
- In-progress drag preview: `Color.White * 0.25f` fill + `Color.White` 1-pixel border
- Selected building outline: `Color.Yellow * 0.6f`
- Hovered building (hover detection via `_menu.HoveredBuilding`): `Color.Yellow * 0.3f`

**Outbound dependencies**: `ZoneAndChestMenu` (reference), `Game1.viewport`, `Game1.graphics.GraphicsDevice`

---

### M-20: ChestResolver
**Layer**: Mod (`Dayswork/Integration/ChestResolver.cs`)  
**Pattern**: Constructor Injection (singleton, no mutable state)

**Responsibility**: Integration bridge between Core's `ChestRef` value type and the live Stardew game state. Enumerates all accessible chests on the farm and in buildings; resolves a `ChestRef` back to a live `Chest` object; generates display names per FR-HIRE-07.

**Constructor**:
```
ChestResolver(IModHelper helper)
```
Stores `helper` for potential future API use (currently stateless; all methods query `Game1` directly).

**Key methods**:

```csharp
// Returns all chests the player can assign — called once on ZoneAndChestMenu open.
// Groups: open-farm chests first, then per-building chests.
List<ChestEntry> GetAllChests(Farm farm)

// Resolves a stored ChestRef to a live Chest. Returns null if chest was moved/destroyed.
Chest? ResolveChest(ChestRef chestRef)

// Generates i18n-aware display name per FR-HIRE-07.
// Uses chest.Name if set; else I18nHelper.Get("ui.zone_chest.chest_fallback_name", ...)
string GetDisplayName(Chest chest, GameLocation location)

// Returns building outlines for zone-draw overlay building selection.
// Each BuildingOutline has: locationName (indoors), tileRect (building footprint on farm).
List<BuildingOutline> GetBuildingOutlines(Farm farm)
```

**`ChestEntry` record** (UI-only DTO, lives in `Dayswork/UI/`):
```csharp
record ChestEntry(ChestRef Ref, string DisplayName, string GroupLabel)
```
- `Ref`: the `ChestRef` to store in the contract
- `DisplayName`: formatted via `GetDisplayName`
- `GroupLabel`: "Farm" for open-farm chests; building name for building-interior chests

**`BuildingOutline` record** (UI-only DTO):
```csharp
record BuildingOutline(string LocationName, Rectangle TileBounds, string DisplayName)
```

**Outbound dependencies**: `StardewValley.Objects.Chest`, `Game1`, `GameLocation`, `Dayswork.Core.Domain.ChestRef`, `I18nHelper`

---

## Extended Components

### M-03: HiringFlowCoordinator (extended)
**Extension point**: Inserts `ZoneAndChestMenu` between `TaskSelectionMenu` and `SummaryMenu`.

**Change**: The flow sequence was:
```
TaskSelectionMenu → SummaryMenu
```
It becomes:
```
TaskSelectionMenu → ZoneAndChestMenu → SummaryMenu
```

**Implementation**: `HiringFlowCoordinator.OnTaskSelectionConfirmed()` (previously opened SummaryMenu directly) now opens `ZoneAndChestMenu`. `ZoneAndChestMenu`'s "Next" button callback calls `coordinator.OnZoneChestConfirmed()`, which reads zones and assignments from the menu and opens `SummaryMenu`.

**`ContractDraft` extension** (mutable UI state object in `Dayswork/UI/`):
```csharp
// Added fields:
List<Zone> Zones            // from ZoneAndChestMenu.GetZones()
Dictionary<TaskKind, ChestRef?> OutputAssignments  // from GetOutputAssignments()
```

**Wire in ModEntry** (new singleton added this unit):
```csharp
_chestResolver = new ChestResolver(Helper);
// Pass to coordinator:
_coordinator = new HiringFlowCoordinator(..., _chestResolver, ...);
```

---

### M-01: ModEntry (extended)
**Extension point**: Constructs and wires `ChestResolver` singleton (Service S-A step for U-11).

**Change**: One new field + construction line:
```csharp
private ChestResolver _chestResolver;
// In Entry():
_chestResolver = new ChestResolver(Helper);
_coordinator = new HiringFlowCoordinator(..., _chestResolver);
```

No new SMAPI event subscriptions in ModEntry for this unit — `Display.RenderedWorld` is subscribed per-menu-instance in `ZoneAndChestMenu`, not in ModEntry.

---

## Data Flow Diagram

```
ModEntry
  |
  +-- ChestResolver (singleton)
  |
  +-- HiringFlowCoordinator
        |
        +-- TaskSelectionMenu
        |      |
        |      | [confirmed: selected tasks, draft rate]
        |      v
        +-- ZoneAndChestMenu
        |      |    \
        |      |     ZoneDrawOverlay
        |      |          |
        |      |     Display.RenderedWorld
        |      |
        |      | [confirmed: zones, outputAssignments → ContractDraft]
        |      v
        +-- SummaryMenu
               |
               | [confirmed: deposit deducted, contract saved]
               v
           ContractPersistenceAdapter
```

---

## PBT Extension Compliance Summary

| Rule | Status | Rationale |
|---|---|---|
| PBT-02 | N/A | No new serialized types (ChestEntry + BuildingOutline are UI-only DTOs, never persisted) |
| PBT-03 | N/A | No new domain invariants; zone geometry invariants tested in U-04 |
| PBT-07 | N/A | No new FsCheck generators |
| PBT-08 | N/A | No PBT tests in this unit |
| PBT-09 | Satisfied | FsCheck remains the PBT framework; no change |

**Security Baseline**: N/A — disabled (Q28).
