# U-MC-01 NFR Design Patterns

**Unit**: U-MC-01 - Crop-plan Domain + Persistence Foundation  
**Stage**: CONSTRUCTION - NFR Design  
**Status**: Review required

## Pattern Summary

| Pattern ID | Pattern | Primary NFRs |
|---|---|---|
| P-MC1-01 | Null-object empty crop plan | REL-MC1-01, COMPAT-MC1-02, COMPAT-MC1-03 |
| P-MC1-02 | Additive nullable DTO field | COMPAT-MC1-01, COMPAT-MC1-02, PERF-MC1-02 |
| P-MC1-03 | Per-contract deserialize isolation | REL-MC1-02, SEC-MC1-03 |
| P-MC1-04 | Pure deterministic planner services | PERF-MC1-01, PERF-MC1-03, MAINT-MC1-01 |
| P-MC1-05 | Bounded snapshot input model | PERF-MC1-03, SCALE-MC1-02, MAINT-MC1-02 |
| P-MC1-06 | Explicit no-action result model | REL-MC1-04, AVAIL-MC1-02 |
| P-MC1-07 | Atomic supply gate | REL-MC1-05, TEST-MC1-02 |
| P-MC1-08 | Stable deterministic ordering | PERF-MC1-04, MAINT-MC1-05 |
| P-MC1-09 | Generator-backed PBT seam | TEST-MC1-02, TEST-MC1-03, TEST-MC1-04 |
| P-MC1-10 | Complementary examples plus properties | TEST-MC1-01, TEST-MC1-05 |

## P-MC1-01: Null-Object Empty Crop Plan

Use `CropPlan.Empty` as the domain representation for disabled Manage Crops behavior.

Design consequences:

- Missing/null persisted crop-plan DTO maps to `CropPlan.Empty`.
- Existing constructor/call-site churn is managed with an overload or factory that defaults to `CropPlan.Empty`.
- Runtime projection returns `WorkScopeSet.ManagedCrops = null` when the plan is empty.
- Code paths can test `CropPlan.IsEnabled` instead of testing for null domain values.

## P-MC1-02: Additive Nullable DTO Field

Keep the save envelope at `SchemaVersion = 3` and add an optional crop-plan field to the existing contract DTO shape.

Design consequences:

- Existing schema-3 saves remain loadable.
- Empty crop plans may be omitted by `NullValueHandling.Ignore`.
- DTO class names may remain `V2` until a broader cleanup, but the field is explicitly documented as additive schema-3 Manage Crops data.
- No multi-version reader or schema-4 migration branch is introduced in U-MC-01.

## P-MC1-03: Per-Contract Deserialize Isolation

Extend the existing serializer pattern where one malformed contract is skipped without discarding all contracts.

Design consequences:

- Crop-plan mapping exceptions are scoped to the contract being mapped.
- Warning messages identify the affected contract ID when available.
- Invalid JSON/envelope/schema failures keep current whole-payload fallback behavior.
- Unknown item IDs do not fail deserialization because item catalog validation belongs to later Mod seams.

## P-MC1-04: Pure Deterministic Planner Services

Implement each business decision as a pure Core service that consumes explicit inputs and returns explicit outputs.

Services:

- `SeasonAssignmentResolver`
- `PlantingViabilityCalculator`
- `CropSupplyPlanner`
- `StoreResolver`
- `CropShiftPlanner`
- `CropPlanSerialization`

Design consequences:

- No SMAPI references, `GameLocation`, `ShopBuilder`, `Farmer`, chests, map layers, or pathfinding in U-MC-01 planners.
- Same input values produce the same outputs.
- Each service has a narrow test surface for examples and FsCheck properties.

## P-MC1-05: Bounded Snapshot Input Model

Represent live-world facts as pure snapshots supplied by later Mod units.

Snapshot examples:

- `FieldState`
- `SupplyInventory`
- `ShopStockSnapshot`
- `CropDescriptor`
- `GameDate`

Design consequences:

- Planner cost scales with snapshot size, not total farm map size.
- U-MC-01 can be tested without the game runtime.
- Later units own snapshot construction from Stardew data and live maps.

## P-MC1-06: Explicit No-Action Result Model

Use explicit empty results and reason-bearing outcomes rather than exceptions for expected blocked conditions.

Expected blocked conditions:

- Festival purchase skip.
- No open store.
- Preferred store fallback.
- Item unavailable from stores.
- Nonviable tile.
- Insufficient paired seed/fertilizer supply.

Design consequences:

- Pure planners can continue producing supply-independent work when purchasing is blocked.
- Runtime units can convert reasons into HUD notices without re-deriving decisions.
- Exceptions remain reserved for malformed programmer inputs or invalid DTO payloads.

## P-MC1-07: Atomic Supply Gate

Centralize seed/fertilizer atomicity in `CropSupplyPlanner`.

Design consequences:

- Fertilizer-required planting capacity is always `min(seeds, fertilizer)`.
- No tile action list includes `Fertilize` or `Seed` unless both required components are available for that tile.
- The same gate is used by purchase target planning and shift action planning.
- PBT validates the invariant across generated stock levels and plan shapes.

## P-MC1-08: Stable Deterministic Ordering

Use deterministic sorting for serialized output and planner output where equivalent work items exist.

Ordering surfaces:

- DTO season keys serialized by enum-name order or explicit season order.
- Assignments sorted by stable zone description when serializer output needs deterministic text.
- Store resolution deterministic for `Either`.
- Tile action order fixed as harvest, clear debris, till, fertilize, seed, water.

Design consequences:

- Save output is stable across runs.
- PBT and example tests can assert structural output without flaky ordering.
- Code generation must document the exact `Either` store ordering.

## P-MC1-09: Generator-Backed PBT Seam

Create reusable test generators for the crop-plan domain and pure planner inputs.

Generator targets:

- `CropPlan`
- `CropZoneAssignment`
- `SeasonCropChoice`
- `CropDescriptor`
- `SupplyInventory`
- `ShopStockSnapshot`
- `FieldState`
- `ManagedCropShiftPlan` inputs

Design consequences:

- Generators constrain item IDs, seasons, tile coordinates, assignment modes, collection sizes, and stock counts to valid ranges.
- FsCheck shrinking remains enabled.
- Code Generation planning must include generator work before PBT properties.

## P-MC1-10: Complementary Examples Plus Properties

Pair broad properties with concrete examples for critical behavior.

Required example areas:

- Missing crop-plan field loads as `CropPlan.Empty`.
- Existing schema-3 save with no crop plan preserves existing contract data.
- Malformed crop-plan data skips only the affected contract.
- Seed/fertilizer partial stock completes only paired tiles.
- Festival store resolution produces no store.

Design consequences:

- PBT is not the only coverage for critical compatibility and item-safety behavior.
- Example tests serve as executable documentation for reviewers and future maintainers.

## Category Coverage

| Category | Pattern coverage |
|---|---|
| Resilience | P-MC1-01, P-MC1-03, P-MC1-06, P-MC1-07 |
| Scalability | P-MC1-04, P-MC1-05, P-MC1-08, P-MC1-09 |
| Performance | P-MC1-02, P-MC1-04, P-MC1-05, P-MC1-08 |
| Security | P-MC1-03 as defensive parsing; Security Baseline otherwise N/A |
| Logical components | Patterns map to components in `logical-components.md` |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | PBT obligations are incorporated as generator-backed and complementary-test design patterns for Code Generation. |

