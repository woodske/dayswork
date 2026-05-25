# U-19 — Tech Stack Decisions

**Unit**: U-19 — Contract Snapshot Persistence + Legacy Cleanup

NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=C, NFR-Q5=A. Functional-design decisions FD-Q1=A through FD-Q8=A apply.

---

## TS-U19-01 — Stay on the existing Core persistence stack
U-19 introduces no new persistence framework or package. Implementation stays on the existing stack:
- `Dayswork.Core/Persistence/`
- `Newtonsoft.Json`
- explicit DTO mapping
- in-memory `ContractStore`

This preserves Core purity and avoids migration-framework overhead for a small local save contract.

## TS-U19-02 — Keep save/load synchronous
Serialization and deserialization remain synchronous methods invoked directly during the normal save/load lifecycle. No task-based async API, no background save worker, and no deferred hydration subsystem are introduced.

## TS-U19-03 — Use explicit schema-version branching, not shape inference
Version branching should stay explicit at the envelope level:
- schema v1 -> legacy hourly pre-release data -> drop
- schema v2 -> current redesign persistence path
- future schema -> reject with warning

This keeps compatibility behavior understandable and deterministic.

## TS-U19-04 — Deterministic structural ordering must be enforced explicitly
Implementation must not rely on incidental ordering from dictionaries, sets, or runtime JSON emission behavior. Canonical ordering should be applied before emitting:
- contract collections
- task lists, where persisted as ordered collections
- pricing lines
- action-cost entries, if materialized through ordered collections

## TS-U19-05 — Preserve valid siblings on deserialize
Deserializer implementation should keep contract mapping isolated per entry so malformed contracts can be skipped without losing valid siblings in the same v2 payload.

Practical implication:
- per-contract mapping barrier
- warning/log per bad entry
- collect valid results independently

## TS-U19-06 — Keep the compatibility bridge local and simple
Because there are no active legacy consumers yet, U-19 should not introduce a dedicated compatibility framework, adapter layer, or large bridge test matrix. Compatibility projections and bridge fields should stay simple and local to the serializer/domain hydration seam.

If a remaining old consumer becomes awkward to support, the preferred action is to update that consumer instead of expanding bridge complexity.

## TS-U19-07 — Strong persistence tests remain `xUnit` + `FsCheck`
No new testing framework is introduced. U-19 leans into the existing stack:
- `xUnit` for targeted serializer/store examples
- `FsCheck` for round-trip and mixed-payload invariants

This is the right fit because the unit is mostly about structural fidelity and deterministic data transformations.

## TS-U19-08 — DTO mapping stays explicit and readable
The schema-v2 persistence path should continue favoring explicit DTO mapping rather than reflection-heavy or convention-heavy persistence magic. That makes:
- schema versioning easier to audit
- legacy drop behavior easier to reason about
- deterministic ordering easier to control
- round-trip properties easier to test

## TS-U19-09 — No specialized bridge-hardening infrastructure yet
Since bridge quality was explicitly relaxed (NFR-Q4=C), U-19 does not require:
- dedicated compatibility fuzzing beyond normal regression coverage
- consumer-specific bridge emulation harnesses
- a long-lived bridge abstraction layer

The authoritative redesign fields remain the engineering priority.
