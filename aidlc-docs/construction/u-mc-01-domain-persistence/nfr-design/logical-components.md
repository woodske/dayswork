# U-MC-01 Logical Components

**Unit**: U-MC-01 - Crop-plan Domain + Persistence Foundation  
**Stage**: CONSTRUCTION - NFR Design  
**Status**: Review required

## Component Map

| Component | Layer | Responsibility | Key patterns |
|---|---|---|---|
| `CropPlan` and domain records | Core domain | Durable authored crop-plan configuration and empty-plan opt-in boundary. | P-MC1-01 |
| `CropPlanSerialization` | Core persistence mapping | Domain/DTO crop-plan mapping and additive nullable field handling. | P-MC1-02, P-MC1-08 |
| `SaveDataSerializer` extension | Core persistence shell | Per-contract mapping isolation and defensive load behavior. | P-MC1-03 |
| `SeasonAssignmentResolver` | Core planner | Multi-season assignment and lock derivation. | P-MC1-04, P-MC1-08 |
| `PlantingViabilityCalculator` | Core planner | Season-end viability and greenhouse/shed bypass. | P-MC1-04, P-MC1-05 |
| `CropSupplyPlanner` | Core planner | Purchase target calculation and atomic seed/fertilizer gate. | P-MC1-04, P-MC1-06, P-MC1-07 |
| `StoreResolver` | Core planner | Preferred/fallback/no-store decision from pure store snapshots. | P-MC1-04, P-MC1-06, P-MC1-08 |
| `CropShiftPlanner` | Core planner | Pure action plan composition and supply-independent/dependent partitioning. | P-MC1-04, P-MC1-05, P-MC1-07, P-MC1-08 |
| Manage Crops test generators | Test support | Reusable FsCheck generators for domain and planner inputs. | P-MC1-09 |
| Example test suite | Test support | Concrete compatibility and critical behavior examples. | P-MC1-10 |

## Component Responsibilities

### CropPlan and Domain Records

Responsibilities:

- Represent enabled/disabled crop management.
- Own assignments, seasonal choices, season-agnostic greenhouse/shed choices, store preference, and global clear toggles.
- Keep item IDs opaque and SMAPI-free.

NFR mapping:

- Compatibility through `CropPlan.Empty`.
- Maintainability through explicit value records and enums.
- Scalability through assignment lists rather than farm-global state.

### CropPlanSerialization

Responsibilities:

- Map `CropPlan` to and from DTO types.
- Preserve schema-3 additive behavior.
- Keep serialization deterministic where ordering matters.

NFR mapping:

- Reliability through missing/null DTO defaulting.
- Performance through simple domain/DTO mapping.
- Test rigor through DTO round-trip property testing.

### SaveDataSerializer Extension

Responsibilities:

- Add crop-plan mapping into existing contract serialization.
- Reuse existing serializer settings and warning path.
- Skip malformed contracts without aborting all contract loads.

NFR mapping:

- Resilience through per-contract isolation.
- Defensive parsing for malformed save payloads.
- Compatibility with current schema-3 reader behavior.

### SeasonAssignmentResolver

Responsibilities:

- Apply a player-authored crop choice to a season.
- Derive locked multi-season choices.
- Keep greenhouse/shed assignments season-agnostic.

NFR mapping:

- Determinism through pure inputs and outputs.
- Maintainability through a single source of lock derivation.
- PBT through idempotence and lock-span invariants.

### PlantingViabilityCalculator

Responsibilities:

- Compute first-harvest viability for open-farm seasonal planting.
- Bypass season-end checks in season-agnostic contexts.
- Use explicit growth-day inputs from `CropDescriptor`.

NFR mapping:

- Performance through constant-time date/growth calculation.
- Resilience through pure false/true outcomes rather than exceptions for ordinary nonviability.
- PBT through determinism and greenhouse bypass invariants.

### CropSupplyPlanner

Responsibilities:

- Compute how many tiles can be completed from available supplies.
- Compute purchase targets for missing auto-buyable supplies.
- Enforce the seed/fertilizer atomicity gate.

NFR mapping:

- Item safety through paired-component planning.
- Reliability under partial stock.
- PBT through `min(seeds, fertilizer)` and never-one-without-both invariants.

### StoreResolver

Responsibilities:

- Resolve store preference, fallback, closed state, and festival no-store decisions.
- Check pure stock snapshots for item availability.
- Return reason-bearing outcomes for future HUD notices.

NFR mapping:

- Resilience through explicit no-store outcomes.
- Determinism through stable fallback ordering.
- Maintainability by separating live `ShopBuilder` reads into later Mod units.

### CropShiftPlanner

Responsibilities:

- Compose viability, supply, store, and tile-action decisions into a pure shift plan.
- Partition supply-independent and supply-dependent work.
- Preserve action order for each tile.

NFR mapping:

- Performance through precomputed snapshot inputs.
- Scalability by scaling with field-state input size.
- Reliability through atomic supply and no-action outcomes.

### Manage Crops Test Generators

Responsibilities:

- Provide reusable FsCheck generators for valid domain and planner input shapes.
- Constrain item IDs, coordinate ranges, assignment counts, stock counts, and season combinations.
- Preserve shrinking and replay support.

NFR mapping:

- PBT-07 generator quality.
- PBT-08 reproducibility.
- Test maintainability across U-MC-01 and later pure-planner work.

### Example Test Suite

Responsibilities:

- Pin critical save compatibility and business behavior with concrete examples.
- Sit beside property tests without replacing them.
- Capture future shrunk counterexamples as regressions when needed.

NFR mapping:

- PBT-10 complementary testing.
- Maintainability through executable documentation.
- Reliability for critical compatibility scenarios.

## Non-Applicable Infrastructure Components

| Component type | Applicability | Rationale |
|---|---|---|
| Queue | N/A | U-MC-01 performs local pure planning, not asynchronous work dispatch. |
| Cache | N/A | No expensive live queries or repeated external lookups are owned by U-MC-01. |
| Circuit breaker | N/A | No network or external service calls. |
| Retry policy | N/A | Expected blocked conditions are represented as explicit outcomes, not retried operations. |
| Database migration engine | N/A | Persistence is an additive schema-3 JSON field handled by existing serializer mapping. |
| Background worker | N/A | Runtime execution is owned by later units; U-MC-01 only produces pure plan outputs. |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops and no security infrastructure applies. |
| Property-Based Testing | Compliant | Logical test-support components explicitly carry generator quality, shrinking, seed replay, and complementary example-test obligations. |

