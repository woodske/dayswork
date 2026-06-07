# U-MC-06 Logical Components

**Unit**: U-MC-06 — Town Shopping
**Stage**: CONSTRUCTION — NFR Design
**Status**: Review required

## New components

| Component | Project | Responsibility | Pattern |
|---|---|---|---|
| `PlannedPlantableTileCounter` (static/pure) | `Dayswork.Core/Crops` | Count tiles the worker plans to till+plant this shift (sizes purchase). | P3 |
| `ShiftSupplyAggregator` (pure) *(or methods on `CropSupplyPlanner`)* | `Dayswork.Core/Crops` | Aggregate per-zone targets into one `ShiftPurchaseManifest` (per-store groups, preferred-first). | P1/P3/P9 |
| `ShiftPurchaseManifest` (record) | `Dayswork.Core/Crops` | Whole-shift purchase intent: store groups + chest-supply-only items. | P1/P3 |
| `PurchaseAffordabilityCalculator` (pure) | `Dayswork.Core/Crops` | Clamp manifest to wallet; max-affordable with seed/fertilizer atomic parity; shortfall flag. | P1/P4 |
| `AffordablePurchasePlan` (record) | `Dayswork.Core/Crops` | Affordable per-store lines + total cost + shortfall. | P1/P4 |
| `StoreHoursPolicy` (pure) | `Dayswork.Core/Crops` | `IsOpen`/`OpensAt` over store×time×weekday (Pierre 9–17 closed Wed; Joja 9–23). | P6 |
| `ShopStockReader` (M-26) | `Dayswork/Integration` | Live `Data/Shops`/`ShopBuilder` → immutable `ShopStockSnapshot` (stock+price+open), once per store per shift. | P2 |
| `ShopPurchaseService` (M-26) | `Dayswork/Integration` | Headless exchange-atomic buy: grant items, deduct gold only for granted; per-line `PurchaseResult`. | P4 |
| `PurchaseResult` / `PurchaseLineOutcome` (records) | `Dayswork.Core/Crops` or `Dayswork` | Per-line bought qty/unit cost/outcome for paced notices. | P4/P8 |

## Extended components

| Component | Change | Pattern |
|---|---|---|
| `ShopStockSnapshot` | + per-item **unit price** (`UnitPriceOf` / `Prices`); back-compat constructor retained. | P2 |
| `CrossLocationRouteNavigator` | + vanilla Farm↔SeedShop / Farm↔JojaMart route definitions consumed by the existing navigate path; multi-store visit order. | P7 |
| `ManagedCropShiftRunner` (M-27 / `ShiftOrchestrator.ManagedCrops.cs`) | Insert shopping phase between supply-independent and supply-dependent beats: build manifest, defer on hours, navigate, headless paced buy, return, settle leftovers. | P1/P5/P6/P8/P10 |
| `CropHudNotifier` (M-29) | + `PurchaseCompleted`, `UsingFallbackStore`, `FestivalSkipped`, `InsufficientFunds`, `ShoppingUnavailable`. | P5/P8 |
| `ModConfig` + `GMCMRegistrar` + `i18n/default.json` | + global `PreferredCropStore` (Pierre/Joja/Either, default Either) dropdown + strings. | P9 |
| `CropShiftPlanner.Plan` | Accept the effective (global) store preference + real `stockSnapshots`; otherwise unchanged. | P9 |

## Reused (unchanged)

`StoreResolver`, `CropSupplyPlanner`, `SupplyTarget`, `PurchaseLine`, `StoreResolution`,
`StoreClosedReason`, `Store`, `StorePreference`, `SupplyInventory`, `CropDescriptor`,
`PlantingViabilityCalculator`, `FieldState`/`TileState` (U-MC-01); `ManagedCropFieldReader`,
the per-tile beat executor, `WorkerMovementDriver`, `WorkerPacingProfile`
(`WorkerActionAnimationMs`), input chest + `ChestResolver`, deposit/overflow pipeline,
carried-supply settle seam (U-MC-02/U-MC-05).

## Dependency notes
- **No new project reference, no new NuGet dependency** — uses SMAPI 1.6 `Data/Shops`/`ShopBuilder` already on the platform.
- **No persistence schema change** — only one new GMCM config key (`config.json`).
- The single new gold-mutation point is `ShopPurchaseService.BuyHeadless` (P4 barrier); every
  other component is read-only or operates on pure/carry/chest state.
- Forward seam to U-MC-07 (per-zone harvest routing / greenhouse-shed) is untouched and still
  attaches at the deposit step.

## Component interaction (one shopping phase)
```
PlannedPlantableTileCounter + CropSupplyPlanner ─> ShiftSupplyAggregator ─> ShiftPurchaseManifest
ShopStockReader (once/store) ──────────────────────────────┘ (live stock+prices)
ShiftPurchaseManifest + Farmer.Money ─> PurchaseAffordabilityCalculator ─> AffordablePurchasePlan
StoreHoursPolicy ─> (defer/fallback/skip decision)
CrossLocationRouteNavigator(town routes) ─> [P5 guard] ─> ShopPurchaseService.BuyHeadless ─> PurchaseResult
PurchaseResult ─> CropHudNotifier (paced) ; carry inventory ─> U-MC-05 planting ─> leftovers ─> input chest
```

## Extension Compliance
| Extension | Status |
|---|---|
| Security Baseline | N/A (disabled). |
| Property-Based Testing | Compliant, full — pure manifest/affordability/store-hours/planned-tile seams carry blocking FsCheck properties; the live reader, purchase service, town routes, and runner are example/playtest-covered adapters. |
