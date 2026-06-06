# U-MC-05 Business Logic Model

**Unit**: U-MC-05 — Shift Crop Behavior
**Stage**: CONSTRUCTION — Functional Design
**Status**: Review required

## Scope

U-MC-05 makes the farmhand actually execute a contract's managed crop plan on the open
farm each shift. It is the runtime wiring for the pure planners built in U-MC-01. It
introduces a managed-crop work batch, a live-map field reader, per-tile beat execution
with capability/energy gating, and the new hoe/plant/fertilize action kinds and tool.

Town shopping (U-MC-06) and per-zone harvest routing + greenhouse/shed support (U-MC-07)
are explicitly out of scope; this unit supplies the worker from the **input chest only**
and routes harvest through the existing output-chest deposit pipeline.

## Flow 1: Managed-crop batch construction

1. At shift start, `WorkScopeClassifier` already projects an enabled `CropPlan` into
   `WorkScopeSet.ManagedCrops` (U-MC-01).
2. `ShiftPlanBuilder.BuildBatchPlan(...)` emits a `BatchKind.ManagedCrops` batch for each
   managed-crop location present in `ManagedCrops` whose location is the open farm
   (`"Farm"`). The managed-crop batch is ordered **before** the general `OutdoorCrops`
   and `OutdoorClearing` batches so prepare/harvest-first work runs early in the day.
3. Greenhouse/shed managed locations are skipped this unit (U-MC-07).
4. If `WorkScopeSet.ManagedCrops` is null/empty, no managed-crop batch is emitted and all
   existing behavior is unchanged.

## Flow 2: Field-state intake (live world → pure FieldState)

When the managed-crop batch begins, a thin mod reader (`ManagedCropFieldReader`) snapshots
the live location into a pure `FieldState`:

1. Enumerate every tile inside the union of the batch location's `CropZoneAssignment`
   zones.
2. For each tile produce a `TileState`:
   - `ReadyToHarvest` — a `HoeDirt` with a `Crop` whose `fullyGrown`/harvestable state is
     ready (vanilla `readyForHarvest()`), **including dead-crop detection routed to
     dead-plant clearing, not harvest** (a dead crop is never "ready").
   - `HasCrop` — the tile carries a live (non-dead) crop.
   - `HasDebris` — a clearable object/terrain feature blocks the tile (stone/twig/weeds,
     and dead crops when `ClearDeadPlants` is enabled).
   - `IsTilled` — the tile is `HoeDirt` (tilled soil).
   - `HasFertilizer` — the `HoeDirt` already carries fertilizer.
   - `IsWatered` — the `HoeDirt` is watered today.
   - The reader only includes tiles the live map marks `Diggable` (Back-layer property)
     for till/plant candidacy; non-diggable tiles in a drawn zone are still read (so
     existing crops there are harvested/watered) but never tilled/seeded.
3. `FieldState.IsSeasonAgnosticLocation` is **false** for the farm (viability applies).
4. `FieldState.Date` is the current `GameDate`.

The reader performs no mutation; it is the boundary between live Stardew state and the
pure planner. It reads `Diggable` from whichever map variant is live (FR-MC-44 watch item
is fully addressed in U-MC-07 for the cleared shed variants; the open farm has a single
map).

## Flow 3: Per-zone plan composition (pure, reused from U-MC-01)

For each `CropZoneAssignment` in the batch location, the runner calls
`CropShiftPlanner.Plan(assignment, fieldState, inventory, stockSnapshots: null,
isFestivalDay: <today>)`:

1. The planner resolves the season's `SeasonCropChoice` (or the season-agnostic choice).
2. It partitions work into **supply-independent** actions (harvest, clear debris, till,
   water-existing) and **supply-dependent** actions (fertilize, seed, water-new).
3. Because `stockSnapshots` is null, the store resolver yields **no** purchase lines, so
   the projected inventory equals the input-chest inventory and supply-dependent actions
   are bounded by what the player actually stocked.
4. Viability and seed/fertilizer atomicity are applied inside the planner (U-MC-01):
   candidate tiles are filtered by `PlantingViabilityCalculator`, and fertilize/seed are
   only emitted in `min(seeds, fertilizer)` pairs.
5. The result is a deterministic, dependency-ordered `ManagedCropShiftPlan` of
   `TileAction`s.

The runner concatenates the per-zone plans (stable ordered by zone, then tile) into the
managed-crop batch's execution queue.

## Flow 4: Per-tile beat execution

Each `TileAction` is executed as its **own paced beat** through the existing tick/intent
loop:

1. Navigate the worker to the tile (HoeDirt is walkable, so nav tile = task tile; a
   blocked tile resolves an orthogonal stand tile via the existing routing helper).
2. Map the action to its tool, capability check, and energy cost (Flow 5).
3. If capability passes, play the tool-swing/animation beat, apply the world mutation
   (Flow 6), and spend energy via `WorkerEnergyLedger.ApplyActionCost`.
4. If capability fails (missing/under-leveled tool), **skip this action/tile**, emit a
   one-per-reason HUD notice, and advance — no energy spent, no world mutation.
5. Advance to the next action; respect the existing work-unit boundary / 8 PM cap /
   stamina-zero stop logic (a tile in progress finishes its current beat).

Each beat is one action; a single tile that needs harvest→till→fertilize→seed→water is
five sequential beats, exactly as authored in the plan order.

## Flow 5: Action → tool / capability / energy mapping

| `ManagedCropActionKind` | Tool (`WorkerTool`) | Capability gate | `WorkActionKind` (energy) |
|---|---|---|---|
| Harvest | None | none (item pickup) | `HarvestCrop` |
| ClearDebris | Axe / Pickaxe / Scythe (by debris type) | `CapabilityMatrix` level gate | `AxeSwing` / `PickaxeSwing` / `ScytheSwing` |
| Till | **Hoe** (new) | hoe present (vanilla starter; runtime skip if absent) | **`HoeSwing`** (new) |
| Fertilize | None | **not tool-gated** (item availability) | **`ApplyFertilizer`** (new) |
| PlantSeed | None | **not tool-gated** (item availability) | **`PlantSeed`** (new) |
| Water | WateringCan | watering-can present | `WaterTile` |

- The three new `WorkActionKind`s carry **configurable non-zero** energy costs in
  `WorkerEnergyProfile.ActionCosts`, defaulted in `ConfigDefaults` and surfaced in GMCM
  exactly like the existing costs.
- Dead-plant clearing reuses the clearing tools/costs (it is a `ClearDebris` action
  produced for a dead-crop tile when `ClearDeadPlants` is enabled).
- Shopping (U-MC-06) costs time only, never energy.

## Flow 6: World mutations per action

Each beat applies the minimal vanilla mutation and routes any produced item:

1. **Harvest** — perform the crop harvest; harvested item(s) go to worker inventory
   (settled to the output chest at end of shift via the existing pipeline). Non-regrow
   crops leave the soil tilled for same-shift replant.
2. **ClearDebris** — remove the blocking object/feature using the gated tool; debris
   drops route through the existing debris-sweep/collection path.
3. **Till** — convert the diggable tile to `HoeDirt`.
4. **Fertilize** — apply the configured fertilizer item to the bare `HoeDirt`, consuming
   one fertilizer from the carried supply.
5. **PlantSeed** — plant the configured seed on the `HoeDirt`, consuming one seed.
6. **Water** — water the `HoeDirt`.

Supplies (seed/fertilizer) are drawn from the worker's carried supply, which is loaded
from the **input chest** at batch start (Flow 7). Item & gold safety (NFR-MC-03): supplies
consumed are exactly those carried; leftover carried supplies are returned to the input
chest at end of shift.

## Flow 7: Supply lifecycle (input chest only this unit)

1. At managed-crop batch start, the runner reads the input chest's seed/fertilizer stacks
   into a `SupplyInventory` and into the worker's carried supply.
2. The planner bounds plantings to that stock (Flow 3).
3. As seed/fertilize beats execute, carried supply decrements.
4. At end of shift, any leftover carried supply is returned to the **input chest**
   (idempotent settle; nothing is lost).
5. No store trip is made; chest-supply-only crops and store-buyable crops are treated
   identically this unit (both plant only from chest stock). U-MC-06 adds the store leg.

## Flow 8: Viability and fertilizer-unavailable handling

1. On the open farm, a tile is a planting candidate only if the crop can mature and be
   harvested at least once before season end, using **fertilized** growth time when the
   choice configures fertilizer (`PlantingViabilityCalculator`, U-MC-01).
2. If a zone configures a fertilizer that is **entirely absent** from the input chest
   (no shopping this unit), the planner emits **no** seed/fertilize actions for that zone
   (atomicity), and the runner emits a single "fertilizer unavailable — planting skipped"
   HUD notice for that zone. Seed is never planted un-fertilized.
3. Under partial fertilizer stock, only `min(seeds, fertilizer)` tiles complete; the rest
   wait for a later shift.

## Flow 9: Coexistence with general Water/Harvest

1. Managed-zone tiles are removed from the general `WaterCrops`/`HarvestCrops` candidate
   set for that location, so the general outdoor scan never targets a managed tile.
2. Watering and harvesting inside managed zones happen only through the managed-crop
   batch. The two paths cannot double-act on the same tile in the same shift.
3. Crops the player plants manually **outside** any managed zone are unaffected and still
   serviced by the general tasks.

## Flow 10: Re-till, replant, gap-fill, dead plants

1. **Re-till** — a managed tile that reverted to untilled is re-tilled each shift
   (planner emits `Till` for diggable, crop-free, debris-free, untilled tiles).
2. **Replant/gap-fill** — harvest-first frees tiles; the planner refills empty prepared
   **viable** tiles from available supply, honoring each season's `AutoReplant`. Empty
   tiles left from earlier partial stock are filled on later shifts once stock is
   available.
3. **Dead plants** — when `ClearDeadPlants` is enabled, dead/wilted crops on managed
   tiles are treated as debris and cleared opportunistically as the worker reaches them,
   freeing the tile for re-till/replant. When disabled, dead-crop tiles are skipped (not
   re-tilled/replanted).

## Testable Properties (PBT-01, full mode)

| Component | Property |
|---|---|
| `CropShiftPlanner` (U-MC-01, reused) | Dependency order preserved; no seed/fertilize without the atomicity gate; null stock ⇒ supply-dependent actions bounded by input inventory. |
| Action→tool/energy mapping (new pure seam) | Total and deterministic: every `ManagedCropActionKind` maps to exactly one `WorkActionKind`/tool tuple; same input ⇒ same output. |
| Managed-zone tile-exclusion predicate (new pure seam) | A tile is excluded from general crop work **iff** it lies in some managed-crop zone for that location; partition is disjoint (no tile both managed and general). |
| `PlantingViabilityCalculator` (U-MC-01, reused) | Determinism; open-farm viability uses fertilized growth when fertilizer configured. |

## Extension Compliance

| Extension | Status | Result |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant (full) | Pure decision logic stays in Core; new pure mapping/exclusion seams carry the properties above; runtime adapter example-covered. |
