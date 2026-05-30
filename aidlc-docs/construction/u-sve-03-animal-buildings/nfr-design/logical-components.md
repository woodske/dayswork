# Logical Components — U-SVE-03 SVE Animal Buildings

NFR responsibilities mapped onto **existing** components. No new components; no new infrastructure.

| Component | Layer | NFR responsibility (U-SVE-03) | Patterns |
|---|---|---|---|
| `AnimalBuildingCapacityPolicy` (Dayswork.Core/Compat) | Pure Core | Total, deterministic `min(troughs, maxOccupants)`; clamp-not-throw. Now consumed with the real `MaxOccupants`. | P-SVE3-01 |
| `SveExpansionProfile` (Dayswork.Core/Compat) | Pure Core | Sole home for premium `buildingType → Deluxe*` table; deterministic O(1) lookup; null for non-premium. | P-SVE3-03, P-SVE3-04 |
| `VanillaExpansionProfile` (Dayswork.Core/Compat) | Pure Core | Null-object: `null` tier for all; preserves vanilla path. | P-SVE3-04 |
| `ExpansionCompatService` (Dayswork/Compat) | Mod adapter (SMAPI) | Only SMAPI-touching seam: count live `Trough` tiles, read real `MaxOccupants`, delegate tier mapping. Fail-safe; never throws into runtime. | P-SVE3-01, P-SVE3-03, P-SVE3-05 |
| `AnimalTaskHandler` (Dayswork/Orchestration) | Mod runtime | Keep auto-feed gate ahead of capacity; replace `FeedCapacity` ladder with `ResolveAnimalFeedCapacity`; pet/collect unchanged. | P-SVE3-01, P-SVE3-02 |
| hiring enumeration `LegacyScopeBootstrapper` (Dayswork/UI) | Mod | Consult `ResolveAnimalBuildingTier` first; vanilla substring inference unchanged on `null`. | P-SVE3-03 |
| `Dayswork.Tests/Compat/*` (+ animal-handler tests) | Tests | xUnit examples + FsCheck properties for capacity invariants, tier-map totality/pass-through, vanilla parity. | P-SVE3-05 |

## Data / control flow (unchanged shape)
```
6am shift → AnimalTaskHandler.CreateFeedWork
   auto-feed gate (unchanged) → [skip premium/Deluxe]
   else → ExpansionCompatService.ResolveAnimalFeedCapacity
            → AnimalBuildingCapacityPolicy.DeriveCapacity(troughTiles, MaxOccupants)

hire screen → building enumeration (BuildingOutline carries buildingType)
   → ExpansionCompatService.ResolveAnimalBuildingTier
        → SveExpansionProfile.MapPremiumBuildingTier  (else vanilla inference)
```

## Infrastructure
None. No deployment, storage, or cloud resource changes (Infrastructure Design skipped for the SVE change per the execution plan).
