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
