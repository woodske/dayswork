# Services — Dayswork Pricing Model Redesign

A component is a single-responsibility class. A service is an orchestrator that sequences components to produce a user-visible behavior.

This refresh keeps the existing top-down orchestration style, but updates the service layer to use:
- fixed contract terms snapshots instead of hourly deposits/refunds
- typed work scopes instead of mixed zone/building interpretation
- worker energy as a first-class runtime concern

---

## S-A — ModEntry (composition root)

**Lifecycle**: Once at SMAPI mod load.

**Sequence**:
1. Read `config.json` and overlay user values on `ConfigDefaults`.
2. Construct Core singletons in dependency order:
   - `WorkScopeClassifier`
   - `OutdoorServiceBandClassifier`
   - `ContractPriceCalculator`
   - `PriceBreakdownBuilder`
   - `WorkerEnergyProfileBuilder`
   - `ContractTermsBuilder`
   - `WorkerEnergyLedger`
   - `ZoneGeometry`
   - `CapabilityEvaluator`
   - `TaskPriorityOrderer`
   - `ShiftStateMachine`
   - `StuckDetector`
   - `ItemBuffer`
   - `DepositPlanner`
   - `ContractStore`
   - `SaveDataSerializer`
3. Construct Mod singletons:
   - `I18nHelper`
   - `MultiplayerGuard`
   - `ToolLevelReader`
   - `ChestResolver`
   - `MailDispatcher`
   - `GMCMRegistrar`
   - `CalendarHandlers`
   - `ContractPersistenceAdapter`
   - `HiringFlowCoordinator`
   - `ShiftOrchestrator`
   - `RecurringContractScheduler`
4. Register SMAPI events:
   - `SaveLoaded` -> `ContractPersistenceAdapter.OnSaveLoaded`
   - `Saving` -> `ContractPersistenceAdapter.OnSaving` + `CalendarHandlers.OnSavingHook`
   - `DayStarted` -> `RecurringContractScheduler.OnDayStarted`
   - `UpdateTicked` -> `ShiftOrchestrator.OnUpdateTicked`
   - `TimeChanged` -> `ShiftOrchestrator.OnTimeChanged`
5. Apply the bulletin-board Harmony patch.
6. Register GMCM if available.
7. If multiplayer is active, short-circuit user-facing entry points through `MultiplayerGuard`.

---

## S-B — ContractTermsBuilder (pure preview/query facade)

**Lifecycle**: Used by the hiring UI and recurring scheduler whenever contract terms need to be built or rebuilt.

**Sequence**:
1. Accept either a draft scope selection or an existing contract.
2. `WorkScopeClassifier` builds `WorkScopeSet`.
3. `OutdoorServiceBandClassifier` assigns relevant outdoor service bands.
4. `ContractPriceCalculator` computes raw fixed-price totals.
5. `PriceBreakdownBuilder` converts totals into persisted/UI-ready `PricingSnapshot`.
6. `WorkerEnergyProfileBuilder` creates the worker-energy profile.
7. Return:
   - `ContractTermsSnapshot` for persistence/runtime
   - `ContractPreview` for UI display

**Why it exists**:
- Menus do not compose pricing/energy logic themselves
- The recurring scheduler can reuse the same pure facade when rebuilding daily recurring terms
- The fixed-price and energy preview always come from the same source of truth

---

## S-C — HiringFlowCoordinator (four-screen UI orchestration)

**Lifecycle**: From clicking “Hire a Farmhand” or “Edit” until the player confirms or aborts.

**Forward path**:
1. Create a fresh `ContractDraft` or hydrate one from an existing contract.
2. Open `TaskSelectionMenu`.
3. On every relevant draft change, call `ContractTermsBuilder.BuildPreview(...)`.
4. Push updated preview into the currently active screen.
5. Move through:
   - `TaskSelectionMenu`
   - `ZoneAndChestMenu`
   - `ScheduleMenu`
   - `SummaryMenu`
6. On confirm:
   - Build final `ContractTermsSnapshot`
   - For one-time contracts, deduct the fixed total price immediately
   - Persist the contract with:
     - selected tasks
     - typed scope inputs
     - task destinations
     - schedule/status
     - `ContractTermsSnapshot`
7. Close the flow.

**Back/abort behavior**:
- Back retains the draft
- Abort closes without persisting or charging

---

## S-D — ShiftOrchestrator (per-shift runtime execution)

**Lifecycle**: 6am contract start until deposit-and-exit completes.

**StartShift sequence**:
1. Read tool levels through `ToolLevelReader`.
2. Evaluate capabilities through `CapabilityEvaluator`.
3. Start `WorkerEnergyLedger` from the contract's stored `WorkerEnergyProfile`.
4. Construct/reset the shift state machine for this contract day.
5. Spawn `FarmhandNpc` at the farm entrance.
6. Begin live orchestration over SMAPI tick/time events.

**Tick sequence**:
1. Gather runtime progress signals:
   - movement progress
   - path arrival
   - work-unit completion
   - stuck detection
2. Feed the appropriate event into `ShiftStateMachine.Step(...)`.
3. Dispatch resulting intents:
   - move/path
   - perform task
   - play emote
   - deposit output
   - exit shift
4. When a work action occurs, apply its cost through `WorkerEnergyLedger`.
5. Update the worker's visible energy bar.
6. If energy reaches zero, allow the current work unit to resolve, then transition into deposit-and-exit behavior.

**Stop conditions**:
- all work complete
- energy exhausted at a work-unit boundary
- 8pm hard cap
- stuck escalation ends shift early
- player sleep triggers `StopForSleepAndSettle()`

**Important redesign difference**:
- There is no refund/billing settlement at shift end. The day was already charged by contract terms.

---

## S-E — RecurringContractScheduler (daily contract lifecycle)

**Lifecycle**: Every `DayStarted`.

**Sequence per eligible contract**:
1. Skip entirely if multiplayer guard blocks the mod.
2. If festival day:
   - do not charge
   - do not spawn worker
   - queue same-day festival notice
3. If the contract is one-time and scheduled for today:
   - use the stored `ContractTermsSnapshot`
4. If the contract is recurring and active:
   - rebuild fresh terms from saved scope + current config via `ContractTermsBuilder.RebuildTerms(...)`
   - replace the contract's stored terms snapshot
5. Check affordability against the contract's current fixed total price.
6. If unaffordable:
   - do not charge
   - do not spawn worker
   - queue same-day cannot-afford notice
7. If affordable:
   - deduct the contract's current fixed total price
   - start the shift through `ShiftOrchestrator`

**Important redesign difference**:
- Recurring terms are intentionally rebuilt from saved scope plus current config.
- One-time terms are intentionally preserved from confirmation time.

---

## S-F — ContractPersistenceAdapter (save/load lifecycle)

**Lifecycle**: `SaveLoaded` and `Saving`.

**On SaveLoaded**:
1. Read the Dayswork save segment.
2. `SaveDataSerializer.Deserialize(...)` into current contracts.
3. Silently drop legacy pre-release contracts that still use the removed hourly/deposit/refund schema.
4. Hydrate `ContractStore` with only current-schema contracts.

**On Saving**:
1. Serialize current contracts from `ContractStore`.
2. Write them back to SMAPI save data.

**Why the legacy-drop policy is acceptable**:
- The user explicitly chose not to support migration because the project is still unreleased.
- No player-facing explanation is emitted for dropped legacy contracts.

---

## S-G — MailDispatcher

**Lifecycle**: Called from recurring scheduling or shift conclusion paths.

**Responsibilities**:
- Queue overflow/unassigned-output mail for the next morning
- Queue same-day festival notices
- Queue same-day cannot-afford notices

**Important redesign difference**:
- Mail no longer carries refund/change settlement responsibilities
- Mail is strictly about skipped-day notices and output-delivery fallbacks

---

## Interaction Pattern Summary

The redesign preserves the same overall orchestration style:

```text
SMAPI event
  -> top-level service
  -> pure-core computation and/or runtime adapter calls
  -> explicit state update
  -> UI / NPC / persistence / mail effect
```

What changed is the meaning of the data moving through that flow:
- `ContractTermsSnapshot` replaces hourly deposit/refund data
- `WorkScopeSet` replaces ad hoc mixed scope interpretation
- `WorkerEnergyProfile` and `WorkerEnergyState` replace “time worked so far” as the main runtime budget signal
