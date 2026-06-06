# Functional Design Plan — U-MC-05 Shift Crop Behavior

**Unit**: U-MC-05 — Shift Crop Behavior
**Stage**: CONSTRUCTION — Functional Design
**Stories**: S-29, S-33
**Requirements**: FR-MC-09, FR-MC-10, FR-MC-11, FR-MC-21, FR-MC-22, FR-MC-24, FR-MC-25,
FR-MC-26, FR-MC-27, FR-MC-28, FR-MC-30, FR-MC-31, FR-MC-32, FR-MC-40, FR-MC-42
**NFRs**: NFR-MC-01 (determinism/PBT), NFR-MC-02 (perf), NFR-MC-03 (item/gold safety),
NFR-MC-05 (resilience), NFR-MC-07 (i18n), NFR-MC-08 (test rigor), NFR-MC-09 (tech stack)

## Unit Boundary (what is and isn't in U-MC-05)

U-MC-05 wires the **already-built pure planners** (U-MC-01: `CropShiftPlanner`,
`PlantingViabilityCalculator`, `CropSupplyPlanner`, `StoreResolver`,
`ManagedCropShiftPlan`/`TileAction`) into the **live shift runtime** so the farmhand
actually prepares, plants, and self-heals managed crop zones each shift.

**In scope**
- A new managed-crop **work batch** that runs the per-tile plan on the **open farm**.
- Building a pure `FieldState`/`TileState` from the live farm map (HoeDirt, debris,
  crops, watered, per-tile `Diggable`).
- Executing each `TileAction` as its **own paced beat** (harvest → clear debris → till →
  fertilize → seed → water) at `WorkerActionAnimationMs`, gated by capability + energy.
- New `WorkActionKind`s `HoeSwing`/`PlantSeed`/`ApplyFertilizer` with configurable
  non-zero energy costs (config/GMCM); new `WorkerTool.Hoe` + `ForTask` mapping.
- Viability gate (open farm), seed/fertilizer atomicity, re-till, replant/gap-fill.
- Debris/dead-plant toggles (default ON, honored from `CropPlan`), opportunistic
  dead-plant clearing scoped to managed zones.
- Coexistence: managed-zone tiles excluded from general `WaterCrops`/`HarvestCrops`.
- Capability skip + HUD notice for missing/under-leveled tools; fertilizer-unavailable
  zone-skip notice.

**Out of scope (deferred, with seams left)**
- **Town shopping** (walk-to-store, headless purchase) → **U-MC-06**. U-MC-05 supplies
  the farmhand **from the input chest only**; the planner is invoked with no shop stock,
  so plantings are bounded strictly by current input-chest contents.
- **Per-zone harvest routing to assigned `ChestRef`** and **greenhouse/SVE-shed
  season-agnostic support** → **U-MC-07**. U-MC-05 routes managed-crop harvest through
  the existing inventory→output-chest deposit/overflow pipeline (current fallback), and
  operates on the open farm only.

## Recommended Design Decisions (no blocking question round)

The spec (§6.1–6.9), requirements (FR-MC-09/10/11/21/22/24/25/26/27/28/30/31/32/40/42),
and the U-MC-01 pure planners already fix the behavior precisely. Remaining design
choices are resolved below with the recommended option; none are ambiguous enough to
block. (Consistent with the user's standing pre-authorization to use recommended options
and continue to the playtest gate.)

| # | Decision | Recommended |
|---|----------|-------------|
| Q1 | **Batch integration.** Add `BatchKind.ManagedCrops`; `ShiftPlanBuilder` emits one managed-crop batch per managed-crop location from `WorkScopeSet.ManagedCrops` (open farm this unit), ordered ahead of general outdoor crop/clearing batches so harvest-first/prepare runs early. | **A** |
| Q2 | **Field-state intake.** A thin mod reader (`ManagedCropFieldReader`) snapshots the live location into a pure `FieldState`/`TileState` at batch start (and re-reads per managed zone as needed); `CropShiftPlanner.Plan(...)` is called **per `CropZoneAssignment`** to produce the ordered `TileAction` list. | **A** |
| Q3 | **Supply source.** Input chest only this unit. The runner reads the input-chest contents into `SupplyInventory`; `Plan(...)` is invoked with `stockSnapshots: null`, so the store resolver yields no purchase lines and supply-dependent actions are bounded by chest stock. Purchase target is computed by Core but **not executed** (U-MC-06 seam). | **A** |
| Q4 | **Execution model.** Execute `TileAction`s as discrete beats through the existing tick/intent state machine, reusing the tool-swing animator (`WorkerActionAnimationMs` cadence) and `WorkerEnergyLedger.ApplyActionCost`. The worker walks to each tile (nav tile = task tile for walkable HoeDirt), performs the beat, advances. | **A** |
| Q5 | **Capability + energy mapping.** till→`WorkerTool.Hoe` + `HoeSwing`; water→`WateringCan` + `WaterTile`; harvest→`HarvestCrop`; clear-debris→`Axe`/`Pickaxe`/`Scythe` via `CapabilityMatrix` level gating + existing swing costs; fertilize→`ApplyFertilizer` (no tool gate); seed→`PlantSeed` (no tool gate). Missing/under-leveled tool → **skip just that action/tile** + HUD notice; rest of contract proceeds. | **A** |
| Q6 | **Harvest output routing.** Managed-crop harvest is held in worker inventory and settled to the office **output chest** via the existing deposit/overflow pipeline (current fallback). Per-zone `ChestRef` routing deferred to U-MC-07. | **A** |
| Q7 | **Coexistence.** `WorkAreaScanner`/general crop scans exclude any tile inside a managed-crop zone for that location, so `WaterCrops`/`HarvestCrops` never double-act on a managed tile in the same shift. | **A** |
| Q8 | **Toggles.** Honor `CropPlan.ClearDebrisBeforeTilling` and `CropPlan.ClearDeadPlants` (default ON) at runtime; clear dead plants opportunistically within managed zones (assigned-area scope). Surface the two plan-level toggles as checkboxes on the Manage Crops page (deferred here from U-MC-03). | **A** |
| Q9 | **Replant / gap-fill.** Harvest-first frees non-regrow tiles; each shift the planner refills empty prepared **viable** tiles from available supply, honoring per-season `AutoReplant`. Runtime simply executes the planner output. | **A** |
| Q10 | **Location scope.** Open farm only (`FieldState.IsSeasonAgnosticLocation=false`); greenhouse/shed batch handling is U-MC-07. | **A** |
| Q11 | **Test rigor (PBT full mode).** Keep all decision logic in pure Core (already PBT-covered from U-MC-01) and add pure coverage for the new mapping seams (`WorkActionKind`/`WorkerTool` mapping, the managed-zone tile-exclusion predicate). Cover the runtime adapter with examples; manual playtest closes the unit. | **A** |

## Plan Checklist

- [x] Analyze unit context (unit-of-work U-MC-05, stories S-29/S-33, FR set, U-MC-01 seams).
- [x] Confirm pure-planner boundary: `CropShiftPlanner` is shopping-agnostic with null stock.
- [x] Resolve design decisions Q1–Q11 with recommended options (above).
- [x] Generate `business-logic-model.md` (shift flow, per-tile execution, supply/viability gating).
- [x] Generate `domain-entities.md` (new runtime types/extensions: batch kind, field reader, action/energy/tool mappings, runner).
- [x] Generate `business-rules.md` (BR-MC5-* runtime rules incl. atomicity, capability skip, coexistence, toggles, item/gold safety).
- [x] Generate `frontend-components.md` (two plan-level toggle checkboxes on the Manage Crops page).
- [x] Present completion message; continue to NFR Requirements under standing pre-authorization.

## Extension Compliance

- **Security Baseline**: N/A (disabled for Manage Crops; no network/PII/auth surface).
- **Property-Based Testing (full mode)**: compliant. Pure decision logic stays in Core and
  carries the U-MC-01 PBT obligations forward; new pure mapping seams identify their own
  properties (deterministic action→tool/energy mapping; managed-zone tile-exclusion
  partition is total and disjoint). The runtime adapter is example-covered (live-API
  boundary, per Q5=B precedent in U-MC-02).
