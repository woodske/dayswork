# U-07 Capability & Priority Core — Code Generation Plan

## Unit context

**Purpose**: Two pure-logic Core primitives — CapabilityEvaluator (tool-level gate) and
TaskPriorityOrderer (FR-WORK-03 sort). No SMAPI refs. No new NuGet packages.

**Stories implemented**: Foundation for S-08 (task priority), S-09 (capability snapshot,
skip rules), S-19 (PBT-03, PBT-07).

**Dependencies**: U-01 (solution + Core csproj), U-02 (test infra + FsCheck), U-04
(TaskKind enum, existing domain types).

**C# compatibility note**: C# 10 / .NET 6 throughout. No collection expressions (`[]`),
no raw string literals, no `Gen.zip*` (use LINQ query syntax). Consistent with U-05/U-06.

**Workspace root**: `C:\Users\kwood\Repos\dayswork`

---

## Part 1 — Planning checklist

- [x] Step 1: Analyze unit context (functional design, NFR requirements, NFR design loaded)
- [x] Step 2: Create detailed code generation plan (this file)
- [x] Step 3: Include unit generation context (see above)
- [x] Step 4: Create unit plan document (this file)
- [x] Step 5: Summarize plan to user
- [ ] Step 6: Log approval prompt in audit.md
- [ ] Step 7: Wait for explicit approval
- [ ] Step 8: Record approval response in audit.md
- [ ] Step 9: Update progress in aidlc-state.md

---

## Part 2 — Generation checklist

### Domain types (Dayswork.Core/Domain/)

- [ ] **Step 10**: Create `Dayswork.Core/Domain/ToolLevel.cs`
  - `public enum ToolLevel { Basic = 0, Copper = 1, Steel = 2, Gold = 3, Iridium = 4 }`
  - XML doc: maps directly to SV `UpgradeLevel` int

- [ ] **Step 11**: Create `Dayswork.Core/Domain/ToolSnapshot.cs`
  - `public sealed record ToolSnapshot(ToolLevel AxeLevel, ToolLevel PickaxeLevel, ToolLevel WateringCanLevel)`
  - XML doc: immutable; built once at 6am spawn and locked for shift (FR-TOOL-01)

### Capability types (Dayswork.Core/Capabilities/)

- [ ] **Step 12**: Create `Dayswork.Core/Capabilities/AxeTarget.cs`
  - `public enum AxeTarget { StandingTree, FruitTree, SmallStump, LargeStump, LargeLog }`
  - XML doc per value noting the chopping requirement from spec

- [ ] **Step 13**: Create `Dayswork.Core/Capabilities/PickTarget.cs`
  - `public enum PickTarget { SmallRock, LargeBoulder, Meteorite }`
  - XML doc per value

- [ ] **Step 14**: Create `Dayswork.Core/Capabilities/CapabilityMatrix.cs`
  - `public static class CapabilityMatrix`
  - `public static bool CanChop(ToolLevel axeLevel, AxeTarget target)` — switch expression;
    `FruitTree` branch is unconditional `false` first (FR-SKIP-03); then `LargeLog >= Gold`,
    `LargeStump >= Steel`, default `true`
  - `public static bool CanBreak(ToolLevel pickLevel, PickTarget target)` — switch expression;
    `Meteorite >= Gold`, `LargeBoulder >= Steel`, default `true`
  - No constructor; static class cannot be instantiated

- [ ] **Step 15**: Create `Dayswork.Core/Capabilities/ICapabilityEvaluator.cs`
  - `public interface ICapabilityEvaluator`
  - `bool CanChop(ToolSnapshot snap, AxeTarget target)`
  - `bool CanBreak(ToolSnapshot snap, PickTarget target)`

- [ ] **Step 16**: Create `Dayswork.Core/Capabilities/CapabilityEvaluator.cs`
  - `public sealed class CapabilityEvaluator : ICapabilityEvaluator`
  - `CanChop` delegates to `CapabilityMatrix.CanChop(snap.AxeLevel, target)`
  - `CanBreak` delegates to `CapabilityMatrix.CanBreak(snap.PickaxeLevel, target)`
  - No constructor arguments; stateless

### Priority orderer (Dayswork.Core/Shifts/)

- [ ] **Step 17**: Create `Dayswork.Core/Shifts/ITaskPriorityOrderer.cs`
  - `public interface ITaskPriorityOrderer`
  - `IReadOnlyList<TaskKind> Order(IEnumerable<TaskKind> enabledTasks)`
  - XML doc: returns only enabled tasks in FR-WORK-03 order; empty input → empty list

- [ ] **Step 18**: Create `Dayswork.Core/Shifts/TaskPriorityOrderer.cs`
  - `public sealed class TaskPriorityOrderer : ITaskPriorityOrderer`
  - `private static readonly Dictionary<TaskKind, int> s_rank` — FR-WORK-03 table:
    FeedAnimals=0, PetAnimals=1, CollectAnimalProducts=2, WaterCrops=3, HarvestCrops=4,
    CollectFruit=5, ClearWeeds=6, ClearGrass=7, ClearRocks=8, CutTrees=9
  - `Order`: LINQ `.OrderBy(t => s_rank[t]).ToList()` returning `IReadOnlyList<TaskKind>`
  - Unknown `TaskKind` value (not in `s_rank`) → `KeyNotFoundException` propagates naturally
    (no explicit throw needed; dictionary lookup handles it)

### Test generator

- [ ] **Step 19**: Create `Dayswork.Tests/Generators/ToolSnapshotGen.cs`
  - `public static class ToolSnapshotGen`
  - `private static readonly Arbitrary<ToolLevel> ArbToolLevel` — `Arb.From(Gen.Elements(...))`
    with all 5 `ToolLevel` values
  - `public static readonly Arbitrary<ToolSnapshot> ArbToolSnapshot` — LINQ query syntax
    composing three `ArbToolLevel.Generator` calls into `new ToolSnapshot(axe, pick, can)`
  - PBT-07: available to U-10+ downstream tests

### Capability tests

- [ ] **Step 20**: Create `Dayswork.Tests/Capabilities/CapabilityEvaluatorTests.cs`
  - `[Theory] [InlineData(...)]` — `CanChop_ReturnsExpectedResult`: 20 cases covering all
    5 AxeLevel × 4 non-FruitTree AxeTarget combinations (expected results from spec table)
  - `[Theory] [InlineData(...)]` — `FruitTree_AlwaysReturnsFalse_FR_SKIP_03`: 5 cases,
    one per AxeLevel — named test explicitly documenting FR-SKIP-03 hard rule
  - `[Theory] [InlineData(...)]` — `CanBreak_ReturnsExpectedResult`: 15 cases covering all
    5 PickaxeLevel × 3 PickTarget combinations (expected results from spec table)
  - Total: 3 `[Theory]` methods / 40 test cases run by xUnit

### Priority orderer tests

- [ ] **Step 21**: Create `Dayswork.Tests/Shifts/TaskPriorityOrdererTests.cs`
  - `[Fact]` — `AllTenTasksEnabled_ReturnsInFRWork03Order`: passes all 10 `TaskKind` values,
    asserts output sequence matches FR-WORK-03 table exactly
  - `[Fact]` — `SingleTask_ReturnsOneElementList`: passes one `TaskKind`, asserts count == 1
  - `[Fact]` — `EmptyInput_ReturnsEmptyList`: passes empty enumerable, asserts empty list
  - `[Property]` — `AnySubset_OrderIsDeterministic`: `forall subset` →
    `Order(subset).SequenceEqual(Order(subset)) == true`; ≥ 1000 generated inputs (PBT-03)
  - `[Property]` — `AnySubset_OutputIsInSpecPriorityOrder`: `forall subset` → consecutive
    elements satisfy `rank[output[i]] < rank[output[i+1]]`; ≥ 1000 inputs (PBT-03)
  - Total: 3 `[Fact]` + 2 `[Property]` = 5 test methods

### Build and verification

- [ ] **Step 22**: Run `dotnet build Dayswork.sln` — verify 0 errors, 0 warnings;
  confirm no SMAPI/SV imports in `Dayswork.Core/Capabilities/` or `Dayswork.Core/Shifts/`

- [ ] **Step 23**: Run `dotnet test` — verify all new tests pass; verify 0 regressions
  against prior suite (U-02 through U-06: currently 100 passing tests)

### Documentation

- [ ] **Step 24**: Create `aidlc-docs/construction/U-07-capability-priority-core/code/u-07-code-summary.md`

---

## File inventory (15 steps → 14 new files)

| # | File | Type |
|---|---|---|
| 10 | `Dayswork.Core/Domain/ToolLevel.cs` | Production |
| 11 | `Dayswork.Core/Domain/ToolSnapshot.cs` | Production |
| 12 | `Dayswork.Core/Capabilities/AxeTarget.cs` | Production |
| 13 | `Dayswork.Core/Capabilities/PickTarget.cs` | Production |
| 14 | `Dayswork.Core/Capabilities/CapabilityMatrix.cs` | Production |
| 15 | `Dayswork.Core/Capabilities/ICapabilityEvaluator.cs` | Production |
| 16 | `Dayswork.Core/Capabilities/CapabilityEvaluator.cs` | Production |
| 17 | `Dayswork.Core/Shifts/ITaskPriorityOrderer.cs` | Production |
| 18 | `Dayswork.Core/Shifts/TaskPriorityOrderer.cs` | Production |
| 19 | `Dayswork.Tests/Generators/ToolSnapshotGen.cs` | Test |
| 20 | `Dayswork.Tests/Capabilities/CapabilityEvaluatorTests.cs` | Test |
| 21 | `Dayswork.Tests/Shifts/TaskPriorityOrdererTests.cs` | Test |
| 22–23 | Build + test run | Verification |
| 24 | `aidlc-docs/construction/U-07-capability-priority-core/code/u-07-code-summary.md` | Docs |

**Total new test cases**: 40 (capability table) + 5 (orderer facts) + 2 (orderer properties, 1000 inputs each) = 47 test methods
