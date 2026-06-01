# U-SVE-03 — Domain Entities — SVE Animal Buildings

**Unit**: U-SVE-03 · **Story**: S-23 · **Decisions**: Q1=A, Q2=A, Q3=A, Q4=A

All entities below already exist (from U-SVE-01 and the base domain). U-SVE-03 **populates** them; it introduces no new types and no schema change.

## AnimalBuildingCapacityInputs (Dayswork.Core/Compat) — existing
Pure inputs to the capacity policy.
- `TroughTileCount : int` — number of `Trough` map-property tiles in the building interior.
- `MaxOccupants : int` — the building's real max occupants (e.g., 16 for SVE premium; vanilla values for vanilla buildings).
- Invariant: negatives are treated as zero by `AnimalBuildingCapacityPolicy` (BR-SVE3-03).

## AnimalBuildingCapacityPolicy (Dayswork.Core/Compat) — existing, now consumed
- `DeriveCapacity(inputs) = min(max(0, TroughTileCount), max(0, MaxOccupants))`.
- Replaces the `AnimalTaskHandler.FeedCapacity` ladder. Total, deterministic, throw-free.

## IExpansionProfile.MapPremiumBuildingTier(buildingType) — existing signature, SVE table populated
Premium → nearest-vanilla-tier table (the **only** change to `SveExpansionProfile` data in this unit):

| `buildingType` (raw `building.buildingType.Value`) | Mapped `AnimalBuildingTier` |
|---|---|
| `FlashShifter.StardewValleyExpandedCP_PremiumCoop` | `DeluxeCoop` |
| `FlashShifter.StardewValleyExpandedCP_PremiumBarn` | `DeluxeBarn` |
| anything else | `null` (pass-through) |

- `VanillaExpansionProfile.MapPremiumBuildingTier` stays `null` for all inputs (unchanged).

## AnimalBuildingTier (Dayswork.Core/Domain) — existing enum, UNCHANGED
The six vanilla tiers (`Coop`, `BigCoop`, `DeluxeCoop`, `Barn`, `BigBarn`, `DeluxeBarn`). No premium value added (BR-SVE3-07). Premium buildings reuse the `Deluxe*` values.

## ExpansionCompatService (Dayswork/Compat) — existing seams
- `ResolveAnimalFeedCapacity(AnimalHouse) : int` — counts `Trough` tiles, reads the building's real `MaxOccupants`, returns `AnimalBuildingCapacityPolicy.DeriveCapacity(...)`. (U-SVE-03 wires the real `MaxOccupants` upper bound; U-SVE-01 had passed troughs as both args.)
- `ResolveAnimalBuildingTier(Building, vanillaTier) : AnimalBuildingTier` — returns `MapPremiumBuildingTier(buildingType) ?? vanillaTier`.

## Relationships
```
AnimalTaskHandler.CreateFeedWork
  └─ (auto-feed gate unchanged) ─ ExpansionCompatService.ResolveAnimalFeedCapacity
        └─ AnimalBuildingCapacityPolicy.DeriveCapacity(AnimalBuildingCapacityInputs)

hiring enumeration (BuildingOutline.DisplayName = buildingType)
  └─ ExpansionCompatService.ResolveAnimalBuildingTier
        └─ SveExpansionProfile.MapPremiumBuildingTier  (else → vanilla substring inference)
```

## Out of scope (entities deliberately NOT introduced)
- No auto-petter/auto-grabber entity or detector (BR-SVE3-09 — scan & natural skip).
- No premium-specific feed/hopper entity (premium auto-feeds; the existing trough/hopper resolution is reused only for non-auto-feed buildings).
