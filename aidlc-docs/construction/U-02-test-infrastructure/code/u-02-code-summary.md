# U-02 Test Infrastructure — Code Summary

**Unit**: U-02 Test Infrastructure
**Construction loop**: Code Generation only (Functional Design, NFR Requirements, NFR Design, Infrastructure Design all SKIPPED)
**Date completed**: 2026-05-18

---

## Files created

| File | Purpose |
|---|---|
| `Dayswork.Tests/Dayswork.Tests.csproj` | xUnit 2.6.2 + FsCheck.Xunit 2.16.5 test project; references only Dayswork.Core (compile-time SMAPI coupling guard) |
| `Dayswork.Tests/Generators/DaysworkGenerators.cs` | Empty static class establishing the PBT-07 generators namespace; foundation units add generators here |
| `Dayswork.Tests/Smoke/FrameworkSmokeTests.cs` | One xUnit `[Fact]` + one FsCheck `[Property]` — proves both frameworks are wired up |
| `Dayswork.Tests/Smoke/SeedLoggingDemoTests.cs` | Disabled `[Property]` with `Skip` attribute demonstrating PBT-08 default seed + shrunk-input logging |
| `Dayswork.Tests/README.md` | Testing conventions: framework choices, directory layout mirroring Core, PBT-07 generator pattern, PBT-08 seed replay |

## Files modified

| File | Change |
|---|---|
| `Dayswork.sln` | Added `Dayswork.Tests` Project entry (GUID `C3D4E5F6-E7F8-9012-CDEF-123456789012`) and four `ProjectConfigurationPlatforms` entries (Debug + Release × ActiveCfg + Build) |

---

## PBT compliance summary

| Rule | Status | Notes |
|---|---|---|
| PBT-02 Round-trip | N/A | Per-component obligation; lands in U-04 (Zone), U-06 (SaveData), U-10 (ItemBuffer) |
| PBT-03 Invariant | N/A | Per-component obligation; lands in foundation units |
| PBT-07 Generator quality | **Compliant** | `Dayswork.Tests/Generators/` namespace established; `DaysworkGenerators` class is the single generator registration point per PBT-07; convention documented in README |
| PBT-08 Shrinking + reproducibility | **Compliant** | FsCheck.Xunit `[Property]` prints seed + shrunk minimal input on failure by default; `SeedLoggingDemoTests` demonstrates and documents this behavior |
| PBT-09 Framework selection | **Compliant** | FsCheck.Xunit 2.16.5 included as PackageReference; framework choice and usage documented in `README.md` |

---

## Key project properties

### Dayswork.Tests

- `TargetFramework`: net6.0
- `Nullable`: enable, `LangVersion`: 10.0, `TreatWarningsAsErrors`: true (matches Core and Mod)
- `IsPackable`: false, `IsTestProject`: true
- **External packages**: Microsoft.NET.Test.Sdk 17.8.0, xunit 2.6.2, xunit.runner.visualstudio 2.5.4, FsCheck.Xunit 2.16.5, coverlet.collector 6.0.0
- **ProjectReference**: `Dayswork.Core` only — no reference to Dayswork SMAPI project (compile-time enforcement of NFR-MAINT-01/02 testability isolation)

---

## Definition of Done — verification steps

1. Run `dotnet build Dayswork.sln` from workspace root. Expected: **0 errors, 0 warnings** (`TreatWarningsAsErrors=true`).
2. Run `dotnet test Dayswork.sln` from workspace root. Expected: **2 tests run** (1 xUnit fact + 1 FsCheck property), **2 pass, 0 fail, 0 skipped** (`SeedLoggingDemoTests` is excluded from count via `Skip` attribute).
3. **Optional PBT-08 demo**: Remove `Skip = "..."` from `Dayswork.Tests/Smoke/SeedLoggingDemoTests.cs`, run `dotnet test`, confirm console shows `StdGen (...)` seed AND the shrunk minimal failing input. Re-add `Skip` after confirming.

---

## What U-03 inherits from U-02

- `Dayswork.Tests/Generators/DaysworkGenerators.cs` — U-03 adds `ConfigSnapshotGen` to this class (PBT-07)
- `Dayswork.Tests/` directory layout convention — U-03 adds `Dayswork.Tests/Config/` mirroring `Dayswork.Core/Config/`
- Testing conventions documented in `Dayswork.Tests/README.md`
