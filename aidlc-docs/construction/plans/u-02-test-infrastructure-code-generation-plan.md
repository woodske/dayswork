# U-02 Test Infrastructure — Code Generation Plan

**Unit**: U-02 Test Infrastructure (see [unit-of-work.md](../../inception/application-design/unit-of-work.md))
**Stage**: CONSTRUCTION → U-02 → Code Generation (Part 1: Planning)
**Workspace root**: `C:\Users\kwood\Repos\dayswork`

---

## Unit context

### Stories assigned to U-02
- **S-19** — "Pure logic separable from SMAPI for testability" (PBT-08 + PBT-09 infrastructure obligations land in this unit; per-component PBT-02 / PBT-03 obligations land in U-04, U-05, U-06, U-10)

### Components owned
- **None** (U-02 is test scaffolding, not production code)

### Dependencies on other units
- **U-01 Project Scaffold** — needs `Dayswork.sln` and `Dayswork.Core/Dayswork.Core.csproj` (extends the .sln; references the Core project)

### Dependencies this unit unblocks
- **U-03 Config Foundation** through **U-07 Capability & Priority Core** — each foundation unit drops test files and FsCheck generators into the framework U-02 establishes
- **U-10 Minimum Worker Shift** — PBT obligations for ShiftStateMachine + ItemBuffer live in this test project

### Skipped Construction stages for U-02
| Stage | Decision | Rationale |
|---|---|---|
| Functional Design | SKIP | No business logic in test infrastructure |
| NFR Requirements | SKIP | This unit *is* the NFR fulfillment (NFR-MAINT-01/02 testability; PBT-08/09 infrastructure obligations) — no separate NFR doc to produce |
| NFR Design | SKIP | Cascades from NFR Requirements skip |
| Infrastructure Design | SKIP | Per execution plan, all units skip Infra |

### Definition of Done (carry-over from [unit-of-work.md](../../inception/application-design/unit-of-work.md))
> `dotnet test` runs the stub property and passes; deliberately failing a property prints both the seed AND the shrunk input to console.

### PBT compliance scope for U-02 (Partial enforcement mode)
| Rule | Status in U-02 | Notes |
|---|---|---|
| PBT-02 Round-trip | N/A in U-02 | Per-component obligation; lands in U-04 (Zone), U-06 (SaveData), U-10 (ItemBuffer) |
| PBT-03 Invariant | N/A in U-02 | Per-component obligation; lands in foundation units |
| PBT-07 Generator quality | **Compliant** | `Dayswork.Tests/Generators/` namespace established; convention documented in README |
| PBT-08 Shrinking + reproducibility | **Compliant** | FsCheck.Xunit's `[Property]` attribute prints seed + shrunk input on failure by default; smoke-test demonstrates this is wired up |
| PBT-09 Framework selection | **Compliant** | FsCheck.Xunit 2.16.x included as PackageReference; documented in `Dayswork.Tests/README.md` and aidlc-state.md Extension Configuration |

---

## Code Generation Steps (Part 2 — executes after approval)

### Step 1 — Create `Dayswork.Tests` project file
- [x] Create `Dayswork.Tests/Dayswork.Tests.csproj`:
  - `<TargetFramework>net6.0</TargetFramework>`, `<Nullable>enable</Nullable>`, `<LangVersion>10.0</LangVersion>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (mirrors U-01 conventions)
  - `<IsPackable>false</IsPackable>` and `<IsTestProject>true</IsTestProject>`
  - PackageReferences:
    - `Microsoft.NET.Test.Sdk` version `17.8.0`
    - `xunit` version `2.6.2` (test framework chosen in Q4)
    - `xunit.runner.visualstudio` version `2.5.4`
    - `FsCheck.Xunit` version `2.16.5` (PBT framework per PBT-09; integrates with xUnit)
    - `coverlet.collector` version `6.0.0` (code coverage for future Build & Test)
  - **ProjectReference only to `..\Dayswork.Core\Dayswork.Core.csproj`** — per [component-dependency.md](../../inception/application-design/component-dependency.md) rule 2, the Tests project must not reference the Dayswork SMAPI project (catches accidental SMAPI coupling at test build time)

### Step 2 — Update `Dayswork.sln` to include Dayswork.Tests
- [x] Edit `Dayswork.sln` (from U-01) to:
  - Add Project line for `Dayswork.Tests` with a fresh project GUID
  - Add ProjectConfigurationPlatforms entries (Debug + Release × Any CPU, both ActiveCfg + Build) for the new project

### Step 3 — Create `Dayswork.Tests/Generators/` namespace placeholder
- [x] Create `Dayswork.Tests/Generators/DaysworkGenerators.cs`:
  - Empty static class `DaysworkGenerators` in namespace `Dayswork.Tests.Generators`
  - File-level comment documenting that foundation units (U-03 ConfigSnapshotGen, U-04 ZoneGen + TileCoordGen, U-06 ContractGen) add their generators here per PBT-07 (centralized + reusable)
  - This empty-now-grows-later pattern lets the PBT-07 convention be enforced from the start

### Step 4 — Create smoke tests proving the framework is wired up
- [x] Create `Dayswork.Tests/Smoke/FrameworkSmokeTests.cs`:
  - One xUnit `[Fact]` test that asserts `Assert.True(true)` — proves xUnit + .NET 6 test SDK + xunit.runner.visualstudio are wired up
  - One FsCheck `[Property]` test that asserts `x + 0 == x` for generated `int x` — proves FsCheck.Xunit is wired up
  - These two tests collectively satisfy the Definition of Done's "dotnet test runs the stub property and passes" clause

### Step 5 — Document seed-logging convention via deliberate-failure example
- [x] Create `Dayswork.Tests/Smoke/SeedLoggingDemoTests.cs`:
  - Contains a **disabled** (`Skip = "demonstrates PBT-08 seed logging — enable to see output"`) `[Property]` test that asserts a deliberately false property (e.g., `x => x != x`)
  - The test header comment explains: "Remove the Skip attribute to confirm FsCheck.Xunit prints the seed (`StdGen ...`) and the shrunk minimal failing input. This is PBT-08 satisfied by default — no custom seed-logging plumbing is required."
  - Leaves the disabled test in the codebase as living documentation; CI never runs it

### Step 6 — Create `Dayswork.Tests/README.md` documenting testing conventions
- [x] Sections:
  - **Project purpose**: pure-Core test project; cannot reference SMAPI/Stardew (compile-time enforcement)
  - **Test framework**: xUnit (Q4 decision) + FsCheck.Xunit (PBT-09 recommendation)
  - **Where tests live**: mirrors `Dayswork.Core/` directory layout — `Dayswork.Tests/Config/` tests `Dayswork.Core/Config/`, `Dayswork.Tests/Pricing/` tests `Dayswork.Core/Pricing/`, etc.
  - **Generators (PBT-07)**: all FsCheck generators live in `Dayswork.Tests/Generators/`. Foundation units add domain generators here. Tests reference them via `Arb.From<T>()` or `[Property(Arbitrary = new[] { typeof(DaysworkGenerators) })]`.
  - **Seed logging (PBT-08)**: FsCheck.Xunit's `[Property]` attribute prints the seed and shrunk minimal failing input on failure automatically. The `SeedLoggingDemoTests` file demonstrates this. No custom plumbing required. To replay a known failure: `[Property(Replay = "(seed1, seed2)")]`.
  - **Running tests locally**: `dotnet test Dayswork.sln`
  - **CI (PBT-09)**: deferred to U-16's Build-and-Test wiring; the existing test output already includes seed values, so the only CI requirement is to capture stdout

### Step 7 — Create code summary doc
- [x] Create `aidlc-docs/construction/U-02-test-infrastructure/code/u-02-code-summary.md` with file list, PBT compliance summary, and verification steps

### Step 8 — Update aidlc-state.md
- [x] Mark U-02 complete; Current Stage advances to U-03 Config Foundation

### Step 9 — Update audit.md
- [x] Append Part 2 execution entry

---

## Files this plan will produce

| File | Type | Purpose |
|---|---|---|
| `Dayswork.Tests/Dayswork.Tests.csproj` | created | xUnit + FsCheck.Xunit + Core-only ref test project |
| `Dayswork.sln` | modified | Add Dayswork.Tests Project + ProjectConfigurationPlatforms entries |
| `Dayswork.Tests/Generators/DaysworkGenerators.cs` | created | Empty placeholder establishing the PBT-07 generators namespace |
| `Dayswork.Tests/Smoke/FrameworkSmokeTests.cs` | created | One xUnit fact + one FsCheck property to prove the framework is wired up |
| `Dayswork.Tests/Smoke/SeedLoggingDemoTests.cs` | created | Disabled deliberate-failure test demonstrating PBT-08 seed + shrunk-input logging |
| `Dayswork.Tests/README.md` | created | Documents the testing conventions for foundation units to follow |
| `aidlc-docs/construction/U-02-test-infrastructure/code/u-02-code-summary.md` | created | Code summary + PBT compliance |
| `aidlc-docs/aidlc-state.md` | modified | Mark U-02 complete; advance to U-03 |
| `aidlc-docs/audit.md` | modified | Part 2 execution log |

**Total**: 5 application-code/config files created, 1 modified (`Dayswork.sln`); 3 documentation files.

---

## Verification approach

1. From workspace root: `dotnet build Dayswork.sln` — expected: 0 errors, 0 warnings (Tests project compiles against only Core)
2. From workspace root: `dotnet test Dayswork.sln` — expected: 2 tests run (1 xUnit fact + 1 FsCheck property), 2 pass, 0 fail, 0 skipped (the SeedLoggingDemo test is skipped via attribute and excluded from the count)
3. Manual PBT-08 verification (optional, demonstrates the default behavior): temporarily remove the `Skip = "..."` attribute from `SeedLoggingDemoTests.cs`, run `dotnet test`, observe console output contains `StdGen (...)` seed value AND the shrunk minimal failing input. Re-add the `Skip`.

---

## Open questions for the user

None — package versions are conservative stable picks. If the user prefers newer versions, flag now and I'll bump the PackageReference numbers in Step 1.
