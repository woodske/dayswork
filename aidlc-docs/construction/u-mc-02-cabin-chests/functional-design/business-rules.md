# U-MC-02 Business Rules

**Unit**: U-MC-02 - Cabin Chests (Input + Backfill)
**Stage**: CONSTRUCTION - Functional Design
**Status**: Review required

## Chest Identity Rules

| Rule | Requirement | Design rule |
|---|---|---|
| BR-MC2-01 | FR-MC-33, S-31 | The farmhand office has exactly two built-in chest identities: `Bindicle.Dayswork_Input` and `Bindicle.Dayswork_Output`. |
| BR-MC2-02 | FR-MC-33 | The input chest display tile is `(1, 2)`. |
| BR-MC2-03 | Existing behavior | The output chest remains at display tile `(3, 2)`. |
| BR-MC2-04 | User clarification | The input chest is the managed-crop supply source for seeds and fertilizer. |
| BR-MC2-05 | User clarification | The output chest is the task-output deposit chest and remains the default/fallback destination. |

## Declaration and Backfill Rules

| Rule | Requirement | Design rule |
|---|---|---|
| BR-MC2-06 | FR-MC-33 | `HiringBuilding.BuildData()` declares both built-in chest entries in `BuildingData.Chests`. |
| BR-MC2-07 | FR-MC-39, S-34 | Backfill runs as an idempotent ensure operation for pre-existing offices. |
| BR-MC2-08 | FR-MC-39 | Running backfill repeatedly must not create duplicate input chests. |
| BR-MC2-09 | NFR-MC-06 | Backfill must not remove or replace existing chest contents. |
| BR-MC2-10 | S-34 | If the input chest already exists, the ensure operation leaves it in place. |

## Naming Rules

| Rule | Requirement | Design rule |
|---|---|---|
| BR-MC2-11 | FR-MC-36 | Both built-in office chests receive fixed i18n-backed names. |
| BR-MC2-12 | Q3=A | Naming is programmatic and always reapplied, overriding generic/default names. |
| BR-MC2-13 | Existing behavior | Ordinary player chests keep existing `ChestResolver.GetDisplayName` behavior. |

## Selectability and Deposit Rules

| Rule | Requirement | Design rule |
|---|---|---|
| BR-MC2-14 | Q4=B, clarification | The input chest is excluded from selectable destination lists. |
| BR-MC2-15 | Q4=B, clarification | The output chest is included in selectable destination lists and remains the default/fallback output destination. |
| BR-MC2-16 | User clarification | Crop management must draw supplies from the input chest, not from the output chest. |
| BR-MC2-17 | User clarification | Task output can be deposited to the output chest. |
| BR-MC2-18 | Scope control | U-MC-02 defines the chest identities/roles; U-MC-05 and U-MC-07 perform runtime supply reads and output routing. |

## Interaction Rules

| Rule | Requirement | Design rule |
|---|---|---|
| BR-MC2-19 | S-31 | Action-clicking the input chest display tile opens the input chest. |
| BR-MC2-20 | Existing behavior | Action-clicking the output chest display tile opens the output chest. |
| BR-MC2-21 | Existing behavior | Bulletin board tiles continue opening the hire/manage flow. |
| BR-MC2-22 | Existing behavior | Non-interactive office footprint tiles remain non-interactive. |

## Safety Rules

| Rule | Requirement | Design rule |
|---|---|---|
| BR-MC2-23 | NFR-MC-03 | Backfill and naming must not delete items. |
| BR-MC2-24 | NFR-MC-03 | If a chest cannot be resolved, the service skips that specific operation rather than failing the save/load flow. |
| BR-MC2-25 | Brownfield compatibility | Existing saves with only the output chest gain the input chest without requiring player action. |

## Test Obligations

| Rule | Test type | Required coverage |
|---|---|---|
| BR-MC2-T01 | Example | `BuildData()` declares both chest IDs at the expected tiles. |
| BR-MC2-T02 | Example | Backfill is idempotent and preserves existing chest content. |
| BR-MC2-T03 | Example | Programmatic names are reapplied to both built-in chests. |
| BR-MC2-T04 | Example | `ChestResolver` excludes input chest but includes output chest and ordinary chests. |
| BR-MC2-T05 | Example | Interaction opens input/output chests from their display tiles without breaking bulletin-board behavior. |

## PBT Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-01 | Compliant | Idempotence and invariant properties are identified in the test obligations. |
| PBT-02 | N/A | No round-trip transformation is introduced. |
| PBT-03 | Compliant for design | Discovery/selectability invariants are documented. |
| PBT-04 | Compliant for design | Backfill and naming idempotence are documented. |
| PBT-05 | N/A | No oracle/reference model is needed. |
| PBT-06 | N/A | Live game state dominates this unit; examples are the approved testing emphasis. |
| PBT-07 | N/A | No property generator requirement is created at Functional Design. |
| PBT-08 | N/A | No PBT execution is introduced at Functional Design. |
| PBT-09 | Compliant | FsCheck.Xunit remains the selected framework. |
| PBT-10 | Compliant | Critical behavior is assigned example-based tests. |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | Functional Design identifies properties and explains the live-API example-test emphasis selected by Q5=B. |
