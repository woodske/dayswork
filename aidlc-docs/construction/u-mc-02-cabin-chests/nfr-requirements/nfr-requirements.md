# U-MC-02 NFR Requirements

**Unit**: U-MC-02 - Cabin Chests (Input + Backfill)
**Stage**: CONSTRUCTION - NFR Requirements
**Status**: Complete

## Summary

U-MC-02 is a small live-game integration unit. The recommended NFR posture is conservative: add no new infrastructure or packages, keep the backfill idempotent and low-frequency, protect player items, and preserve existing hiring/output behavior while adding a clear input/output chest distinction.

## Scalability

| ID | Requirement | Rationale |
|---|---|---|
| NFR-MC2-SCA-01 | Support the existing one-office-per-farm model. | `HiringBuilding.OnePerFarmBuildCondition` constrains scale. |
| NFR-MC2-SCA-02 | Avoid broad farm scans outside save-load/day-start or menu-open flows. | Chest discovery and backfill should not create per-frame work. |

## Performance

| ID | Requirement | Rationale |
|---|---|---|
| NFR-MC2-PERF-01 | Backfill and naming must run at lifecycle frequency, not per tick or per draw. | Prevents avoidable SMAPI/game-loop overhead. |
| NFR-MC2-PERF-02 | `ChestResolver.GetAllChests(...)` remains menu-open scoped. | Existing design already avoids per-frame chest discovery. |
| NFR-MC2-PERF-03 | Built-in chest tile checks use direct tile/ID comparisons. | Keeps destination filtering cheap and deterministic. |

## Availability and Reliability

| ID | Requirement | Rationale |
|---|---|---|
| NFR-MC2-REL-01 | Input-chest backfill is idempotent. | Save-load/day-start repetition must not create duplicates. |
| NFR-MC2-REL-02 | Backfill and naming must not delete, replace, or clear chest contents. | Player item safety is the highest reliability concern. |
| NFR-MC2-REL-03 | Missing office or missing chest references fail narrowly. | The mod should continue running if no office exists or a chest is temporarily unavailable. |
| NFR-MC2-REL-04 | Output chest remains a stable fallback output destination. | Existing output behavior must remain available while output becomes selectable. |

## Security and Compliance

| ID | Requirement | Rationale |
|---|---|---|
| NFR-MC2-SEC-01 | No additional security controls are required. | Manage Crops security baseline is disabled; this unit has no network, auth, PII, or file-system boundary. |

## Maintainability

| ID | Requirement | Rationale |
|---|---|---|
| NFR-MC2-MAINT-01 | Isolate new behavior in a small `CabinChestService`. | Keeps `HiringBuilding`, interaction, and discovery changes understandable. |
| NFR-MC2-MAINT-02 | Keep chest IDs and display tiles centralized on `HiringBuilding`. | Prevents string/tile drift across service, interaction, and tests. |
| NFR-MC2-MAINT-03 | Use example tests for live SMAPI/Stardew API behavior. | User selected Q5=B; live APIs make meaningful PBT impractical in this unit. |

## Usability

| ID | Requirement | Rationale |
|---|---|---|
| NFR-MC2-UX-01 | Built-in chest names clearly distinguish input and output roles. | Players need to know where to put crop supplies and where output lands. |
| NFR-MC2-UX-02 | Existing bulletin-board and output-chest interaction behavior remains predictable. | Adding the input chest must not make the office harder to use. |

## PBT Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-09 | Compliant | FsCheck.Xunit remains selected and present. |
| Other PBT rules | N/A for NFR Requirements | Functional Design identified properties; Q5=B and live APIs mean code generation will emphasize examples for this unit. |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | PBT framework choice remains valid; no new PBT framework decision is required. |
