# NFR Requirements Plan - U-WR Worker Routing and Dynamic Task Selection

## Unit Context

- **Unit**: U-WR Worker Routing and Dynamic Task Selection
- **Stage**: NFR Requirements
- **Functional design source**: `aidlc-docs/construction/u-wr-worker-routing-dynamic-task-selection/functional-design/`
- **Execution plan source**: `aidlc-docs/inception/plans/worker-routing-execution-plan.md`
- **Extension status**: Security Baseline disabled; Property-Based Testing enabled in full mode.

## NFR Assessment Checklist

- [x] Confirm Functional Design is complete and approved for NFR Requirements.
- [x] Load NFR Requirements stage rule, content-validation rule, question-format rule, workflow plan, functional design, and PBT rules.
- [x] Identify NFR decision points for performance, reliability, determinism, maintainability, observability, and test strategy.
- [x] Create this NFR requirements plan with `[Answer]:` tags.
- [x] Collect all user answers.
- [x] Validate answers for completeness, ambiguity, and contradictions.
- [x] Create clarification questions if any answer is ambiguous. N/A - no ambiguities detected.
- [x] Generate `nfr-requirements.md`.
- [x] Generate `tech-stack-decisions.md`.
- [x] Update this plan, `aidlc-state.md`, and `audit.md`.

## Content Validation

- No Mermaid diagrams are included.
- No ASCII diagrams are included.
- Markdown tables, code spans, and `[Answer]:` tags were checked for parse compatibility before file creation.

## NFR Decision Questions

Please fill in each `[Answer]:` tag with the letter choice. If none of the choices fit, choose `X` and describe your preference after the tag.

### Question 1
What performance envelope should guide route-cost evaluation inside an active batch?

A) Exact path length for all currently valid candidates and interaction tiles, with optimization limited to short-lived caching and reuse.
B) Exact path length after cheap structural filtering such as valid task state, valid interaction tile, and basic passability checks; no task is skipped by approximation.
C) Exact path length with an explicit per-pass route-evaluation budget; if the budget is exceeded, remaining candidates are deferred and retried later in the same batch.
X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

### Question 2
How aggressively should route-cost data be invalidated after work changes the world?

A) Recompute route costs after every progress event so newly cleared paths are considered immediately.
B) Keep route costs for one batch pass, then rebuild them before retrying deferred work.
C) Reuse route costs until movement or execution fails, then rebuild only the affected candidate.
X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

### Question 3
What runtime architecture constraint should NFR Requirements preserve for worker routing?

A) Stay fully synchronous inside the existing SMAPI shift loop with deterministic in-memory route selection.
B) Allow background or precomputed route evaluation if it remains deterministic and isolated from world mutation.
C) Extract route selection into a separate Core service even if the live SMAPI adapter must become broader.
X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

### Question 4
What reliability guard should back the blocked-task retry loop?

A) Use the no-progress pass rule as the primary stop condition plus a defensive max-pass guard derived from finite candidate count.
B) Use only the no-progress pass rule, with no additional numeric guard.
C) Use a fixed small retry count per deferred candidate before skipping it for the day.
X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

### Question 5
What observability should the implementation add for routing decisions?

A) Keep normal gameplay silent; add maintainer/debug logs only when candidates are skipped after no-progress retry or route evaluation fails unexpectedly.
B) Add detailed debug tracing for every candidate, route cost, tie-break, deferral, and retry decision behind the existing logging level.
C) Add no new logging; rely on automated tests and existing stuck-abort behavior.
X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

### Question 6
What test-quality bar should NFR Requirements set for this unit?

A) Add example tests for the reported regressions plus FsCheck properties for selector minimum route cost, deterministic tie-breaks, unreachable filtering, and zero-cost current-tile interaction; add deferral PBT if a pure helper is extracted.
B) Make both selector route ordering and deferral state-machine termination mandatory FsCheck targets, with example tests for all reported regressions.
C) Keep the PBT obligation focused on selector invariants only, with example-based tests for deferral and the reported gameplay regressions.
X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

## PBT Compliance Planning

| Rule | Applicability for NFR Requirements | Planned handling |
|---|---|---|
| PBT-09 | Applicable | Document FsCheck as the selected C#/.NET property-based testing framework in `tech-stack-decisions.md`; confirm it is already part of the test stack or preserve/add the dependency during code generation as needed. |
| PBT-08 | Later Build/Test stage | Carry seed logging and reproducibility into build/test instructions. |
| PBT-01 through PBT-10 | Later Code Generation stages | Carry identified route-selection and deferral properties into code-generation planning and generated tests. |

## Extension Compliance

| Extension | Status | NFR planning compliance |
|---|---|---|
| Security Baseline | Disabled | N/A - no security-sensitive behavior, network surface, auth, or PII is introduced. |
| Property-Based Testing | Enabled, full | Compliant so far - PBT-09 is explicitly planned for NFR Requirements and downstream PBT obligations are carried forward. |
