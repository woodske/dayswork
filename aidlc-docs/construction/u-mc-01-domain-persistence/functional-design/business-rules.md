# U-MC-01 Business Rules

**Unit**: U-MC-01 - Crop-plan Domain + Persistence Foundation  
**Stage**: CONSTRUCTION - Functional Design  
**Status**: Review required

## Contract and Opt-In Rules

| Rule | Requirement | Design rule |
|---|---|---|
| BR-MC1-01 | FR-MC-37, FR-MC-42 | `CropPlan.Empty` or a null/missing persisted crop plan means Manage Crops is disabled and existing contract behavior is unchanged. |
| BR-MC1-02 | FR-MC-37 | Authored crop configuration lives on `Contract.CropPlan`; runtime serviceable locations live in `WorkScopeSet.ManagedCrops`. |
| BR-MC1-03 | NFR-MC-01 | All U-MC-01 planners are pure Core services with no SMAPI, map, wallet, menu, shop, or chest dependencies. |

## Persistence Rules

| Rule | Requirement | Design rule |
|---|---|---|
| BR-MC1-04 | FR-MC-37, FR-MC-38, NFR-MC-06 | The current save envelope remains `SchemaVersion = 3`; crop plan is an additive nullable contract DTO field. |
| BR-MC1-05 | FR-MC-38 | Missing/null crop-plan DTO maps to `CropPlan.Empty`. Existing schema-3 saves therefore load with Manage Crops disabled. |
| BR-MC1-06 | NFR-MC-06 | Domain-to-DTO mapping omits empty crop-plan data where possible so old behavior stays compact and opt-in. |
| BR-MC1-07 | NFR-MC-05 | A malformed crop-plan DTO skips only the affected contract through the existing per-contract warning path. |
| BR-MC1-08 | FR-MC-13, FR-MC-37 | Unknown seed/fertilizer/crop item IDs are not persistence errors; Core treats item IDs as opaque strings and leaves validation to Mod catalog/runtime seams. |

## Crop Assignment Rules

| Rule | Requirement | Design rule |
|---|---|---|
| BR-MC1-09 | FR-MC-02, FR-MC-08 | Each `CropZoneAssignment` owns one independent `Zone` and one independent crop plan, even when another zone has identical choices. |
| BR-MC1-10 | FR-MC-04 | Multi-season crops auto-populate their consecutive season span and mark non-origin seasons locked. |
| BR-MC1-11 | FR-MC-04 | Locked seasons are non-authoritative derived choices; replacing the origin choice recomputes the span. |
| BR-MC1-12 | FR-MC-05, FR-MC-23 | Greenhouse and shed greenhouse assignments are season-agnostic and store exactly one continuous crop choice. |
| BR-MC1-13 | FR-MC-29 | An assignment may optionally carry an output `ChestRef`; null means future runtime uses the output-chest fallback. |

## Viability Rules

| Rule | Requirement | Design rule |
|---|---|---|
| BR-MC1-14 | FR-MC-21 | Open-farm planting is viable only when the crop can mature at least once before season end. |
| BR-MC1-15 | FR-MC-21 | Viability uses fertilized growth time when fertilizer is part of the choice. |
| BR-MC1-16 | FR-MC-23 | Season-agnostic greenhouse/shed locations bypass end-of-season viability checks. |
| BR-MC1-17 | FR-MC-21, FR-MC-24 | Regrow crops must still satisfy first-harvest viability; regrow potential does not allow planting too late. |

## Supply and Atomicity Rules

| Rule | Requirement | Design rule |
|---|---|---|
| BR-MC1-18 | FR-MC-11 | If fertilizer is required, completed tiles are capped at `min(availableSeeds, availableFertilizer)`. |
| BR-MC1-19 | FR-MC-11, FR-MC-22 | The planner never emits a fertilize-only or seed-only action for a fertilizer-required tile. |
| BR-MC1-20 | FR-MC-14 | Chest-supply-only crops never create store purchase demand. |
| BR-MC1-21 | FR-MC-12 | Purchase demand is limited to empty viable tiles still missing supplies after input inventory is considered. |
| BR-MC1-22 | FR-MC-22 | If required fertilizer is unavailable from both input inventory and future store stock, no seed is planned for that tile. |

## Store Resolution Rules

| Rule | Requirement | Design rule |
|---|---|---|
| BR-MC1-23 | FR-MC-15, FR-MC-16 | Festival days resolve to no store and purchase planning stops without affecting supply-independent work. |
| BR-MC1-24 | FR-MC-15, FR-MC-20 | A closed preferred store may fall back to the other open store and reports `UsingFallback = true`. |
| BR-MC1-25 | FR-MC-15 | `Either` is deterministic. The implementation must document and test its stable ordering before code generation completes. |
| BR-MC1-26 | FR-MC-13, FR-MC-18 | Store stock checks consume a pure `ShopStockSnapshot`; live `ShopBuilder` reads stay outside Core. |

## Shift Planning Rules

| Rule | Requirement | Design rule |
|---|---|---|
| BR-MC1-27 | FR-MC-09 | The pure plan separates supply-independent work from supply-dependent work that may require a later shopping trip. |
| BR-MC1-28 | FR-MC-10 | Per-tile action order is always harvest, clear debris, till, fertilize, seed, water, omitting actions that are not needed. |
| BR-MC1-29 | FR-MC-24 | Auto-replant can plan same-shift replanting only after harvest frees a tile and viability plus supply gates pass. |
| BR-MC1-30 | FR-MC-25 | The pure planner can mark diggable-required actions, but live `Diggable` tile checks remain a Mod runtime responsibility. |
| BR-MC1-31 | FR-MC-28 | Managed crop actions and general crop tasks must have a future runtime dedupe boundary so the same tile is not acted twice in one shift. U-MC-01 defines the managed action provenance needed for that boundary. |

## PBT Property Table

| PBT ID | Rule | Component | Required property |
|---|---|---|---|
| PBT-MC1-01 | PBT-01, PBT-02 | CropPlanSerialization | For generated valid crop plans, DTO round-trip preserves the domain value. |
| PBT-MC1-02 | PBT-01, PBT-03 | PlantingViabilityCalculator | Viability is deterministic for identical inputs and greenhouse/shed context always returns viable. |
| PBT-MC1-03 | PBT-01, PBT-03 | CropSupplyPlanner | Fertilizer-required completion is capped by `min(seeds, fertilizer)` and never creates one-component planting. |
| PBT-MC1-04 | PBT-01, PBT-03, PBT-04 | SeasonAssignmentResolver | Multi-season choice locking is deterministic and applying the same choice twice is idempotent. |
| PBT-MC1-05 | PBT-01, PBT-03 | StoreResolver | Store/fallback results are deterministic and festival inputs always resolve to no store. |
| PBT-MC1-06 | PBT-01, PBT-03 | CropShiftPlanner | Generated tile action lists preserve dependency order and respect supply atomicity. |
| PBT-MC1-07 | PBT-07 | Test generators | Use reusable generators for crop plans, zones, crop descriptors, shop stock snapshots, supply inventories, and field states. |
| PBT-MC1-08 | PBT-08 | FsCheck execution | Keep shrinking enabled and preserve existing seed logging/reproducibility behavior. |

## PBT Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-01 | Compliant | Functional Design identifies round-trip, invariant, and idempotence properties for every U-MC-01 business-logic/data-transformation component. |
| PBT-02 | Compliant for design | Serialization round-trip property is explicitly required for code generation. |
| PBT-03 | Compliant for design | Viability, supply, store, and shift-planning invariants are explicitly required. |
| PBT-04 | Compliant for design | Season-assignment idempotence is explicitly required. |
| PBT-05 | N/A | No optimized algorithm or legacy/reference implementation is introduced at this stage. |
| PBT-06 | N/A | U-MC-01 planners are pure functions and do not manage mutable state. |
| PBT-07 | Compliant for design | Domain-specific generator obligations are identified. |
| PBT-08 | Compliant for design | Shrinking and seed reproducibility are carried forward as code-generation obligations. |
| PBT-09 | Compliant by project decision | FsCheck is already selected for C#/.NET and documented in state. |
| PBT-10 | N/A for Functional Design | Complementary example/PBT test file structure is enforced during code generation. |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops in requirements. |
| Property-Based Testing | Compliant | Full-mode design obligations are documented above; no blocking PBT finding remains. |

