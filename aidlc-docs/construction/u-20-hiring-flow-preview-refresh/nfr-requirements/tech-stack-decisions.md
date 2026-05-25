# U-20 — Tech Stack Decisions

**Unit**: U-20 — Hiring Flow Preview Refresh

NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A. Functional-design decisions FD-Q1=A through FD-Q9=A apply.

---

## TS-U20-01 — Stay on the existing SMAPI menu + coordinator stack
U-20 introduces no new UI framework. Implementation stays on the established architecture:
- `HiringFlowCoordinator` as the orchestration seam
- SMAPI menu classes for the four screens
- Core-owned preview/terms builders from U-18/U-19

This keeps the retrofit incremental and avoids rewriting working input/navigation behavior.

## TS-U20-02 — Keep preview refresh synchronous
Task and scope mutations should call into the existing Core preview pipeline directly and update shared view state inline. No async preview API, debounce layer, or background worker is introduced.

## TS-U20-03 — Reuse Core preview seams; do not duplicate pricing logic in UI
`ContractTermsBuilder` and related Core types remain the source of truth for:
- fixed pricing preview
- validation reasons
- worker energy profile summary inputs

Menus should consume coordinator-produced view models rather than reconstructing these rules locally.

## TS-U20-04 — Coordinator owns canonical view-model shaping
Deterministic ordering and screen-specific summary shaping should be concentrated in the coordinator/view-model layer rather than spread across individual menus. This is the cleanest place to enforce:
- stable service row ordering
- stable scope summary ordering
- stable review breakdown ordering
- stable validation reason presentation

## TS-U20-05 — Preserve existing menu-navigation idioms
Because usability and gamepad support are part of the quality bar, U-20 should preserve the project's existing screen/back/confirm patterns rather than inventing a custom navigation system for this unit.

## TS-U20-06 — Legacy edit bootstrap remains a narrow compatibility helper
Any one-time derivation from compatibility `Zones` into typed scope should stay in a small, explicit helper seam. That keeps:
- legacy-specific assumptions isolated
- authoritative-scope edit hydration straightforward
- bootstrap behavior directly testable

## TS-U20-07 — Tests stay on `xUnit` + `FsCheck`
No new test framework is needed. U-20 should lean on:
- `xUnit` for coordinator/view-model examples and edit-flow regressions
- `FsCheck` for deterministic preview and non-pricing-mutation invariants where pure inputs exist

This matches the enabled Property-Based Testing extension without forcing fragile UI automation.

## TS-U20-08 — Prefer view-model tests over UI-automation infrastructure
The recommended regression strategy is:
- pure/helper tests where possible
- coordinator-level tests for orchestration and summary shaping
- focused menu tests only where direct menu behavior cannot be covered indirectly

No dedicated end-to-end UI automation harness is required for this unit.

## TS-U20-09 — No caching or performance framework is required
Because the draft data set is tiny and preview recomputation is intentionally synchronous, U-20 should not introduce:
- memoization frameworks
- retained preview caches
- observer/event-bus infrastructure
- performance instrumentation subsystems

The simpler architecture is the preferred tech decision for this retrofit.
