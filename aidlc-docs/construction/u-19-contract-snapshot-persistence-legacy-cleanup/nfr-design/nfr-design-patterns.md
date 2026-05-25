# U-19 — NFR Design Patterns

**Unit**: U-19 — Contract Snapshot Persistence + Legacy Cleanup

NFR design decisions applied: no additional question round required. NFR requirements NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=C, NFR-Q5=A apply, along with functional-design decisions FD-Q1=A through FD-Q8=A.

---

## Applicability Scope

| Category | Applicability |
|---|---|
| Security | **N/A** — Security Baseline is disabled project-wide and U-19 has no network/auth/PII surface |
| Scalability / HA | **N/A** — local in-process save seam with tiny contract counts; no replicas, shards, queues, or distributed scale mechanisms |
| Distributed infrastructure | **N/A** — no database, queue, cache server, or background persistence worker |
| Resilience | **Applicable** — version gating, legacy drop, valid-sibling preservation, malformed-entry isolation |
| Performance | **Applicable** — synchronous save/load and bounded serializer work |
| Determinism / correctness | **Applicable** — schema-v2 structural stability is a hard quality bar |
| Maintainability / testability | **Applicable** — strong explicit persistence property coverage and local/simple bridge design |

---

## PAT-U19-01 — Version-Gated Envelope Branching

**What**: Save payload handling is split first by explicit envelope schema version, not by per-contract shape inference.

**Applies to**:
- `REL-U19-04` legacy-drop behavior is handled, not exceptional
- `SAFE-U19-02` no migration guesswork for legacy hourly data
- `COMPAT-U19-03` no dedicated migration subsystem required
- `TS-U19-03` explicit schema-version branching

**How**:
- schema v1 -> legacy hourly pre-release data -> drop
- schema v2 -> current redesign persistence path
- future schema -> reject with warning

**Why this pattern**:
- makes compatibility behavior explicit and auditable
- avoids fragile shape-inference heuristics
- keeps legacy cleanup simple for an unreleased project

**Not responsible for**:
- per-contract malformed-entry isolation inside v2 payloads
- canonical ordering of emitted schema-v2 content

Those belong to the next patterns.

---

## PAT-U19-02 — Per-Contract Exception Barrier with Valid-Sibling Preservation

**What**: Each current-schema contract entry is mapped behind its own exception barrier so malformed entries are isolated and valid siblings survive.

**Applies to**:
- `REL-U19-03` best-effort preservation of valid redesigned contracts
- `REL-U19-05` malformed current-schema entries are isolated failures
- `TS-U19-05` preserve valid siblings on deserialize
- `PBT-U19-03` mixed-payload survival invariants

**How**:
- envelope-level parsing happens first
- once a v2 envelope is accepted, each contract entry is mapped independently
- mapping failures log diagnostics and skip only the failed contract
- valid contracts continue accumulating

**Why this pattern**:
- prevents one bad save record from destroying unrelated valid redesign contracts
- aligns with the mod’s long-standing skip-and-warn persistence style from U-06
- makes mixed-payload behavior easy to test directly

**Boundary rule**:
- this barrier isolates data-compatibility failures
- it is not a blanket catch-all for programmer errors elsewhere in the runtime

---

## PAT-U19-03 — Canonical Serializer Ordering

**What**: Schema-v2 emission uses explicit canonical ordering before persisted collections are written.

**Applies to**:
- `REL-U19-01` strict schema-v2 determinism
- `REL-U19-02` determinism must not depend on incidental collection order
- `PBT-U19-05` deterministic persisted-structure invariants
- `TS-U19-04` deterministic structural ordering must be enforced explicitly

**How**:
- normalize collection ordering before JSON emission
- never rely on hash-set or dictionary enumeration order
- keep ordering rules local to the serializer/mapping seam

Relevant persisted structures include:
- contract collections inside the save envelope
- typed scope collections where ordering is material
- persisted pricing line items
- persisted action-cost entries if emitted via ordered collections

**Why this pattern**:
- stabilizes diffs and debugging output
- makes round-trip tests cleaner
- prevents machine/runtime-specific ordering drift

---

## PAT-U19-04 — Explicit DTO Mapping and Compatibility Projection

**What**: The schema-v2 persistence path uses explicit DTO mapping for authoritative redesign fields and performs compatibility projection locally during hydration.

**Applies to**:
- `SAFE-U19-01` authoritative redesign fields must round-trip without loss
- `SAFE-U19-03` bridge fields are retained but not authoritative
- `COMPAT-U19-02` bridge fields remain present while needed
- `TS-U19-08` DTO mapping stays explicit and readable

**How**:
- map typed scope directly into schema-v2 DTOs
- map `ContractTermsSnapshot` directly into schema-v2 DTOs
- hydrate bridge-domain `Contract` values from the authoritative redesign fields
- derive compatibility `Zones` locally from saved typed scope rather than treating them as persisted source of truth

**Why this pattern**:
- keeps the redesign source of truth explicit
- avoids hiding scope semantics inside legacy zone conventions
- makes bridge behavior visible and testable without promoting it to a first-class subsystem

---

## PAT-U19-05 — Narrow Immutable Terms Replacement

**What**: Repricing updates use an explicit narrow store mutation instead of forcing a whole-contract rewrite for every terms refresh.

**Applies to**:
- `PBT-U19-04` `ReplaceTermsSnapshot` invariants
- `TS-U19-01` existing Core persistence stack
- functional rule `BR-STORE-01..03`

**How**:
- `ContractStore` gains `ReplaceTermsSnapshot(...)`
- the operation replaces only `TermsSnapshot`
- all other contract data is preserved
- implementation follows the existing immutable-record replacement style rather than in-place mutation

**Why this pattern**:
- matches the conceptual scope of recurring repricing
- reduces risk of accidental unrelated field changes
- creates a crisp invariant for property-based tests

---

## PAT-U19-06 — Local Minimal Compatibility Bridge

**What**: Compatibility handling remains local and intentionally light because there are no active legacy consumers yet.

**Applies to**:
- `MAINT-U19-04` compatibility bridge hardening is intentionally minimal
- `COMPAT-U19-02` bridge fields remain present while needed
- `TS-U19-06` keep the compatibility bridge local and simple
- `TS-U19-09` no specialized bridge-hardening infrastructure yet

**How**:
- keep compatibility projections inside serializer/domain hydration logic
- avoid introducing a separate bridge framework, adapter layer, or compatibility harness subsystem
- if a remaining old consumer becomes awkward, prefer updating that consumer rather than expanding bridge complexity

**Why this pattern**:
- matches the current project state: no active legacy consumers
- avoids investing in complexity that later units are likely to delete
- preserves the authoritative priority of typed scope and terms snapshot

**Important limit**:
- minimal bridge hardening does **not** mean removing bridge fields early
- it means the design avoids overengineering them

---

## PAT-U19-07 — Dedicated Persistence Property-Test Support

**What**: U-19 gets explicit persistence-focused example and property-test support rather than relying only on general serializer smoke tests.

**Applies to**:
- `MAINT-U19-02` strong example + property coverage
- `MAINT-U19-03` persistence invariants, not just happy-path I/O
- `TS-U19-07` strong persistence tests remain `xUnit` + `FsCheck`

**How**:
- add dedicated test-side helpers/generators for schema-v2 contract shapes
- cover round-trip, mixed-payload, deterministic-ordering, and terms-replacement invariants
- keep these helpers on the test side only

**Why this pattern**:
- U-19 is the seam where redesign meaning becomes durable save data
- persistence regressions are easy to miss in playtesting alone
- explicit generators make mixed valid/malformed payloads easy to express and shrink

---

## Pattern Summary

U-19’s NFR design stays intentionally focused:
- one explicit envelope version gate
- one per-contract isolation barrier for v2 loads
- one canonical ordering policy at serializer/mapping time
- one explicit DTO/projection layer for authoritative plus bridge fields
- one narrow immutable repricing mutation seam
- one intentionally local/minimal compatibility bridge
- one dedicated persistence property-test support strategy

That gives the persistence retrofit a high determinism and regression bar without introducing infrastructure or migration machinery the mod does not need.
