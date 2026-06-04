# Manage Crops — Services & Orchestration

Service-layer view of how the Manage Crops components coordinate. The pattern follows
the established split: **pure-Core decision services** + **thin Mod adapters** that touch
the game.

---

## S-A Authoring service (UI orchestration)
**Components**: `HubMenu` → M-24 `ManageCropsMenu` ← M-25 `CropCatalogProvider`, C-27 `SeasonAssignmentResolver`, `ZoneDrawOverlay` (M-08), `ContractDraft`.
**Flow**: player opens Manage Crops → catalog supplies season-appropriate `CropDescriptor`s → player configures per-season choices (resolver auto-populates/locks multi-season spans) → optional output-chest assignment → "draw" hands off to the overlay (existing zones red/unselectable, active draw green) → drawn zones + plan written to `ContractDraft.CropPlan` → persisted with the contract.

## S-B Per-shift planning service (pure)
**Components**: C-29 `CropShiftPlanner` over C-25 `PlantingViabilityCalculator`, C-26 `CropSupplyPlanner`, C-28 `StoreResolver`.
**Flow**: at shift assessment, build the `ManagedCropShiftPlan` — partition supply-independent work (do during 6–9 AM) vs supply-dependent work (after the store trip); compute the purchase target; per-tile action ordering; replant/gap-fill. Fully deterministic and SMAPI-free (FsCheck-covered).

## S-C Shift execution service (runtime adapter)
**Components**: `ShiftPlanBuilder` + `ShiftOrchestrator` (M-12) → M-27 `ManagedCropShiftRunner` → `CrossLocationRouteNavigator`, M-26 `ShopPurchaseService`, M-29 `CropHudNotifier`, `CapabilityEvaluator` (C-09), `WorkerEnergyLedger` (C-07).
**Flow**: the managed-crop batch runs the plan from S-B against the live world — per-tile beats (capability/energy gated, `Diggable`-checked), deferring the store trip until stores open, walking to the resolved store, performing the headless paced purchase (HUD notice per transaction), returning to complete supply-dependent planting, routing harvest to the per-zone chest (or output-chest fallback), and settling leftover supplies to the input chest at end of shift.

## S-D Shopping sub-service
**Components**: C-28 `StoreResolver` (which store) + C-26 `CropSupplyPlanner` (what/how many) → `CrossLocationRouteNavigator` (walk there) → M-26 `ShopPurchaseService` (headless transaction) → M-29 (paced notices).
**Rules**: wallet-funded, max-affordable on shortfall; only items the store stocks; festival → skip + notice; chest-supply-only crops never trigger a trip; shopping costs shift time only (no energy).

## S-E Cabin-chest service
**Components**: M-28 `CabinChestService` + `HiringBuilding` + `ChestResolver` (M-20).
**Flow**: declare input chest in `BuildData()`; on load, backfill the input chest for pre-existing offices (idempotent); apply programmatic i18n names; exclude both built-in chests from selectable per-zone destinations. The input chest is the availability-gate reservoir; the output chest is the harvest/overflow fallback.

## S-F Persistence service
**Components**: C-30 `CropPlanSerialization` + C-16 `SaveDataSerializer` + `ContractPersistenceAdapter` (M-15).
**Flow**: V2→V3 schema bump; existing contracts migrate to an empty/disabled crop plan; new crop plans round-trip exactly (PBT-02).

## S-G Greenhouse/shed service
**Components**: M-27 + `SveExpansionProfile`/`ExpansionProfileSelector` + existing `GreenhouseWork` routes.
**Flow**: season-agnostic authoring (no season filter, viability bypass) + reused shed routes; plantable area resolved per tile from the **live** map's `Diggable` property (default vs `...Cleared` variant).

---

## Orchestration summary (one managed-crop shift)
1. Shift start (6 AM) → S-B builds the plan from saved `CropPlan` + field state + input-chest stock.
2. S-C runs supply-independent work (harvest-first per-tile order) during 6–9 AM.
3. If supplies still needed and not a festival → S-D resolves store, walks there, headless paced purchase.
4. Return → S-C completes supply-dependent fertilize/seed/water; harvest routed via S-E.
5. End of shift → leftover supplies to input chest (S-E); overflow/output to assigned/output chest.
