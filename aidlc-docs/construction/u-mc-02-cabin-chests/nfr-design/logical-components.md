# U-MC-02 Logical Components

**Unit**: U-MC-02 - Cabin Chests (Input + Backfill)
**Stage**: CONSTRUCTION - NFR Design
**Status**: Complete

## Component Map

| Component | Layer | NFR responsibility |
|---|---|---|
| `HiringBuilding` | Mod integration | Centralizes chest IDs, display tiles, and `BuildData()` declarations. |
| `CabinChestService` | Mod service | Encapsulates idempotent ensure, naming, input/output role lookup, and tile role checks. |
| `HiringBuildingInteraction` | Mod input adapter | Opens input/output chests from stable display tiles and preserves bulletin-board behavior. |
| `ChestResolver` | Mod discovery adapter | Excludes input chest and includes output chest during selectable destination discovery. |
| `ModEntry` | Composition root | Constructs and registers `CabinChestService` with low-frequency lifecycle hooks. |
| `i18n/default.json` | Localization | Adds fixed input/output chest labels. |
| Tests | Test project | Adds example coverage for declaration, interaction, naming, backfill, and discovery behavior. |

## Logical Flow

1. `ModEntry` constructs `CabinChestService`.
2. `ModEntry` registers ensure/name work on save-load/day-start style events.
3. `HiringBuilding.BuildData()` declares input and output built-in chest entries.
4. `CabinChestService` ensures input chest availability and labels both built-in chests.
5. `HiringBuildingInteraction` dispatches action-clicks to the correct built-in chest.
6. `ChestResolver` filters the input chest from selectable destinations and includes output chest.

## Implementation Constraints

| Constraint | Design implication |
|---|---|
| Application code stays outside `aidlc-docs/` | Production changes go under `Dayswork/`; tests under `Dayswork.Tests/`. |
| Brownfield files are modified in place | Do not create duplicate `HiringBuilding_new.cs` or `ChestResolver_modified.cs`. |
| No new dependencies | Use existing SMAPI/StardewValley and xUnit references. |
| No per-frame scans | Do not register backfill/naming on update/render events. |
| Item safety | Do not clear or replace chest inventories during ensure/name operations. |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | Components preserve identified properties; example testing remains the approved implementation strategy. |
