# U-MC-01 NFR Requirements

**Unit**: U-MC-01 - Crop-plan Domain + Persistence Foundation  
**Stage**: CONSTRUCTION - NFR Requirements  
**Status**: Review required

## Scope

U-MC-01 is a pure Core foundation unit. Its NFRs govern domain records, additive persistence mapping, deterministic planners, and property-based test obligations. It does not own menus, live map scanning, chests, shop transactions, worker animation, town navigation, or live SMAPI runtime behavior.

## Performance

| ID | Requirement |
|---|---|
| PERF-MC1-01 | Pure planner operations must be deterministic in-memory operations over bounded contract data, field-state snapshots, and shop-stock snapshots. |
| PERF-MC1-02 | Crop-plan serialization and deserialization must remain lightweight enough to run on the existing save/load path without live game queries or graph/pathfinding work. |
| PERF-MC1-03 | `CropShiftPlanner.BuildPlan(...)` must avoid live map traversal and pathfinding; it consumes precomputed pure field-state inputs and emits an execution plan. |
| PERF-MC1-04 | Season assignment and store resolution must be O(number of assignments/seasons/stores) with no allocation-heavy global scans. |

## Scalability

| ID | Requirement |
|---|---|
| SCALE-MC1-01 | The model must support multiple crop-zone assignments per contract, including non-contiguous zones with identical plans. |
| SCALE-MC1-02 | Planner and serializer behavior must scale with the number of assignments and tile candidates, not with total farm map size. |
| SCALE-MC1-03 | Generated PBT inputs must cover empty, small, and larger valid crop-plan shapes without relying on unbounded random strings or unbounded collections. |

## Reliability and Resilience

| ID | Requirement |
|---|---|
| REL-MC1-01 | Missing or null persisted crop-plan data must map to `CropPlan.Empty`, preserving existing saves and opt-in behavior. |
| REL-MC1-02 | Malformed crop-plan data must use the existing per-contract skip-and-warn path rather than aborting all contract loading. |
| REL-MC1-03 | Opaque item IDs must round-trip without catalog validation; unknown IDs are handled later by Mod boundary validation, not by persistence. |
| REL-MC1-04 | Pure planners must return explicit no-store/no-purchase/no-action outcomes for festivals, unavailable store stock, insufficient supply, or nonviable tiles instead of throwing. |
| REL-MC1-05 | Supply planning must preserve seed/fertilizer atomicity under partial inventory and purchase-target inputs. |

## Compatibility

| ID | Requirement |
|---|---|
| COMPAT-MC1-01 | The save envelope remains `SchemaVersion = 3` per approved Functional Design Q1=B. |
| COMPAT-MC1-02 | The crop-plan DTO field is additive and nullable so existing schema-3 saves load as disabled Manage Crops plans. |
| COMPAT-MC1-03 | Empty crop plans must not alter existing hiring, scheduling, pricing, scope, or runtime behavior. |
| COMPAT-MC1-04 | Core types must remain SMAPI-free and compatible with the existing `Dayswork.Core` dependency boundary. |

## Maintainability

| ID | Requirement |
|---|---|
| MAINT-MC1-01 | Keep all U-MC-01 business decisions in pure Core components: `CropPlan`, `SeasonAssignmentResolver`, `PlantingViabilityCalculator`, `CropSupplyPlanner`, `StoreResolver`, `CropShiftPlanner`, and crop-plan DTO mapping. |
| MAINT-MC1-02 | Keep live Stardew concerns out of U-MC-01: catalog construction, `Data/Crops`, `ShopBuilder`, wallet mutation, map `Diggable`, chest access, and worker animation belong to later Mod units. |
| MAINT-MC1-03 | Preserve clear ownership between authored configuration (`Contract.CropPlan`) and runtime projection (`WorkScopeSet.ManagedCrops`). |
| MAINT-MC1-04 | Prefer small value records and explicit enums over dictionary string conventions except where serializing enum names or opaque Stardew qualified item IDs. |
| MAINT-MC1-05 | Use existing project patterns: nullable enabled, warnings as errors, sorted deterministic DTO output, and per-contract exception isolation in `SaveDataSerializer`. |

## Security and Privacy

| ID | Requirement |
|---|---|
| SEC-MC1-01 | Security Baseline is disabled for Manage Crops; no Security Baseline checks are blocking in this stage. |
| SEC-MC1-02 | U-MC-01 introduces no network I/O, authentication, authorization, secrets, PII, or external process execution. |
| SEC-MC1-03 | Save parsing remains defensive: invalid JSON/schema/contract payloads must not crash the mod. |

## Availability

| ID | Requirement |
|---|---|
| AVAIL-MC1-01 | U-MC-01 has no service uptime or failover requirement because it is local single-player mod logic. |
| AVAIL-MC1-02 | Failure handling focuses on local resilience: bad save data should degrade to skipped contracts or empty crop plans according to existing serializer behavior. |

## Test Rigor

| ID | Requirement |
|---|---|
| TEST-MC1-01 | Code generation must include example-based tests for missing crop-plan defaulting, additive schema-3 save load, malformed crop-plan contract skip, and critical planner scenarios. |
| TEST-MC1-02 | Code generation must include FsCheck properties for crop-plan DTO round-trip, viability determinism and greenhouse bypass, seed/fertilizer atomicity, multi-season lock idempotence, store/fallback determinism, and tile-action ordering. |
| TEST-MC1-03 | PBT generators must be domain-specific and reusable for crop plans, assignments, choices, descriptors, supply inventories, shop stock snapshots, field states, and tile actions. |
| TEST-MC1-04 | FsCheck shrinking and seed reproducibility must remain enabled; failures must expose replayable seeds through the existing xUnit/FsCheck output path. |
| TEST-MC1-05 | PBT must complement example tests; it must not be the only coverage for save compatibility and supply atomicity. |

## PBT Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-01 | Previously satisfied | Functional Design artifacts identify properties and carry them forward. |
| PBT-02 | Required for code generation | Crop-plan DTO/domain round-trip must be property-tested. |
| PBT-03 | Required for code generation | Viability, supply, store, and shift-planning invariants must be property-tested. |
| PBT-04 | Required for code generation | Season assignment idempotence must be property-tested. |
| PBT-05 | N/A | No optimized algorithm or reference implementation is introduced. |
| PBT-06 | N/A | U-MC-01 components are pure functions, not mutable state machines. |
| PBT-07 | Required for code generation | Domain-specific generators are mandatory for all PBT inputs. |
| PBT-08 | Required for code generation/build-test | FsCheck shrinking and seed reproducibility must remain enabled. |
| PBT-09 | Compliant | FsCheck.Xunit is selected and present in `Dayswork.Tests.csproj`. |
| PBT-10 | Required for code generation | Example tests must accompany PBT for critical paths. |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | Full-mode rules are evaluated; PBT-09 is satisfied at NFR Requirements and downstream obligations are explicit. |

