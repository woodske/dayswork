# U-06 Persistence Core — NFR Design Plan

## Steps

- [x] Step 1: Analyze NFR requirements artifacts
- [x] Step 2: Create this plan
- [x] Step 3: Questions — none required (all patterns determined by NFR Requirements decisions)
- [x] Step 4: Generate NFR design artifacts
  - [x] nfr-design-patterns.md
  - [x] logical-components.md
- [x] Step 5: Present completion message and await approval

---

## Pattern determination (no questions needed)

All five NFR design patterns for U-06 are fully determined from the preceding stages:

| Pattern | Determined by |
|---|---|
| Exception Barrier | NFR-SAFE-03 + Q9-A (skip malformed, warn) |
| Null-Safe Empty Result | NFR-SAFE-03 (null/empty input → empty list) |
| Versioned Envelope | Q6-C (DaysworkSaveDataV1 with SchemaVersion + ModVersion) |
| Immutable Record + `with` | Q3-A (3-state status; store never mutates records in-place) |
| Explicit DTO Mapping Layer | NFR-MAINT-03 + Q1-B (explicit ToString/Enum.Parse; no converter magic) |
| Atomic Hydration | Q2-A (clear-then-populate; no partial state) |
