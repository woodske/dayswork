# U-07 Capability & Priority Core — Code Summary

**Unit**: U-07 — Capability & Priority Core
**Status**: Complete
**Build**: 0 errors, 0 warnings
**Tests**: 45 new tests pass; 0 regressions (145 total passed, 1 skipped)

---

## Files created

### Dayswork.Core — Domain types (2 files)

| File | Type | Description |
|---|---|---|
| `Dayswork.Core/Domain/ToolLevel.cs` | `enum` | Basic=0..Iridium=4; values match SV's internal UpgradeLevel int |
| `Dayswork.Core/Domain/ToolSnapshot.cs` | `sealed record` | Immutable 6am snapshot: AxeLevel, PickaxeLevel, WateringCanLevel |

### Dayswork.Core — Capability types (5 files)

| File | Type | Description |
|---|---|---|
| `Dayswork.Core/Capabilities/AxeTarget.cs` | `enum` | StandingTree, FruitTree, SmallStump, LargeStump, LargeLog |
| `Dayswork.Core/Capabilities/PickTarget.cs` | `enum` | SmallRock, LargeBoulder, Meteorite |
| `Dayswork.Core/Capabilities/CapabilityMatrix.cs` | `static class` | Spec threshold table; FruitTree unconditional false first (FR-SKIP-03) |
| `Dayswork.Core/Capabilities/ICapabilityEvaluator.cs` | `interface` | CanChop + CanBreak |
| `Dayswork.Core/Capabilities/CapabilityEvaluator.cs` | `sealed class` | Delegates to CapabilityMatrix; stateless |

### Dayswork.Core — Priority orderer (2 files)

| File | Type | Description |
|---|---|---|
| `Dayswork.Core/Shifts/ITaskPriorityOrderer.cs` | `interface` | Order(IEnumerable<TaskKind>) → IReadOnlyList<TaskKind> |
| `Dayswork.Core/Shifts/TaskPriorityOrderer.cs` | `sealed class` | Static rank dictionary + LINQ OrderBy; FR-WORK-03 table |

### Dayswork.Tests (3 files)

| File | Tests | Description |
|---|---|---|
| `Dayswork.Tests/Generators/ToolSnapshotGen.cs` | N/A | FsCheck Arbitrary<ToolSnapshot> via Gen.Elements over ToolLevel (PBT-07) |
| `Dayswork.Tests/Capabilities/CapabilityEvaluatorTests.cs` | 40 `[Theory]` cases | 20 CanChop + 5 FR-SKIP-03 + 15 CanBreak exhaustive table cases |
| `Dayswork.Tests/Shifts/TaskPriorityOrdererTests.cs` | 3 `[Fact]` + 2 `[Property]` | All-tasks order, single, empty; determinism + ordering invariants (PBT-03) |

---

## Test results

| Category | Count | Result |
|---|---|---|
| CapabilityEvaluator [Theory] cases | 40 | All pass |
| TaskPriorityOrderer [Fact] | 3 | All pass |
| TaskPriorityOrderer [Property] PBT-03 (1000 inputs each) | 2 | All pass |
| Prior suite (U-02 through U-06) | 100 | No regressions |
| **Total new** | **45** | |

---

## NFR compliance

| NFR | Status | Evidence |
|---|---|---|
| NFR-MAINT-03 | Compliant | No `using StardewValley` or `using StardewModdingAPI` in any new Core file |
| PBT-03 | Compliant | 2 invariant properties on TaskPriorityOrderer; both pass at 1000 inputs |
| PBT-07 | Compliant | ToolSnapshotGen.ArbToolSnapshot available for downstream use |

---

## Key design decisions

- **FruitTree unconditional first branch**: `AxeTarget.FruitTree => false` is evaluated before any `axeLevel` comparison in the switch expression — FR-SKIP-03 cannot be accidentally bypassed
- **`ArgumentOutOfRangeException` on unknown enum values**: Both `CapabilityMatrix` methods and `TaskPriorityOrderer.Order` throw on unknown values (via switch default arm and dictionary `KeyNotFoundException` respectively) — defensive against future enum additions
- **LINQ `OrderBy` for stable sort**: .NET's LINQ `OrderBy` is a stable sort; with unique ranks per `TaskKind` this doesn't affect correctness, but the stability guarantee is free
- **No C# 12 features**: All code uses C# 10 syntax (no collection expressions, no raw string literals) consistent with prior units
