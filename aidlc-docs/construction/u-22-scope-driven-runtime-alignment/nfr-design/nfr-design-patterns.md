# U-22 — NFR Design Patterns

**Unit**: U-22 — Scope-Driven Runtime Alignment

NFR design decisions applied: no additional question round required. NFR requirements NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A apply, along with functional-design decisions FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=B, FD-Q8=X, and FD-Q9=A.

---

## Applicability Scope

| Category | Applicability |
|---|---|
| Security | **N/A** — Security Baseline is disabled project-wide and U-22 has no network/auth/PII surface |
| Scalability / HA | **N/A** — local single-worker in-process runtime seam; no replicas, shards, queues, or distributed scale mechanisms |
| Distributed infrastructure | **N/A** — no service deployment, queue, cache server, or async worker runtime |
| Resilience | **Applicable** — unsupported-contract fail-fast behavior, no-item-loss guarantees, and bounded existing mail-path reuse |
| Performance | **Applicable** — lightweight synchronous scope planning and bounded provenance-aware categorization |
| Determinism / correctness | **Applicable** — strict deterministic scope normalization, batch-family shaping, task-owned routing, and mail categorization |
| Maintainability / testability | **Applicable** — pure or near-pure scope seams plus strong mixed-scope example/property coverage |

---

## PAT-U22-01 — Authoritative Typed-Scope Gate

**What**: Live runtime scope intake passes through one explicit authority gate: supported contracts must have `Contract.ScopeSelection`, and unsupported no-scope contracts fail fast before live work begins.

**Applies to**:
- `SAFE-U22-01` unsupported no-scope contracts fail fast and safely
- `SAFE-U22-02` fail-fast handling preserves world and data safety
- `TS-U22-02` make typed scope the only supported runtime authority
- `TS-U22-03` keep fail-fast handling explicit and safe

**How**:
- shift start checks for authoritative typed scope
- if present, planning proceeds through normalization
- if absent, runtime rejects the contract predictably, logs maintainable diagnostics, and avoids any guessed execution path

**Why this pattern**:
- the project is not yet live, so backward runtime compatibility is unnecessary complexity
- a hard gate is safer than partial execution under ambiguous scope
- it keeps the supported runtime contract clear and testable

---

## PAT-U22-02 — Deterministic Scope Normalization and Batch-Family Segregation

**What**: Typed scope is normalized once into canonical outdoor, animal-building, and greenhouse families, and those families drive a deterministic execution batch structure.

**Applies to**:
- `PERF-U22-01` lightweight scope planning
- `REL-U22-01` strict deterministic pure-output behavior
- `REL-U22-03` animal and greenhouse behavior stay predictably separated
- `TS-U22-01` stay on existing runtime shell
- `TS-U22-06` keep normalization and batch-family shaping deterministic
- `TS-U22-07` keep runtime planning synchronous and lightweight

**How**:
- `WorkScopeClassifier` normalizes `ContractScopeSelection`
- normalized outdoor zones, animal-building scopes, and greenhouse scope are ordered canonically
- runtime batch families are derived from that canonical scope set
- greenhouse work stays separate from outdoor work, and outdoor-zone changes never affect selected-building animal eligibility

**Why this pattern**:
- it satisfies the strict determinism bar without adding caches or async planning
- it gives code generation one clear planning seam to harden
- it keeps runtime-alignment logic out of incidental menu or persistence ordering

---

## PAT-U22-03 — Task-Owned Routing with Sidecar Provenance

**What**: Delivery routing stays keyed by `TaskKind`, while scope provenance travels alongside buffered output solely for explanation and categorization.

**Applies to**:
- `REL-U22-04` task-owned routing remains stable under richer provenance
- `MAINT-U22-01` scope authority and categorization stay in deterministic seams
- `TS-U22-04` keep destinations task-owned and add provenance separately

**How**:
- buffered output retains `SourceTask`
- destination lookup continues through the existing task-owned assignment map
- additional provenance metadata records whether the output came from outdoor, greenhouse, or animal-building work
- destination resolution ignores provenance, but categorization consumes it

**Why this pattern**:
- preserves the existing player-facing destination model
- avoids multiplying destinations by scope family or building
- enables richer overflow explanations without destabilizing delivery routing

---

## PAT-U22-04 — Existing Mail Pipeline with Deterministic Scope-Aware Categorization

**What**: Scope-aware overflow and unassigned-output explanation is layered onto the existing farmhand mail path through a deterministic categorization step, not through a new mail subsystem.

**Applies to**:
- `PERF-U22-03` scope-aware mail shaping stays bounded
- `USAB-U22-01` scope-aware overflow letters must be clear and concise
- `USAB-U22-03` added specificity should reduce confusion
- `TS-U22-05` keep scope-aware mail shaping on the existing mail pipeline

**How**:
- overflow/unassigned-output records are grouped by cause plus scope provenance
- grouped categories feed the existing `MailDispatcher`
- one shift still prefers one bounded letter, but with richer categorized body lines
- categorization runs over already-buffered output/provenance data rather than over fresh world scans

**Why this pattern**:
- preserves the mod’s no-item-loss and one-letter-per-shift mental model
- improves player clarity without adding mail delivery complexity
- keeps categorization deterministic and easy to regression-test

---

## PAT-U22-05 — Thin Orchestrator, Narrow Scope-Support Helpers

**What**: `ShiftOrchestrator` remains the live world adapter, while narrower helper seams own normalization, support gating, routing, and scope-aware categorization.

**Applies to**:
- `MAINT-U22-01` scope authority and normalization stay in pure or near-pure seams
- `MAINT-U22-04` no new runtime architecture required
- `TS-U22-01` stay on the existing SMAPI runtime shell

**How**:
- orchestrator initiates shift start and live world actions
- narrow helpers decide scope support, normalize scope, preserve task-owned routing, and shape categorized overflow inputs
- orchestrator coordinates rather than inventing the new alignment rules locally

**Why this pattern**:
- prevents scope-alignment logic from collapsing into a large world-specific branch set
- keeps the hardest logic testable with generated mixed-scope inputs
- supports an incremental retrofit instead of a runtime rewrite

---

## PAT-U22-06 — Dedicated Mixed-Scope Regression Support

**What**: U-22’s stronger regression bar is satisfied through explicit mixed-scope example tests plus property-based coverage for normalization, routing, and categorized mail invariants.

**Applies to**:
- `MAINT-U22-02` strong example + property coverage
- `MAINT-U22-03` property coverage must target alignment invariants
- `PBT-U22-01` through `PBT-U22-05`
- `TS-U22-08` tests stay on `xUnit` + `FsCheck`

**How**:
- example tests pin concrete stories such as greenhouse-vs-outdoor separation, animal-zone independence, and unsupported no-scope contract rejection
- FsCheck generators produce mixed combinations of outdoor zones, selected buildings, greenhouse selection, task sets, destination maps, and overflow causes
- property tests verify deterministic normalized scope, invariant task-owned routing, and stable categorized mail outputs

**Why this pattern**:
- U-22’s hardest risks come from combinations of scope families rather than single isolated calls
- generated mixed-scope inputs are the best fit for the enabled partial PBT mode
- dedicated test-side helpers prevent the production runtime from carrying extra complexity just for verification

---

## Pattern Summary

U-22’s NFR design stays intentionally focused:
- one authoritative typed-scope gate at shift start
- one deterministic scope-normalization and batch-segregation path
- one task-owned routing model with sidecar provenance
- one scope-aware categorization step on the existing mail pipeline
- one thin-orchestrator / narrow-helper split
- one dedicated mixed-scope regression-support strategy

That gives the scope-alignment retrofit a strong performance, determinism, and clarity bar without introducing new runtime infrastructure or a legacy compatibility path.
