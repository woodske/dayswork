# U-18 — NFR Design Patterns

**Unit**: U-18 — Contract Terms Foundation

NFR design decisions applied: NFR-DES-Q1=A (dedicated config-resolution seam), NFR-DES-Q2=A (pure recompute, no cache component), NFR-DES-Q3=A (deterministic ordering owned by `PriceBreakdownBuilder`), NFR-DES-Q4=A (dedicated U-18 test helpers/generators). Builds on approved NFR Requirements and Functional Design for `U-18`.

---

## Applicability Scope

| Category | Applicability |
|---|---|
| Security | **N/A** — Security Baseline is disabled project-wide and U-18 has no network/auth/PII surface |
| Scalability / HA | **N/A** — local in-process Core seam with bounded input sizes; no replicas, queues, or distributed scale mechanisms |
| Distributed infrastructure | **N/A** — no cache server, queue, circuit breaker, or external service |
| Resilience | **Applicable** — default-backed config resolution and handled invalid-preview outcomes |
| Performance | **Applicable** — immediate synchronous preview and bounded pure recomputation |
| Determinism / correctness | **Applicable** — canonical ordering and snapshot stability are central requirements |
| Maintainability / testability | **Applicable** — strong FsCheck support and pure seam preservation |

---

## PAT-U18-01 — Default-Backed Key Resolution (Resilience)

**What**: Missing or stale keyed config values are resolved through one dedicated pure fallback seam rather than by scattered dictionary probing across multiple builders.

**Applies to**:
- `REL-U18-03` missing/stale config keys fall back to defaults and log
- `REL-U18-04` fallback is key-local, not table-global
- `TS-U18-04` default-backed lookup strategy

**How**:
- Introduce one small pure config-resolution helper in Core.
- Callers request a typed key and receive:
  - the effective resolved value
  - whether fallback-to-default was used
- Builders consume this helper instead of probing config dictionaries directly.
- Missing keys do not throw and do not invalidate preview by themselves.

**Why this pattern**:
- keeps fallback semantics consistent across outdoor, animal, greenhouse, and energy tables
- avoids duplicated warning/fallback logic in multiple builders
- makes fallback behavior directly unit-testable

**Not responsible for**:
- emitting player-facing strings
- deciding whether warnings are logged to SMAPI; it only produces fallback metadata for later layers to observe

---

## PAT-U18-02 — Pure Recompute Preview (Performance)

**What**: Every preview build recomputes terms directly from the current draft input, with no memoization or cache component in the unit design.

**Applies to**:
- `PERF-U18-01` live preview remains immediate
- `PERF-U18-02` scope-bounded, allocation-light work
- `TS-U18-02` synchronous preview pipeline
- `TS-U18-09` no caching requirement at U-18

**How**:
- `ContractTermsBuilder.BuildPreview(...)` remains a pure synchronous method
- each call reruns:
  - scope classification
  - outdoor band assignment
  - price calculation
  - breakdown building
  - energy-profile building
- performance comes from bounded linear work and a simple data flow, not from retained preview state

**Why this pattern**:
- keeps the pure seam simple
- avoids invalidation bugs and hidden state
- matches the small bounded input sizes of a single Stardew contract draft

**Tradeoff accepted**:
- repeated identical inputs are recomputed
- if profiling later proves this expensive, memoization can be added as an optimization in Code Generation or a later retrofit, not as a foundational design constraint

---

## PAT-U18-03 — Canonical Breakdown Ownership (Determinism)

**What**: `PriceBreakdownBuilder` is the single owner of canonical line-item ordering and aggregation policy for `PricingSnapshot`.

**Applies to**:
- `REL-U18-01` strict deterministic stability
- `REL-U18-02` determinism must not depend on dictionary enumeration order
- `BR-PRICE-01..05` breakdown aggregation and canonical ordering rules

**How**:
- upstream stages may emit raw or semi-structured contributions
- `PriceBreakdownBuilder` performs final aggregation by pricing key
- `PriceBreakdownBuilder` applies explicit canonical ordering before emitting `PricingSnapshot.LineItems`
- ordering is implemented by explicit family/service/key comparers, never by relying on insertion order or hash iteration

**Why this pattern**:
- keeps determinism responsibility in the component that already owns the snapshot shape
- avoids leaking ordering concerns into calculators/classifiers
- makes snapshot ordering easier to reason about and test

**Boundary rule**:
- upstream stages may preserve stable local ordering for convenience
- only `PriceBreakdownBuilder` is authoritative for final persisted/UI-visible snapshot order

---

## PAT-U18-04 — Invalid-As-Data, Not Exception (Resilience / Usability)

**What**: A contract with zero chargeable scope-task pairs overall is represented as a structured invalid preview outcome, not as an exceptional code path.

**Applies to**:
- `REL-U18-05` invalid preview is a handled business outcome
- `BR-VAL-01..05` preview validity rules
- `UX-U18-01/02` structural outputs support later UI clarity

**How**:
- `BuildPreview(...)` returns a `ContractPreview`
- invalid previews contain:
  - `IsValid = false`
  - structured validation issues
  - no proposed terms snapshot
- callers can render validation feedback without exception handling

**Why this pattern**:
- keeps business invalidity separate from programmer errors
- supports live menu interaction cleanly
- improves testability by making invalid cases explicit data, not side effects

---

## PAT-U18-05 — Dedicated Property-Test Support (Maintainability)

**What**: U-18 receives dedicated domain-specific test helpers and FsCheck generators rather than relying only on generic/shared generators.

**Applies to**:
- `MAINT-U18-02` strong example + property coverage
- `MAINT-U18-03` property coverage must target real contract invariants
- `TS-U18-05/06` strong test stack and generator quality

**How**:
- add focused U-18 test-side generators/builders for:
  - overlapping outdoor zones
  - partially matched scope families
  - repeated building tiers
  - mixed outdoor/animal/greenhouse task sets
  - valid vs invalid preview shapes
- keep these helpers on the test side, not in production Core
- pair example-based tests with property invariants for the same concepts

**Why this pattern**:
- generic generators are often too shallow for pricing-model edge cases
- dedicated helpers make failures smaller, easier to reproduce, and easier to reason about
- U-18 is exactly the kind of pure seam where domain-specific generators pay off

---

## PBT Compliance Mapping

| Requirement | Pattern that supports it | Notes |
|---|---|---|
| `PBT-U18-01` zone-union pricing invariants | `PAT-U18-01`, `PAT-U18-05` | generators must cover overlapping and equivalent outdoor shapes |
| `PBT-U18-02` deterministic snapshot invariants | `PAT-U18-03`, `PAT-U18-05` | explicit canonical ordering is the core design move |
| `PBT-U18-03` breakdown reconciliation invariants | `PAT-U18-03` | one component owns aggregation and totals |
| `PBT-U18-04` invalid-preview invariants | `PAT-U18-04`, `PAT-U18-05` | invalid states are structured data, easy to property-test |
| `PBT-U18-05` aggregation invariants | `PAT-U18-03`, `PAT-U18-05` | repeated building-tier aggregation is explicitly modeled |
| `PBT-U18-06` energy-snapshot invariants | `PAT-U18-01`, `PAT-U18-05` | full-table config fallback and full-table snapshot rules are testable |

---

## Pattern Summary

U-18's NFR design intentionally stays lightweight:
- one dedicated resilience seam for keyed config fallback
- no cache or async machinery
- one authoritative component for canonical snapshot ordering
- one explicit invalid-preview-as-data pattern
- one dedicated property-test support strategy

That keeps the unit simple enough for fast preview work, while still meeting a high correctness and testability bar.
