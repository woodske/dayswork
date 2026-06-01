# Code Generation Plan — U-SVE-01 Expansion-Compatibility Provider Foundation

**Stage**: CONSTRUCTION → U-SVE-01 → Code Generation (Part 1: Planning). This plan is the single source of truth for generation. No code is written until it is approved.

## Unit context

- **Stories**: S-21 (vanilla invariance + SVE auto-detect), S-26 (provider seam / extensibility / PBT).
- **Components**: C-19 `IExpansionProfile`, C-20 `ExpansionProfileSelector`, C-21 `VanillaExpansionProfile`, C-22 `SveExpansionProfile`, C-23 `AnimalBuildingCapacityPolicy`; M-22 `ExpansionDetector`, M-23 `ExpansionCompatService`.
- **Dependencies**: none beyond the existing built baseline. **No** dependency on U-SVE-02..04 (they depend on this unit).
- **Scope guardrail (unit-plan Q3=A)**: ship the *full seam* with the Vanilla profile fully working and the SVE profile's override tables **empty**. **Consumers are NOT rewired in this unit** (entrance/capacity/classification/work-location consumption happens in U-SVE-02..04), so U-SVE-01 introduces **no behavior change** — only detection + the seam existing. This keeps vanilla provably unchanged and avoids unused-injection warnings.
- **Workspace root**: `C:\Users\kwood\Repos\dayswork` (app code never in `aidlc-docs/`).
- **Brownfield**: `ModEntry.cs` is modified in place; all other files are new under `Dayswork.Core/Compat/`, `Dayswork/Compat/`, `Dayswork.Tests/Compat/`.

## Grounded wiring note (verified in `Dayswork/ModEntry.cs`)
`Entry()` builds Core then Mod singletons; an existing `GameLoop.GameLaunched` handler already fetches the MFM API and registers GMCM. Detection will be added **inside that existing handler** (after all mods load), mirroring the MailDispatcher "construct-now, set-dependency-at-GameLaunched" idiom: the compat singletons are constructed in `Entry()` with a default Vanilla profile, and the resolved profile is assigned at `GameLaunched`.

---

## Steps

### Core (pure — `Dayswork.Core/Compat/`)

- [x] **Step 1 — Pure value types.** Create `WorkClassification.cs` (result enum/record mirroring the classifier vocabulary: e.g., `None`/skip + axe/pick categories), `ContentDescriptor.cs` (minimal pure descriptor: resource-clump index, tree type, object/animal type identifiers), `AnimalBuildingCapacityInputs.cs` (`record(int TroughTileCount, int MaxOccupants)`). *(S-26)*
- [x] **Step 2 — `IExpansionProfile.cs`.** The full interface: `Id`, `FarmMapModIds`, `TryGetEntranceOverride`, `TryClassifyContentOverride`, `IsExpansionWorkLocation`, `MapPremiumBuildingTier`. *(S-21, S-26)*
- [x] **Step 3 — `VanillaExpansionProfile.cs`.** Null-Object: `Id="vanilla"`, empty `FarmMapModIds`, all `Try*`→false, `IsExpansionWorkLocation`→false, `MapPremiumBuildingTier`→null (BR-SVE-05 / P-SVE-04). *(S-21)*
- [x] **Step 4 — `SveExpansionProfile.cs`.** `Id="sve"`; `FarmMapModIds` = {`flashshifter.immersivefarm2remastered`, `flashshifter.GrandpasFarm`, `flashshifter.FrontierFarm`}; SVE content IDs (`FlashShifter.StardewValleyExpandedCP`, `FlashShifter.SVECode`) as detection constants; override tables **empty** → all `Try*`/`IsExpansionWorkLocation`→false, `MapPremiumBuildingTier`→null in this unit (BR-SVE-07). Centralizes SVE identifiers (NFR-SVE-07). *(S-26)*
- [x] **Step 5 — `ExpansionProfileSelector.cs`.** `Select(IReadOnlySet<string> installedModIds) → IExpansionProfile`: deterministic, ordered scan; SVE when its content id present; else Vanilla (BR-SVE-01 / P-SVE-05). *(S-21, S-26)*
- [x] **Step 6 — `AnimalBuildingCapacityPolicy.cs`.** `DeriveCapacity(AnimalBuildingCapacityInputs) → int = clamp(TroughTileCount, 0, MaxOccupants)`; total, deterministic (BR-SVE-08/10 / P-SVE-06). *(S-23 foundation)*

### Mod (SMAPI adapters — `Dayswork/Compat/`)

- [x] **Step 7 — `ExpansionDetector.cs`.** Ctor `(IModRegistry, ExpansionProfileSelector, IMonitor)`. `ResolveActiveProfile()`: build installed-id set via `IModRegistry.IsLoaded(...)` for known ids, call selector, log the active profile once at debug; **guarded** — any exception → log warning + return Vanilla (P-SVE-01). *(S-21)*
- [x] **Step 8 — `ExpansionCompatService.cs`.** Ctor `(IExpansionProfile defaultProfile=Vanilla, AnimalBuildingCapacityPolicy, IMonitor)`. `SetActiveProfile(IExpansionProfile)`, `ActiveProfileId`. Runtime operations delegating to the active profile / capacity policy: `TryGetFarmEntranceOverride(GameLocation, out Point)`, `ResolveAnimalFeedCapacity(AnimalHouse)` (counts "Trough" tiles + reads MaxOccupants → policy), `ResolveAnimalBuildingTier(Building, AnimalBuildingTier)`, `TryClassifyContentOverride(GameLocation, TileCoord, out WorkClassification)`, `IsExpansionWorkLocation(GameLocation)`. With empty tables these are no-ops/passthrough now; consumed by later units (P-SVE-02/03). *(S-26)*

### Composition root

- [x] **Step 9 — Wire `ModEntry.cs`.** In `Entry()`: construct `VanillaExpansionProfile`, `SveExpansionProfile`, `ExpansionProfileSelector`, `AnimalBuildingCapacityPolicy`, `ExpansionDetector`, and `ExpansionCompatService` (default Vanilla); expose `internal static ExpansionCompatService ExpansionCompat { get; private set; }`. In the existing `GameLaunched` handler: `ExpansionCompat.SetActiveProfile(detector.ResolveActiveProfile())`. No consumer rewiring in this unit. *(S-21)*

### Tests (`Dayswork.Tests/Compat/`)

- [x] **Step 10 — `ExpansionProfileSelectorTests.cs`.** xUnit examples (no expansion→Vanilla; SVE id present→SVE; SVE+other ids→SVE) + FsCheck properties: determinism, exactly-one, Vanilla-default-when-no-expansion, SVE-when-content-id-present (BR-SVE-01). *(S-21, S-26)*
- [x] **Step 11 — `AnimalBuildingCapacityPolicyTests.cs`.** xUnit examples + FsCheck: result in `[0, MaxOccupants]`, equals clamped trough count, never throws for any ints (BR-SVE-08/10). *(S-23 foundation, S-26)*
- [x] **Step 12 — `ExpansionProfileNoOpTests.cs`.** `VanillaExpansionProfile` and (this-unit) `SveExpansionProfile` return "no override" for all lookups; `MapPremiumBuildingTier`→null (BR-SVE-05/07). *(S-21)*

### Verification & docs

- [x] **Step 13 — Verify.** `dotnet build Dayswork.sln /p:EnableModDeploy=false` (expect 0 warnings / 0 errors) and `dotnet test Dayswork.sln /p:EnableModDeploy=false` (expect all pass). Fix issues before completion.
- [x] **Step 14 — Code summary + state/audit.** Write `aidlc-docs/construction/u-sve-01-expansion-compat-foundation/code/code-summary.md` (files created/modified, vanilla-invariance note, test results); update `aidlc-state.md`; append to `audit.md`.

---

## Story traceability

| Story | Steps |
|---|---|
| S-21 (vanilla invariance + SVE auto-detect) | 2, 3, 4, 5, 7, 9, 10, 12 |
| S-26 (provider seam / extensibility / PBT) | 1, 2, 4, 5, 6, 8, 10, 11 |
| S-23 foundation (capacity policy; consumed in U-SVE-03) | 6, 11 |

## Notes / guardrails
- **No behavior change** in this unit: consumers are not rewired; the seam exists and detects, nothing more.
- Vanilla parity of the capacity policy vs. the legacy ladder is *documented* here and will be exercised against vanilla building data when U-SVE-03 swaps `AnimalTaskHandler.FeedCapacity` to the policy.
- `Dayswork.Core` stays free of SMAPI/Stardew refs (`ExpansionCompatService` lives in `Dayswork`).
- Exact FsCheck API usage will follow the patterns in the existing `Dayswork.Tests` suite.
