# U-MC-01 Business Logic Model

**Unit**: U-MC-01 - Crop-plan Domain + Persistence Foundation  
**Stage**: CONSTRUCTION - Functional Design  
**Status**: Review required

## Scope

U-MC-01 creates the pure Core foundation for Manage Crops. It defines the authored crop-plan model, projects that model into runtime work scope, adds additive persistence, and implements the pure planners that later Mod units will wire to live Stardew data.

The unit intentionally does not create menus, building chests, live crop catalog reads, shop transactions, worker animation, or harvest routing. Those are owned by U-MC-02 through U-MC-07.

## Answered Design Decisions

| Question | Answer | Locked decision |
|---|---|---|
| Q1 | B | Keep save `SchemaVersion = 3`; add a nullable crop-plan DTO field to the existing contract DTO. Missing crop plan means empty/disabled plan. |
| Q2 | A | Model farm zones with `Season -> SeasonCropChoice` assignments, and model greenhouse/shed zones with one season-agnostic choice. |
| Q3 | A | Store authored `CropPlan` on `Contract`; derive `ManagedCropWorkScope` into `WorkScopeSet` for runtime planning. |
| Q4 | A | Implement the fully functional pure planners in this unit, with C-29 as a pure composition planner. |
| Q5 | A | Represent seeds, fertilizer, and crop items in Core as opaque Stardew 1.6 qualified item-ID strings. |
| Q6 | A | Carry full-mode PBT obligations for crop-plan serialization, viability, supply atomicity, season locking, store resolution, and reproducibility. |

Q1 is a deliberate source-grounded deviation from the original spec wording. The live code already writes and requires `SchemaVersion = 3`, while DTO class names still say `V2`. U-MC-01 therefore treats Manage Crops as an additive schema-3 field rather than bumping to schema 4 or pretending a V2-to-V3 migration still exists.

## Flow 1: Crop Plan Authoring Model

U-MC-03 will collect player choices, but U-MC-01 owns the model those choices write:

1. A `Contract` has a `CropPlan`.
2. An empty `CropPlan` means Manage Crops is disabled and all existing contract behavior is unchanged.
3. A `CropPlan` contains zero or more `CropZoneAssignment` entries.
4. Each assignment owns exactly one `Zone`, optional output chest override, and either:
   - farm seasonal choices keyed by `Season`; or
   - one season-agnostic choice for greenhouse-style locations.
5. Each choice references seed and optional fertilizer IDs as opaque strings. The Mod-side catalog later resolves whether an ID is valid, stocked, localized, or displayable.

## Flow 2: Season Assignment Resolution

`SeasonAssignmentResolver.ApplyChoice(...)` is pure and deterministic:

1. Validate the target assignment mode.
2. For normal farm zones, place the chosen crop in the requested season.
3. If the crop spans consecutive seasons, auto-populate the span and mark derived seasons locked.
4. Preserve a stable origin season so the UI can explain locked seasons.
5. Reapplying a different crop to the origin season replaces the prior locked span deterministically.
6. Greenhouse/shed assignments ignore seasonal span logic and store a single continuous choice.

The resolver does not read live Stardew data. It consumes a pure `CropDescriptor` supplied by the future catalog seam.

## Flow 3: Runtime Scope Projection

The authored plan and runtime scope remain separate:

1. `Contract.CropPlan` is the durable authored configuration.
2. `ContractScopeSelection` can expose managed crop zones as part of the selected scope state needed by preview and runtime.
3. `WorkScopeClassifier` projects an enabled, non-empty crop plan into `WorkScopeSet.ManagedCrops`.
4. `ManagedCropWorkScope` carries the locations and zones that future shift planning should service.
5. If the plan is empty or disabled, `ManagedCropWorkScope` is null and the existing runtime ignores Manage Crops.

This mirrors the existing split between authored scope selection and classified runtime scope.

## Flow 4: Additive Persistence

Persistence remains schema 3:

1. `SaveDataSerializer.Serialize(...)` writes `SchemaVersion = 3` as it does today.
2. Contract DTOs gain nullable `CropPlan`.
3. Domain-to-DTO mapping writes a crop-plan DTO only when the plan is non-empty, respecting `NullValueHandling.Ignore`.
4. DTO-to-domain mapping treats missing/null `CropPlan` as `CropPlan.Empty`.
5. Malformed crop-plan data causes only that contract to be skipped through the existing per-contract skip-and-warn path.
6. Unknown item IDs are not considered malformed at persistence time. They remain opaque strings and are handled by catalog/runtime validation later.

No multi-version reader is introduced in this design because Q1 selected additive schema-3 persistence.

## Flow 5: Planting Viability

`PlantingViabilityCalculator.IsViable(...)` evaluates whether a crop can mature at least once before the season ends:

1. If `seasonAgnosticLocation` is true, return true.
2. Compute days remaining using the current date and configured season length.
3. Use fertilized growth days when fertilizer is required or configured for the choice.
4. A crop is viable when its fertilized growth time fits within the remaining season days.
5. Regrow behavior does not weaken the first-harvest requirement; at least one harvest must still fit.

## Flow 6: Supply Planning

`CropSupplyPlanner` produces deterministic supply requirements:

1. Count empty viable tiles per crop/fertilizer choice.
2. Subtract valid seed and fertilizer stock already present in the input inventory model.
3. For choices requiring fertilizer, complete only `min(seedCount, fertilizerCount)`.
4. For choices without fertilizer, complete by seed count alone.
5. Never produce a tile action that lays seed without required fertilizer or fertilizer without seed.
6. Produce purchase targets only for auto-buyable seeds/fertilizer. Chest-supply-only crops never request store purchase.

## Flow 7: Store Resolution

`StoreResolver.Resolve(...)` is pure:

1. A festival day resolves to no store.
2. `Pierre` preference uses Pierre when open; if closed and Joja is open, resolves Joja with `UsingFallback = true`.
3. `Joja` preference uses Joja when open; if closed and Pierre is open, resolves Pierre with `UsingFallback = true`.
4. `Either` chooses the deterministic preferred ordering documented in business rules.
5. Store stock membership is evaluated against a supplied pure `ShopStockSnapshot`.

Live stock reading and wallet transactions are not part of U-MC-01.

## Flow 8: Crop Shift Plan Composition

`CropShiftPlanner.BuildPlan(...)` composes the pure decisions:

1. Select active assignments for today's location context and season.
2. Filter tile candidates to viable choices.
3. Partition work into supply-independent actions and supply-dependent planting actions.
4. Order each tile as harvest, clear debris, till, fertilize, seed, water.
5. Apply the seed/fertilizer atomicity gate before producing fertilize/seed actions.
6. Produce a purchase target for missing purchasable supplies.
7. Return a pure `ManagedCropShiftPlan` for future runtime units to execute.

The planner does not mutate inventory, wallet, map tiles, crops, or chests.

## Testable Properties

Functional Design identifies these PBT-01 properties for code generation:

| Component | Property category | Property |
|---|---|---|
| CropPlanSerialization | Round-trip | Valid crop plans deserialize from their serialized DTO shape unchanged. |
| PlantingViabilityCalculator | Invariant | Same inputs always produce the same result; greenhouse/shed context always bypasses season-end rejection. |
| CropSupplyPlanner | Invariant | Completable fertilizer-required tiles never exceed `min(seeds, fertilizer)` and never create one-component planting. |
| SeasonAssignmentResolver | Invariant, idempotence | Applying the same multi-season choice twice produces the same assignment and locked seasons remain inside the crop span. |
| StoreResolver | Invariant | Same preference/day/time/festival/stock input resolves the same store and fallback flag every time. |
| CropShiftPlanner | Invariant | Produced tile action lists preserve the dependency order and never include seed/fertilizer actions without the atomicity gate passing. |

## Extension Compliance

| Extension | Status | Functional Design result |
|---|---|---|
| Security Baseline | Disabled | N/A. Manage Crops requirements opted out; no security rules enforced for this stage. |
| Property-Based Testing | Enabled, full | Compliant. PBT-01 properties are identified above and are also carried into `business-rules.md`. |

