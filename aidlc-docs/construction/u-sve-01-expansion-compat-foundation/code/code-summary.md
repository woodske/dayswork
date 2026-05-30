# Code Summary — U-SVE-01 Expansion-Compatibility Provider Foundation

**Stage**: CONSTRUCTION → U-SVE-01 → Code Generation (Part 2). Executed the approved 14-step plan.

## What shipped

The isolated expansion-compatibility **seam** plus SVE detection. Per the unit scope guardrail, **consumers were not rewired**, so this unit introduces **no behavior change** — vanilla runs exactly as before, and with SVE installed the mod only logs which profile is active (override tables are empty until U-SVE-02..04).

## Files created

**Core (pure — `Dayswork.Core/Compat/`)**
- `WorkClassification.cs` — `WorkClassificationKind` + `WorkClassification` value type.
- `ContentDescriptor.cs` — `WorldContentKind` + `ContentDescriptor` value type.
- `AnimalBuildingCapacityInputs.cs` — pure capacity inputs record.
- `IExpansionProfile.cs` — the profile contract (Id, FarmMapModIds, Matches, entrance/content/work-location/premium-tier lookups).
- `VanillaExpansionProfile.cs` — Null-Object; all lookups "no override"; matches any set (fallback).
- `SveExpansionProfile.cs` — centralizes SVE ids (`FlashShifter.StardewValleyExpandedCP`, `FlashShifter.SVECode`, the three farm-map ids); matches on SVE content/code id; override tables empty (BR-SVE-07).
- `ExpansionProfileSelector.cs` — deterministic priority-ordered selection; Vanilla fallback.
- `AnimalBuildingCapacityPolicy.cs` — `DeriveCapacity` = `min(max(troughs,0), max(maxOccupants,0))`; total/deterministic.

**Mod (`Dayswork/Compat/`)**
- `ExpansionDetector.cs` — guarded `IModRegistry.IsLoaded` scan → selector → logs the active profile once; any failure falls back to Vanilla.
- `ExpansionCompatService.cs` — the runtime seam: `TryGetFarmEntranceOverride`, `ResolveAnimalFeedCapacity` (trough-count based for now; MaxOccupants bound deferred to U-SVE-03), `ResolveAnimalBuildingTier`, `TryClassifyContentOverride` (no-op until U-SVE-04), `IsExpansionWorkLocation`, `ActiveProfileId`, `SetActiveProfile`.

**Tests (`Dayswork.Tests/Compat/`)**
- `ExpansionCompatGenerators.cs` — FsCheck generator for installed-mod-id sets.
- `ExpansionProfileSelectorTests.cs` — xUnit examples + FsCheck (determinism, SVE-presence tracking, always-one-profile).
- `AnimalBuildingCapacityPolicyTests.cs` — xUnit examples + FsCheck (clamp bounds, totality incl. negatives).
- `ExpansionProfileNoOpTests.cs` — both profiles report "no override" (BR-SVE-05/07); Vanilla matches any; SVE doesn't match without its ids.

## Files modified

- `Dayswork/ModEntry.cs` — added `using Dayswork.Compat;` / `using Dayswork.Core.Compat;`; added `internal static ExpansionCompatService ExpansionCompat`; constructed the seam in `Entry()`; resolved + assigned the active profile inside the existing `GameLaunched` handler (after all mods load).

## Vanilla-invariance note (NFR-SVE-01 / S-21)

No consumer call sites changed. The seam is constructed and detects the active profile, but nothing in the worker/animal/classifier/navigation paths calls it yet. Therefore vanilla behavior is byte-for-byte unchanged, and SVE behavior is unchanged too (empty override tables). The capacity policy is implemented and unit/property-tested but not yet wired into `AnimalTaskHandler` (that swap + vanilla-parity verification is U-SVE-03).

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false` → **0 warnings / 0 errors**.
- `dotnet test Dayswork.sln /p:EnableModDeploy=false` → **336 passed / 1 expected skip / 0 failed** (was 320; +16 from the new Compat tests).

## Story coverage

- **S-21** (vanilla invariance + SVE auto-detect): detection wired; Vanilla/empty-SVE no-op verified.
- **S-26** (provider seam / extensibility / PBT): seam + selector implemented; FsCheck properties for selection and capacity.
- **S-23 foundation**: `AnimalBuildingCapacityPolicy` created + tested (consumed in U-SVE-03).
