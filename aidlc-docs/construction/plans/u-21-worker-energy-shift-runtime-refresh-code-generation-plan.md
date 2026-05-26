# U-21 — Worker Energy + Shift Runtime Refresh: Code Generation Plan

**Unit**: U-21 — Worker Energy + Shift Runtime Refresh  
**Phase**: CONSTRUCTION — Code Generation (Part 1 — Planning)  
**Builds on**: approved Functional Design, approved NFR Requirements, and approved NFR Design for a visible worker stamina model, per-beat energy spending, work-unit boundary completion at zero stamina or 8pm, unified stop-and-settle behavior with no refund/debt settlement, and slower readable worker pacing driven by explicit runtime seams.

> **This plan is the single source of truth for U-21 Code Generation.** Part 2 executes these steps in order, checking each box on completion. No code or test work should happen outside these steps.

---

## Stories & Traceability

| Story / Requirement | Coverage in U-21 |
|---|---|
| **S-07** | Makes the worker visibly stamina-limited and slower/more readable during the day-one shift. |
| **S-08** | Retrofits the runtime loop to spend stamina on actual labor beats while preserving the broad existing priority feel until U-22 scope alignment. |
| **S-09** | Preserves tool-capability snapshot behavior while shifting runtime billing/energy away from hourly settlement. |
| **S-10** | Keeps deposit/output safety intact at shift end while removing refund settlement from the active runtime model. |
| **S-15** | Preserves the current sleep-stop operational shape, but routes it through the no-refund energy-era stop path. |
| **S-16** | Ensures stuck recovery still stops the shift safely under the new stamina/stop model. |
| **S-17** | Preserves worker invulnerability and resume behavior through the runtime rewrite. |
| **S-19** *(supporting)* | Introduces new pure Core seams for energy arithmetic, work-unit boundaries, and stop-decision determinism. |

**Definition of Done** (from [unit-of-work.md](/C:/Users/kwood/Repos/dayswork/aidlc-docs/inception/application-design/unit-of-work.md)):
- Worker energy is spent per work action, never per movement.
- Energy clamps at zero and no new work unit starts when stamina is zero.
- If stamina reaches zero during a work unit, the worker finishes that visible unit, then deposits materials and exits.
- The worker exposes visible stamina in-world and moves/works at a slower, readable cadence.
- Shift-end runtime no longer computes refund/billing settlement.

---

## Project Context & Execution Boundaries

- **Workspace root**: `C:\Users\kwood\Repos\dayswork`
- **Solution shape**: existing multi-project mod solution with `Dayswork.Core`, `Dayswork`, and `Dayswork.Tests`
- **Brownfield retrofit rule**: all target files are modified in place or created fresh in the existing structure; never create duplicate `*_new.cs` or `*_modified.cs` files
- **Layer mapping for this unit**:
  - **Pure Core runtime seams**: `Dayswork.Core/Energy/`, `Dayswork.Core/Shifts/`
  - **Runtime shell / SMAPI orchestration**: `Dayswork/Orchestration/`, `Dayswork/Worker/`, `Dayswork/ModEntry.cs`
  - **Supporting day-start / lifecycle callers**: `Dayswork/Orchestration/RecurringContractScheduler.cs`, `Dayswork/Orchestration/CalendarHandlers.cs`
  - **Tests**: `Dayswork.Tests/` with a focused U-21 area plus shared generators/helpers updated in place
  - **Documentation**: `aidlc-docs/construction/u-21-worker-energy-shift-runtime-refresh/code/`
  - **API / Infra / Deployment artifacts**: N/A in this unit

### Explicit retrofit constraints for U-21

1. **U-21 is the runtime energy/pacing switchover, not the typed-scope runtime alignment unit.**  
   Do not reinvent outdoor/animal/greenhouse targeting rules here beyond what is necessary to preserve the current behavior safely. Deeper scope-driven execution changes belong to `U-22`.

2. **Active runtime semantics must stop depending on hourly settlement.**  
   `Contract` compatibility fields can remain for persistence and transitional consumers, but `ShiftContext`, `ShiftOrchestrator`, and stop paths must no longer compute refunds or billable hours.

3. **Deposit/output safety remains mandatory.**  
   The worker must still preserve collected items and use the existing output/deposit pipeline on normal completion, zero stamina, 8pm stop, sleep-stop, and stuck abort.

4. **Movement does not spend stamina.**  
   Only actual labor beats may debit stamina, and the worker must be allowed to finish the already-started visible work unit even after the bar reaches zero.

5. **Readable pacing must be explicit and configurable.**  
   Slower behavior should come from named movement/action cadence controls, not from hidden sleeps or scattered magic delays.

6. **Existing safety behaviors must survive the retrofit.**  
   Tool snapshotting, invulnerability, stuck recovery, and overnight cleanup remain part of the supported runtime contract.

---

# PART 1 — PLANNING (this document)

Steps 1–23 below. Approval of this plan authorizes Part 2 (execution).

---

# PART 2 — GENERATION STEPS

## Phase A — Pure Core energy and stop-decision foundation

- [x] **Step 1 — Retrofit the Core shift model away from hourly settlement ownership.** Update `Dayswork.Core/Shifts/ShiftContext.cs` and nearby shift types so runtime state is anchored in authoritative contract terms / energy state / stop metadata instead of `DepositAmount`, `HourlyRate`, and refund calculation. Remove only the active-runtime refund semantics that U-21 supersedes, while preserving any compatibility surface still needed outside the live shift loop. *S-10, S-15, S-19*

- [x] **Step 2 — Add explicit runtime worker-energy state and ledger seams.** Create or expand the pure Core types needed to represent current stamina and apply per-beat energy spending, such as `WorkerEnergyState` and `WorkerEnergyLedger`, using `WorkerEnergyProfile` / `WorkActionKind` as the authoritative source. Clamp stamina at zero and keep arithmetic deterministic. *S-07, S-08, S-19*

- [x] **Step 3 — Add a pure work-unit boundary decision helper.** Introduce the narrow Core seam that decides whether a labor beat starts a new unit, advances an in-progress unit, or completes the current visible unit so zero-stamina and 8pm stop behavior can remain deterministic and never abort a unit mid-resolution. *S-08, S-10, S-15, S-19*

- [x] **Step 4 — Expand the shift-state model for unified stop reasons.** Update `Dayswork.Core/Shifts/ShiftStateMachine.cs` and adjacent shift enums/contracts only as needed so normal completion, zero stamina, 8pm, sleep-stop, and stuck abort can converge into one consistent stop-and-settle path without refund/debt branches. *S-10, S-15, S-16, S-19*

- [x] **Step 5 — Add focused Core tests for the new stamina and stop seams.** Create or update pure tests covering stamina clamping, action-cost application, boundary completion at zero stamina, deterministic stop transitions, and removal of refund-era assumptions from active runtime state. *S-07, S-08, S-10, S-15, S-19*

## Phase B — Orchestrator start-up and per-beat stamina integration

- [x] **Step 6 — Refactor live shift start-up around saved contract terms.** Update `Dayswork/Orchestration/ShiftOrchestrator.cs` so shift start consumes the authoritative `ContractTermsSnapshot.Energy` profile (or an equivalent U-21-ready carrier) instead of `dayDeposit` / `dayRate` inputs, and update all call sites accordingly. *S-07, S-10, S-19*

- [x] **Step 7 — Remove hourly/refund assumptions from runtime callers and logs.** Update `RecurringContractScheduler`, `CalendarHandlers`, `ModEntry`, and any other active runtime callers/logging so they no longer treat live worker runtime as deposit/rate/refund-driven, while leaving compatibility data intact where later units still need it. *S-10, S-15*

- [x] **Step 8 — Spend stamina only on actual labor beats in the orchestrator.** Thread the new ledger through `ShiftOrchestrator` so watering, harvesting, animal interactions, and tool swings debit the correct action costs, while navigation, deposit trips, and non-labor bookkeeping do not spend stamina. *S-07, S-08, S-10*

- [x] **Step 9 — Enforce “finish current unit, then stop” for zero stamina and 8pm.** Retrofit the orchestrator’s live task execution so stamina reaching zero, or the 8pm cap landing mid-unit, still lets the current visible unit resolve before the worker transitions into deposit-and-exit. No new unit may begin when stamina is already zero. *S-08, S-10, S-15*

- [x] **Step 10 — Converge runtime wrap-up on one no-refund stop-and-settle path.** Rework normal completion, zero-stamina exhaustion, 8pm stop, sleep-stop, and stuck abort to share the same output-safe wrap-up path, excluding refund/debt settlement while preserving item-buffer and deposit behavior. *S-10, S-15, S-16*

- [x] **Step 11 — Preserve existing capability snapshotting through the runtime rewrite.** Ensure tool-tier snapshot evaluation still happens once at shift start and remains the rule used when deciding whether tasks can proceed, even though runtime pricing/billing semantics have changed. *S-09*

## Phase C — Visible stamina and slower worker feel

- [x] **Step 12 — Add explicit pacing carriers for movement and labor cadence.** Create or expand the runtime types needed for slower readable worker motion and action timing, such as a `WorkerPacingProfile`, keeping values config-driven and central rather than scattered across arbitrary delays. *S-07, S-08*

- [x] **Step 13 — Retrofit movement and action execution to use the pacing profile.** Update `Dayswork/Worker/WorkerMovementDriver.cs`, `Dayswork/Worker/ToolSwapAnimator.cs`, and the relevant `ShiftOrchestrator` timing hooks so walking and task beats are visibly slower/readable without breaking location traversal or tool visuals. *S-07, S-08*

- [x] **Step 14 — Surface live worker stamina on the NPC.** Update `Dayswork/Worker/FarmhandNpc.cs` and related draw/update plumbing so the worker shows an overhead stamina bar that tracks the authoritative runtime stamina state immediately on each labor beat. *S-07*

- [x] **Step 15 — Preserve invulnerability and interrupted-task safety under the new pacing model.** Re-verify and adjust the live worker shell so player attacks, hit reactions, and task interruptions do not cause stamina/state corruption or abandon the shift outside the approved stop path. *S-17*

## Phase D — Runtime safety, stuck handling, and regression coverage

- [x] **Step 16 — Keep stuck recovery aligned with the new stop semantics.** Retrofit stuck detection and abort handling so a worker that cannot continue still routes through the unified no-refund stop-and-settle path and leaves collected items safe. *S-16*

- [x] **Step 17 — Add focused U-21 example tests for runtime edge cases.** Create a dedicated U-21 test area in `Dayswork.Tests/` covering representative scenarios like zero stamina on the final swing of a tree stage, no new work unit starting at zero, 8pm mid-unit completion, sleep-stop no-refund wrap-up, and stuck abort preserving collected outputs. *S-08, S-10, S-15, S-16*

- [x] **Step 18 — Add FsCheck coverage for stamina and stop invariants.** Add property tests for stamina bounds, per-beat determinism, stop-reason convergence, and “movement never spends stamina” semantics, reusing or extending shared generators where practical. *S-07, S-08, S-10, S-19*

- [x] **Step 19 — Update shared generators/helpers/comparers only as needed.** Refresh any shared test generators, structural comparers, or runtime helper seams touched by the new energy-era shift model so U-21 integrates cleanly with the existing suites without weakening prior coverage. *S-19*

## Phase E — Verification, documentation, and workflow state

- [x] **Step 20 — Build the solution with deploy disabled.** Run `dotnet build Dayswork.sln /p:EnableModDeploy=false` and fix any U-21 breakage before moving on. *Workflow requirement*

- [x] **Step 21 — Run the full automated test suite.** Run `dotnet test Dayswork.sln` and ensure the existing suite plus the new U-21 coverage pass cleanly. *Workflow requirement*

- [x] **Step 22 — Write the U-21 code summary artifact.** Create `aidlc-docs/construction/u-21-worker-energy-shift-runtime-refresh/code/code-summary.md` summarizing modified vs created files, the new energy/boundary/pacing seams, removed active-runtime refund semantics, test additions, and deliberate deferrals to `U-22` and `U-23`. *Workflow requirement*

- [x] **Step 23 — Update workflow tracking and present the standardized completion gate.** Mark completed plan steps `[x]`, update `aidlc-docs/aidlc-state.md`, append the completion audit entry, and present the required 2-option Code Generation completion message. *Workflow requirement*

---

## Risk Notes

- **Highest risk**: retrofitting `ShiftOrchestrator` to stop depending on deposit/rate/refund semantics without breaking end-of-shift item safety.
- **Second highest risk**: defining work-unit boundaries cleanly enough that zero-stamina and 8pm stops feel fair across trees, rocks, crops, and animal interactions.
- **Third highest risk**: slowing the worker down visibly without regressing pathing, stuck recovery, or tool visuals.
- **Test strategy matters here.** U-21 is stateful and timing-sensitive, so the safety net belongs at the Core energy/state seam first, then at orchestrator edge cases where stop reasons converge.

## Artifact Output

- **Application code**:
  - `Dayswork.Core/Shifts/ShiftContext.cs`
  - `Dayswork.Core/Shifts/ShiftStateMachine.cs`
  - new or expanded energy/boundary files under `Dayswork.Core/Energy/`
  - `Dayswork/Orchestration/ShiftOrchestrator.cs`
  - `Dayswork/Orchestration/RecurringContractScheduler.cs`
  - `Dayswork/Orchestration/CalendarHandlers.cs` only if required by the new stop path
  - `Dayswork/Worker/FarmhandNpc.cs`
  - `Dayswork/Worker/WorkerMovementDriver.cs`
  - `Dayswork/Worker/ToolSwapAnimator.cs`
  - `Dayswork/ModEntry.cs` and adjacent runtime composition/logging seams only if needed
- **Tests**:
  - a new focused U-21 test area under `Dayswork.Tests/`
  - any shared generators/comparers/helpers updated in place
- **Documentation**:
  - `aidlc-docs/construction/u-21-worker-energy-shift-runtime-refresh/code/code-summary.md`
