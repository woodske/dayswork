# U-03 Config Foundation — Code Summary

**Unit**: U-03 Config Foundation
**Stage completed**: Code Generation (Part 2)
**Date**: 2026-05-18

---

## Files produced

| File | Type | Status |
|---|---|---|
| `Dayswork.Core/Domain/TaskKind.cs` | Application code | Created |
| `Dayswork.Core/Config/IConfigSnapshot.cs` | Application code | Created |
| `Dayswork.Core/Config/ConfigSnapshot.cs` | Application code | Created |
| `Dayswork.Core/Config/ConfigDefaults.cs` | Application code | Created |
| `Dayswork.Tests/Generators/ConfigSnapshotGen.cs` | Test code | Created |
| `Dayswork.Tests/Config/ConfigDefaultsTests.cs` | Test code | Created |
| `Dayswork.Tests/Config/ConfigSnapshotGenSmokeTests.cs` | Test code | Created |

**Implementation note**: `ConfigSnapshot.Equals(ConfigSnapshot?)` is manually implemented (no modifier — suppresses record synthesis) to compare `TaskIncrements` by structural content rather than reference. `GetHashCode` is likewise overridden for consistency. This enables the `Build_is_deterministic` test to exercise value equality across two independently constructed snapshots.

---

## PBT compliance summary

| Rule | Status | Notes |
|---|---|---|
| PBT-02 Round-trip | N/A | No serialization in U-03 |
| PBT-03 Invariant | Compliant | `ConfigSnapshotGenSmokeTests` asserts every generated `IConfigSnapshot` satisfies all 7 INV-CFG-* invariants (100 samples default) |
| PBT-07 Generator quality | Compliant | `ConfigSnapshotGen.Snapshot()` produces only invariant-preserving snapshots; documented for U-05+ reuse |
| PBT-08 Shrinking + reproducibility | Compliant | Inherited from U-02 wiring; `[Property]` attribute provides seed + shrunk input on failure |
| PBT-09 Framework selection | Compliant | FsCheck.Xunit 2.16.5 wired in U-02 |

---

## Verification results

```
dotnet build Dayswork.Tests/Dayswork.Tests.csproj
  → Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test Dayswork.Tests/Dayswork.Tests.csproj
  → Total tests: 22
       Passed:  21
      Skipped:   1  (PBT-08 demo — expected)
       Failed:   0
```

Test breakdown:
- 2 U-02 smoke tests (XUnit + FsCheck framework) — passed
- 12 `ConfigDefaultsTests` (8 facts + 10-row theory = 12 test cases) — all passed
- 1 `ConfigSnapshotGenSmokeTests` PBT (100 samples, all INV-CFG-* invariants) — passed
- 1 PBT-08 seed demo — skipped (intentional)

---

## What U-04 inherits

- `Dayswork.Core.Domain` namespace is live; U-04 adds `Zone`, `TileCoord`, `ChestRef`, `DestinationKey` to the same namespace.
- `Dayswork.Core.Config` namespace is live; U-05+ pricing units consume `IConfigSnapshot` via constructor injection.
- `ConfigSnapshotGen` is available in `Dayswork.Tests.Generators` for U-05+ pricing PBTs.
- `ConfigSnapshot.Equals` structural equality is available for any test that builds expected/actual snapshots.
