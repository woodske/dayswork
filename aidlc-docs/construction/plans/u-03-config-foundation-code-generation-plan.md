# U-03 Config Foundation — Code Generation Plan

**Unit**: U-03 Config Foundation (see [unit-of-work.md](../../inception/application-design/unit-of-work.md))
**Stage**: CONSTRUCTION → U-03 → Code Generation (Part 1: Planning)
**Workspace root**: `C:\Users\kwood\Repos\dayswork`
**Functional Design**: approved (see [functional-design/](../U-03-config-foundation/functional-design/))

---

## Unit context

### Stories assigned to U-03
- None delivered directly. Foundation for **S-13** (GMCM exposes these fields in U-16).

### Components owned
- **C-14 ConfigSnapshot** — `IConfigSnapshot` interface + `ConfigSnapshot` sealed record
- **C-15 ConfigDefaults** — static factory returning spec-default snapshot
- **TaskKind enum** — relocated to U-03 per Q1 decision (see [functional-design/domain-entities.md](../U-03-config-foundation/functional-design/domain-entities.md))

### Dependencies on other units
- **U-01 Project Scaffold** — `Dayswork.Core/Dayswork.Core.csproj`
- **U-02 Test Infrastructure** — `Dayswork.Tests/Dayswork.Tests.csproj`, FsCheck.Xunit, `Generators/DaysworkGenerators.cs` namespace

### Dependencies this unit unblocks
- **U-04 Geometry & Domain Primitives** — extends `Dayswork.Core/Domain/` with Zone, TileCoord, ChestRef, DestinationKey
- **U-05 Pricing Core** — all four calculators constructor-inject `IConfigSnapshot`
- **U-10 / U-13 / U-16** — see Functional Design lifecycle diagram

### Definition of Done
> `dotnet build` succeeds with 0 warnings/errors. `dotnet test` runs ConfigDefaultsTests (all spec defaults assert correctly) + ConfigSnapshotGen smoke PBT (generated snapshots satisfy all INV-CFG-* invariants for ≥100 samples). Existing U-02 smoke tests still pass.

### PBT compliance scope for U-03
| Rule | Status | Notes |
|---|---|---|
| PBT-02 Round-trip | N/A | No serialization in U-03 |
| PBT-03 Invariant | **Compliant** | Smoke PBT: every `ConfigSnapshotGen`-generated snapshot satisfies INV-CFG-01..07 |
| PBT-07 Generator quality | **Compliant** | `ConfigSnapshotGen` added to `Dayswork.Tests/Generators/`; produces valid snapshots only (no invariant violations) |
| PBT-08 Shrinking + reproducibility | **Compliant** | Inherited from U-02 framework wiring; smoke PBT uses `[Property]` attribute → seed + shrunk input on failure |
| PBT-09 Framework selection | **Compliant** | FsCheck.Xunit 2.16.5 already wired in U-02 |

---

## Code Generation Steps (Part 2 — executes after approval)

### Step 1 — Create `Dayswork.Core/Domain/TaskKind.cs`
- [ ] Public enum with 10 values matching FR-TASK-01: `WaterCrops`, `HarvestCrops`, `CollectFruit`, `FeedAnimals`, `PetAnimals`, `CollectAnimalProducts`, `CutTrees`, `ClearRocks`, `ClearWeeds`, `ClearGrass`
- [ ] File-scoped namespace `Dayswork.Core.Domain`
- [ ] No XML doc comments (well-named enum values; comments would rot)

### Step 2 — Create `Dayswork.Core/Config/IConfigSnapshot.cs`
- [ ] Public interface with 6 read-only properties per [domain-entities.md](../U-03-config-foundation/functional-design/domain-entities.md):
  - `int BaseRate { get; }`
  - `IReadOnlyDictionary<TaskKind, int> TaskIncrements { get; }`
  - `double AverageSpeedConstant { get; }`
  - `int HardCapTime { get; }`
  - `int StuckInitialWaitMinutes { get; }`
  - `int StuckPostTeleportWaitMinutes { get; }`
- [ ] File-scoped namespace `Dayswork.Core.Config`
- [ ] `using Dayswork.Core.Domain;` for TaskKind reference

### Step 3 — Create `Dayswork.Core/Config/ConfigSnapshot.cs`
- [ ] Public `sealed record ConfigSnapshot` (positional) implementing `IConfigSnapshot`
- [ ] 6 positional parameters in the order defined in [domain-entities.md](../U-03-config-foundation/functional-design/domain-entities.md)
- [ ] File-scoped namespace `Dayswork.Core.Config`

### Step 4 — Create `Dayswork.Core/Config/ConfigDefaults.cs`
- [ ] Public static class with `public static IConfigSnapshot Build()`
- [ ] Inline dictionary literal with all 10 TaskKind → int entries per spec §Pricing rate table
- [ ] INV-CFG-03 enforcement: foreach over `Enum.GetValues<TaskKind>()` throws `InvalidOperationException` if any key missing
- [ ] Returns `new ConfigSnapshot(...)` wrapped via `new ReadOnlyDictionary<TaskKind, int>(increments)`
- [ ] All default values match [business-rules.md](../U-03-config-foundation/functional-design/business-rules.md) Defaults table

### Step 5 — Create `Dayswork.Tests/Generators/ConfigSnapshotGen.cs`
- [ ] Public static class `ConfigSnapshotGen` in namespace `Dayswork.Tests.Generators`
- [ ] `public static Arbitrary<IConfigSnapshot> Snapshot()` method using FsCheck `Gen` combinators:
  - `BaseRate`: `Gen.Choose(0, 1000)` (non-negative; reasonable upper bound for shrinking)
  - `TaskIncrements`: for each `TaskKind` value generate `Gen.Choose(0, 200)`; wrap in `ReadOnlyDictionary`
  - `AverageSpeedConstant`: `Gen.Choose(1, 100).Select(x => (double)x)` (strictly positive integer doubles; satisfies INV-CFG-04 without floating-point edge cases)
  - `HardCapTime`: `Gen.Choose(1000, 2600)` (valid Stardew time range)
  - `StuckInitialWaitMinutes`, `StuckPostTeleportWaitMinutes`: `Gen.Choose(1, 120)` (≥ 1; reasonable upper bound)
- [ ] Comment header: documents PBT-07 role + that this generator is used by U-05+ pricing PBTs

### Step 6 — Create `Dayswork.Tests/Config/ConfigDefaultsTests.cs`
- [ ] xUnit `[Fact]` tests (per Functional Design "Validation expectations" §, items 1–8):
  - `Build_returns_non_null_snapshot`
  - `Build_BaseRate_is_50`
  - `Build_TaskIncrements_match_spec_rate_table` (parameterized via `[Theory]` + `[InlineData]` for each of the 10 tasks)
  - `Build_TaskIncrements_covers_every_TaskKind_value` (count assertion catches INV-CFG-03)
  - `Build_AverageSpeedConstant_is_positive`
  - `Build_HardCapTime_is_2000`
  - `Build_StuckInitialWaitMinutes_at_least_1`
  - `Build_StuckPostTeleportWaitMinutes_at_least_1`
  - `Build_is_deterministic` — `ConfigDefaults.Build().Equals(ConfigDefaults.Build())` (exercises record value equality)

### Step 7 — Create `Dayswork.Tests/Config/ConfigSnapshotGenSmokeTests.cs`
- [ ] One `[Property(Arbitrary = new[] { typeof(ConfigSnapshotGen) })]` PBT that asserts every generated `IConfigSnapshot` satisfies all 7 INV-CFG-* invariants:
  - `BaseRate >= 0`
  - `TaskIncrements.Values.All(v => v >= 0)`
  - `TaskIncrements.Count == Enum.GetValues<TaskKind>().Length` AND every `TaskKind` is a key
  - `AverageSpeedConstant > 0`
  - `HardCapTime in [1000, 2600]`
  - `StuckInitialWaitMinutes >= 1`
  - `StuckPostTeleportWaitMinutes >= 1`
- [ ] Test exists primarily to fail loudly if `ConfigSnapshotGen` ever drifts from invariant-preserving generation; U-05+ tests will exercise the generator with real assertions

### Step 8 — Create code summary doc
- [ ] Create `aidlc-docs/construction/U-03-config-foundation/code/u-03-code-summary.md` with file list, PBT compliance summary, verification results, what U-04 inherits

### Step 9 — Update aidlc-state.md
- [ ] Advance Current Stage to **U-04 Geometry & Domain Primitives — Functional Design (Pending)**

### Step 10 — Update audit.md
- [ ] Append Part 2 execution entry

### Step 11 — Mark all plan checkboxes [x]
- [ ] This file's Steps 1–10 checkboxes all marked complete

---

## Files this plan will produce

| File | Type | Purpose |
|---|---|---|
| `Dayswork.Core/Domain/TaskKind.cs` | created | 10-value enum (moved from U-04 per Q1) |
| `Dayswork.Core/Config/IConfigSnapshot.cs` | created | Read-only config contract |
| `Dayswork.Core/Config/ConfigSnapshot.cs` | created | Immutable record implementation |
| `Dayswork.Core/Config/ConfigDefaults.cs` | created | Static factory with spec-default values + INV-CFG-03 guard |
| `Dayswork.Tests/Generators/ConfigSnapshotGen.cs` | created | FsCheck arbitrary producing valid snapshots (PBT-07) |
| `Dayswork.Tests/Config/ConfigDefaultsTests.cs` | created | xUnit facts + theory for all defaults |
| `Dayswork.Tests/Config/ConfigSnapshotGenSmokeTests.cs` | created | Smoke PBT validating generator preserves all INV-CFG-* invariants |
| `aidlc-docs/construction/U-03-config-foundation/code/u-03-code-summary.md` | created | Code summary + PBT compliance + verification |
| `aidlc-docs/aidlc-state.md` | modified | Advance to U-04 Functional Design (Pending) |
| `aidlc-docs/audit.md` | modified | Part 2 execution log |

**Total**: 4 production code files + 3 test files = **7 application code files created**; 0 modified; 3 documentation files.

---

## Verification approach

1. From workspace root: `dotnet build Dayswork.Tests/Dayswork.Tests.csproj` — expected: 0 errors, 0 warnings (Tests project's Core dependency means Core also rebuilds).
2. From workspace root: `dotnet test Dayswork.Tests/Dayswork.Tests.csproj --verbosity normal` — expected: previous 2 smoke tests + ~12 ConfigDefaultsTests + 1 ConfigSnapshotGen smoke PBT all pass; 1 PBT-08 demo test still skipped. **Total: ~14 passed, 1 skipped, 0 failed**.
3. Optional: temporarily delete a `TaskKind` entry from `ConfigDefaults.Build()`'s dictionary to confirm the INV-CFG-03 guard throws `InvalidOperationException` with the expected message; restore after confirming.

---

## Open questions for the user

None. All architectural decisions locked during Functional Design (Q1, Q2, Q3). Package versions inherited from U-02 (no new packages needed).
