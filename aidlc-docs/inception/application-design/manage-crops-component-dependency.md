# Manage Crops — Component Dependencies & Data Flow

## Dependency matrix (new components)

| Component | Depends on | Used by |
|---|---|---|
| C-24 ManagedCropDomain | (none) | C-25..C-30, M-24, M-27, persistence |
| C-25 PlantingViabilityCalculator | C-24, C-31 | C-29 |
| C-26 CropSupplyPlanner | C-24, C-31 | C-29, S-D shopping |
| C-27 SeasonAssignmentResolver | C-24, C-31 | M-24 (authoring) |
| C-28 StoreResolver | C-24 | C-29, S-D shopping |
| C-29 CropShiftPlanner | C-24, C-25, C-26, C-28, C-31 | M-27 |
| C-30 CropPlanSerialization | C-24, C-16 SaveDataSerializer | M-15 persistence |
| C-31 CropDescriptor | (none) | C-25/26/27/29, M-24, M-25 |
| M-24 ManageCropsMenu | C-27, C-31, M-25, M-08 overlay, ContractDraft | HubMenu |
| M-25 CropCatalogProvider | game `Data/Crops`, shop stock | M-24, (descriptors to Core) |
| M-26 ShopPurchaseService | `ShopBuilder`/`Data/Shops`, Farmer wallet | M-27 (via S-D) |
| M-27 ManagedCropShiftRunner | C-29, CrossLocationRouteNavigator, M-26, M-29, C-09, C-07 | ShiftOrchestrator/ShiftPlanBuilder |
| M-28 CabinChestService | HiringBuilding, ChestResolver (M-20) | save load, BuildData, destinations |
| M-29 CropHudNotifier | SMAPI HUD | M-26, M-27 |

## Communication patterns
- **Pure-Core ← descriptors**: M-25 reads game data once and produces pure `CropDescriptor`s (C-31); all decision logic (C-25..C-29) operates only on pure inputs → SMAPI-free, FsCheck-testable (NFR-MC-01/09).
- **Thin adapter over pure planner**: M-26/M-27 touch the live world; the *decisions* (what to buy, which store, which tiles, which order) come from C-26/C-28/C-29. No game state leaks into Core.
- **Scope→batch runtime**: `ManagedCropWorkScope` (C-24) flows from `WorkScopeSet` into `ShiftPlanBuilder`, which emits a managed-crop batch executed by M-27 (same pattern as animal/greenhouse/outdoor batches).
- **Navigation reuse**: town-store legs are added to the existing `CrossLocationRouteNavigator` as vanilla routes (Q2=A) — not a parallel navigator and not in the SVE seam.

## Data flow — authoring (S-A)
```
HubMenu --open--> ManageCropsMenu(M-24)
  M-24 --GetCrops--> CropCatalogProvider(M-25) --> [CropDescriptor]
  M-24 --ApplyChoice--> SeasonAssignmentResolver(C-27)  (auto-populate + lock)
  M-24 --BeginDraw--> ZoneDrawOverlay(M-08)  (existing=red, active=green)
  M-24 --writes--> ContractDraft.CropPlan(C-24) --persist--> Contract
```

## Data flow — managed-crop shift (S-B/S-C/S-D)
```
ShiftOrchestrator(M-12)
  --assess--> CropShiftPlanner(C-29)
                 ├ PlantingViabilityCalculator(C-25)
                 ├ CropSupplyPlanner(C-26)
                 └ StoreResolver(C-28)
  --execute--> ManagedCropShiftRunner(M-27)
                 ├ supply-independent beats (6-9AM): harvest->clear->till->fertilize->seed->water
                 ├ store trip: CrossLocationRouteNavigator --> ShopPurchaseService(M-26) --> CropHudNotifier(M-29)
                 ├ supply-dependent beats (post-trip)
                 └ harvest routing --> assigned ChestRef | output chest fallback
  --end of shift--> leftovers --> input chest (CabinChestService S-E)
```

## Vanilla / SVE seam boundary
- Town-store routes and the shop transaction are **vanilla** (core path), independent of `IExpansionProfile`.
- Only the **shed greenhouse** (route reuse + live-map `Diggable` variant) touches `SveExpansionProfile`; the standard greenhouse and open farm are vanilla.
- When SVE is absent, all Manage Crops behavior is unchanged (NFR-MC-04).

## Risk / isolation notes
- Highest-risk new surface (town-store navigation, headless shop) is isolated in M-26/M-27 behind pure planners (C-26/C-28/C-29), so the hard rules are deterministically testable and the live integration is a thin, replaceable adapter.
- Persistence change is additive (C-30) with empty-plan migration; feature is opt-in.
