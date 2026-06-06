# Functional Design — Business Logic Model — U-MC-03 Manage Crops Authoring UI

**Unit**: U-MC-03 — Manage Crops Authoring UI
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: Q1=A, Q2=B, Q3=A, Q4=A, Q5=A, Q6=A, Q7=A, Q8=A

Covers the authoring workflows that let the player declare a seasonal crop plan crop-first,
then draw zone(s) to apply it. Pure logic (catalog filtering/tagging, multi-season resolution)
lives in `Dayswork.Core`; orchestration/state lives in the `Dayswork` UI layer.

---

## 1. Components & responsibilities

| Component | Layer | Responsibility |
|---|---|---|
| `HubMenu` (extended) | mod UI | Adds the **Manage Crops** nav row + status chip; opens the page via a new coordinator hook. |
| `ManageCropsMenu` (M-24, new) | mod UI | The single scrolling authoring page (Q1=A): season rows, output-chest control, draw button; mouse/keyboard + gamepad. |
| `CropCatalogProvider` (M-25, new) | mod adapter + Core seam | Reads live crop/shop data; produces season-filtered, supply-tagged `CropCatalogEntry`/`FertilizerOption` lists. Pure filter/tag/sort in Core (Q3=A). |
| `SeasonAssignmentResolver` (C-27, wired) | Core (pure) | Multi-season auto-population + locking when a multi-season crop is chosen (FR-MC-04). |
| `ContractDraft` + `CropPlanDraft` (extended/new) | mod state | Holds in-progress authoring state and materialized assignments. |
| `HiringFlowCoordinator` (extended) | mod | Wires the page open, the crop/fertilizer picker, the begin-draw handoff, and assignment materialization. |
| `ChestResolver` (reused) | mod | Supplies the selectable chest list for the output-chest picker (already excludes both office chests, U-MC-02). |

---

## 2. Workflow: open the Manage Crops page

```
Player clicks "Manage Crops" row on HubMenu
  → coordinator.ShowManageCrops(draft)
      → ensure draft.CropPlanDraft exists (lazily created, seeded from any prior session
        and, when editing an existing contract, hydrated from contract.CropPlan)
      → ManageCropsMenu.Open(draft, catalog)
  → page renders four season rows (current season ordering), output-chest row, draw button
  → back returns to HubMenu (hub-and-spoke, consistent with existing spokes)
```

Edit-flow hydration: `CreateEditDraft` is extended so an existing contract's `CropPlan`
populates `CropPlanDraft.MaterializedAssignments` (and, for display, derives season slots from
the first assignment's choices). Delete-and-redraw remains the only edit model (FR-MC-07): the
page does not offer in-place per-zone reshaping.

---

## 3. Workflow: configure a season (crop → fertilizer → auto-replant)

For an **Open** season row:

```
1. Player opens the crop picker for season S (Q2=B: scrollable list picker)
     → catalog = CropCatalogProvider.GetCatalog(seasonFilter: S, greenhouseContext: false)
        (vanilla + modded; filtered to S; each entry tagged AutoBuyable / ChestSupplyOnly)
     → player selects a CropCatalogEntry  →  slot.Crop = entry.Crop
2. If slot.Crop.IsMultiSeason:
     → resolver.Apply(draft.CropPlanDraft, originSeason: S, crop)
        • auto-populate the crop's other consecutive growable seasons
        • mark those slots MultiSeasonLocked with LockOrigin = S
        • (a slot already locked/configured by another crop blocks the choice — see rules)
3. Player opens the fertilizer picker for season S (Q2=B)
     → fertilizers = CropCatalogProvider.GetFertilizers()
     → player selects FertilizerOption or "None"  →  slot.Fertilizer = option | null
4. Player toggles "auto-replant this season's crop"  →  slot.AutoReplant = !slot.AutoReplant
```

Configuration is free-order and repeatable across one to four seasons before any draw
(crop-first authoring, FR-MC-02). Unconfigured seasons stay null (ignored at runtime).

`MultiSeasonLocked` rows are read-only: opening their picker is suppressed; they display the
origin crop and a reason (FR-MC-04). Clearing the **origin** slot's crop releases its locked
slots back to `Open`.

---

## 4. Workflow: assign an output chest (Q7=A)

```
Player opens the output-chest control
  → chestList = ChestResolver selectable chests (both office chests already excluded, U-MC-02)
  → player picks a chest  →  draft.CropPlanDraft.OutputChest = ChestRef(picked)
  → player may clear it    →  OutputChest = null  (runtime falls back to office output chest, §6.8)
```

The output chest is **shared across all seasons** of the zone(s) drawn from this draft.

---

## 5. Workflow: draw zone(s) to apply the plan (Q4=A)

```
Player clicks "Draw zone(s)"  (enabled only when HasAnyConfiguredSeason — see rules)
  → coordinator.BeginCropZoneDraw(draft)
      → reuse the existing ZoneDrawMenu machinery (same as BeginZoneDraw today)
      → on draw-complete with zones Z[]:
          for each zone z in Z:
            choices = project configured + locked SeasonSlotDrafts → SeasonCropChoice[]
            assignment = CropZoneAssignment(
                Zone: z, Mode: Seasonal,
                Choices: choices, OutputChest: draft.CropPlanDraft.OutputChest)
            draft.CropPlanDraft.MaterializedAssignments.Add(assignment)
          RefreshPreview(draft)            // updates hub status/chip
          return to ManageCropsMenu
      → on cancel: no assignments added; return to ManageCropsMenu
```

This unit wires begin-draw to the **existing** overlay as-is. U-MC-04 later adds existing-zone
red rendering, active-draw green, and overlap prevention (DEV-MC-01); until then the existing
overlap behavior of the shared machinery applies. Two non-contiguous zones drawn in one
operation each become their own independently-configured `CropZoneAssignment` carrying the same
projected plan (FR-MC-08).

**Projection rule:** every non-null season slot (Open with a crop, plus MultiSeasonLocked
mirror slots) yields a `SeasonCropChoice { Season, Crop, IsLocked, OriginSeason }`. Fertilizer
selection is carried on the choice via the crop's configured fertilizer item (the
`CropDescriptor`/choice already model the fertilizer linkage from U-MC-01). `StorePreference`
stays at its default `Either` (authored later, U-MC-06).

---

## 6. Workflow: status chip & confirm

- **Hub chip (FR-MC-01, Q8=A):** `Done` when `draft.CropPlanDraft?.HasAnyAssignment == true`
  (≥1 materialized `CropZoneAssignment`); otherwise `Optional` (crop management is opt-in). A
  configured-but-undrawn plan still reads `Optional`.
- **Confirm (extends `BuildContract`):** `CropPlan(MaterializedAssignments)` is attached to the
  built `Contract`; empty → `CropPlan.Empty`. Crop management does **not** gate `CanConfirm`
  (it is optional) and does **not** change pricing (OQ-1; flat energy-tier price stands).

---

## 7. Crop catalog logic (Q3=A — pure seam + thin adapter)

```
CropCatalogProvider (mod adapter):
  reads live game data (crop data, seed→crop links, seasons/regrow, store stock)
    → builds raw catalog records
PureCropCatalog (Core):
  • FilterBySeason(records, season)        → only crops growable in that season (open farm)
  • TagSupply(record, storeStock)          → AutoBuyable if stocked at Pierre/Joja, else ChestSupplyOnly
  • SortForDisplay(entries)                → deterministic ordering (name/id, Ordinal)
  • (greenhouseContext flag is plumbed but always false in U-MC-03 — Q5=A)
```

Determinism and the season-filter/tagging/sort logic are pure-Core and PBT-covered; the live
data read is an example-tested thin adapter (Q3=A; NFR-MC-01/08).

---

## 8. Error / edge handling (functional)

| Scenario | Behavior |
|---|---|
| No crops returned for a season (e.g. winter on vanilla) | Picker shows an empty/"no crops this season" state; the season stays unconfigured. |
| Player picks a multi-season crop whose linked season is already configured | Choice is rejected with an explanatory message; existing config is preserved (see business-rules R-07). |
| Player clears the origin crop of a locked group | Linked locked slots revert to `Open` and become editable again. |
| Draw cancelled / zero zones drawn | No assignments added; draft unchanged; page reopens. |
| Output chest no longer resolvable later | Not validated at authoring time; runtime falls back to office output chest (§6.8). Out of this unit. |
| Modded crop with missing/invalid data | Adapter skips unmappable records (example-tested); they simply don't appear in the picker. |

---

## 9. Boundaries (explicitly deferred)
- Applying the plan to **tiles**, viability, planting/maintenance → U-MC-05.
- Overlay coloring/overlap/existing-zone awareness → U-MC-04.
- Global debris/dead-plant toggles, store preference UI → U-MC-05 / U-MC-06 (Q6=A).
- Season-agnostic greenhouse/shed authoring → U-MC-07 (Q5=A).
