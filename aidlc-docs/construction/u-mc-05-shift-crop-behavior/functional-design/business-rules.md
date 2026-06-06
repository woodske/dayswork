# U-MC-05 Business Rules

**Unit**: U-MC-05 — Shift Crop Behavior
**Stage**: CONSTRUCTION — Functional Design
**Status**: Review required

Rules are prefixed `BR-MC5-*`. They govern the live runtime; the pure decision rules they
build on (viability, atomicity, ordering, store resolution) are owned by U-MC-01.

## Batch & scope

- **BR-MC5-01** A `BatchKind.ManagedCrops` batch is emitted only when
  `WorkScopeSet.ManagedCrops` is non-empty and the managed location is the open farm
  (`"Farm"`). Greenhouse/shed managed locations are skipped this unit (U-MC-07). (FR-MC-09)
- **BR-MC5-02** The managed-crop batch is ordered **before** the general `OutdoorCrops`
  and `OutdoorClearing` batches so prepare/harvest-first work runs early. (FR-MC-09)
- **BR-MC5-03** An empty/disabled `CropPlan` produces no managed-crop batch and leaves all
  existing contract behavior unchanged. (NFR-MC-04, NFR-MC-06)

## Per-tile execution & ordering

- **BR-MC5-04** Each tile is taken through **harvest → clear debris → till → fertilize →
  seed → water** in dependency order, exactly as the planner orders it; each action is its
  **own beat** paced at `WorkerActionAnimationMs`. (FR-MC-10)
- **BR-MC5-05** Fertilizer is laid on bare tilled soil **before** the seed; the worker
  **never** lays fertilizer or seed on a tile unless **both** required components are on
  hand for that tile (atomicity, enforced by the U-MC-01 planner). (FR-MC-11)
- **BR-MC5-06** Harvest runs first so a non-regrow crop's freed tile can be replanted the
  **same shift** (soil stays tilled → only fertilize→seed→water remain), subject to
  viability and supply. (FR-MC-10, FR-MC-24)
- **BR-MC5-07** Each executed beat spends the action's energy via
  `WorkerEnergyLedger.ApplyActionCost`; reaching zero energy requests a work-unit-boundary
  stop (the current beat finishes; no new tile is started). Skipped actions spend **no**
  energy. (FR-MC-40, FR-MC-42)

## Supply (input chest only this unit)

- **BR-MC5-08** The worker's supply is loaded from the **input chest** at batch start;
  the planner is invoked with no shop stock, so supply-dependent actions are bounded
  strictly by current input-chest contents. No store trip is made (U-MC-06). (FR-MC-13,
  FR-MC-34)
- **BR-MC5-09** Supplies consumed are exactly those carried; **leftover carried supply is
  returned to the input chest at end of shift** — nothing is lost. (NFR-MC-03, FR-MC-34)
- **BR-MC5-10** Partial stock completes only `min(seeds, fertilizer)` fertilized tiles (or
  `min(tiles, seeds)` unfertilized); remaining empty tiles wait for a later shift. (FR-MC-11,
  FR-MC-24)

## Viability & fertilizer availability

- **BR-MC5-11** On the open farm, a tile is planted only if the crop can mature and be
  harvested at least once before season end, computed with **fertilized** growth time when
  the choice configures fertilizer. (FR-MC-21)
- **BR-MC5-12** If a zone configures a fertilizer that is **entirely unavailable** (absent
  from the input chest, no shopping this unit), **no** tiles in that zone are planted and a
  single "fertilizer unavailable — planting skipped" HUD notice fires for that zone. Seed
  is never planted un-fertilized. (FR-MC-22)

## Tools & capability gating

- **BR-MC5-13** Crop actions reuse the capability model: till→`Hoe`+`HoeSwing`;
  water→`WateringCan`+`WaterTile`; harvest→`HarvestCrop`; debris/dead-plant
  clearing→`Axe`/`Pickaxe`/`Scythe` respecting `CapabilityMatrix` **level** gating + the
  existing swing costs. (FR-MC-30)
- **BR-MC5-14** Planting and fertilizing are **not** tool-gated; they gate only on item
  availability. (FR-MC-31)
- **BR-MC5-15** A missing or under-leveled tool causes the worker to **skip just that
  action/tile** and emit a HUD notice; the rest of the contract proceeds. No
  contract-creation tool validation. (FR-MC-32, NFR-MC-05)
- **BR-MC5-16** A tile blocked by debris the worker cannot clear (too-weak tool) cannot be
  tilled/planted that shift; it is retried on later shifts once removable. (FR-MC-32)

## Field maintenance & toggles

- **BR-MC5-17** Managed-zone tiles reverted to untilled are **re-tilled** each shift; tilling
  and planting target only tiles the live map marks `Diggable` and not blocked by an
  unclearable object. Non-diggable tiles in a drawn zone are skipped. (FR-MC-25, FR-MC-44)
- **BR-MC5-18** When `CropPlan.ClearDebrisBeforeTilling` (default ON) is enabled, debris
  blocking a managed tile is cleared before tilling; no energy is spent on a tile with no
  debris. When disabled, debris-blocked tiles are skipped for till/plant. (FR-MC-26)
- **BR-MC5-19** When `CropPlan.ClearDeadPlants` (default ON) is enabled, dead/wilted plants
  on managed tiles are cleared **opportunistically as encountered**, scoped to the
  contract's assigned managed zones — **not farm-wide**. When disabled, a dead-crop tile
  cannot be re-tilled/replanted and is skipped. (FR-MC-27)
- **BR-MC5-20** When per-season `AutoReplant` is enabled, emptied prepared **viable** tiles
  are refilled each shift from available supply; when disabled, emptied tiles are not
  refilled within the season. (FR-MC-24)

## Coexistence & output

- **BR-MC5-21** Managed-zone tiles are excluded from the general `WaterCrops`/`HarvestCrops`
  scans for that location; the managed and general paths never double-act on the same tile
  in the same shift. Crops planted manually outside managed zones are still serviced by the
  general tasks. (FR-MC-28)
- **BR-MC5-22** Managed-crop harvest is held in worker inventory and settled to the office
  **output chest** via the existing deposit/overflow pipeline (current fallback). Per-zone
  `ChestRef` routing is deferred to U-MC-07. (FR-MC-29 — fallback only this unit)

## Safety & resilience

- **BR-MC5-23** No managed-crop runtime path throws or aborts the shift: missing tools,
  unavailable fertilizer, partial stock, non-diggable tiles, and unreachable tiles are all
  handled by skip + (where player-relevant) notify. (NFR-MC-05)
- **BR-MC5-24** All new player-facing text (HUD notices, toggle labels, GMCM cost labels)
  is i18n-backed and passes the hardcoded-string lint gate. (NFR-MC-07)
- **BR-MC5-25** When SVE is absent, behavior is unchanged from vanilla; the managed-crop
  batch operates only on the open farm this unit. (NFR-MC-04)

## Extension Compliance

| Extension | Status | Rule impact |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant (full) | BR-MC5-04/05/10/11 inherit U-MC-01 planner properties; BR-MC5-13 (action map) and BR-MC5-21 (tile exclusion) add total/deterministic pure properties; runtime rules are example-covered. |
