# Manage Crops — Application Design (Consolidated)

Consolidates the Manage Crops application-design artifacts:
[components](manage-crops-components.md) · [methods](manage-crops-component-methods.md) ·
[services](manage-crops-services.md) · [dependencies](manage-crops-component-dependency.md).

**Source**: [manage-crops-requirements.md](../requirements/manage-crops-requirements.md)
(FR-MC-01..44, NFR-MC-01..09); stories S-27..S-35; spec
[manage-crops-spec.md](../manage-crops-spec.md).
**Design answers** (all = A): pure-Core planning components (Q1); extend
`CrossLocationRouteNavigator` with vanilla town routes (Q2); thin Mod shop adapter over a
pure purchase planner (Q3); new `ManagedCropWorkScope` peer + batch (Q4); new
`ManageCropsMenu` hub page (Q5); dedicated crop-catalog seam (Q6).

## Architectural shape
The feature follows the project's established **pure-Core + thin-Mod-adapter** architecture
and **scope→batch** runtime model:

- **Pure Core (`Dayswork.Core`)** — new domain (C-24) and deterministic decision services
  (C-25 viability, C-26 supply/`min`, C-27 season locking, C-28 store/fallback, C-29 shift
  planner), serialization (C-30), and a pure crop descriptor (C-31). SMAPI-free and
  FsCheck-testable (full-mode PBT per S-35).
- **Mod adapters (`Dayswork`)** — `ManageCropsMenu` (M-24), `CropCatalogProvider` (M-25),
  `ShopPurchaseService` (M-26, headless `ShopBuilder`), `ManagedCropShiftRunner` (M-27),
  `CabinChestService` (M-28), `CropHudNotifier` (M-29) — touch the live game, driven by the
  pure planners.
- **Extensions to existing components** — `HubMenu`, `ZoneDrawOverlay`, `ContractDraft`,
  `Contract`/`WorkScopeSet`, `ShiftPlanBuilder`/`ShiftOrchestrator`,
  `CrossLocationRouteNavigator`, `WorkerTool`/`ForTask`, `CapabilityEvaluator`,
  `WorkActionKind`/`WorkerEnergyProfile`, `SaveDataSerializer`, `ChestResolver`,
  `GMCMRegistrar`/`I18nHelper`, `SveExpansionProfile`.

## New components at a glance
| # | Component | Layer | Role |
|---|---|---|---|
| C-24 | ManagedCropDomain | Core | crop-plan/scope record types |
| C-25 | PlantingViabilityCalculator | Core | fertilized-growth viability + greenhouse bypass |
| C-26 | CropSupplyPlanner | Core | purchase target + `min(seeds,fertilizer)` + both-on-hand gate |
| C-27 | SeasonAssignmentResolver | Core | multi-season auto-populate + lock |
| C-28 | StoreResolver | Core | preferred/fallback/festival + stocks-it gate |
| C-29 | CropShiftPlanner | Core | per-tile ordering + supply-(in)dependent partition + replant |
| C-30 | CropPlanSerialization | Core | V3 DTOs + migration round-trip |
| C-31 | CropDescriptor | Core | pure plantable-crop descriptor |
| M-24 | ManageCropsMenu | Mod | crop-first authoring hub page |
| M-25 | CropCatalogProvider | Mod | `Data/Crops` + shop-stock → descriptors |
| M-26 | ShopPurchaseService | Mod | headless paced 1.6 purchase |
| M-27 | ManagedCropShiftRunner | Mod | live managed-crop batch execution + store trip |
| M-28 | CabinChestService | Mod | input chest declare/backfill/name + destination exclusion |
| M-29 | CropHudNotifier | Mod | feature HUD notifications |

## Requirements coverage (design level)
- Authoring & UI (FR-MC-01..08): M-24, M-25, C-27, C-31, `ZoneDrawOverlay` ext (DEV-MC-01).
- Shift behavior/viability (FR-MC-09..11, 21..27): C-25, C-26, C-29, M-27, `CapabilityEvaluator`/`WorkActionKind` ext.
- Purchasing (FR-MC-12..20, 41): C-26, C-28, M-26, M-29, navigator ext.
- Chests & routing (FR-MC-28, 29, 33..36): M-28, M-27, `ChestResolver` ext.
- Tools/energy/pricing (FR-MC-30..32, 40, 42): `WorkerTool`/`CapabilityEvaluator`/`WorkerEnergyProfile` ext, M-29.
- Persistence/migration (FR-MC-37..39): C-30, M-28, `SaveDataSerializer` ext.
- Greenhouse/shed (FR-MC-05, 23, 43, 44): M-27, C-25, `SveExpansionProfile` ext.
- NFRs (NFR-MC-01..09): pure-Core seams + thin adapters, scope→batch reuse, additive persistence, i18n/GMCM, vanilla invariance.

## Vanilla-path guarantee
No vanilla/core call site gains Manage-Crops branches that change non-crop behavior; the
feature is opt-in (empty `CropPlan` = today's behavior). Only the SVE shed greenhouse touches
the expansion seam; everything else (town stores, standard greenhouse, open farm) is vanilla.

## Open items deferred to Functional Design (per unit)
- Exact per-tile beat sequencing vs the existing animation/pacing pipeline.
- Precise `Data/Crops` 1.6 fields used for fertilized growth time and modded-crop discovery.
- `ShopBuilder` invocation specifics and the carried-inventory ↔ input-chest settlement timing.
- Input-chest backfill mechanism (whether the game auto-creates a newly-declared `BuildingChest`).
- `Diggable` live-map variant resolution for the shed greenhouse (`...Cleared`).
