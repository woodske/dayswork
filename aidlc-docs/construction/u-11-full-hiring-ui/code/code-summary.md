# Code Summary — U-11 Full Hiring UI: Zones & Chests

## Build result
`dotnet build` — **0 errors, 0 warnings**. Auto-deployed to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork\`.

---

## Files created

### `Dayswork/UI/ChestEntry.cs`
Two UI-only DTO records:
- `ChestEntry(ChestRef Ref, string DisplayName, string GroupLabel)` — a resolved chest for display in the picker; the `ChestRef` inside flows into `ContractDraft.Destinations`.
- `BuildingOutline(string LocationName, Rectangle TileBounds, string DisplayName)` — a building's tile footprint on the farm, used by `ZoneDrawOverlay` and building-select mode.

### `Dayswork/Integration/ChestResolver.cs` (M-20)
Singleton integration bridge. Queried once on `ZoneAndChestMenu` open; never per frame.
- `GetAllChests(GameLocation farm)` — iterates `farm.Objects.Pairs` for open-farm chests; iterates `farm.buildings → indoors.Objects.Pairs` for building-interior chests; returns `List<ChestEntry>` grouped by "Farm" or building name.
- `ResolveChest(ChestRef)` — live chest lookup by location + tile; returns null if orphaned.
- `GetDisplayName(Chest, GameLocation, int, int)` — returns `chest.Name` if player-set; otherwise `"{building} — Chest at {x}, {y}"` via i18n fallback key (FR-HIRE-07).
- `GetBuildingOutlines(Farm)` — returns building footprint rectangles for overlay rendering and click-detection.

### `Dayswork/UI/ZoneDrawOverlay.cs` (M-08)
Pure renderer subscribed to `Display.RenderedWorld` by `ZoneAndChestMenu` (subscribed in ctor; unsubscribed in `cleanupBeforeExit`). Reads owning menu's read-only properties; holds no mutable state.
- Renders completed zone fills: `Color.LightBlue * 0.4f`, O(zone count) via single `SpriteBatch.Draw` per zone bounding rectangle (NFR-PERF-03).
- Renders selected building footprints: `Color.Yellow * 0.35f`.
- Renders in-progress drag preview: `Color.White * 0.25f`.
- Disposes `Texture2D _pixelTexture` (1×1 white pixel) on `Dispose()`.

### `Dayswork/UI/ZoneAndChestMenu.cs` (M-05)
Screen 2 of the hiring flow.
- **Three modes**: Normal (full panel), ZoneDrawMode (mini-banner, player sees farm), BuildingSelectMode (mini-banner).
- **Zone drawing**: `receiveLeftClick` → `leftClickHeld` → `releaseLeftClick` drag pattern; `FinalizeZone` normalizes coordinates, guards single-click, appends `Zone("Farm", topLeft, bottomRight)`, syncs `draft.Zones`.
- **Building selection**: click-to-toggle building from `_buildingOutlines`; adds/removes a whole-interior `Zone(locationName, (0,0), (999,999))`.
- **Output picker**: per-task "Set" button opens a popup panel with Mail / Shipping Bin (if eligible) / chest list options; `ApplyPickerSelection` writes to `draft.Destinations`.
- **Back-nav restore**: constructor reads `draft.Zones` and `draft.Destinations` to repopulate state when player navigates back from SummaryMenu.
- **Gamepad**: `Buttons.B` exits draw/picker/building mode in sequence before navigating back; `populateClickableComponentList` wires D-pad chain.
- `cleanupBeforeExit()` unsubscribes `RenderedWorld` and disposes overlay.

---

## Files modified

### `Dayswork/UI/HiringFlowCoordinator.cs` (M-03 extended)
- Added `ChestResolver _chestResolver` and `IModHelper _helper` fields + constructor parameters.
- Added `ShowZoneAndChest(ContractDraft)` — creates `ZoneAndChestMenu` with correct callbacks.
- `ShowTaskSelection` onAdvance now calls `ShowZoneAndChest` (was `ShowSummary`).
- `ShowSummary` onBack now calls `ShowZoneAndChest` (was `ShowTaskSelection`).
- Flow: TaskSelection → ZoneAndChest → Summary → Confirm.

### `Dayswork/ModEntry.cs` (M-01 extended)
- Added `var chestResolver = new ChestResolver(Helper);` singleton construction.
- Updated `HiringFlowCoordinator` constructor call with `chestResolver` and `Helper`.

### `Dayswork/i18n/default.json`
Added 19 new keys under `ui.zone_chest.*`:
`title`, `draw_zone_btn`, `clear_zones_btn`, `select_building_btn`, `done_drawing_btn`, `zone_count_label`, `no_zones_hint`, `output_section_label`, `no_output_tasks`, `set_output_btn`, `no_chest_assigned`, `shipping_bin_option`, `chest_fallback_name`, `confirm_btn`, `back_btn`, `group_farm`, `picker_mail_option`, `draw_mode_hint`, `building_select_hint`.

---

## Stories implemented
- **S-03** (full zone drawing + building selection): `FinalizeZone`, `ToggleBuilding`, `ZoneDrawOverlay`, building-select mode in `ZoneAndChestMenu`.
- **S-04** (full chest assignment + orphaned-chest fallback): `ChestResolver`, output picker, `MailDestination.Instance` default (orphan → mail handled by U-14 MailDispatcher).

---

## Bug fix: zone draw now uses Robin building-placement UX (camera pan, no warp)

**Problem**: "Draw Zone" opened a mini-banner mode while the player was in Pelican Town — the farm was not visible. A first fix warped the player to the farm, but the camera could not be scrolled.

**Solution (final)**: True `CarpenterMenu` pattern — do NOT warp. Swap the *displayed* location to the farm and pan the camera; the player character stays put.

### New / changed files

**`Dayswork/UI/IZoneDrawSource.cs`** (new)
Data contract between `ZoneDrawMenu` and `ZoneDrawOverlay`.

**`Dayswork/UI/ZoneDrawMenu.cs`** (new, M-08 full implementation)
`IClickableMenu` + `IZoneDrawSource`. On open it captures `Game1.currentLocation`, then sets `Game1.currentLocation = getFarm()`, `Game1.viewportFreeze = true`, `Game1.displayHUD = false`, and centers the viewport. `cleanupBeforeExit` restores all three.
- **Camera panning** in `update()`: mouse within `PanMargin` (64px) of any screen edge pans `PanSpeed` (12 px/frame); gamepad right-stick also pans. Suppressed while the cursor is over a corner button. Clamped to map bounds via `PanViewport`.
- **Modeless drawing**: left-drag creates `Zone("Farm", …)`; a single-tile click on a building footprint toggles it. `CursorTile()` uses `getMouseX(false)+viewport` (UI-scale robust).
- **Toolbar**: Cancel + Clear (bottom-left), Done (bottom-right); top-center instruction + scroll hint. Corners-only so all four screen edges stay pannable.
- `readyToClose() => false` — exit only via Done/Cancel (or gamepad B). `_resultDelivered` guards a cleanup-time `onCancel` fallback for forced closes.
- Owns `ZoneDrawOverlay` (subscribe/unsubscribe `RenderedWorld`, dispose).

**`Dayswork/UI/ZoneDrawOverlay.cs`** (updated)
- Takes `IZoneDrawSource` instead of `ZoneAndChestMenu`.
- Added `DrawTileGrid()`: 1-px semi-transparent grid lines over the visible viewport (always on during the session — `IsInZoneDrawMode => true`), satisfying "clearly see the grid markers".

**`Dayswork/UI/ZoneAndChestMenu.cs`** (simplified)
- Removed all in-menu zone-draw/building-select code, `IModHelper`, overlay, drag handlers, mini-banner.
- Added `Action<ContractDraft> onBeginZoneDraw`; "Draw Zone"/"Select Building" both call it. Zone count reads `_draft.Zones.Count`.

**`Dayswork/UI/HiringFlowCoordinator.cs`** (extended)
- `BeginZoneDraw(draft)`: gets building outlines, opens `ZoneDrawMenu` directly (no warp). `onComplete` writes zones + whole-interior building zones into the draft then re-opens `ZoneAndChestMenu`; `onCancel` re-opens it unchanged.
- No `Player.Warped` machinery.

**`Dayswork/i18n/default.json`**: added `ui.zone_chest.session_hint`, `ui.zone_chest.scroll_hint`.

---

## Definition of Done (play-test checklist)
- [ ] Open hiring flow → Task Selection → "Next" → Zone & Output screen appears
- [ ] Click "Draw Zone" → farm visible, mini-banner shows; click-drag draws a blue rectangle
- [ ] "Done Drawing" returns to full panel; zone count increments
- [ ] Click "Select Building" → click a building footprint → yellow outline appears, zone count increments
- [ ] "Clear Zones" resets zone count to 0
- [ ] "Set" button for an output task opens picker; select a chest → label updates
- [ ] Select "Mail next morning" → label shows "Mail next morning"
- [ ] Select "Shipping Bin" (harvest task only) → label shows "Shipping Bin"
- [ ] "Back" returns to Task Selection; "Next" advances to Summary
- [ ] Summary's "Back" returns to Zone & Output (previous zones/assignments preserved)
- [ ] Controller: D-pad navigates between buttons; A confirms; B cancels/exits mode
