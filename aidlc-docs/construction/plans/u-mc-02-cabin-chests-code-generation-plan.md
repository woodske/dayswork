# Code Generation Plan - U-MC-02 Cabin Chests

**Unit**: U-MC-02 - Cabin Chests (Input + Backfill)
**Stage**: CONSTRUCTION - Code Generation Part 1 (Planning)
**Status**: Approved; Part 2 generation in progress
**This plan is the single source of truth for U-MC-02 Code Generation Part 2.**

## Planning Checklist

- [x] Load Code Generation rule details.
- [x] Load U-MC-02 Functional Design artifacts.
- [x] Load U-MC-02 NFR Requirements and NFR Design artifacts.
- [x] Load unit-of-work and story-map context.
- [x] Inspect existing `HiringBuilding`, `HiringBuildingInteraction`, `ChestResolver`, `ModEntry`, i18n, and integration tests.
- [x] Determine application-code and test-code paths.
- [x] Create this executable code generation plan.
- [x] Log the approval prompt in `aidlc-docs/audit.md`.
- [x] Receive explicit approval before changing application code.

## Unit Context

**Stories implemented by this unit**

| Story | U-MC-02 responsibility |
|---|---|
| S-31 | Add the farmhand office input chest, preserve output chest as default/selectable output destination, and keep chest roles clear. |
| S-34 | Ensure pre-existing offices gain the input chest through idempotent backfill. |

**Dependencies**

- U-MC-01 domain/persistence foundation is complete.
- U-MC-02 does not depend on U-MC-03..U-MC-07.
- Later U-MC-05 and U-MC-07 depend on the input/output chest identities and role helpers created here.

**Application code roots**

- Mod production code: `Dayswork/`
- Tests: `Dayswork.Tests/`
- Documentation summary only: `aidlc-docs/construction/u-mc-02-cabin-chests/code/`

**Brownfield modification rules**

- Modify existing files in place where they already own behavior.
- Create new files only for the new `CabinChestService` and focused tests if needed.
- Do not create duplicate files such as `HiringBuilding_new.cs` or `ChestResolver_modified.cs`.

## Expected Application Files

### New Mod files

- `Dayswork/Integration/CabinChestService.cs`

### Modified Mod files

- `Dayswork/Integration/HiringBuilding.cs`
- `Dayswork/Integration/HiringBuildingInteraction.cs`
- `Dayswork/Integration/ChestResolver.cs`
- `Dayswork/ModEntry.cs`
- `Dayswork/i18n/default.json`

### New or modified test files

- `Dayswork.Tests/Integration/HiringBuildingTests.cs`
- `Dayswork.Tests/Integration/CabinChestServiceTests.cs`
- `Dayswork.Tests/Integration/ChestResolverTests.cs`

## Generation Steps

- [x] **Step 1 - HiringBuilding chest identities and declarations**  
  Add `InputChestId`, `InputChestDisplayTile`, and input-chest lookup/tile helpers to `HiringBuilding.cs`. Modify `BuildData()` to declare both `Bindicle.Dayswork_Input` at `(1, 2)` and `Bindicle.Dayswork_Output` at `(3, 2)`.

- [x] **Step 2 - CabinChestService creation**  
  Create `CabinChestService.cs` with idempotent office-chest ensure, input/output role lookup helpers, built-in chest tile classification, and programmatic naming methods.

- [x] **Step 3 - Lifecycle wiring**  
  Modify `ModEntry.cs` to instantiate `CabinChestService` and register low-frequency save-load/day-start style ensure/name hooks. Do not register per-frame work.

- [x] **Step 4 - Interaction wiring**  
  Modify `HiringBuildingInteraction.cs` so action-clicking the input chest display tile opens the input chest, output tile opens output chest, and bulletin-board behavior remains unchanged.

- [x] **Step 5 - ChestResolver selectability update**  
  Modify `ChestResolver.cs` to exclude the input built-in office chest from selectable destinations while including the output built-in office chest and ordinary chests.

- [x] **Step 6 - i18n labels**  
  Add fixed i18n-backed labels for the farmhand cabin input and output chests to `Dayswork/i18n/default.json`.

- [x] **Step 7 - HiringBuilding tests**  
  Extend `HiringBuildingTests.cs` to assert both chest IDs, display tiles, distinct in-footprint locations, and `BuildData()` declarations.

- [x] **Step 8 - CabinChestService tests**  
  Add example tests for idempotent ensure behavior where feasible, programmatic naming convergence, and role/tile helper behavior. If direct live chest construction is constrained by Stardew APIs, pin the pure helper behavior and document remaining manual verification in the code summary.

- [x] **Step 9 - ChestResolver tests**  
  Add example tests or focused helper-level assertions proving input chest exclusion and output chest inclusion. Preserve ordinary chest discovery expectations where feasible.

- [x] **Step 10 - Build and test verification**  
  Run `dotnet build Dayswork.sln /p:EnableModDeploy=false` and `dotnet test Dayswork.sln /p:EnableModDeploy=false`. If failures occur, fix within the approved U-MC-02 scope and rerun.

- [x] **Step 11 - Duplicate-file and scope verification**  
  Verify no duplicate brownfield files were created, no application code was placed under `aidlc-docs/`, no new package dependency was added, and backfill/naming is not wired to per-frame events.

- [x] **Step 12 - Code summary and workflow updates**  
  Create `aidlc-docs/construction/u-mc-02-cabin-chests/code/code-summary.md`, update this plan's checkboxes as each step completes, update `aidlc-state.md`, and log completion in `audit.md`.

## Story Traceability

| Story | Plan steps |
|---|---|
| S-31 | Steps 1, 2, 4, 5, 6, 7, 8, 9 |
| S-34 | Steps 2, 3, 8, 10 |

## NFR Traceability

| NFR | Plan coverage |
|---|---|
| NFR-MC2-PERF-01 | Step 3 avoids per-frame wiring; Step 11 verifies. |
| NFR-MC2-REL-01 | Step 2 implements idempotent ensure; Step 8 tests. |
| NFR-MC2-REL-02 | Step 2 avoids clearing/replacing contents; Step 8 tests or documents API constraints. |
| NFR-MC2-MAINT-01 | Step 2 isolates service behavior. |
| NFR-MC2-UX-01 | Step 6 adds fixed role labels. |

## PBT and Extension Compliance

| Extension | Status | Plan coverage |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops; no security surface in this unit. |
| Property-Based Testing | Compliant for planning | Q5=B selected example-focused tests for live Stardew APIs. Functional Design identified properties; Steps 7-9 pin them with examples. No new PBT generator or package is planned. |

## Out of Scope for U-MC-02

- Reading seeds/fertilizer from the input chest during shifts.
- Returning purchased leftovers to the input chest.
- Per-zone harvest output routing.
- Manage Crops authoring UI.
- Town shopping.
- Greenhouse/SVE shed crop behavior.

## Approval Gate

Code Generation Part 2 must not begin until this plan is explicitly approved.
