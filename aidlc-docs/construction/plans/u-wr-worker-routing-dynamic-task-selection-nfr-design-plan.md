# NFR Design Plan - U-WR Worker Routing and Dynamic Task Selection

## Unit Context

- **Unit**: U-WR Worker Routing and Dynamic Task Selection
- **Stage**: NFR Design
- **NFR requirements source**: `aidlc-docs/construction/u-wr-worker-routing-dynamic-task-selection/nfr-requirements/`
- **Functional design source**: `aidlc-docs/construction/u-wr-worker-routing-dynamic-task-selection/functional-design/`
- **Extension status**: Security Baseline disabled; Property-Based Testing enabled in full mode.

## NFR Design Checklist

- [x] Confirm NFR Requirements are complete and approved for NFR Design.
- [x] Load NFR Design stage rule, approved NFR requirements, tech-stack decisions, and functional design context.
- [x] Evaluate resilience, scalability, performance, security, and logical-component categories.
- [x] Create this NFR design plan with `[Answer]:` tags.
- [x] Collect all user answers.
- [x] Validate answers for completeness, ambiguity, and contradictions.
- [x] Create clarification questions if any answer is ambiguous. N/A - no ambiguities detected.
- [x] Generate `nfr-design-patterns.md`.
- [x] Generate `logical-components.md`.
- [x] Update this plan, `aidlc-state.md`, and `audit.md`.

## Category Applicability Review

| Category | Applicability | Design question |
|---|---|---|
| Resilience patterns | Applicable | Deferral, movement failure, stale targets, and defensive max-pass guard need concrete pattern choices. |
| Scalability patterns | Applicable in local-game scope | Exact route evaluation needs bounded local candidate handling without approximation-based task loss. |
| Performance patterns | Applicable | Immediate invalidation and exact path costs need a concrete cache/evaluation pattern. |
| Security patterns | N/A for implementation design | Security Baseline is disabled and this unit adds no network, auth, PII, file parsing, or external integration. |
| Logical components | Applicable | Selector, candidate adapter, route oracle, and retry coordinator boundaries need confirmation. |

## Content Validation

- No Mermaid diagrams are included.
- No ASCII diagrams are included.
- Markdown tables, code spans, and `[Answer]:` tags were checked for parse compatibility before file creation.

## NFR Design Questions

Please fill in each `[Answer]:` tag with the letter choice. If none of the choices fit, choose `X` and describe your preference after the tag.

### Question 1
Which selector extraction pattern should NFR Design recommend?

A) Extract a small pure selector helper that ranks already-evaluated candidates by route cost, task priority, and stable order; keep live candidate building and pathfinding in `Dayswork`.
B) Extract a larger pure routing service that owns candidate building, route evaluation, selection, and retry state, with SMAPI adapters around all world access.
C) Keep selector logic inside `ShiftOrchestrator` and cover it mostly through integration-style tests.
X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

### Question 2
Which route-cost evaluation pattern should NFR Design recommend?

A) Add or expose a narrow route-cost oracle on `WorkerMovementDriver` that returns reachable path length using the same passability assumptions as actual movement.
B) Implement a separate lightweight BFS/path helper just for selection, and keep movement unchanged.
C) Use movement attempts to discover reachability lazily rather than precomputing path lengths for all candidates.
X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

### Question 3
Which deferral/retry component pattern should NFR Design recommend?

A) Keep retry orchestration in `ShiftOrchestrator`, but isolate the pass accounting and defensive finite guard in a tiny helper or clearly bounded method.
B) Extract a pure active-batch state machine that owns deferred sets, progress accounting, retry passes, and skip decisions.
C) Keep all deferral state inline in the existing queue-draining loop with no new helper.
X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

### Question 4
How should NFR Design shape route-cache lifetime?

A) No cross-selection cache; each task boundary computes route costs fresh, with only local memoization inside that single selection call.
B) Keep a per-pass route cache and clear it after any progress event, movement failure, or stale-target result.
C) Keep a per-location route cache keyed by worker tile and interaction tile until the worker changes location.
X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

### Question 5
Which test seam should NFR Design prioritize for code generation?

A) Prioritize a pure selector/ranking seam with generated candidate route results; cover live world candidate adapters through focused example tests.
B) Prioritize a pure deferral state-machine seam with generated progress/blocking sequences; cover selector ordering through example tests.
C) Prioritize live integration-style tests around the orchestrator; use pure tests only where they fall out naturally.
X) Other (please describe after `[Answer]:` tag below)

[Answer]: A

## PBT Compliance Planning

| Rule | Applicability for NFR Design | Planned handling |
|---|---|---|
| PBT-01 | Already complete | Functional Design identified route-selection and deferral properties. |
| PBT-03 | Applicable later | NFR Design must preserve selector invariant test seams for code generation. |
| PBT-05 | Applicable later | NFR Design should allow a simple minimum-cost/tie-break oracle in property tests. |
| PBT-06 | Conditional | If a pure deferral helper/state machine is selected, code generation must add stateful or sequence-style PBT. |
| PBT-07 | Applicable later | NFR Design should keep route result and candidate data domain-shaped for generators. |
| PBT-09 | Complete | FsCheck.Xunit is already selected and present. |
| PBT-10 | Applicable later | Example regression tests remain mandatory alongside selector properties. |

## Extension Compliance

| Extension | Status | NFR design planning compliance |
|---|---|---|
| Security Baseline | Disabled | N/A - no security-sensitive behavior is introduced. |
| Property-Based Testing | Enabled, full | Compliant so far - design questions preserve selector and conditional deferral PBT seams for code generation. |
