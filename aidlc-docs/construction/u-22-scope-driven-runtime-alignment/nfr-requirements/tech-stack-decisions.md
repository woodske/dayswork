# U-22 — Tech Stack Decisions

**Unit**: U-22 — Scope-Driven Runtime Alignment

NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A. Functional-design decisions FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=B, FD-Q8=X, and FD-Q9=A apply.

---

## TS-U22-01 — Stay on the existing SMAPI runtime shell
U-22 introduces no new runtime framework. Implementation stays on the established architecture:
- `ShiftOrchestrator` as the live world adapter
- `WorkScopeClassifier` as the normalization seam
- `DepositPlanner` as the task-owned routing seam
- `MailDispatcher` as the delivery-notice shaping seam

This keeps the retrofit incremental instead of layering in a second runtime model.

## TS-U22-02 — Make typed scope the only supported runtime authority
The preferred implementation direction is:
- read `Contract.ScopeSelection`
- normalize it into `WorkScopeSet`
- plan execution from that normalized scope

No compatibility-derived runtime fallback path should be built for unsupported no-scope contracts in this unit.

## TS-U22-03 — Keep fail-fast unsupported-contract handling explicit and safe
If runtime encounters a contract without `ScopeSelection`, the preferred behavior is:
- reject before live work begins
- log maintainable diagnostics
- avoid partial guessed execution

This is simpler and safer than inventing a compatibility runtime path for a project that is not yet live.

## TS-U22-04 — Keep destinations task-owned and add provenance separately
The preferred implementation split is:
- `TaskKind` continues to drive destination lookup
- scope provenance is added only for explanation and categorization

This preserves the existing destination model while making scope-aware overflow letters possible.

## TS-U22-05 — Keep scope-aware mail shaping on the existing mail pipeline
No new mail subsystem is needed. The preferred direction is:
- continue using the existing farmhand mail flow
- enrich the categorization/body-shaping inputs with scope provenance
- preserve one bounded letter per shift where possible

This satisfies the new clarity goal without duplicating delivery infrastructure.

## TS-U22-06 — Keep normalization, batch-family shaping, and categorization deterministic
The main U-22 seams should remain practical to test with pure or near-pure inputs:
- scope normalization
- greenhouse/outdoor batch-family separation
- animal-building eligibility shaping
- scope-aware overflow grouping

This is the cleanest way to satisfy the strict determinism bar.

## TS-U22-07 — Keep runtime planning synchronous and lightweight
Shift-start scope classification and wrap-up categorization should stay inline with the existing runtime flow. No async planning worker, background categorization pipeline, or speculative cache layer is required.

## TS-U22-08 — Tests stay on `xUnit` + `FsCheck`
No new test framework is needed. U-22 should lean on:
- `xUnit` for concrete mixed-scope runtime and mail scenarios
- `FsCheck` for normalization, routing, and categorization invariants

The strongest value comes from generated mixed-scope inputs rather than from UI-only tests.
