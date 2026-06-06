# Functional Design — Frontend Components — U-MC-03 Manage Crops Authoring UI

**Unit**: U-MC-03 — Manage Crops Authoring UI
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: Q1=A, Q2=B, Q3=A, Q4=A, Q5=A, Q6=A, Q7=A, Q8=A

UI is Stardew `IClickableMenu`-based (no web framework). "Component" = a menu class or a
self-contained sub-region/control. Layout follows the existing `ContractMenuLayout`
single-panel idiom and the `HubMenu` hub-and-spoke navigation.

---

## 1. Component hierarchy

```
HubMenu (extended)
└── NavItem "Manage Crops"  ──opens──► ManageCropsMenu (M-24, new)
      (status chip: Done | Optional, R-02/Q8=A)

ManageCropsMenu  [single scrolling page, Q1=A]
├── Title + status/help line
├── SeasonRow × 4   (Spring, Summer, Fall, Winter — current-season-first ordering)
│   ├── Season label
│   ├── Crop control      → opens CropPickerMenu (Q2=B)
│   ├── Fertilizer control→ opens FertilizerPickerMenu (Q2=B)
│   ├── Auto-replant checkbox
│   └── Lock badge + reason   (when MultiSeasonLocked, R-07)
├── OutputChestRow        → opens chest picker (existing idiom, Q7=A)
├── "Draw zone(s)" button (enabled per R-17)
├── (optional) configured-assignments summary + "Clear" (delete-and-redraw, R-21)
└── Back button (→ HubMenu)

CropPickerMenu (scrollable list, reuses MenuScrollBar)
└── CropEntryRow × N   (DisplayName + AutoBuyable/ChestSupplyOnly tag, R-05)

FertilizerPickerMenu (scrollable list, reuses MenuScrollBar)
└── FertilizerEntryRow × N   ("None" + fertilizer options)
```

`MenuScrollBar` and the existing chest-picker idiom (used by `OutputDestinationsMenu` /
`ZoneAndChestMenu` via `ChestResolver`) are reused rather than rebuilt.

---

## 2. Components — props & state

### 2.1 `HubMenu` (extended)
- **New prop:** `Action<ContractDraft> onManageCrops` (coordinator hook), mirroring the existing
  `onWorkScope`/`onOutput` constructor actions.
- **New row:** `NavItem("ui.hub.manage_crops", onManageCrops, ManageCropsStatus)`.
- **New status delegate:** `ManageCropsStatus()` → `Done` when
  `draft.CropPlanDraft?.HasAnyAssignment == true`, else `Optional` (R-02).
- State: none beyond the existing draft reference.

### 2.2 `ManageCropsMenu` (M-24)
- **Props/ctor:** `ContractDraft draft`, `CropCatalogProvider catalog`, `ChestResolver
  chestResolver`, `Action<ContractDraft> onBack`, `Action<ContractDraft> onBeginCropZoneDraw`,
  `Action<ContractDraft> onPickCrop/onPickFertilizer/onPickChest` (or internal sub-menu opens).
- **Reads (from draft.CropPlanDraft):** four `SeasonSlotDraft`s, `OutputChest`,
  `MaterializedAssignments`, derived `HasAnyConfiguredSeason`/`HasAnyAssignment`.
- **Local UI state:** scroll offset, snapped component id, clickable-component list.
- **Writes:** delegates all mutation to the coordinator/draft (selecting crop/fertilizer,
  toggling replant, setting/clearing chest, beginning a draw).

### 2.3 `SeasonRow` (sub-region of `ManageCropsMenu`)
- **Props:** the `SeasonSlotDraft` for its season.
- **Render:** season name; crop name or "Choose crop…"; fertilizer name or "None"; replant
  checkbox; when `LockState == MultiSeasonLocked`, a distinct style + lock reason and the
  crop/fertilizer/replant controls are **read-only/disabled** (R-07).
- **Interactions:** open crop picker; open fertilizer picker; toggle replant. All suppressed
  when locked.

### 2.4 `CropPickerMenu` (Q2=B)
- **Props:** `IReadOnlyList<CropCatalogEntry> entries` (already season-filtered + tagged),
  `Action<CropDescriptor> onSelect`, `Action onCancel`.
- **State:** scroll offset (via `MenuScrollBar`), snapped row.
- **Render per row:** localized crop name + a supply tag chip (`AutoBuyable` /
  `ChestSupplyOnly`). Empty list → "no crops this season" message.

### 2.5 `FertilizerPickerMenu` (Q2=B)
- **Props:** `IReadOnlyList<FertilizerOption> options` plus a leading **"None"** row,
  `Action<FertilizerOption?> onSelect`, `Action onCancel`.

### 2.6 Output-chest control (Q7=A)
- Reuses the existing `ChestResolver` selectable-chest picker idiom (both office chests already
  excluded, U-MC-02). Shows current chest name or "None (uses office output chest)"; supports
  pick and clear.

---

## 3. User interaction flows

### Flow A — configure a season
1. Open page → `SeasonRow` shows "Choose crop…".
2. Click crop control → `CropPickerMenu` opens with
   `catalog.GetCatalog(season, greenhouseContext:false)`.
3. Select a crop → `slot.Crop` set; if multi-season, resolver locks linked seasons (R-07/R-08).
4. Click fertilizer control → `FertilizerPickerMenu` → set/clear fertilizer.
5. Toggle auto-replant.
6. Repeat for other seasons (any order, R-11).

### Flow B — assign output chest
1. Click output-chest control → chest picker → select or clear (R-15/R-16).

### Flow C — draw to apply
1. "Draw zone(s)" enabled once ≥1 season configured (R-17).
2. Click → existing zone-draw overlay (reused) → draw one or more zones → confirm.
3. On complete, each zone → its own `CropZoneAssignment` projected from the plan (R-18/R-19);
   return to the page; hub chip updates to "Done" (R-02).
4. Cancel → no change (R-20).

### Flow D — redraw / clear (delete-and-redraw, R-21)
1. Player clears a materialized assignment (or all) from the summary, then redraws. No in-place
   reshaping.

### Flow E — gamepad
- All rows/buttons/pickers are snap-navigable; **B** backs out of pickers and the page
  (R-25), consistent with `HubMenu`/`ZoneAndChestMenu`.

---

## 4. Validation rules (UI-enforced)

| Rule | Where | Behavior |
|---|---|---|
| Draw requires ≥1 configured season (R-17) | "Draw zone(s)" button | Disabled/greyed until satisfied. |
| Multi-season conflict (R-08) | Crop select | Reject with explanatory message; preserve existing config. |
| Locked season non-assignable (R-07) | `SeasonRow` | Crop/fertilizer/replant controls disabled; lock reason shown. |
| Output chest optional (R-16) | Output-chest control | Valid when unset; no reachability check here. |
| Crop management never blocks Confirm (R-02/R-23) | Hub `CanConfirm` | Unaffected by crop plan. |

No numeric/text free-entry fields exist, so there is no format/range validation; all input is
selection/toggle-based.

---

## 5. Integration points (this unit)

| Control | Calls | Returns |
|---|---|---|
| Crop picker | `CropCatalogProvider.GetCatalog(season, greenhouseContext:false)` | season-filtered, tagged `CropCatalogEntry[]` |
| Fertilizer picker | `CropCatalogProvider.GetFertilizers()` | `FertilizerOption[]` |
| Crop select (multi-season) | `SeasonAssignmentResolver.Apply(draft, originSeason, crop)` | updated slot lock states |
| Output-chest picker | `ChestResolver` selectable chests | `ChestRef` |
| Draw button | coordinator `BeginCropZoneDraw` → existing `ZoneDrawMenu` | drawn `Zone[]` → `CropZoneAssignment[]` |
| Confirm | extended `BuildContract` | `Contract` carrying `CropPlan` |

No backend/HTTP endpoints (local SMAPI mod). "API integration" here means the Core seams and
live-game adapters above.

---

## 6. Deferred to later units (not built here)
- Overlay red/green coloring, overlap prevention, existing-zone awareness → **U-MC-04**.
- Greenhouse/shed season-agnostic authoring UI → **U-MC-07** (Q5=A).
- Global "clear debris"/"clear dead plants" toggles → **U-MC-05**; store-preference UI →
  **U-MC-06** (Q6=A).
