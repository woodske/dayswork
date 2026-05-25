# U-19 — NFR Requirements

**Unit**: U-19 — Contract Snapshot Persistence + Legacy Cleanup

U-19 is a persistence retrofit unit. Its NFR surface is centered on **lightweight synchronous save/load**, **strict schema-v2 determinism**, **best-effort preservation of valid redesigned contracts**, and **strong persistence regression coverage**. NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=C, NFR-Q5=A. Functional-design decisions FD-Q1=A through FD-Q8=A apply throughout.

---

## Performance

### PERF-U19-01 — Save/load remains synchronous and lightweight (NFR-Q1=A)
Schema-v2 serialization and deserialization must remain cheap enough to run inline during normal SMAPI save/load events without background work, chunking, or noticeable hitching for Dayswork's small contract counts.

This unit is not permitted to require:
- background serialization workers
- staged save pipelines
- deferred hydration after `SaveLoaded`

### PERF-U19-02 — Persistence cost stays bounded by contract count and snapshot size
Serializer work is bounded by:
- number of saved contracts
- size of each contract's typed scope
- size of each contract's saved terms snapshot
- number of saved pricing line items and action-cost entries

Implementation should stay linear in those inputs and avoid repeated re-materialization of equivalent intermediate state within one serialize/deserialize pass.

### PERF-U19-03 — No speculative optimization over a tiny data set
U-19 should not introduce complexity such as caching serialized fragments, incremental save diffs, or custom binary formats. The chosen performance target is met by keeping the implementation simple and explicit.

---

## Reliability & Correctness

### REL-U19-01 — Schema-v2 output is strictly deterministic (NFR-Q2=A)
Equivalent current-schema contracts must serialize to the same structural content and stable ordering across runs and machines, aside from explicitly variable envelope metadata such as mod version.

This includes deterministic stability for:
- contract ordering within the envelope
- typed scope content ordering
- persisted pricing line ordering
- persisted action-cost table structural content

### REL-U19-02 — Determinism must not depend on incidental collection order
No saved ordering may depend on raw enumeration order from hash-based collections or runtime-specific dictionary behavior. Canonical ordering must be enforced explicitly before persisted arrays or maps are emitted.

### REL-U19-03 — Best-effort preservation of valid redesigned contracts is mandatory (NFR-Q3=A)
When a save payload contains a mix of:
- valid schema-v2 contracts
- malformed schema-v2 contracts
- legacy schema-v1 data

valid schema-v2 contracts must still survive load whenever possible. One bad entry must not cause avoidable loss of unrelated valid redesign contracts.

### REL-U19-04 — Legacy-drop behavior is a handled compatibility outcome, not a fault
Schema-v1 envelopes being dropped under the approved pre-release policy is a normal supported behavior of the redesign, not a crash path and not an exceptional failure.

### REL-U19-05 — Malformed current-schema entries are isolated failures
Malformed v2 contracts are a contract-level failure only. They may be skipped with diagnostics, but they must not poison sibling valid v2 contracts in the same payload.

---

## Safety & Data Integrity

### SAFE-U19-01 — Authoritative redesign fields must round-trip without loss
For valid v2 contracts, the authoritative persisted redesign fields must round-trip without semantic drift:
- `ContractScopeSelection`
- `ContractTermsSnapshot`
- enabled tasks
- destinations
- schedule
- status
- hire date

### SAFE-U19-02 — No migration guesswork for legacy hourly data
U-19 must not attempt best-effort reinterpretation of legacy schema-v1 contracts as schema-v2 contracts. The approved safety policy is explicit drop, not risky reconstruction.

### SAFE-U19-03 — Bridge fields are structurally retained, not semantically authoritative
`DepositAmount`, `HourlyRate`, and compatibility `Zones` may continue to exist during the retrofit, but they are not the authoritative redesign data model. The authoritative safety bar is on typed scope and terms snapshot round-trip.

### SAFE-U19-04 — Current-schema validity requires redesign completeness
A schema-v2 contract missing required redesign data such as typed scope or terms snapshot is unsafe to auto-heal and must be dropped rather than silently repaired.

---

## Maintainability & Testability

### MAINT-U19-01 — Persistence logic remains in Core-only seams
Serializer, DTO mapping, and store behavior remain in `Dayswork.Core` with no SMAPI/Stardew dependencies, preserving direct unit-testability and matching `NFR-MAINT-03`.

### MAINT-U19-02 — Strong example + property coverage is required (NFR-Q5=A)
Because U-19 is the seam where redesigned contract meaning becomes long-lived save data, it carries stronger test rigor than a typical bridge unit. It requires:
- focused example-based serializer/store tests
- meaningful FsCheck round-trip and invariant coverage
- explicit mixed-payload survival tests

### MAINT-U19-03 — Property coverage must target persistence invariants, not just happy-path I/O
At minimum, the FsCheck suite for U-19 must exercise:
- valid schema-v2 round-trip fidelity
- schema-v1 drop behavior
- mixed valid/malformed v2 survival
- `ReplaceTermsSnapshot` invariants
- deterministic persisted structural ordering

### MAINT-U19-04 — Compatibility bridge hardening is intentionally minimal for now (NFR-Q4=C)
There are no active legacy consumers yet, so U-19 should not overinvest in elaborate compatibility-harness infrastructure. If a remaining old consumer becomes awkward to support through the bridge, the preferred response is to update that consumer quickly rather than expand bridge complexity.

This does **not** remove the bridge fields functionally; it lowers the non-functional hardening bar around them.

---

## Compatibility / Retrofit Support

### COMPAT-U19-01 — Current-schema save files remain human-readable
The schema-v2 envelope remains readable JSON to support debugging, diffs, and manual inspection during the retrofit.

### COMPAT-U19-02 — Bridge fields remain present while consumers still compile against them
Even with a minimal hardening bar, compatibility-facing fields must still be present in the current-schema persistence path until later retrofit units remove or replace those consumers.

### COMPAT-U19-03 — No dedicated migration subsystem is required
The approved compatibility strategy is:
- versioned schema split
- legacy v1 drop
- explicit DTO mapping

No external migration framework, migration history registry, or multistage upgrader is required.

---

## Availability / Security / Infrastructure

### AVAIL-U19-01 — No availability-specific requirements
U-19 is an in-process local save-data seam inside a single-player SMAPI mod. It has no separate uptime, failover, or disaster-recovery surface.

### SEC-U19-01 — Security Baseline is N/A
Security Baseline is disabled project-wide (`NFR-SEC-01`). U-19 has no network, auth, or PII surface. Security Baseline rules are N/A for this unit.

### INFRA-U19-01 — No infrastructure decisions introduced
U-19 requires no external database, queue, background worker, or deployment artifact beyond the existing `.NET 6` / SMAPI mod environment.

---

## Property-Based Testing Obligations

### PBT-U19-01 — Schema-v2 round-trip invariants
Valid current-schema contracts must survive serialize/deserialize with no loss of authoritative redesign scope, authoritative terms snapshot, or preserved metadata.

### PBT-U19-02 — Legacy-drop invariants
Schema-v1 envelopes must deserialize to no contracts under the approved pre-release cleanup policy.

### PBT-U19-03 — Mixed-payload survival invariants
In payloads containing both valid and malformed schema-v2 contracts, valid contracts must survive and malformed ones must be isolated.

### PBT-U19-04 — Terms-replacement invariants
`ReplaceTermsSnapshot` must change only the saved terms snapshot while preserving id, scope, schedule, status, destinations, and other non-terms fields.

### PBT-U19-05 — Deterministic persisted-structure invariants
Equivalent contracts must serialize to stable structural content and ordering across repeated executions.
