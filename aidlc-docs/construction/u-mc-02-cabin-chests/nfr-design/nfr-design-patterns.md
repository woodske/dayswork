# U-MC-02 NFR Design Patterns

**Unit**: U-MC-02 - Cabin Chests (Input + Backfill)
**Stage**: CONSTRUCTION - NFR Design
**Status**: Complete

## Pattern Summary

U-MC-02 uses small, local design patterns rather than infrastructure patterns. The goal is to make a live-game state change safe, predictable, and cheap.

## Resilience Patterns

| Pattern | Application |
|---|---|
| Idempotent ensure | `CabinChestService.EnsureOfficeChests(...)` can run repeatedly and converges to one input chest per office. |
| Narrow failure skip | Missing farmhand office or temporarily missing chest skips only that operation. |
| Preserve existing contents | Backfill/naming never clears or replaces chest contents. |

## Performance Patterns

| Pattern | Application |
|---|---|
| Lifecycle-scoped work | Backfill/naming runs during save-load/day-start style hooks, not per frame. |
| Menu-open discovery | `ChestResolver.GetAllChests(...)` remains scoped to menu opening and destination picker refreshes. |
| Constant-time role checks | Built-in chest role filtering uses centralized ID/tile checks rather than scans through arbitrary state. |

## Maintainability Patterns

| Pattern | Application |
|---|---|
| Centralized constants | `HiringBuilding` owns `InputChestId`, `OutputChestId`, and display tiles. |
| Service encapsulation | `CabinChestService` owns backfill, naming, and built-in role queries. |
| Brownfield extension | Existing `HiringBuilding`, `HiringBuildingInteraction`, and `ChestResolver` are modified in place. |

## Usability Patterns

| Pattern | Application |
|---|---|
| Clear labels | Add fixed i18n labels for input and output chest roles. |
| Stable interaction zones | Input chest gets its own porch tile; output chest remains where players expect it. |
| Explicit output destination | Output chest remains both default/fallback and selectable. |

## Security Patterns

| Pattern | Application |
|---|---|
| N/A | No network/auth/PII/security boundary; Security Baseline disabled for Manage Crops. |

## PBT Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-01 | Satisfied by Functional Design | Testable properties are already identified. |
| PBT-02 through PBT-08 | N/A for NFR Design | U-MC-02 has no generated PBT obligation under Q5=B. |
| PBT-09 | Compliant | FsCheck.Xunit remains available. |
| PBT-10 | Compliant for design | Example-test strategy remains documented. |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | NFR patterns preserve identified idempotence/invariant properties and keep examples as the chosen test vehicle. |
