# Business Logic Model — U-SVE-01 Expansion-Compatibility Provider Foundation

## Overview

U-SVE-01 introduces the expansion-compatibility seam and its detection/selection. It delivers the **Vanilla profile fully working** and the **SVE profile present but with empty override tables** (entrance, content, work-location, premium-tier tables are filled by U-SVE-02..04). Therefore, even with SVE installed, this unit alone produces **no behavior change** — it only makes the mod *expansion-aware*. That makes U-SVE-01 independently verifiable.

Decisions: FD Q1=A (capacity = trough-tile count clamped to `MaxOccupants`, vanilla parity verified at code-gen), Q2=A (deterministic priority-scan selection, Vanilla default), Q3=A (detect once at `GameLaunched`, cached, logged once; Vanilla operations are no-ops).

---

## Flow 1 — Expansion detection & profile selection (startup, once)

```
GameLaunched
  -> ExpansionDetector.CollectInstalledExpansionIds()
       reads IModRegistry.IsLoaded(id) for each known expansion id
  -> ExpansionProfileSelector.Select(installedIds)
       priority-ordered scan of known profiles' detection predicates
       first match wins; no match -> VanillaExpansionProfile
  -> active IExpansionProfile cached for the session
  -> ExpansionCompatService constructed with (activeProfile, AnimalBuildingCapacityPolicy)
  -> log once at debug: "Dayswork expansion profile: <Id>"
```

- Known profiles at this unit: `SveExpansionProfile` (detects `FlashShifter.StardewValleyExpandedCP`, also recognizes `FlashShifter.SVECode`) and `VanillaExpansionProfile` (always-true fallback, lowest precedence).
- Selection is a **pure function** of the installed-id set (testable without SMAPI).

## Flow 2 — Animal-building feed-capacity derivation (pure)

```
AnimalBuildingCapacityPolicy.DeriveCapacity(AnimalBuildingCapacityInputs)
  inputs: TroughTileCount (real "Trough" Back-layer tiles), MaxOccupants
  capacity = clamp(TroughTileCount, 0, MaxOccupants)        // Q1=A
```

- The Mod-side seam (`ExpansionCompatService.ResolveAnimalFeedCapacity(AnimalHouse)`) gathers the live inputs (counts the building's "Trough" tiles, reads `MaxOccupants`) and calls the pure policy.
- **Vanilla parity** is verified during Code Generation against vanilla coop/barn maps; if a vanilla tier's real trough count differs from the old `4/8/12` ladder, the trough-true value is authoritative (the feeding loop already places one hay per real trough tile, so this matches actual behavior).
- This unit defines and unit-tests the **pure policy**; the `AnimalTaskHandler` call-site swap is exercised here only enough to wire it (full premium behavior is U-SVE-03).

## Flow 3 — Vanilla fall-through (seam operations)

Every `ExpansionCompatService` operation consults the active profile and, for `VanillaExpansionProfile`, returns "no override":

| Operation | Vanilla profile result | Consumer behavior |
|---|---|---|
| `TryGetFarmEntranceOverride` | `false` | existing `Farm.warps` heuristic + `(77,15)` fallback |
| `TryClassifyContentOverride` | `false` | existing `ObjectTargetClassifier` result (incl. skip) |
| `IsExpansionWorkLocation` | `false` | location not added as an expansion work location |
| `ResolveAnimalBuildingTier` | returns the passed-in vanilla tier | unchanged scope/pricing |
| `ResolveAnimalFeedCapacity` | trough-count-clamped capacity (the one intentional general improvement) | feeds real troughs |

With the **SVE profile** active in *this unit*, all override tables are empty, so `TryGet*`/`TryClassify*`/`IsExpansionWorkLocation` also return `false` and `ResolveAnimalBuildingTier` returns the vanilla tier — identical to vanilla until U-SVE-02..04 populate the tables.

---

## Data flow & boundaries

- **Pure (Core)**: `IExpansionProfile` + `VanillaExpansionProfile`/`SveExpansionProfile` data and lookups; `ExpansionProfileSelector`; `AnimalBuildingCapacityPolicy`. No SMAPI/Stardew refs → fully unit/PBT-testable.
- **Mod adapter**: `ExpansionDetector` (queries `IModRegistry`), `ExpansionCompatService` (operates on live `Farm`/`AnimalHouse`/`GameLocation`/`Building`, delegates decisions to the pure layer).
- **No persistence change**: the active profile is derived at runtime; nothing is saved.

## This-unit scope guardrail

- SVE override tables (`SveExpansionProfile`) are present but empty/stubbed; populated by later units.
- The only behavior that can differ from the prior release in this unit is `ResolveAnimalFeedCapacity` (capacity now trough-derived). This is gated by the vanilla-parity verification in Code Generation, so vanilla buildings keep their effective feeding behavior.
