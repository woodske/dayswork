# U-25 — Worker Redesign: Implementation Plan

**Status**: In progress. Captures a major design pivot agreed in discussion on 2026-06-01.

**Progress**:
- ✅ **Workstream 1 COMPLETE** (energy-tier pricing + category priority + v3 persistence + hire UI + config plumbing). Whole solution builds; `dotnet test` = 343 passed / 0 failed / 1 skipped. Branch `u-25-worker-redesign`.
- ✅ **Requirements-doc amended** — see `requirements.md` §1.5 "U-25 Worker Redesign Amendment" (removed FR-PAY-03/04/05; superseded entry-point/mail/spawn/priority FRs; added FR-PAY-13/14, FR-PRIORITY-01, FR-HIRE-17, FR-OUT-06, FR-NOTIF-01, FR-WORK-20).
- ✅ **Workstream 2 COMPLETE in code** (placeholder art); whole solution builds, `dotnet test` = 340 passed / 0 failed / 1 skipped:
  - ✅ **Building** (`HiringBuilding`): `Data/Buildings` entry `Bindicle.Dayswork_Office`, buildable from Robin (5000g + wood/stone), 3×3, placeholder texture `assets/hq-building.png`. **Playtested** — places from Robin's menu.
  - ✅ **Entry point** (`HiringBuildingInteraction`): action-click → `Coordinator.OpenFromBuilding()` (manage if a contract exists, else hire). SP-guarded. Replaces the bulletin board.
  - ✅ **Static output chest**: built-in `BuildingData.Chests` entry `Bindicle.Dayswork_Output`; `HiringBuilding.TryGetOutputChest`.
  - ✅ **Spawn/exit from door** (2B): `ShiftOrchestrator.ResolveSpawnExitTile` uses the building's human door, falling back to the old entrance heuristic only if no building.
  - ✅ **Mail removed** (2C): `ShiftOutcomeDispatcher` deposits overflow into the office chest (fallback shipping bin) + HUD notices + direct festival-refund gold. `Integration/MailFramework/` deleted; **MFM dependency removed** from `manifest.json`.
  - ✅ **Bulletin board removed** (2D): `BulletinBoardPatch`, `BulletinBoardInteractionPolicy` (+tests) deleted.
  - ✅ **Output chest access fixed**: action-clicking the office chest display tile opens the built-in output chest; action-clicking the rest of the building opens hire/manage.
  - ⚠️ **In-game verification still needed** for: the office chest (open/deposit), worker spawn/exit at the door, overflow-to-chest, HUD notices, and that hiring now works only via the building.

### Known follow-up cleanup (non-blocking)
- [x] Legacy mail naming removed: `IMailDispatcher`/`MailDispatcher` became `IShiftOutcomeDispatcher`/`ShiftOutcomeDispatcher`, and `MailDestination` became `AutomaticOutputDestination`.
- [x] Stale i18n keys (`bulletin.*`, `mail.*`) and the lint test's now-dead `/Integration/MailFramework/` exclusion removed before playtesting.
- [x] `EnableHarmony`/`PatchAll()` removed now that no Harmony patches are left.
- [x] Farmhand Cabin Robin menu limit added: `HiringBuilding.BuildData()` now sets `BuildCondition` so the building is hidden once one `Bindicle.Dayswork_Office` exists or is under construction, with focused regression coverage.

### Pre-playtest cleanup verification
- [x] `dotnet build Dayswork.sln /p:EnableModDeploy=false` — 0 warnings / 0 errors.
- [x] `dotnet test Dayswork.sln /p:EnableModDeploy=false` — 340 passed / 0 failed / 1 skipped.
- [x] `dotnet build Dayswork.sln` — 0 warnings / 0 errors; deployed to `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork`.
- [x] Source search confirms no remaining Harmony setup, no `bulletin.*` / `mail.*` i18n keys, and no mail-named output/dispatcher classes.

### Farmhand Cabin limit verification
- [x] `dotnet build Dayswork.sln /p:EnableModDeploy=false` — 0 warnings / 0 errors.
- [x] `dotnet test Dayswork.sln /p:EnableModDeploy=false` — 341 passed / 0 failed / 1 skipped.

**Scope**: Replace per-scope pricing with purchased energy tiers; add player-ordered task-category priority; move hiring/contract terms onto a placeable farm building with a static item chest; spawn/exit the worker at the building door; remove the Mail Framework Mod dependency, the bulletin-board Harmony patch, and the farm-entrance heuristics.

> This is a **requirements-level pivot**. It supersedes FR-PAY-03/04/05, reframes FR-PAY-06/09, and replaces the bulletin-board and mail-based FRs. The requirements doc should be updated to match (can lead or follow the code per the owner's choice).

---

## Locked decisions

- **Pricing — purchased energy tiers** replace per-scope pricing. Three tiers: Half = 100 energy, Full = 200, Overtime = 300, with Overtime priced at a gold premium. Fixed price is paid up front (one-time at confirmation) or at 6am (recurring). **No chest-based wage system** — payment remains direct gold deduction.
- **Overtime does NOT extend the working-time cap** (`hardCapTime`). Excess purchased energy may strand on inefficient days; acceptable because the worker has ample clock. Default values will be tuned via playtest.
- **Priority — 3 reorderable categories**: `AnimalCare`, `Crops` (watering folded in), `Fieldwork`. Default order `[AnimalCare, Crops, Fieldwork]`. Execution is **strict between categories, nearest-task within a category**.
- **Entry / world** — hire and manage via a placeable building (bought from Robin's shop) with a tile-action menu. Missed/overflow items deposit to a **static chest** on the building. Worker **spawns and exits at the building door**.
- **Persistence — clean break.** Bump schema to **v3**, reshape DTOs in place; old saves auto-discard on load. **No migration code anywhere** (project is in development).

## External dependency (gates Workstream 2)

- **Building art**: building sprite + constructed sprite (and optional under-construction sprite). **No interior** — a solid building with a front Action tile. This is the only true blocker and is owner-sourced.

## Deliberate simplifications

- **No chest-wage / withhold-labor mechanic.** Fixed-price-up-front makes auto-deduction simpler; the existing cannot-afford path already covers non-payment (worker doesn't show). The chest holds **items only**.
- **Festival on a one-time contract** refunds the tier price directly to `Game1.player.Money` (the direct-credit fallback already exists). Recurring contracts are simply not charged on festival days.

---

## Workstream 1 — Contract terms model (Core + persistence + hire UI)

*No art dependency; fully unit-testable. Can still launch from the existing bulletin board until WS2 swaps the entry point. Do this first.*

### 1A. Energy tiers replace per-scope pricing

**Add**
- `Dayswork.Core/Domain/EnergyTier.cs` — `enum EnergyTier { HalfDay, FullDay, Overtime }`.
- Config energy-tier table (`EnergyTier -> (Energy, Price)`) on `IConfigSnapshot`/`ConfigSnapshot`, populated in `ConfigDefaults` (starting values: Half 100/250, Full 200/450, Overtime 300/750 — to be tuned).
- `ConfigValueResolver.ResolveEnergyTier(config, tier)`.

**Change**
- `Dayswork.Core/Pricing/ContractTermsBuilder.cs` — `BuildPreview`/`BuildTerms` take `EnergyTier`; keep the scope-validity gate (`HasChargeableScopeTaskPair`); set price = tier price and energy capacity = tier energy. Remove band/price-calc/breakdown calls.
- `Dayswork.Core/Energy/WorkerEnergyProfileBuilder.cs` — `DailyCapacity` from the chosen tier; `ActionCosts` still from config (per-task energy knob).
- `Dayswork.Core/Domain/ContractTermsSnapshot.cs` / `PricingSnapshot` — collapse pricing to a single price int (or keep `PricingSnapshot` populated only with `TotalPrice`). Downstream consumers read only `TotalPrice`.
- `Dayswork.Core/Pricing/RecurringDayStartDecisionEngine.cs` — logic unchanged; `dailyPrice` now = tier price; still rebuilds from current config each eligible day.
- `Dayswork/UI/HiringFlowCoordinator.cs` — afford-check + deduct against the tier price.

**Delete**
- Pricing: `ContractPriceCalculator`(+`IContractPriceCalculator`), `OutdoorServiceBandClassifier`(+I), `PriceBreakdownBuilder`(+I).
- Domain: `OutdoorServiceBand`, `OutdoorBandSize`, `OutdoorPriceKey`, `GreenhousePriceKey`, `ContractPriceTotals`, `PricingFamily`, `PricingLineItem`, and the pricing form of `AnimalBuildingPriceKey`.
- Config: `outdoorBandThresholds`, `outdoorServiceBandPrices`, `animalBuildingPrices`, `greenhouseServicePrices` + their three resolver methods + the `workerDailyEnergyCapacity` constant.
- **Verify before deleting** `AnimalBuildingTier` — may have non-pricing uses (`AnimalBuildingCapacityPolicy` in Compat). Keep the enum if so; remove only its price-key usage.

### 1B. Category-based player priority

**Add**
- `Dayswork.Core/Domain/TaskCategory.cs` — `enum TaskCategory { AnimalCare, Crops, Fieldwork }`.
- `TaskKindSets.CategoryOf(TaskKind)` — 3-way switch on existing `IsAnimalService` / `IsOutdoorCropService` / `IsOutdoorClearingService`.

**Change**
- `Dayswork.Core/Domain/Contract.cs` — add `IReadOnlyList<TaskCategory> CategoryPriority` (default `[AnimalCare, Crops, Fieldwork]`). Keep `EnabledTasks` as the set (membership drives validity/energy).
- `Dayswork.Core/Shifts/TaskPriorityOrderer.cs` — replace static `s_rank` with `Rank(task) = index of CategoryOf(task) in contract.CategoryPriority`. Construct per-shift from the contract; remove inline instantiations at `ShiftOrchestrator.cs:52` and `ShiftPlanBuilder.cs:75`, passing the contract order in.
- `Dayswork.Core/Shifts/WorkerRouteSelector.cs` — `Select` becomes `OrderBy(CategoryRank).ThenBy(RouteCost).ThenBy(StableOrder)` (strict-between / nearest-within).

### 1C. Hire UI

**Change**
- Add a **tier picker** and a **3-row category reorder** control (in `HiringFlowCoordinator` / `HiringFlowViewModelBuilder`, plus a small menu or `SummaryMenu` additions).
- `SummaryMenu` — remove line-item price breakdown; show "Tier: Full Day · 200 energy · 450g" + category order.
- Keep all scope/zone/building/greenhouse selection and task toggles — scope still defines the **work area**, it just no longer prices.

### 1D. Persistence (clean break)

**Change**
- `Dayswork.Core/Persistence/SaveDataSerializer.cs` — `CurrentSchemaVersion = 3` (mismatch already auto-discards; no migration).
- DTOs: `ContractTermsSnapshotDto` pricing collapses to a price int; `WorkerEnergyProfileDto.DailyCapacity` = tier energy; contract DTO gains `EnergyTier` + `CategoryPriority`; drop `PricingLineItemDto` and `PricingSnapshotDto` line-item fields.
- Optional cleanup: drop misleading `V1`/`V2` DTO name suffixes (no migration exists).

### 1E. Tests (WS1)

- **Delete**: band classifier, price calculator, price-breakdown, price property tests; the 10-entry `s_rank` ordering tests.
- **Add**: tier -> (price, capacity) resolution; capacity flows into `WorkerEnergyState`; category-rank ordering; route selection strict-between/nearest-within; serializer round-trips the v3 shape.

---

## Workstream 2 — World integration (SMAPI layer; needs building art)

### 2A. The building

- `Data/Buildings` content-pack entry (no Harmony): cheap recipe in Robin's shop; no interior.
- Front **Action tile** -> opens hire/manage flow (replaces bulletin-board buttons).
- Persistent **static chest** in the building's `modData` (reuse `ChestResolver` / `ChestRef` / `DepositPlanner` — just another well-known `ChestRef`).
- Lifecycle: on demolish/move, invalidate/cancel the contract cleanly (no-migration freedom makes this trivial).

### 2B. Spawn / exit from the building door

- In `Dayswork/Orchestration/ShiftOrchestrator.cs`, replace `FindFarmExitTile` + `ResolvePassableNearby` with the building door via `building.getPointForHumanDoor()` + the existing `ResolveOutdoorApproachTile` in `BuildingLocationResolver.cs`. Spawn **and** end-of-day exit both use the door.
- **Verify, then likely delete** the SVE entrance-override seam (`TryGetFarmEntranceOverride`, the entrance bits of `ExpansionCompatService` / `SveExpansionProfile`, `EntranceOverrideTests`) — obsolete once spawn is door-based. Confirm no other dependents.

### 2C. Remove mail -> chest + HUD

- **Replace/delete**: legacy mail dispatcher types, `Integration/MailFramework/` (adapter + records), the `Dayswork.PendingSettlements` save-data flow, MFM dependency in `manifest.json`.
- **Reroute**: overflow/missed items -> static chest (promote the existing shipping-bin fallback to the primary path, retargeted at the chest). Text notices (cannot-afford, needs-attention, festival) -> `Game1.addHUDMessage`. Festival one-time refund -> direct `Money` credit.
- Touch points: `RecurringContractScheduler` (notice calls), `ShiftOrchestrator` (settlement/overflow), `CalendarHandlers`.

### 2D. Remove bulletin board

- **Delete**: `Patches/BulletinBoardPatch.cs`, `Guards/BulletinBoardInteractionPolicy.cs` (+ tests). Re-point `OpenHiringFlow`/`OpenManageFlow` to the building tile action.

### 2E. Tests (WS2)

- Delete bulletin-board interaction-policy and entrance-override tests.
- Add: door-based spawn/exit resolution; chest-deposit-on-overflow; building-demolish cancels contract. World/SMAPI-bound behavior gets manual playtest coverage per existing convention.

---

## Cross-cutting

- **GMCM / config** (`GMCMRegistrar`, `ModConfig`, `ModConfigManager`, `RuntimeConfigSnapshotMapper`): remove per-scope price knobs; add the energy-tier table; keep per-action energy costs exposed (the tuning surface). `ContractTermsConfigKeyCodec` likely shrinks.
- **i18n** (`i18n/default.json`): remove `mail.*` and `bulletin.*` keys; add tier/category/building/HUD strings. Respect the no-hardcoded-user-facing-string lint.
- **Manifest**: drop the Mail Framework Mod dependency.

---

## Suggested sequence & checkpoints

1. **WS1A + 1B + 1D** (contract-model core): tiers + categories + v3 persistence. Build + unit tests green. *Checkpoint: terms model reworked, still launched from bulletin board.*
2. **WS1C** (hire UI): tier picker + category reorder. *Checkpoint: playable new hire flow end-to-end.*
3. **WS2A + 2B** (building + door spawn) — once art is in hand. *Checkpoint: hire from building, worker spawns at door.*
4. **WS2C + 2D** (delete mail + bulletin board). *Checkpoint: MFM dependency gone, items land in chest, notices via HUD.*
5. **Cross-cutting cleanup** (GMCM, i18n, manifest) + full test pass + manual SMAPI playtest.

Each checkpoint is independently buildable and testable. WS1 carries no art dependency, so the entire pricing/priority redesign can land before the building art exists.

---

## Open items before/while coding

- **Building art** (only true blocker for WS2).
- **Verify-then-delete** confirmations: `AnimalBuildingTier` non-pricing uses (1A); SVE entrance-override seam dependents (2B).
- **Requirements update**: revise the FR-PAY set and the bulletin-board/mail FRs to match this pivot.
