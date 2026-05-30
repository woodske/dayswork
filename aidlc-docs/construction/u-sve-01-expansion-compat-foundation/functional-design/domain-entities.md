# Domain Entities — U-SVE-01 Expansion-Compatibility Provider Foundation

## Overview

Logical entities for the compat seam foundation. The Core entities are pure (no SMAPI/Stardew refs) and fully unit/PBT-testable. The Mod entities are thin adapters listed for completeness. No persisted data is introduced.

## Core (pure) entities

### Entity: IExpansionProfile
Immutable description of one expansion's compatibility data + pure lookups.

| Member | Meaning |
|---|---|
| `Id` | Stable profile id (e.g., `"vanilla"`, `"sve"`). |
| `FarmMapModIds` | Set of farm-map mod IDs this profile recognizes (used by U-SVE-02). |
| `TryGetEntranceOverride(farmIdentity, out tile)` | Per-map entrance override lookup; false when none. |
| `TryClassifyContentOverride(descriptor, out result)` | Content-classification override; false when none. |
| `IsExpansionWorkLocation(locationName)` | Whether a location is an expansion work location (e.g., Grandpa's Shed). |
| `MapPremiumBuildingTier(buildingType)` | Maps an expansion premium building type to a vanilla `AnimalBuildingTier`; null when not applicable. |

### Entity: VanillaExpansionProfile (implements IExpansionProfile)
Default profile. `Id = "vanilla"`, empty `FarmMapModIds`, all `Try*` return false, `IsExpansionWorkLocation` false, `MapPremiumBuildingTier` null. Guarantees BR-SVE-05.

### Entity: SveExpansionProfile (implements IExpansionProfile)
SVE profile. `Id = "sve"`. In U-SVE-01 it carries only identity + `FarmMapModIds` (`flashshifter.immersivefarm2remastered`, `flashshifter.GrandpasFarm`, `flashshifter.FrontierFarm`); entrance/content/work-location/premium-tier tables are empty (BR-SVE-07) and are populated by U-SVE-02..04. Centralizes all SVE identifiers (NFR-SVE-07).

### Entity: ExpansionProfileSelector
Pure selector. `Select(IReadOnlySet<string> installedModIds) -> IExpansionProfile`. Priority-ordered scan; Vanilla is the lowest-precedence always-match fallback (BR-SVE-01).

### Entity: AnimalBuildingCapacityInputs
Pure input record for capacity derivation.

| Field | Meaning |
|---|---|
| `TroughTileCount` | Count of real "Trough" Back-layer tiles in the animal house. |
| `MaxOccupants` | Building-data max occupants. |

### Entity: AnimalBuildingCapacityPolicy
Pure policy. `DeriveCapacity(AnimalBuildingCapacityInputs) -> int` = `clamp(TroughTileCount, 0, MaxOccupants)` (BR-SVE-08/10).

### Entity: ContentDescriptor
Pure, content-agnostic descriptor passed to classification overrides (e.g., resource-clump parent-sheet index, tree type, object/animal type identifiers). Lets the Core profile decide overrides without referencing live Stardew objects. (Shape finalized as U-SVE-04 needs it; defined minimally here.)

### Entity: WorkClassification
Pure result of a classification override: the resolved work category (e.g., axe target / pick target / none-skip). Mirrors the existing classifier's vocabulary so the Mod adapter can translate.

## Mod (adapter) entities — listed for completeness

### Entity: ExpansionDetector
Queries `IModRegistry.IsLoaded(id)` for known expansion IDs, builds the installed-id set, invokes `ExpansionProfileSelector`, logs the active profile once (BR-SVE-02/03/04).

### Entity: ExpansionCompatService
Holds the active `IExpansionProfile` + `AnimalBuildingCapacityPolicy`; applies them to live `Farm`/`AnimalHouse`/`GameLocation`/`Building` objects. The single seam consumers depend on. Operations: `TryGetFarmEntranceOverride`, `ResolveAnimalFeedCapacity`, `ResolveAnimalBuildingTier`, `TryClassifyContentOverride`, `IsExpansionWorkLocation`, `ActiveProfileId`.

## No persistence changes
No saved data and no player-facing configuration are introduced. The active profile is a runtime-derived, session-cached value.

## Frontend/UI artifact
N/A for this unit. No menus, screens, controls, or localized strings are added (the premium-tier scope UI surfacing is U-SVE-03).

## Testable properties

| Entity / operation | Property category | Property |
|---|---|---|
| `ExpansionProfileSelector.Select` | Invariant | Deterministic; exactly one profile; Vanilla when no expansion id present; SVE when SVE content id present. |
| `VanillaExpansionProfile` | Invariant | All override lookups return "no override" for all inputs. |
| `AnimalBuildingCapacityPolicy.DeriveCapacity` | Invariant | Result in `[0, MaxOccupants]`; equals clamped trough count; total (never throws). |
| `SveExpansionProfile` (this unit) | Invariant | Override tables are empty → all `Try*`/`IsExpansionWorkLocation` false, `MapPremiumBuildingTier` null. |

## Extension Compliance

| Extension | Status | Functional-design compliance |
|---|---|---|
| Security Baseline | Disabled | N/A — no security behavior is introduced. |
| Property-Based Testing | Enabled, full | Compliant — pure entities (`ExpansionProfileSelector`, `AnimalBuildingCapacityPolicy`, profile lookups) carry FsCheck properties into Code Generation. |
