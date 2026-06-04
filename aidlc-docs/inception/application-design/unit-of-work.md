# Unit of Work — Pricing Model Redesign Retrofit

## What a retrofit unit means here

The original Dayswork build already has historical units `U-01` through `U-17`. Those units remain part of the project's audit trail and historical Construction record.

For the pricing redesign, we are **appending** a new retrofit sequence rather than pretending the historical units never existed.

### Retrofit conventions
- **Historical ownership stays historical**: earlier units remain the source of truth for how the original system was first built.
- **Retrofit units start at `U-18`**: they either replace outdated seams or extend historical behavior to the new pricing/energy model.
- **Owns** means a retrofit unit introduces a new redesign seam or fully rewrites a historical seam into its new architectural form.
- **Extends** means a retrofit unit changes an existing historical component/file/behavior without claiming that it originally introduced it.

### Approved decomposition approach
- Append redesign units after the historical sequence
- Use a **hybrid** slicing strategy
- Target **7 medium retrofit units**
- Land typed outdoor/animal/greenhouse scope modeling in the **first foundation unit**
- Keep a **distinct final cleanup/regression unit**

---

## Historical baseline

All retrofit units assume the existing U-01..U-17 implementation is present.

Historical units most directly affected by the redesign:
- **U-05 Pricing Core**
- **U-09 Minimum Hiring Flow**
- **U-10 Minimum Worker Shift**
- **U-12 Hiring UI Schedule**
- **U-15 Recurring Lifecycle + Calendar**
- **U-16 Animals & Buildings**
- **U-17 GMCM + i18n Polish**

These historical units are not erased. They are the baseline that the retrofit units now revise.

---

## The 7 retrofit units

### U-18 — Contract Terms Foundation

**Purpose**: Replace the old hourly/deposit/refund pricing architecture with pure fixed-price contract terms, typed work scopes, and worker-energy profiles.

**Relationship to historical units**:
- **Supersedes** the pricing assumptions introduced by historical `U-05`
- **Lays the foundation** for reinterpretation of `U-11/U-16` building selection semantics and `U-17` config semantics

**Owns**:
- `C-01 WorkScopeClassifier`
- `C-02 OutdoorServiceBandClassifier`
- `C-03 ContractPriceCalculator`
- `C-04 PriceBreakdownBuilder`
- `C-05 WorkerEnergyProfileBuilder`
- `C-06 ContractTermsBuilder`
- redesign domain/value types:
  - `ContractScopeSelection`
  - `WorkScopeSet`
  - `OutdoorWorkScope`
  - `AnimalBuildingScope`
  - `GreenhouseWorkScope`
  - `OutdoorServiceBand`
  - `PricingSnapshot`
  - `PricingLineItem`
  - `ContractTermsSnapshot`
  - `ContractPreview`
  - `WorkerEnergyProfile`

**Extends**:
- `C-17 ConfigSnapshot`
- `C-18 ConfigDefaults`

**Stories touched**:
- `S-02` Configure tasks and see the live contract price
- `S-03` Draw zones and select buildings on the farm
- `S-06` Review the contract, price, and worker stamina before confirming
- `S-19` Pure logic separable from SMAPI for testability

**Definition of Done**:
- Pure Core code can classify selected outdoor zones, animal buildings, and greenhouse work into typed scopes.
- Fixed contract price and price breakdown are deterministic for the same scope/task/config input.
- Worker energy profile is produced alongside the pricing snapshot.
- FsCheck coverage exists for contract-price determinism and scope/terms round-trip invariants where applicable.

---

### U-19 — Contract Snapshot Persistence + Legacy Cleanup

**Purpose**: Persist the new contract-terms snapshot model and silently drop unreleased legacy hourly contracts.

**Relationship to historical units**:
- **Extends** historical `U-06` persistence core
- **Replaces** the old persistence assumptions wired through `U-09`, `U-12`, and `U-15`

**Owns / rewrites in redesign form**:
- current-schema contract DTO shape for persisted `ContractTermsSnapshot`

**Extends**:
- `C-15 ContractStore`
- `C-16 SaveDataSerializer`
- `M-15 ContractPersistenceAdapter`
- persisted `Contract` state shape

**Stories touched**:
- `S-05` Choose a one-time or recurring schedule
- `S-12` Pause, cancel, or edit a recurring contract
- `S-19` Pure logic separable from SMAPI for testability

**Definition of Done**:
- Contracts round-trip with saved scope plus saved `ContractTermsSnapshot`.
- One-time contracts preserve the terms snapshot charged at confirmation.
- Recurring contracts persist enough information to rebuild tomorrow's terms from saved scope.
- Legacy pre-release hourly/deposit/refund contracts are silently dropped on load with no player-facing explanation.

---

### U-20 — Hiring Flow Preview Refresh

**Purpose**: Refresh the hiring/editing flow so it previews fixed price, typed scope effects, and worker stamina instead of deposit/refund/hour estimates.

**Relationship to historical units**:
- **Extends** historical `U-09`, `U-11`, and `U-12`

**Extends**:
- `M-03 HiringFlowCoordinator`
- `M-04 TaskSelectionMenu`
- `M-05 ZoneAndChestMenu`
- `M-06 ScheduleMenu`
- `M-07 SummaryMenu`
- `M-20 ChestResolver`
- `M-01 ModEntry`
- `i18n/default.json`

**Stories touched**:
- `S-01` Discover the hiring option on the bulletin board
- `S-02` Configure tasks and see the live contract price
- `S-03` Draw zones and select buildings on the farm
- `S-05` Choose a one-time or recurring schedule
- `S-06` Review the contract, price, and worker stamina before confirming
- `S-12` Pause, cancel, or edit a recurring contract

**Definition of Done**:
- The hiring flow renders fixed-price preview data supplied by `ContractTermsBuilder`.
- Screen 1 shows per-service price contributions.
- Screen 2 reflects typed-scope semantics for barns/coops and greenhouse.
- Screen 4 shows fixed contract price plus worker energy summary and never mentions deposits/refunds/hours.
- Editing a recurring contract previews the revised next-day fixed price before confirmation.

---

### U-21 — Worker Energy + Shift Runtime Refresh

**Purpose**: Introduce energy-limited worker runtime, visible stamina, slower pacing, and zero-energy work-unit completion semantics.

**Relationship to historical units**:
- **Extends** historical `U-10`, `U-13`, `U-13B`, and the runtime parts of `U-14`

**Owns**:
- `C-07 WorkerEnergyLedger`
- `WorkerEnergyState`
- `WorkActionKind`

**Extends**:
- `C-11 ShiftStateMachine`
- `C-12 StuckDetector`
- `C-13 ItemBuffer`
- `M-09 FarmhandNpc`
- `M-10 ToolSwapAnimator`
- `M-11 PathFindControllerAdapter`
- `M-12 ShiftOrchestrator`
- `M-19 ToolLevelReader`

**Stories touched**:
- `S-07` Watch the farmhand arrive and work on day one
- `S-08` Execute prioritized work across zones, buildings, and animals
- `S-09` Snapshot tool capabilities at spawn and skip what can't be done
- `S-10` Deposit collected items at shift end
- `S-16` Recover from getting stuck
- `S-17` Survive player attacks without abandoning the shift
- `S-19` Pure logic separable from SMAPI for testability

**Definition of Done**:
- Worker energy is spent per work action, never per movement.
- Energy clamps at zero and no new work unit starts at zero.
- If energy reaches zero during a work unit, the current unit completes, then deposit-and-exit begins.
- Worker energy is visible in-world and pacing is intentionally slower/readable.
- Shift-end behavior no longer computes refund/billing settlement.

---

### U-22 — Scope-Driven Runtime Alignment

**Purpose**: Make runtime execution and output routing consume the new typed work-scope model consistently, especially for animal buildings and greenhouse work.

**Relationship to historical units**:
- **Extends** historical `U-11`, `U-14`, and `U-16`

**Extends**:
- `C-08 ZoneGeometry`
- `C-10 TaskPriorityOrderer`
- `C-14 DepositPlanner`
- `M-05 ZoneAndChestMenu`
- `M-12 ShiftOrchestrator`
- `M-16 MailDispatcher`
- `M-20 ChestResolver`

**Stories touched**:
- `S-03` Draw zones and select buildings on the farm
- `S-04` Assign output destinations per task
- `S-08` Execute prioritized work across zones, buildings, and animals
- `S-10` Deposit collected items at shift end
- `S-11` Receive mail for overflow and unassigned output

**Definition of Done**:
- Selected barns/coops are treated as building-owned animal scopes at runtime.
- Worker services those animals wherever they are on the farm.
- Greenhouse is treated as dedicated crop-work scope rather than generic building geometry.
- Deposit/mail routing still works correctly under the new typed scope model.

---

### U-23 — Recurring Billing + Calendar Refresh

**Purpose**: Rework recurring lifecycle, festival/rain/low-work semantics, and sleep settlement around fixed daily price instead of deposits/refunds.

**Relationship to historical units**:
- **Extends** historical `U-12` and `U-15`

**Extends**:
- `C-15 ContractStore`
- `M-13 RecurringContractScheduler`
- `M-14 CalendarHandlers`
- `M-15 ContractPersistenceAdapter`
- `M-16 MailDispatcher`

**Stories touched**:
- `S-12` Pause, cancel, or edit a recurring contract
- `S-14` Handle festivals, rainy days, and low-work days without confusing contract behavior
- `S-15` Player sleeps before the farmhand finishes — shift settles cleanly before rollover

**Definition of Done**:
- Recurring day-start logic rebuilds terms from saved scope plus current config.
- Fixed recurring charge is applied at 6am only on eligible days.
- Festival days skip with no charge and same-day notice.
- Rain and low-work days keep stable recurring pricing.
- Sleep-stop behavior settles output and exits cleanly without refund logic.

---

### U-24 — Config, Regression, and Documentation Cleanup

**Purpose**: Consolidate remaining config/i18n/test/doc updates and verify that historically unchanged behavior still holds under the redesign.

**Relationship to historical units**:
- **Extends** historical `U-17`
- **Regression-verifies** unaffected but still critical behavior from `U-08`, `U-13`, `U-14`, `U-15`, and `U-16`

**Extends**:
- `C-17 ConfigSnapshot`
- `C-18 ConfigDefaults`
- `M-17 GMCMRegistrar`
- `M-21 I18nHelper`
- build/test docs under `aidlc-docs/construction/build-and-test/`
- story/traceability docs and regression notes

**Stories touched**:
- `S-01` Discover the hiring option on the bulletin board
- `S-04` Assign output destinations per task
- `S-09` Snapshot tool capabilities at spawn and skip what can't be done
- `S-11` Receive mail for overflow and unassigned output
- `S-13` Tune contract prices, worker stamina, and action costs in GMCM
- `S-16` Recover from getting stuck
- `S-17` Survive player attacks without abandoning the shift
- `S-18` Multiplayer refuses to load with a friendly message
- `S-19` Pure logic separable from SMAPI for testability
- `S-20` Externalize all user-visible strings for community translation

**Definition of Done**:
- GMCM exposes price, energy, pacing, and threshold knobs matching the redesign.
- All new strings are routed through i18n.
- Build/test docs describe the fixed-price and worker-energy model rather than hourly deposits/refunds.
- Regression coverage verifies historically unchanged behaviors still work after the redesign.

---

## Recommended execution order

Because this is a solo brownfield retrofit, the recommended order is intentionally conservative:

1. `U-18` Contract Terms Foundation
2. `U-19` Contract Snapshot Persistence + Legacy Cleanup
3. `U-20` Hiring Flow Preview Refresh
4. `U-21` Worker Energy + Shift Runtime Refresh
5. `U-22` Scope-Driven Runtime Alignment
6. `U-23` Recurring Billing + Calendar Refresh
7. `U-24` Config, Regression, and Documentation Cleanup

This order keeps the redesign coherent:
- first define what a contract means
- then persist it
- then show it to the player
- then make the worker obey it
- then align multi-scope runtime behavior
- then update recurring/day-start rules
- then sweep config, docs, tests, and regressions

---

## Retrofit component coverage summary

| Retrofit area | Covered by |
|---|---|
| Typed scope classification + fixed pricing + contract terms snapshots | `U-18` |
| Contract persistence schema + silent legacy drop | `U-19` |
| Hiring/edit preview and confirmation flow | `U-20` |
| Worker energy, pacing, and zero-energy runtime behavior | `U-21` |
| Animal/greenhouse runtime scope consumption + destination alignment | `U-22` |
| Recurring repricing + festival/rain/low-work/sleep behavior | `U-23` |
| GMCM, i18n, build/test docs, and redesign regression sweep | `U-24` |

**Validation**:
- Every redesign-impacted component from the refreshed Application Design is either owned or explicitly extended by at least one retrofit unit.
- Every refreshed story has at least one retrofit unit assigned in [unit-of-work-story-map.md](unit-of-work-story-map.md).

---

# SVE Compatibility Units (appended 2026-05-29)

A second appended sequence for the **Stardew Valley Expanded compatibility** change. These units assume the full historical baseline (U-01..U-17), the pricing retrofit (U-18..U-24), and the worker-routing change (U-WR) are all already built. They add the isolated expansion-compatibility seam (see [sve-compatibility-application-design.md](sve-compatibility-application-design.md)) and its overrides. **Decomposition approved**: 4 units, foundation-first, entrance → animals → content/shed order (unit-plan Q1=A, Q2=A, Q3=A).

The same conventions apply: **Owns** = introduces a new seam; **Extends** = changes an existing component/file without claiming to have introduced it.

---

### U-SVE-01 — Expansion-Compatibility Provider Foundation

**Purpose**: Introduce the full expansion-compatibility seam plus SVE detection/selection, with the Vanilla profile fully working and the SVE profile present but with override tables filled in incrementally by U-SVE-02..04 (Q3=A). Independently verifiable: vanilla provably unchanged, SVE detected, no SVE override behavior yet.

**Owns**:
- `C-19 IExpansionProfile`, `C-20 ExpansionProfileSelector`, `C-21 VanillaExpansionProfile`, `C-22 SveExpansionProfile` (shell + ID/farm-map tables), `C-23 AnimalBuildingCapacityPolicy`
- New pure helper types: `ContentDescriptor`, `WorkClassification`, `AnimalBuildingCapacityInputs`
- `M-22 ExpansionDetector`, `M-23 ExpansionCompatService`

**Extends**:
- `M-01 ModEntry` (construct + select + inject the seam; log active profile once)

**Stories**: `S-21` (vanilla invariance + SVE auto-detect), `S-26` (provider seam / extensibility / PBT obligations)

**Primary expected files**:
- `Dayswork.Core/Compat/IExpansionProfile.cs`, `ExpansionProfileSelector.cs`, `VanillaExpansionProfile.cs`, `SveExpansionProfile.cs`, `AnimalBuildingCapacityPolicy.cs`, `ContentDescriptor.cs`, `WorkClassification.cs`, `AnimalBuildingCapacityInputs.cs`
- `Dayswork/Compat/ExpansionDetector.cs`, `ExpansionCompatService.cs`
- `Dayswork/ModEntry.cs` (wiring), `Dayswork.Tests/Compat/*` (selection, capacity, profile-lookup examples + FsCheck)

**Definition of Done**:
- With no expansion installed, the selector returns `VanillaExpansionProfile` and all consumers take their existing paths (vanilla-invariance tests pass).
- With SVE installed (`FlashShifter.StardewValleyExpandedCP` / `FlashShifter.SVECode`), the SVE profile is selected and logged once.
- FsCheck covers deterministic provider selection and capacity derivation; xUnit covers profile lookups.
- No SVE override behavior is active yet (tables are empty/stubbed pending U-SVE-02..04).

---

### U-SVE-02 — SVE Farm Maps + Worker Entrance

**Purpose**: Resolve a correct worker spawn/exit on the three supported SVE farm maps (IF2R, Grandpa's Farm, Frontier Farm) by supplying per-map entrance overrides where the `Farm.warps` heuristic misfires; skip unreachable tiles gracefully.

**Extends**:
- `C-22 SveExpansionProfile` (per-map entrance-override table; farm-map IDs `flashshifter.immersivefarm2remastered`, `flashshifter.GrandpasFarm`, `flashshifter.FrontierFarm`)
- `M-12 ShiftOrchestrator` (entrance resolution delegates to `ExpansionCompatService.TryGetFarmEntranceOverride` before the existing heuristic/fallback)

**Stories**: `S-22`

**Primary expected files**:
- `Dayswork.Core/Compat/SveExpansionProfile.cs` (entrance data), `Dayswork/Orchestration/ShiftOrchestrator.cs` (delegation), `Dayswork.Tests/Compat/*`

**Definition of Done**:
- On each supported SVE map, the worker spawns at and exits from a reachable, sensible entrance (confirmed via manual SVE playtest; entrance values grounded in each map's source).
- The `Farm.warps` heuristic + `(77,15)` fallback remain the default when no override applies.
- Unreachable zone tiles on SVE maps are silently skipped (FR-SVE-15).

---

### U-SVE-03 — SVE Animal Buildings

**Purpose**: Service Premium Barn/Coop correctly — data-driven feeding capacity (fixing the 16-animal underfeed) and premium→nearest-vanilla-tier mapping for scope/pricing, with no auto-petter/auto-grabber special-casing.

**Extends**:
- `Dayswork/Orchestration/AnimalTaskHandler.cs` (replace `FeedCapacity` ladder with `ExpansionCompatService.ResolveAnimalFeedCapacity` over `C-23`)
- premium-building → vanilla-tier mapping in the hiring scope/building enumeration (via `ExpansionCompatService.ResolveAnimalBuildingTier`; Q4=A — no enum/save change)

**Stories**: `S-23`

**Primary expected files**:
- `Dayswork/Orchestration/AnimalTaskHandler.cs`, the hiring building-enumeration/scope site, `Dayswork.Core/Compat/AnimalBuildingCapacityPolicy.cs` (consumption), `Dayswork.Tests/Compat/*`

**Definition of Done**:
- A 16-occupant premium building has all its troughs fillable (subject to silo hay).
- Pet/Collect scan as usual and find nothing when an auto-petter/auto-grabber already acted; no machine-presence assumption.
- Premium buildings are selectable in the hiring scope and priced as their nearest vanilla tier; no save-schema change.

---

### U-SVE-04 — New Content + Grandpa's Shed

**Purpose**: Handle new SVE crops/trees/animals/products via existing data-driven paths, add classification overrides only at verified gaps, treat Grandpa's Shed as a work location, and preserve graceful-skip + item-safety guarantees.

**Extends**:
- `C-22 SveExpansionProfile` (content-classification override table; work-location/Grandpa's-Shed identity)
- `Dayswork/Worker/ObjectTargetClassifier.cs` (consult `ExpansionCompatService.TryClassifyContentOverride` before vanilla classification/skip)
- `Dayswork/Orchestration/BuildingWorkNavigator.cs`, `IndoorWorkScanner.cs`, `BuildingLocationResolver.cs` (include `IsExpansionWorkLocation` locations like Grandpa's Shed)

**Stories**: `S-24`, `S-25`

**Primary expected files**:
- `Dayswork.Core/Compat/SveExpansionProfile.cs` (content/work-location data), `Dayswork/Worker/ObjectTargetClassifier.cs`, the building navigators, `Dayswork.Tests/Compat/*`

**Definition of Done**:
- Generic SVE crops/trees/animals/products work through the unchanged data-driven paths; verified gaps (custom clumps, new milk/wool animal types, special trees) handled per SVE source.
- Unclassifiable content is skipped without crashing; no item loss (overflow-to-mail preserved).
- Grandpa's Shed is enterable as a work location and a valid chest-deposit destination (interior task set confirmed from SVE source).

---

## SVE unit component coverage summary

| SVE area | Covered by |
|---|---|
| Provider seam + detection + vanilla invariance | `U-SVE-01` |
| SVE farm-map worker entrance | `U-SVE-02` |
| Premium barn/coop capacity, feeding, scope tier | `U-SVE-03` |
| New content classification + Grandpa's Shed work location | `U-SVE-04` |

**Validation**: every SVE component from [sve-compatibility-application-design.md](sve-compatibility-application-design.md) is owned or extended by a unit; every SVE story (S-21..S-26) is assigned in [unit-of-work-story-map.md](unit-of-work-story-map.md); no SVE unit depends on a later SVE unit.

---

# Unit of Work — Manage Crops Feature

Continues the appended-unit convention (historical U-01..U-17, retrofit U-18..U-25, U-SVE-01..04, U-T09/U-T10). Manage Crops adds **U-MC-01..U-MC-07** (unit-plan answers Q1=A ~7 medium units, Q2=A foundation-first hybrid, Q3=A no separate cleanup unit — final regression in Build and Test, Q4=A sequential U-MC-01→07). Design refs: components C-24..C-31 / M-24..M-29 in [manage-crops-application-design.md](manage-crops-application-design.md). The feature is opt-in: an empty `CropPlan` leaves all existing behavior unchanged.

## U-MC-01 — Crop-plan Domain + Persistence Foundation
**Purpose**: the shared foundation — crop-plan domain types, pure decision-logic skeletons, V3 persistence.
**Owns**: C-24 ManagedCropDomain (`CropPlan`, `CropZoneAssignment`, `SeasonCropChoice`, `StorePreference`, `ManagedCropWorkScope`), C-25 PlantingViabilityCalculator, C-26 CropSupplyPlanner, C-27 SeasonAssignmentResolver, C-28 StoreResolver, C-29 CropShiftPlanner (skeleton), C-30 CropPlanSerialization, C-31 CropDescriptor.
**Extends**: `Contract`, `ContractScopeSelection`, `WorkScopeSet` (carry `CropPlan`/`ManagedCropWorkScope`); C-16 `SaveDataSerializer` (`DaysworkSaveDataV2`→`V3`, `ContractDtoV2`→`V3` empty-plan migration).
**Stories**: S-34, S-35.
**Definition of Done**: domain + pure planners compile in `Dayswork.Core` with no SMAPI deps; V2 saves migrate to empty/disabled crop plans; crop-plan DTO round-trips (PBT-02); FsCheck properties for viability, `min(seeds,fertilizer)`, multi-season locking, store/fallback (PBT-03); build 0/0, tests green.

## U-MC-02 — Cabin Chests (Input + Backfill)
**Purpose**: add the second built-in office chest and make both chests behave correctly.
**Owns**: M-28 CabinChestService.
**Extends**: `HiringBuilding.BuildData()` (second `BuildingChest` = input chest, `DisplayTile`), M-20 `ChestResolver` (exclude both built-in chests), programmatic i18n names on both chests, one-time idempotent input-chest backfill for pre-existing offices.
**Depends on**: U-MC-01 (domain for chest refs not strictly required, but ordered after foundation).
**Stories**: S-31 (chests), S-34 (backfill).
**Definition of Done**: new save shows two named office chests; pre-existing offices gain the input chest on load (idempotent); neither built-in chest appears as a selectable per-zone destination; build 0/0, tests green.

## U-MC-03 — Manage Crops Authoring UI
**Purpose**: the crop-first authoring page.
**Owns**: M-24 ManageCropsMenu, M-25 CropCatalogProvider.
**Extends**: `HubMenu` (nav row + status chip), `ContractDraft` (in-progress crop plan); wires C-27 (multi-season locking), C-31 (descriptors).
**Depends on**: U-MC-01.
**Stories**: S-27.
**Definition of Done**: Manage Crops page opens from the hub; crop-first authoring (season→crop→fertilizer→replant) with season filter on the farm, multi-season locked styling, output-chest assignment; gamepad + mouse/keyboard; build 0/0, tests green.

## U-MC-04 — Zone Draw Overlay Extension
**Purpose**: draw crop zones around existing assignments (DEV-MC-01 coloring).
**Owns**: (none new).
**Extends**: M-08 `ZoneDrawOverlay`, `ZoneDrawMenu`, `IZoneDrawSource` — existing zones rendered **red** & unselectable, active draw **green**, overlap prevention; delete-and-redraw only.
**Depends on**: U-MC-01, U-MC-03.
**Stories**: S-28.
**Definition of Done**: drawing applies the configured seasonal plan to drawn zones; existing zones are red/unselectable, active draw is green; overlap prevented; build 0/0, tests green.

## U-MC-05 — Shift Crop Behavior
**Purpose**: prepare ground, plant viably, self-heal the field each shift.
**Owns**: M-27 ManagedCropShiftRunner (core per-tile execution), C-29 CropShiftPlanner (full).
**Extends**: `ShiftPlanBuilder`/`ShiftOrchestrator` (managed-crop batch), `WorkerTool`/`ForTask` (`Hoe`), C-09 `CapabilityEvaluator`/`CapabilityMatrix` (till/water/clear gating), `WorkActionKind`/`WorkerEnergyProfile` (`HoeSwing`/`PlantSeed`/`ApplyFertilizer`), M-29 CropHudNotifier (tool-skip/fertilizer-unavailable notices).
**Depends on**: U-MC-01, U-MC-03 (plan authored), U-MC-02 (input chest read).
**Stories**: S-29, S-33.
**Definition of Done**: per-tile harvest→clear→till→fertilize→seed→water in dependency order; viability gate; seed/fertilizer atomicity; re-till, replant/gap-fill; debris/dead-plant toggles (default ON); per-tile `Diggable`; capability/energy gating; coexistence with general tasks; build 0/0, tests green (PBT for ordering/viability).

## U-MC-06 — Town Shopping
**Purpose**: autonomous seed/fertilizer purchasing.
**Owns**: M-26 ShopPurchaseService.
**Extends**: `CrossLocationRouteNavigator` (vanilla Farm↔SeedShop / Farm↔JojaMart routes), wires C-26 (purchase target) + C-28 (store/fallback), M-29 (purchase/fallback/festival/insufficient-funds notices).
**Depends on**: U-MC-01, U-MC-05.
**Stories**: S-30.
**Definition of Done**: store-hours-aware deferral; walk-to-store; headless `ShopBuilder` paced transaction (per-transaction HUD notice); preferred/fallback + festival handling; wallet funding/max-affordable; leftovers to input chest; shopping costs time only; build 0/0, tests green.

## U-MC-07 — Output Routing + Greenhouse/Shed
**Purpose**: per-zone harvest routing and season-agnostic greenhouse/shed support.
**Owns**: (none new).
**Extends**: M-27 (per-zone harvest routing to assigned `ChestRef` / output-chest fallback), `SveExpansionProfile`/`ExpansionProfileSelector` (reuse `GreenhouseWork` shed routes; live-map `Diggable` variant awareness).
**Depends on**: U-MC-01, U-MC-05.
**Stories**: S-31 (routing), S-32.
**Definition of Done**: each zone's harvest routes to its assigned chest (else output chest); greenhouse/shed season-agnostic (no season filter, viability bypass) using reused routes and per-tile `Diggable` from the live map variant; build 0/0, tests green; SVE shed validated via manual playtest.

## Manage Crops unit component coverage summary
| Manage Crops area | Covered by |
|---|---|
| Domain + pure planners + V3 persistence | `U-MC-01` |
| Cabin chests (input + backfill + naming) | `U-MC-02` |
| Authoring UI + crop catalog | `U-MC-03` |
| Zone draw overlay (DEV-MC-01) | `U-MC-04` |
| Shift crop behavior (prepare/plant/maintain, tools/energy) | `U-MC-05` |
| Town shopping (navigation + headless shop) | `U-MC-06` |
| Output routing + greenhouse/shed | `U-MC-07` |

**Validation**: every Manage Crops component (C-24..C-31 / M-24..M-29) is owned or extended by a unit; every Manage Crops story (S-27..S-35) is assigned in [unit-of-work-story-map.md](unit-of-work-story-map.md); no unit depends on a later unit.
