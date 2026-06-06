# Functional Design — Business Rules — U-MC-03 Manage Crops Authoring UI

**Unit**: U-MC-03 — Manage Crops Authoring UI
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: Q1=A, Q2=B, Q3=A, Q4=A, Q5=A, Q6=A, Q7=A, Q8=A

Rules governing crop-first authoring on the Manage Crops page. IDs are referenced by the
frontend-components and code-generation artifacts. "Open farm" = `Seasonal` mode (the only mode
this unit authors, Q5=A).

---

## Entry point & status

- **R-01 (FR-MC-01).** A **Manage Crops** nav row is added to `HubMenu`, rendered like existing
  rows. Clicking it opens `ManageCropsMenu`; the page returns to the hub (hub-and-spoke).
- **R-02 (FR-MC-01, Q8=A).** The row's status chip is **"Done"** when the draft holds **≥1
  materialized `CropZoneAssignment`**, else **"Optional"**. A configured-but-undrawn plan reads
  "Optional". Crop management never makes the contract invalid (it is opt-in).

## Catalog & season filtering

- **R-03 (FR-MC-03).** The crop list is built from live game crop data, **vanilla and modded**.
- **R-04 (FR-MC-03, §5.2).** On the open farm the list for a season is **filtered to crops
  growable in that season**. (Greenhouse/shed no-season list is U-MC-07, Q5=A.)
- **R-05 (FR-MC-03).** Each catalog entry is tagged **`AutoBuyable`** (seeds stocked at Pierre
  and/or Joja) or **`ChestSupplyOnly`** (not stocked at any store). The tag is **display-only**
  in this unit — it does not restrict selection.
- **R-06.** Catalog ordering is deterministic (localized name then item id, Ordinal) so the
  picker and tests are stable (NFR-MC-01).

## Multi-season crops & locking

- **R-07 (FR-MC-04, §5.4).** Selecting a **multi-season** crop in its origin season
  auto-populates and **locks** its other consecutive growable seasons via
  `SeasonAssignmentResolver`. Locked slots: carry the origin crop, are **non-assignable**, are
  styled distinctly, and show **why** they are blocked.
- **R-08.** A multi-season selection is **rejected** if any season it would occupy is already
  configured with a different crop or already locked by another multi-season crop. The existing
  configuration is preserved and the player is told why (no silent overwrite).
- **R-09.** Clearing the **origin** season's crop releases all of that crop's locked slots back
  to **Open** (editable). Locked (non-origin) slots cannot be cleared directly.
- **R-10 (§5.4).** The UI never offers to destroy a multi-season crop between two of its viable
  seasons (no mid-life teardown affordance).

## Per-season configuration

- **R-11 (FR-MC-02, §5.2).** Authoring is **crop-first then draw**: the player may configure
  **one to four** seasons (each: crop → optional fertilizer → optional auto-replant) in any
  order **before** drawing. No zone is committed during configuration.
- **R-12.** A season with no crop chosen is **unassigned** for that season (the farmhand ignores
  it). Unassigned seasons are valid and require no fertilizer/replant input.
- **R-13.** **Fertilizer is optional** per season; "None" is the default. Choosing a fertilizer
  does not change the crop's season filter or viability at authoring time (viability is a
  runtime gate, U-MC-05).
- **R-14.** **Auto-replant** is a per-season boolean (default **off**) attached to that season's
  crop (FR-MC-... §6.4). It only has effect at runtime.

## Output chest

- **R-15 (FR-MC-02, §6.8, Q7=A).** The player may optionally assign **one output chest** shared
  across all seasons of the zone(s) drawn from this draft, chosen from the **`ChestResolver`
  selectable chest list** (both built-in office chests already excluded, U-MC-02).
- **R-16 (§6.8).** Output chest is **optional**; when unset, `OutputChest` is null and runtime
  falls back to the office output chest. This unit performs no chest-reachability validation.

## Drawing & materialization

- **R-17 (Q4=A).** The "Draw zone(s)" action is **enabled only when at least one season is
  configured** (`HasAnyConfiguredSeason`). Drawing with no configured season is disallowed
  (nothing to apply).
- **R-18 (FR-MC-08, §5.2, Q4=A).** On draw-complete, **each drawn zone** becomes its **own**
  `CropZoneAssignment` (`Mode = Seasonal`) carrying the **projected** seasonal `SeasonCropChoice`
  set and the draft's `OutputChest`. Two non-contiguous zones in one draw each get their own
  independent assignment with the same projected plan.
- **R-19.** Projection includes every **non-null** season slot — both directly-configured
  (Open + crop) and **MultiSeasonLocked** mirror slots — producing one `SeasonCropChoice` each
  (`IsLocked`/`OriginSeason` preserved). Unconfigured seasons are omitted.
- **R-20.** A cancelled draw (or zero zones) adds **no** assignments and leaves the draft
  unchanged.
- **R-21 (FR-MC-07).** Editing is **delete-and-redraw only**: the page provides no in-place
  reassignment of a materialized zone's crop/fertilizer/shape/chest. (A clear/remove affordance
  for materialized assignments is acceptable so the player can redraw.)

## Confirm & persistence

- **R-22.** On Confirm, the built `Contract` carries `CropPlan(MaterializedAssignments)`; an
  empty draft yields `CropPlan.Empty`. Round-trips through the existing U-MC-01 contract DTO
  (written only when non-empty) — **no schema change in this unit**.
- **R-23 (OQ-1).** Crop management adds **no** pricing; it does not affect `CanConfirm` or the
  flat energy-tier price. The only gold cost (seed/fertilizer) is a runtime concern (U-MC-06).

## Cross-cutting

- **R-24 (NFR-MC-07/i18n).** All new player-facing strings (nav label, season names, crop/
  fertilizer picker chrome, tags, lock reason, chest label, status chip) are i18n-backed and
  pass the hardcoded-string lint gate.
- **R-25 (NFR-UX-01).** The page and both pickers are fully navigable with **mouse/keyboard and
  gamepad**, consistent with existing menus (snap order, B-to-back).
- **R-26 (NFR-MC-04).** When SVE is absent, behavior is unchanged; this unit adds only open-farm
  seasonal authoring and introduces no SVE-conditional UI.
- **R-27 (NFR-MC-01/08, Q3=A).** Catalog season-filtering, supply-tagging, sorting, and
  multi-season resolution are **deterministic and pure-Core**, covered by FsCheck properties;
  the live data adapter is example-tested.
