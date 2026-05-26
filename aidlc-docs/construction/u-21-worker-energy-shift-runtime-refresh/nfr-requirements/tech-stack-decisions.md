# U-21 — Tech Stack Decisions

**Unit**: U-21 — Worker Energy + Shift Runtime Refresh

NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A. Functional-design decisions FD-Q1=A through FD-Q9=A apply.

---

## TS-U21-01 — Stay on the existing SMAPI event-driven runtime shell
U-21 introduces no new runtime framework. Implementation stays on the established architecture:
- `ShiftOrchestrator` as the live world adapter
- `FarmhandNpc` as the visible worker presentation seam
- `WorkerMovementDriver` / existing worker helpers for movement and action support
- pure Core energy/state logic for stamina and stop decisions

This keeps the retrofit incremental instead of rewriting the worker runtime around a new execution model.

## TS-U21-02 — Keep stamina and work-boundary logic in pure Core seams
The preferred implementation shape is:
- live world action occurs in the orchestrator
- action maps to `WorkActionKind`
- pure Core logic updates stamina and stop-boundary state
- the orchestrator reacts to the result

This is the cleanest way to preserve determinism and testability for the new runtime model.

## TS-U21-03 — Keep pacing explicit and config-driven
Movement slowdown and action cadence should be represented by explicit runtime/config values rather than hidden constants scattered across update logic. This supports:
- deterministic behavior
- easier balancing
- future GMCM/config exposure without another redesign

## TS-U21-04 — Keep per-beat updates synchronous and immediate
Stamina spending and overhead-bar updates should happen inline with the live labor beat that caused them. No async HUD pipeline, delayed event queue, or coalescing layer is introduced for this unit.

## TS-U21-05 — Reuse the existing output/deposit pipeline
U-21 should reuse the already-landed output-safety stack:
- `ItemBuffer`
- `DepositPlanner`
- overflow/mail fallback behavior

The runtime retrofit should change *when work stops*, not invent a second settlement pipeline.

## TS-U21-06 — Remove refund/debt concerns from active runtime seams
The runtime-facing shift model should stop depending on:
- `DepositAmount`
- `HourlyRate`
- `ComputeRefund()`

Those may still exist elsewhere in the retrofit bridge until later code cleanup, but U-21 should not build new logic on top of them.

## TS-U21-07 — Keep the visible stamina display as an NPC-attached world element
No separate HUD framework is required. The preferred tech direction is:
- a small energy-bar presentation model
- rendered in association with `FarmhandNpc`
- updated directly from authoritative runtime stamina state

This satisfies the UX goal without expanding the rendering architecture.

## TS-U21-08 — Tests stay on `xUnit` + `FsCheck`
No new test framework is needed. U-21 should lean on:
- `xUnit` for concrete stop-path and runtime-scenario regressions
- `FsCheck` for energy invariants, determinism, and boundary-stop properties

Where practical, a small model/reference helper for command-sequence testing is encouraged, but it should stay within the existing test stack.

## TS-U21-09 — Avoid new runtime concurrency or job infrastructure
Because the runtime handles one worker in a single-player mod context, U-21 should not introduce:
- thread-based worker execution
- background job queues
- observer buses for stamina UI synchronization
- speculative caches for per-frame speed

The simpler single-threaded event/update model is the preferred tech choice for this retrofit.
