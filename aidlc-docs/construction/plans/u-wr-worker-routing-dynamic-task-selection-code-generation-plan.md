# Code Generation Plan - U-WR Worker Routing and Dynamic Task Selection

## Plan Status

- **Unit**: U-WR Worker Routing and Dynamic Task Selection
- **Stage**: Code Generation - Part 2 Generation
- **Status**: Review required after outdoor tile performance fix
- **Single source of truth**: This plan defines the complete generation sequence for U-WR.

## Planning Checklist

- [x] Load Code Generation rule details.
- [x] Load approved functional design, NFR requirements, and NFR design artifacts.
- [x] Read workspace state and code-location rules from `aidlc-docs/aidlc-state.md`.
- [x] Inspect existing routing/orchestration files to identify modification points.
- [x] Map stories and requirements to concrete code/test steps.
- [x] Validate this markdown content before file creation. No Mermaid or ASCII diagrams are included.
- [x] Create this code-generation plan.
- [x] Receive explicit approval for this plan.

## Unit Context

### Stories Implemented By This Unit

- [x] **S-08 - Execute prioritized local work across zones, buildings, and animals**
  - Shortest reachable route to valid interaction tiles.
  - Nearest reachable task inside the active broad batch.
  - Nearer animal work before farther animal work.
  - Egg/product collection from any reachable side.
  - Feed retry after enabled product collection clears paths.
  - No unpaid product collection.
- [x] **S-16 - Recover from getting stuck**
  - Temporary deferral for blocked work inside the active batch.
  - Retry after progress changes passability.
  - No-progress pass termination.
  - Defensive finite retry guard.
- [x] **S-19 - Pure logic separable from SMAPI for testability**
  - Pure route selector property tests.
  - Focused example regression tests for reported cases.
  - Existing FsCheck/xUnit stack preserved.

### Dependencies And Interfaces

- Existing broad batch plan from `ShiftPlanBuilder` remains authoritative.
- Existing task priority from `TaskPriorityOrderer` remains equal-route tie-break authority.
- Existing `WorkerMovementDriver` passability/navigation behavior is the route-cost truth.
- Existing output routing, buffering, deposit, settlement mail, stamina spending, stop conditions, and worker actor behavior are preserved.
- No database, persistence, config, GMCM, i18n, frontend, network, or infrastructure changes are planned.

### Application Code Location

Application code changes must remain in the workspace root, primarily:

- `Dayswork.Core/Shifts/`
- `Dayswork/Worker/`
- `Dayswork/Orchestration/`
- `Dayswork.Tests/UWR/`
- Existing test project file only if implementation discovers dependency drift. `FsCheck.Xunit` is already present, so no package change is expected.

Documentation changes for this stage belong only in:

- `aidlc-docs/construction/u-wr-worker-routing-dynamic-task-selection/code/`

## Generation Steps

### Step 1 - Create Pure Route Selector Model

- [x] Add a small pure selector model under `Dayswork.Core/Shifts/`, likely `WorkerRouteSelector.cs`.
- [x] Define domain-shaped evaluated candidate data containing candidate id, task kind or priority rank, stable order, route cost, and selected interaction tile.
- [x] Implement minimum-cost selection with deterministic tie-breaks: route cost, task priority rank, then stable order.
- [x] Return no selection when no reachable evaluated candidate exists.

### Step 2 - Add Selector Property Generators And Properties

- [x] Create `Dayswork.Tests/UWR/UWRPropertyGenerators.cs`.
- [x] Create `Dayswork.Tests/UWR/WorkerRouteSelectorPropertyTests.cs`.
- [x] Cover FsCheck invariants: minimum route cost wins, task-priority/stable-order tie-breaks, unreachable candidates are excluded before selection, and zero route cost can win for current-tile interaction.
- [x] Use a simple oracle in the tests rather than duplicating implementation internals.

### Step 3 - Add Route Selector Example Tests

- [x] Create `Dayswork.Tests/UWR/WorkerRouteSelectorTests.cs`.
- [x] Pin deterministic examples for nearer task wins, equal-distance priority wins, stable-order final tie-break, and no reachable candidates.

### Step 4 - Expose Movement-Aligned Route Cost

- [x] Modify `Dayswork/Worker/WorkerMovementDriver.cs`.
- [x] Add a narrow route-cost method that computes reachable path length from a source tile to a destination tile using the same passability assumptions as worker movement.
- [x] Reuse existing passability and neighbor logic so selection and movement cannot diverge.
- [x] Preserve current `StartNavigation` behavior and fallback behavior.

### Step 5 - Preserve All Valid Tile Interaction Candidates

- [x] Modify `Dayswork.Core/Shifts/WorkItem.cs` to carry optional candidate interaction/navigation tiles while keeping existing constructor call sites source-compatible.
- [x] Modify `Dayswork/Orchestration/WorkAreaScanner.cs` to enumerate all valid stand tiles for adjacent-interaction work instead of storing only the first top/right/bottom/left match.
- [x] Include the task tile itself for walkable target work when valid.
- [x] Preserve stable scan order and capability filtering.
- [x] Remove or bypass Manhattan greedy ordering where active-batch route selection will choose dynamically.

### Step 6 - Preserve All Valid Animal And Feed Interaction Candidates

- [x] Modify `Dayswork/Orchestration/AnimalTaskHandler.cs`.
- [x] Add methods that expose all currently valid navigation tiles for an animal.
- [x] Update feed hopper and trough work creation so hopper/trough `WorkItem`s carry all valid stand candidates, not only a preferred fixed side.
- [x] Preserve hopper-before-trough prerequisite semantics and existing auto-feed / no-silo behavior.

### Step 7 - Introduce Active-Batch Candidate Evaluation In Orchestrator

- [x] Modify `Dayswork/Orchestration/ShiftOrchestrator.cs`.
- [x] Convert currently queued tile work and animal work into a single active-batch candidate pool at each task boundary.
- [x] Evaluate each candidate's valid interaction tiles through the movement route-cost method.
- [x] Feed evaluated candidates into the pure selector helper.
- [x] Dispatch the selected tile or animal work through existing movement and task execution flows.

### Step 8 - Preserve Broad Batch Order While Reordering Within Batch

- [x] Keep the existing broad batch order: animal building, outdoor animals, greenhouse, outdoor crops, outdoor clearing.
- [x] Within one active batch, select nearest reachable tile, animal, hopper, or trough work.
- [x] Keep equal-distance task priority and stable-order tie-breaks.
- [x] Recompute candidates and route costs after each completed work unit or world-state change.

### Step 9 - Implement Active-Batch Deferral And Retry Guard

- [x] Add bounded pass accounting in `ShiftOrchestrator` or a tiny private helper.
- [x] Defer candidates with no reachable route, navigation failure, stale target, or missing prerequisite.
- [x] Retry deferred work after a pass with progress.
- [x] End retry after a no-progress pass.
- [x] Add a defensive finite max-pass guard derived from active candidate count or equivalent finite measure.
- [x] Log only narrow maintainer diagnostics when blocked work is skipped or the guard fires.

### Step 10 - Revalidate Targets Before Execution

- [x] Ensure selected tile work is still valid before action execution or resolves safely as stale.
- [x] Ensure selected animal work is still live and still needs the chosen task before execution.
- [x] Preserve `CollectAnimalProducts` as the authorization boundary for floor products and animal product work.
- [x] Preserve task-owned output provenance for collected products.

### Step 11 - Integrate Feed/Hopper Retry Semantics

- [x] Keep hopper collection before trough placement.
- [x] Defer blocked hopper or trough candidates instead of permanently skipping the whole feed path while progress is possible.
- [x] Allow enabled product collection to clear feed blockers naturally.
- [x] Do not collect products when `CollectAnimalProducts` is disabled, even if feed remains blocked.

### Step 12 - Update Stuck-Recovery Resume Logic

- [x] Adjust stuck recovery to work with the active-batch candidate pool instead of assuming only `_ctx.WorkList` tile queue entries.
- [x] Preserve existing one-teleport-then-home escalation behavior.
- [x] Ensure recovery resumes through route-ranked selection after teleport.

### Step 13 - Add Routing Regression Tests

- [x] Create `Dayswork.Tests/UWR/WorkerRoutingRegressionTests.cs` or equivalent focused test files.
- [x] Cover wrong-side walking with current tile or nearer side.
- [x] Cover one-side-blocked product/egg collection.
- [x] Cover near animal before far animal in active animal batch through pure/selectable seams where possible.
- [x] Cover feed blocker retry after enabled product collection clears a path through the smallest reliable seam available.
- [x] Cover disabled product collection preventing unpaid product clearing.

### Step 14 - Add Deferral Tests

- [x] Add example tests proving no-progress pass termination and retry-after-progress behavior.
- [x] Add FsCheck deferral property only if the implementation extracts a pure observable deferral helper. N/A - deferral remains integrated with live orchestrator state, so no pure observable deferral helper was extracted.
- [x] Keep PBT generator inputs domain-shaped if deferral PBT is added. N/A - no deferral PBT was added.

### Step 15 - Preserve Existing Regression Surface

- [x] Verify no duplicate source files were created.
- [x] Confirm no save schema, config, GMCM, mail, i18n, or dependency changes were introduced unless required by compilation.
- [x] Confirm existing output routing and stamina spending paths remain task-owned and labor-only.

### Step 16 - Generate Code Summary

- [x] Create `aidlc-docs/construction/u-wr-worker-routing-dynamic-task-selection/code/code-summary.md`.
- [x] List modified and created application/test files.
- [x] Summarize behavior changes, PBT coverage, example coverage, and any caveats.

### Step 17 - Run Local Verification

- [x] Run `dotnet build Dayswork.sln /p:EnableModDeploy=false`.
- [x] Run `dotnet test Dayswork.sln`.
- [x] If failures occur, fix code/tests and rerun the relevant verification.
- [x] Record verification results in `code-summary.md`.

### Step 18 - Final Code Generation State Updates

- [x] Mark all generation steps and story checkboxes complete as appropriate.
- [x] Update `aidlc-docs/aidlc-state.md` to the Code Generation review gate.
- [x] Append completion and review-prompt entries to `aidlc-docs/audit.md`.

### Step 19 - Review Feedback Outdoor Tile Performance Fix

- [x] Log play-test feedback that barn/coop routing performed well but outdoor tile work dropped framerate to 1 FPS.
- [x] Diagnose outdoor tile selection as repeated exact route searches across a much larger candidate set.
- [x] Replace per-candidate route searches with a single exact per-selection route-cost map from the worker's current tile.
- [x] Preserve shortest-route semantics, multi-side candidate scoring, and immediate recomputation at each task boundary.
- [x] Update the code summary and state documentation for the performance fix.
- [x] Re-run `dotnet build Dayswork.sln /p:EnableModDeploy=false`.
- [x] Re-run `dotnet test Dayswork.sln`.
- [x] Record review-fix verification results in `code-summary.md`, `aidlc-state.md`, and `audit.md`.

### Step 20 - Review Feedback Building Exit Walk-Out Fix

- [x] Log play-test feedback that the worker visibly warped out after building tasks instead of walking to the exit.
- [x] Trace the building completion path from interior batch completion to interior exit navigation and farm transition.
- [x] Update building exit selection to choose the nearest reachable exit approach tile from the worker's current interior position.
- [x] Preserve the existing farm transition after the worker reaches the interior exit.
- [x] Add focused tests for reachable exit approach selection.
- [x] Re-run `dotnet build Dayswork.sln /p:EnableModDeploy=false`.
- [x] Re-run `dotnet test Dayswork.sln`.
- [x] Record review-fix verification results in `code-summary.md`, `aidlc-state.md`, and `audit.md`.

### Step 21 - Review Feedback Chest Deposit Walk-To-Chest Fix

- [x] Log play-test feedback that chest-destined materials were deposited automatically without the worker traveling to the chest.
- [x] Trace the deposit trip handler and navigation failure behavior.
- [x] Change chest deposit trips to navigate to the nearest reachable stand tile adjacent to the chest instead of the occupied chest tile.
- [x] Prevent deposit navigation failure from transferring items automatically.
- [x] Add focused tests for chest deposit stand-tile selection.
- [x] Re-run `dotnet build Dayswork.sln /p:EnableModDeploy=false`.
- [x] Re-run `dotnet test Dayswork.sln`.
- [x] Record review-fix verification results in `code-summary.md`, `aidlc-state.md`, and `audit.md`.

## Non-Applicable Generation Areas

- **Project structure setup**: N/A - brownfield multi-project solution already exists.
- **API layer**: N/A - no external/customer API.
- **Repository/database layer**: N/A - no persistence or schema change.
- **Frontend/UI components**: N/A - no UI, GMCM, menu, or localization change.
- **Deployment artifacts**: N/A - no deployment or infrastructure artifacts for this local SMAPI runtime patch.

## PBT Compliance Plan

| Rule | Code-generation handling |
|---|---|
| PBT-01 | Carry functional-design properties into selector and optional deferral tests. |
| PBT-03 | Add FsCheck selector invariant tests. |
| PBT-05 | Use simple oracle comparison for selector properties. |
| PBT-06 | Add stateful/sequence-style deferral PBT only if a pure observable deferral helper is extracted. |
| PBT-07 | Use `UWRPropertyGenerators` for domain-shaped route candidate inputs. |
| PBT-08 | Preserve FsCheck shrinking/replay behavior; Build/Test stage will document seed logging. |
| PBT-09 | Reuse existing `FsCheck.Xunit` dependency. |
| PBT-10 | Add example regression tests alongside property tests. |

## Approval Gate

Code Generation Part 2 must not begin until this plan is explicitly approved.
