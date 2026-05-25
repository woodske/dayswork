# U-19 — Logical Components

**Unit**: U-19 — Contract Snapshot Persistence + Legacy Cleanup

NFR requirements NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=C, NFR-Q5=A apply. Functional-design decisions FD-Q1=A through FD-Q8=A apply throughout.

---

## Component Map

```text
Dayswork.Core/Persistence
  ├── SaveDataSerializer                [existing owned seam, extended]
  ├── ContractStore                     [existing owned seam, extended]
  └── Dto/
       ├── DaysworkSaveDataV2           [NEW envelope DTO]
       ├── ContractDtoV2                [NEW per-contract DTO]
       ├── ContractScopeSelectionDto    [NEW typed scope DTO]
       ├── ContractTermsSnapshotDto     [NEW terms snapshot DTO]
       └── related nested DTOs          [NEW typed scope / pricing / energy carriers]

Dayswork.Tests/Persistence
  ├── U19ExampleTests                   [NEW test-side grouping]
  ├── U19PropertyGenerators             [NEW test-side helper]
  └── U19PropertyTests                  [NEW test-side grouping]
```

No new runtime plugin, migration subsystem, or infrastructure component is introduced. The design deliberately keeps all new behavior inside the existing Core persistence area and test project.

---

## LC-U19-01 — SaveDataSerializer (Extended Ownership)

**Layer**: Core / `Dayswork.Core/Persistence/`  
**Kind**: Existing production seam with expanded redesign ownership

**Purpose under U-19**:
- own envelope-level schema branching
- own schema-v2 serialize/deserialize flow
- own canonical ordering before persisted output
- own per-contract malformed-entry isolation
- own local compatibility projection during hydration

**Responsibilities**:
1. Detect envelope class:
   - null/empty/invalid
   - schema v1 legacy
   - schema v2 current
   - future schema
2. Serialize valid current-schema contracts into v2 DTOs
3. Apply explicit canonical ordering before writing persisted collections
4. Map each v2 contract entry independently during load
5. Derive compatibility `Zones` and bridge financial hydration locally from authoritative redesign fields

**Important design constraints**:
- no async API
- no caching subsystem
- no delegated compatibility framework
- no shape inference for legacy-vs-current contracts

This component is the natural owner of the version gate, per-entry barrier, and canonical ordering policies.

---

## LC-U19-02 — ContractStore (Extended Ownership)

**Layer**: Core / `Dayswork.Core/Persistence/`  
**Kind**: Existing in-memory store with one new narrow mutation seam

**New NFR-design responsibility**:
- own `ReplaceTermsSnapshot(...)` as a narrow immutable update path

**Responsibilities under this design**:
1. Preserve existing add/get/update/cancel/pause/resume/list/hydrate behavior
2. Support narrow recurring repricing updates through `ReplaceTermsSnapshot(...)`
3. Preserve non-terms contract data during that narrow mutation

**Design constraint**:
- use the same immutable replacement style as existing store updates
- no in-place mutation of record fields

This is important because `ReplaceTermsSnapshot(...)` is both a functional requirement and a testable persistence invariant.

---

## LC-U19-03 — Schema-V2 DTO Layer

**Layer**: Core / `Dayswork.Core/Persistence/Dto/`  
**Kind**: New persistence-only data carriers

**Purpose**:
- give the redesigned save contract an explicit, inspectable, versioned shape

**Core members**:
- `DaysworkSaveDataV2`
- `ContractDtoV2`
- typed scope DTOs
- terms snapshot DTOs
- nested pricing/energy DTOs

**Responsibilities**:
1. Represent the authoritative persisted redesign fields directly
2. Keep bridge fields visible but clearly secondary
3. Stay free of business logic beyond being carriers for explicit mapping

**Not responsible for**:
- deciding schema compatibility
- deriving compatibility `Zones`
- mutating domain contracts

Those remain serializer/store concerns.

---

## LC-U19-04 — Local Compatibility Projection

**Layer**: Internal to the serializer/mapping seam, not a standalone subsystem  
**Kind**: Local projection behavior

**Purpose**:
- keep bridge fields functionally present without elevating them into a dedicated architectural subsystem

**Responsibilities**:
1. Derive compatibility `Zones` from authoritative typed scope when hydrating domain contracts
2. Rehydrate persisted bridge financial fields into the bridge-domain `Contract`
3. Keep the compatibility path small enough to delete later when retrofit consumers are gone

**Why this is not a separate large component**:
- NFR-Q4 explicitly chose minimal bridge hardening
- there are no active legacy consumers yet
- a full bridge abstraction would likely become temporary complexity

So this remains a local behavior, not a top-level framework.

---

## LC-U19-05 — Test-Side Support Components

**Layer**: `Dayswork.Tests/Persistence/` only  
**Kind**: Dedicated persistence-focused test helpers

### `U19PropertyGenerators`

**Purpose**:
- generate realistic schema-v2 contract shapes and mixed payloads

**Responsibilities**:
- valid one-time and recurring v2 contracts
- mixed outdoor/animal/greenhouse scope combinations
- persisted pricing line/item variations
- mixed valid + malformed payloads
- contracts suitable for `ReplaceTermsSnapshot(...)` invariants

### `U19ExampleTests`

**Purpose**:
- clear targeted examples for key persistence behaviors

Examples include:
- schema v1 envelope drops cleanly
- one malformed v2 contract does not block a valid sibling
- v2 round-trip preserves authoritative scope and terms
- `ReplaceTermsSnapshot(...)` preserves non-terms fields

### `U19PropertyTests`

**Purpose**:
- express persistence invariants with FsCheck

Examples include:
- deterministic persisted structure
- valid schema-v2 round-trip
- mixed-payload survival
- narrow terms-replacement invariants

These are explicit logical components because U-19’s NFR bar requires more than ordinary serializer unit smoke tests.

---

## Interaction Summary

```text
Save flow
  ContractStore.List()
    -> SaveDataSerializer
         -> schema v2 DTO layer
         -> canonical ordering
         -> JSON output

Load flow
  JSON input
    -> SaveDataSerializer
         -> version gate
         -> per-contract mapping barrier
         -> local compatibility projection
    -> ContractStore.Hydrate()

Recurring repricing flow
  runtime rebuild
    -> ContractStore.ReplaceTermsSnapshot()
```

---

## Why no additional components were introduced

The NFR design intentionally does **not** add:
- a migration framework
- a background save/load worker
- a separate bridge adapter subsystem
- a cache layer
- consumer-specific compatibility harness components

Reason:
- contract counts are tiny
- save/load remains synchronous
- schema compatibility is simple and explicit
- there are no active legacy consumers yet
- the authoritative redesign fields are the long-term priority

That keeps U-19’s persistence redesign sharp, testable, and easy to remove temporary bridge behavior from later.
