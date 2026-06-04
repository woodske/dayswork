# Manage Crops — Components

High-level component identification for the Manage Crops feature. Extends the existing
set (C-01..C-23 Core, M-01..M-23 Mod). **New components: C-24..C-31 (pure Core) and
M-24..M-29 (Mod).** Detailed business rules are deferred to per-unit Functional Design.

Design decisions (from [manage-crops-application-design-plan.md](../plans/manage-crops-application-design-plan.md), all = A):
pure planning logic in new Core components (Q1); town-store routing **extends** the existing
`CrossLocationRouteNavigator` with vanilla routes (Q2); headless purchase via a thin Mod
adapter over a pure-Core planner (Q3); new `ManagedCropWorkScope` peer + batch handling (Q4);
new `ManageCropsMenu` hub page (Q5); dedicated crop-catalog seam → pure descriptors (Q6).

---

## New Core components (pure, SMAPI-free — `Dayswork.Core`)

### C-24 ManagedCropDomain
Domain record types attached to a `Contract`:
- `CropPlan` — list of `CropZoneAssignment` + plan-level settings (global "clear debris before tilling", global "clear dead plants", `StorePreference`).
- `CropZoneAssignment` — a reused `Zone` + up to four `SeasonCropChoice` + per-season auto-replant flags + optional output `ChestRef`/`DestinationKey`.
- `SeasonCropChoice` — `{ Season, SeedItemId, FertilizerItemId?, IsMultiSeasonLocked }`.
- `StorePreference` — enum `{ Pierre, Joja, Either }`.
- `ManagedCropWorkScope` — work-scope record, peer to `OutdoorWorkScope`/`GreenhouseWorkScope`, carried in `WorkScopeSet`.
**Responsibilities**: immutable data carriers; equality for save round-trip (PBT-02). No logic.

### C-25 PlantingViabilityCalculator
**Responsibility**: decide whether a crop can mature + be harvested ≥1 time before season end using **fertilized** growth time; bypass entirely for season-agnostic (greenhouse/shed) locations. Pure, deterministic (FR-MC-21/23; S-35 PBT).

### C-26 CropSupplyPlanner
**Responsibility**: from zone definitions + current input-chest stock, compute (a) the per-shift purchase target (empty assigned viable tiles), and (b) the per-tile completion count under partial stock = `min(seeds, fertilizer)`; enforce the never-lay-one-without-both rule (FR-MC-11/12/22; S-35 PBT).

### C-27 SeasonAssignmentResolver
**Responsibility**: apply a season choice to a zone; auto-populate + **lock** consecutive seasons for multi-season crops; reject reassigning a locked season; never emit mid-life teardown (FR-MC-04; S-35 PBT).

### C-28 StoreResolver
**Responsibility**: resolve which store to use from `StorePreference`, day-of-week (Pierre closed Wed), store hours, and festival flag → `{ Pierre | Joja | None }` + a "using fallback" signal; gate on "store stocks the item" (FR-MC-15/16/20; S-35 PBT).

### C-29 CropShiftPlanner
**Responsibility**: assemble the per-shift managed-crop plan — per-tile dependency order (harvest → clear debris → till → fertilize → seed → water), supply-independent vs supply-dependent partitioning for store-hours scheduling, replant/gap-fill selection. Pure orchestration over C-25..C-28 (FR-MC-09/10/24; S-29).

### C-30 CropPlanSerialization
**Responsibility**: DTOs under `Dayswork.Core/Persistence/Dto/` for the crop plan; participate in the `DaysworkSaveDataV2`→`V3` / `ContractDtoV2`→`V3` bump with empty-plan migration default (FR-MC-37/38; extends C-16 `SaveDataSerializer`).

### C-31 CropDescriptor
**Responsibility**: pure descriptor of a plantable crop — `{ SeedItemId, CropItemId, Seasons, FertilizedGrowthDays, IsAutoBuyable, IsChestSupplyOnly }` — produced by the Mod catalog (M-25) and consumed by the UI and Core planners. No game references.

---

## New Mod components (SMAPI/game-facing — `Dayswork`)

### M-24 ManageCropsMenu
**Responsibility**: the new dedicated hub page (peer of `EnergyMenu`/`OutputDestinationsMenu`), reached from `HubMenu`. Crop-first authoring (season→crop→fertilizer→replant), multi-season locked-season styling, output-chest assignment, then "draw zone(s)". Reads/writes the crop plan on an extended `ContractDraft`. Gamepad + mouse/keyboard (FR-MC-01..05/08; S-27).

### M-25 CropCatalogProvider
**Responsibility**: read live game crop data (`Data/Crops`) and store stock to build `CropDescriptor`s (vanilla + modded); apply the season filter (farm) / no-filter (greenhouse/shed); tag auto-buyable vs chest-supply-only by checking Pierre/Joja stock (FR-MC-03; OQ-3).

### M-26 ShopPurchaseService
**Responsibility**: thin adapter that performs the **headless** purchase against live `Data/Shops` stock/prices via `ShopBuilder` (no visual `ShopMenu`); deduct wallet gold; place items into the worker's carried inventory; driven by C-26's purchase plan. Emits a per-transaction signal for paced HUD notices (FR-MC-18/19; OQ-4).

### M-27 ManagedCropShiftRunner
**Responsibility**: runtime adapter invoked by `ShiftOrchestrator`/`ShiftPlanBuilder` to execute the managed-crop batch — drive per-tile world actions (till/fertilize/seed/water/clear/harvest) at `WorkerActionAnimationMs` beats, apply `Diggable` per-tile checks, route harvest output, and orchestrate the store trip via the navigator + M-26. Bridges pure C-29 decisions to the live world (FR-MC-09/10/25/27/29/44; S-29/S-30/S-31).

### M-28 CabinChestService
**Responsibility**: declare the second built-in **input chest** in `HiringBuilding.BuildData()`, set programmatic i18n names on both office chests, run the one-time idempotent **input-chest backfill** for pre-existing offices, and extend `ChestResolver` (M-20) to exclude both built-in chests from selectable destinations (FR-MC-33..36/39; S-31/S-34).

### M-29 CropHudNotifier
**Responsibility**: surface the feature's immediate HUD messages (purchase per-transaction, insufficient funds, festival skip, fertilizer unavailable → zone skipped, preferred-store-closed → fallback, tool missing/under-leveled → skip). Reuses the existing notification path (FR-MC-16/19/20/22/32; §7).

---

## Existing components extended (no new number)
- **`HubMenu`** — add the Manage Crops nav row + status chip (Done/Optional).
- **`ZoneDrawOverlay` (M-08) / `ZoneDrawMenu` / `IZoneDrawSource`** — existing zones rendered red & unselectable, active draw green, overlap prevention (DEV-MC-01; FR-MC-06).
- **`ContractDraft`** — carry the in-progress crop plan.
- **`Contract` / `ContractScopeSelection` / `WorkScopeSet`** — carry `CropPlan` + `ManagedCropWorkScope`.
- **`ShiftPlanBuilder` / `ShiftOrchestrator` (M-12)** — new managed-crop batch ordering + execution hand-off to M-27.
- **`CrossLocationRouteNavigator`** (from TODO-10) — **extended** with vanilla Farm↔SeedShop / Farm↔JojaMart route definitions (Q2=A).
- **`WorkerTool` / `ForTask`** — add `Hoe`; map till to it.
- **`CapabilityEvaluator` (C-09) / `CapabilityMatrix`** — gate till/water/clear; planting/fertilizing gate on item availability only.
- **`WorkActionKind` / `WorkerEnergyProfile`** — add `HoeSwing`/`PlantSeed`/`ApplyFertilizer` configurable costs.
- **`SaveDataSerializer` (C-16)** — V3 schema + migration (via C-30).
- **`ChestResolver` (M-20)** — exclude both built-in chests (via M-28).
- **`GMCMRegistrar` (M-17) / `I18nHelper` (M-21)** — new toggles/energy costs + new i18n strings.
- **`SveExpansionProfile` / `ExpansionProfileSelector`** — reuse existing `GreenhouseWork` shed routes; live-map `Diggable` variant awareness for the shed greenhouse (FR-MC-43/44).
