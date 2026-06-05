# U-MC-02 Business Logic Model

**Unit**: U-MC-02 - Cabin Chests (Input + Backfill)
**Stage**: CONSTRUCTION - Functional Design
**Status**: Review required

## Purpose

U-MC-02 gives every farmhand office two built-in cabin chests with distinct business roles:

- **Input chest**: the managed-crop supply reservoir. Crop management draws seeds and fertilizer from this chest in later runtime units.
- **Output chest**: the default/fallback task-output deposit destination. Task output can be deposited here, and the output chest remains explicitly selectable as a task-output destination.

This unit declares the new input chest, ensures it exists for pre-existing offices, applies stable names to both office chests, and updates chest discovery so the input chest is never offered as a destination.

## Answered Design Decisions

| Decision | Answer | Design outcome |
|---|---|---|
| Input chest display tile | Q1=A | Input chest appears at `(1, 2)` on the office footprint; output remains at `(3, 2)`. |
| Backfill timing | Q2=A | Backfill runs as an idempotent ensure operation on load/day-start style lifecycle hooks. |
| Programmatic names | Q3=A | Fixed i18n-backed names are always applied to both built-in office chests. |
| Destination selectability | Q4=B + clarification | Input chest is excluded; output chest remains default/fallback and selectable. |
| Testing emphasis | Q5=B | Code generation emphasizes example tests around live Stardew building/chest behavior. PBT candidates are documented for future pure helper extraction. |

## Business Flow

1. `HiringBuilding.BuildData()` declares two `BuildingChest` entries:
   - `Bindicle.Dayswork_Input` at display tile `(1, 2)`.
   - `Bindicle.Dayswork_Output` at display tile `(3, 2)`.
2. Save load/day-start handling invokes `CabinChestService.EnsureOfficeChests(...)`.
3. For each farmhand office found on the farm:
   - Resolve the built-in input chest by ID.
   - If missing, create/backfill the input chest idempotently.
   - Resolve the output chest by ID.
   - Apply fixed i18n-backed names to both built-in chests.
4. `HiringBuildingInteraction` opens either built-in chest when the player action-clicks that chest's display tile.
5. `ChestResolver.GetAllChests(...)` excludes the input chest from selectable destination lists.
6. `ChestResolver.GetAllChests(...)` allows the output chest to appear as an explicit selectable destination.
7. Later runtime units consume these roles:
   - U-MC-05 reads seeds/fertilizer from the input chest.
   - U-MC-07 routes task output to assigned destinations or the output-chest fallback.

## Component Responsibilities

| Component | Responsibility |
|---|---|
| `HiringBuilding` | Owns built-in chest IDs, display tiles, `BuildData()` chest declarations, and direct chest lookup helpers. |
| `CabinChestService` | Ensures the input chest exists on pre-existing offices and applies programmatic names to both built-in office chests. |
| `HiringBuildingInteraction` | Opens input/output chests from their display tiles and keeps bulletin-board interaction separate. |
| `ChestResolver` | Discovers selectable chests while excluding input and retaining output as selectable. |
| `I18nHelper` | Supplies fixed player-facing names for both built-in office chests. |

## Data Flow

| Source | Data | Consumer |
|---|---|---|
| `HiringBuilding.BuildData()` | Built-in chest IDs and display tiles | Stardew building data |
| Farm building collection | Farmhand office instances | `CabinChestService` |
| Building built-in chest storage | Input/output chest objects | `CabinChestService`, `HiringBuildingInteraction`, later runtime units |
| Farm object and building chest discovery | Selectable chest entries | `ChestResolver`, hiring/zone menus |
| i18n keys | Fixed built-in chest names | `CabinChestService` |

## Error and Edge Handling

| Scenario | Handling |
|---|---|
| No farmhand office exists | Backfill and naming do nothing. |
| Input chest already exists | Ensure operation leaves it in place and reapplies the fixed name. |
| Input chest is missing on a pre-existing office | Ensure operation creates/backfills it once, then subsequent runs are no-ops except naming. |
| Output chest is missing unexpectedly | Ensure operation attempts to resolve/name it when available; missing output does not block input backfill. |
| Player moves/deletes unrelated farm chests | `ChestResolver` behavior for unrelated chests remains unchanged. |
| Input chest contains player supplies | U-MC-02 preserves contents; later units decide when to consume supplies. |

## Testable Properties

| Property | Category | Component | Code-generation obligation |
|---|---|---|---|
| Running input-chest ensure repeatedly leaves exactly one input chest for an office. | Idempotence | `CabinChestService` | Example-test with fake or controlled building state; PBT not required because answer Q5=B and live APIs dominate. |
| Input chest is excluded from selectable destination discovery. | Invariant | `ChestResolver` | Example-test discovery list with input/output/ordinary chest positions. |
| Output chest remains selectable while also acting as default/fallback. | Invariant | `ChestResolver`, `HiringBuilding` | Example-test discovery and fallback helper behavior. |
| Programmatic naming always converges to fixed i18n-backed names. | Idempotence | `CabinChestService` | Example-test repeated naming. |

## PBT Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-01 | Compliant | Testable idempotence and invariant properties are identified above. |
| PBT-02 | N/A | U-MC-02 introduces no serialization/deserialization pair. |
| PBT-03 | Compliant for design | Chest discovery and selectability invariants are identified. |
| PBT-04 | Compliant for design | Backfill and naming idempotence are identified. |
| PBT-05 | N/A | No separate oracle/reference algorithm exists. |
| PBT-06 | N/A for Functional Design | Live Stardew state makes stateful PBT impractical in this unit; code generation will use examples unless pure helpers are extracted. |
| PBT-07 | N/A for Functional Design | No generated PBT inputs are required by the approved Q5 testing emphasis. |
| PBT-08 | N/A for Functional Design | No PBT execution behavior is introduced in this stage. |
| PBT-09 | Compliant | FsCheck.Xunit is already selected and present for the project. |
| PBT-10 | Compliant for design | Business-critical paths are assigned explicit example-test obligations. |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops; no network, auth, PII, or security boundary is introduced. |
| Property-Based Testing | Compliant | PBT-01 obligations are met by identifying properties and documenting why code generation emphasizes examples for live API behavior. |
