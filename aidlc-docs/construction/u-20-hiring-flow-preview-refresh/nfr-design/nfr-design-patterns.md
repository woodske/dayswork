# U-20 — NFR Design Patterns

**Unit**: U-20 — Hiring Flow Preview Refresh

NFR design decisions applied: no additional question round required. NFR requirements NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A apply, along with functional-design decisions FD-Q1=A through FD-Q9=A.

---

## Applicability Scope

| Category | Applicability |
|---|---|
| Security | **N/A** — Security Baseline is disabled project-wide and U-20 has no network/auth/PII surface |
| Scalability / HA | **N/A** — local in-process UI flow with tiny draft state; no replicas, shards, queues, or distributed scale mechanisms |
| Distributed infrastructure | **N/A** — no service deployment, queue, cache server, or background preview worker |
| Resilience | **Applicable** — explicit invalid-preview recovery and safe legacy edit hydration |
| Performance | **Applicable** — immediate synchronous preview refresh and narrow non-pricing mutation paths |
| Determinism / correctness | **Applicable** — canonical preview/view-model output is a hard quality bar |
| Maintainability / testability | **Applicable** — thin menus, coordinator-owned shaping, and stronger example/property coverage |

---

## PAT-U20-01 — Synchronous Coordinator-Owned Preview Refresh

**What**: Task and scope mutations refresh preview synchronously through a single coordinator-owned path, while destination and schedule edits take narrower non-pricing refresh paths.

**Applies to**:
- `PERF-U20-01` preview refresh remains immediate and synchronous
- `PERF-U20-02` non-pricing edits stay lightweight
- `PERF-U20-03` no speculative UI-performance complexity
- `TS-U20-02` keep preview refresh synchronous
- `TS-U20-03` reuse Core preview seams

**How**:
- task toggles and scope changes call the Core preview builder inline
- destination changes do not recompute price or worker energy
- schedule changes refresh only schedule-sensitive review copy
- no debounce, no async worker, no retained preview cache

**Why this pattern**:
- preserves the feel of a responsive in-game menu
- avoids unnecessary recomputation on edits that cannot change price
- keeps mutation boundaries obvious and testable

**Not responsible for**:
- ordering of user-facing rows and summaries
- legacy edit hydration

Those belong to later patterns.

---

## PAT-U20-02 — Canonical View-Model Shaping

**What**: User-facing contribution rows, typed-scope summaries, validation messages, and review breakdowns are emitted through a canonical shaping path rather than relying on incidental mutation or collection order.

**Applies to**:
- `REL-U20-01` equivalent drafts produce deterministic preview output
- `REL-U20-02` determinism must not depend on incidental collection order
- `PBT-U20-01` equivalent-draft determinism invariants
- `TS-U20-04` coordinator owns canonical view-model shaping

**How**:
- apply stable ordering before emitting service rows
- apply stable grouping/order for outdoor zones, animal buildings, and greenhouse summary sections
- apply stable ordering for validation reasons and review breakdown lines
- keep this shaping concentrated in the coordinator/view-model seam

**Why this pattern**:
- the same draft should tell the same story every time
- stable output improves reviewability, debugging, and regression tests
- deterministic shaping prevents screen-order drift from becoming a hidden bug source

---

## PAT-U20-03 — Explicit Invalid-Preview Recovery Gate

**What**: Invalid previews remain visible and navigable, but confirmation is blocked at a single explicit review gate with clear reasons and an obvious recovery path.

**Applies to**:
- `REL-U20-03` invalid preview handling is explicit and stable
- `SAFE-U20-02` invalid drafts cannot be confirmed
- `USAB-U20-04` recovery path for invalid preview is obvious
- functional rules `BR-SUM-01` and `BR-SUM-02`

**How**:
- earlier screens remain usable for contract setup
- Screen 4 is the sole blocking confirmation gate
- invalid state displays reasons instead of disappearing services or hidden auto-fixes
- back-navigation is the repair path

**Why this pattern**:
- preserves a smooth four-screen workflow without pretending the draft is valid
- avoids the old whole-farm/fake-complete behavior
- makes failure states honest without making the flow feel brittle

**Important limit**:
- this pattern does not invent missing scope
- it surfaces missing scope and requires the player to fix it

---

## PAT-U20-04 — Narrow Legacy Scope Bootstrap

**What**: Older contracts lacking authoritative typed scope hydrate through a small compatibility bootstrap that derives what it can from legacy `Zones`, then degrades honestly if scope remains incomplete.

**Applies to**:
- `REL-U20-04` legacy edit hydration failures degrade safely
- `SAFE-U20-03` no silent scope invention during edit hydration
- `COMPAT-U20-01` legacy contracts remain editable on a best-effort basis
- `COMPAT-U20-02` authoritative scope stays preferred when available
- `PBT-U20-04` legacy bootstrap safety invariants
- `TS-U20-06` legacy edit bootstrap remains a narrow helper

**How**:
- if authoritative `ScopeSelection` exists, use it directly
- otherwise run a one-time compatibility-zone-to-typed-scope bootstrap
- if the bootstrap cannot justify complete scope, surface an incomplete/invalid draft rather than fabricating scope

**Why this pattern**:
- keeps older contracts editable during the retrofit
- preserves the redesign’s honesty about missing scope
- isolates compatibility assumptions so they are easy to delete later

---

## PAT-U20-05 — Thin Menus, Fat Coordinator

**What**: Menus stay presentation-focused while the coordinator owns draft mutation, preview refresh, summary shaping, and edit-entry behavior.

**Applies to**:
- `MAINT-U20-01` pricing and worker-energy logic remain outside the menus
- `MAINT-U20-04` no new UI framework or automation stack is required
- `TS-U20-01` stay on the existing SMAPI menu + coordinator stack
- `TS-U20-05` preserve existing menu-navigation idioms
- functional rules `BR-PREVIEW-01`, `BR-EDIT-01`, and `BR-EDIT-02`

**How**:
- menus render coordinator-provided state slices
- menus emit user actions rather than interpreting business rules locally
- review-first edit entry remains a coordinator concern
- Core preview seams continue owning price/energy semantics

**Why this pattern**:
- reduces the chance of logic drifting differently across screens
- preserves testability at the coordinator/helper layer
- keeps the retrofit incremental instead of turning into a UI rewrite

---

## PAT-U20-06 — Regression Coverage at the View-Model Boundary

**What**: The stronger U-20 regression bar is satisfied primarily through pure/helper tests and coordinator/view-model tests rather than brittle UI automation.

**Applies to**:
- `MAINT-U20-02` strong example + property coverage
- `MAINT-U20-03` property coverage targets orchestration invariants
- `PBT-U20-02` no-whole-farm-fallback invariants
- `PBT-U20-03` schedule/destination non-pricing invariants
- `TS-U20-07` tests stay on `xUnit` + `FsCheck`
- `TS-U20-08` prefer view-model tests over UI automation

**How**:
- test pure helpers directly where possible
- test coordinator output for service rows, summary models, and invalid-preview gating
- use FsCheck for draft-equivalence and non-pricing-mutation invariants
- keep full UI automation out of scope for this unit

**Why this pattern**:
- catches the highest-risk regressions without a heavy test harness
- aligns with the enabled partial Property-Based Testing extension
- keeps tests stable across menu implementation details

---

## Pattern Summary

U-20’s NFR design stays intentionally focused:
- one synchronous coordinator-owned preview refresh path
- one canonical view-model shaping policy
- one explicit invalid-preview recovery gate
- one narrow compatibility bootstrap for legacy edits
- one thin-menu / fat-coordinator separation
- one view-model-boundary regression strategy

That gives the hire/edit retrofit a strong responsiveness and determinism bar without adding new UI infrastructure or re-embedding pricing logic into menus.
