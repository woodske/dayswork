# Services — Dayswork

A **component** is a single-responsibility class (e.g., `RateCalculator`).
A **service** is an orchestrator that sequences multiple components to fulfill a user-visible behavior.

Per D5 (direct method calls in fixed order), services do not publish events; they call subordinate components in documented sequence.

---

## S-A — ModEntry (composition root)

**Lifecycle phase**: Once at SMAPI mod load.

**Sequence**:
1. Read `config.json` via SMAPI helper → produce initial `IConfigSnapshot` (overlay user values on `ConfigDefaults`).
2. Construct **Core** singletons (no SMAPI deps): RateCalculator, DepositCalculator, RefundCalculator, HoursEstimator, ZoneGeometry, CapabilityEvaluator, TaskPriorityOrderer, StuckDetector, DepositPlanner, ContractStore, SaveDataSerializer.
3. Construct **Mod** singletons in dependency order: I18nHelper → MultiplayerGuard → ToolLevelReader → ChestResolver → MailDispatcher → GMCMRegistrar → CalendarHandlers → ContractPersistenceAdapter → HiringFlowCoordinator → ShiftOrchestrator → RecurringContractScheduler.
4. Wire SMAPI events:
   - `Helper.Events.GameLoop.SaveLoaded` → `ContractPersistenceAdapter.OnSaveLoaded`
   - `Helper.Events.GameLoop.Saving` → `ContractPersistenceAdapter.OnSaving` + `CalendarHandlers.OnSavingHook`
   - `Helper.Events.GameLoop.DayStarted` → `RecurringContractScheduler.OnDayStarted`
   - `Helper.Events.GameLoop.UpdateTicked` → `ShiftOrchestrator.OnUpdateTicked`
   - `Helper.Events.GameLoop.TimeChanged` → `ShiftOrchestrator.OnTimeChanged`
5. Apply Harmony patches: `BulletinBoardPatch` (postfix only).
6. Call `GMCMRegistrar.RegisterIfAvailable()`.
7. If `!MultiplayerGuard.IsSinglePlayerSession()`, log the friendly multiplayer-disabled message and short-circuit further setup (FR-MP-01).

**Inputs**: `IModHelper` (from SMAPI).
**Outputs**: A running mod.

---

## S-B — HiringFlowCoordinator (4-screen UI orchestration)

**Lifecycle phase**: On bulletin board "Hire a Farmhand" click, until the player confirms or aborts.

**Sequence** (forward path):
1. Create a fresh `ContractDraft` (or hydrate from an existing contract for edit-flow).
2. Push `TaskSelectionMenu` onto `Game1.activeClickableMenu` stack. On advance: update draft with chosen tasks.
3. Swap to `ZoneAndChestMenu`. Hands off to `ZoneDrawOverlay` for zone drawing; uses `ChestResolver` to populate the building-chest dropdown. On advance: update draft with zones and chest assignments.
4. Swap to `ScheduleMenu`. On advance: update draft with schedule (one-time vs recurring).
5. Swap to `SummaryMenu`. Renders summary using `RateCalculator`, `HoursEstimator`, `DepositCalculator`. Confirm button enabled only when affordable.
6. On Confirm:
   - Deduct deposit from `Game1.player.Money`.
   - Hand the draft to `ContractStore.Add(contract)`.
   - Close the menu stack.

**Back path**: each menu has a Back action that pops to the previous screen without losing draft state.

**Abort path**: Esc / gamepad-B at any point closes without persisting.

---

## S-C — ShiftOrchestrator (per-shift execution)

**Lifecycle phase**: 6am of a contract day until the worker exits.

**StartShift sequence**:
1. `ToolLevelReader.ReadCurrent()` → `ToolSnapshot`.
2. `CapabilityEvaluator.Evaluate(toolSnapshot)` → `CapabilityMatrix`.
3. Construct a fresh `IShiftStateMachine` parameterized with the contract, capability matrix, and config snapshot.
4. Spawn `FarmhandNpc` at the farm entrance tile.
5. `FarmhandNpc.BeginShift(machine, this)`.
6. Subscribe to path-find arrival events.

**Tick sequence** (every `UpdateTicked`):
1. Build a `ShiftEvent.TickElapsed` carrying the in-game-minutes delta since last tick + a `madeProgress` flag (derived from `PathFindControllerAdapter.IsPathing` change or a task-completion flag).
2. Feed event into `IShiftStateMachine.Step(...)` → receive `(newState, intents)`.
3. Dispatch intents in order:
   - `MoveToTile` → `PathFindControllerAdapter.PathTo(tile)`
   - `PerformTaskOnTile` → execute against `Game1` (water tile, harvest crop, etc.); on success, deposit to `IItemBuffer`; emit `TaskCompleted` next tick
   - `PlayEmote` → trigger `FarmhandNpc` emote animation
   - `TeleportToTile` → set `FarmhandNpc.Position`; emit follow-up event
   - `DepositAtChest` → resolve via `ChestResolver`; if missing, mark overflow; otherwise dump items
   - `DepositInShippingBin` → directly add to `Game1.getFarm().shippingBin`
   - `QueueMail` → `MailDispatcher.QueueOverflowMail(...)`
   - `ApplyRefund` → `Game1.player.Money += refund`
   - `ExitFarm` → despawn `FarmhandNpc`; clear in-flight state

**Time-of-day sequence** (every `TimeChanged`):
- If clock reaches 1800 (8pm cap), feed `ShiftEvent.ClockReached8pm` into state machine.

**Sleep fast-forward sequence** (called by `CalendarHandlers.OnSavingHook` if the worker is mid-shift):
1. Loop: synthesize `TickElapsed` events with a large `inGameMinutesElapsed` value until the state machine reaches `Done`.
2. Dispatch each tick's intents normally (deposits land in actual chests; refund applies to player gold).
3. Return control to the saving flow.

---

## S-D — RecurringContractScheduler (daily lifecycle)

**Lifecycle phase**: Every `DayStarted` event.

**Sequence** per active recurring contract:
1. If `MultiplayerGuard.IsSinglePlayerSession() == false` → skip (FR-MP-01).
2. If `CalendarHandlers.IsFestivalToday()` → skip silently; no deposit, no mail (FR-DAY-01).
3. Compute today's `IConfigSnapshot` (the live config; FR-PAY-08 means rates locked in at day-start become this shift's reality).
4. Compute today's rate via `RateCalculator` (with rainy-day flag from `CalendarHandlers.IsRainyToday()`).
5. Estimate today's hours via `HoursEstimator`.
6. Compute today's deposit.
7. If `Game1.player.Money < deposit` → `MailDispatcher.QueueCannotAffordNotice(...)`; skip (FR-PAY-04).
8. Deduct deposit; call `ShiftOrchestrator.StartShift(contract, toolSnapshot, config)`.
9. If `ToolLevelReader` reports level 0 for any required tool, queue `MailDispatcher.QueueToolMissingWarning(...)` for the morning following (FR-TOOL-03).

---

## S-E — ContractPersistenceAdapter (save/load lifecycle)

**Lifecycle phase**: `SaveLoaded` and `Saving` events.

**On SaveLoaded**:
1. Read save segment via `Helper.Data.ReadSaveData<string>("Dayswork.Contracts")`.
2. `SaveDataSerializer.Deserialize(json)` → list of `Contract`.
3. Bulk-load into `ContractStore`.

**On Saving**:
1. `SaveDataSerializer.Serialize(ContractStore.List())` → JSON.
2. Write via `Helper.Data.WriteSaveData("Dayswork.Contracts", json)`.

---

## S-F — MailDispatcher (one-shot mail jobs)

Not really an orchestrator — closer to a utility. Listed here for completeness because multiple services call into it (CalendarHandlers via state-machine intents, ShiftOrchestrator at deposit-overflow, RecurringContractScheduler on can't-afford, ToolLevelReader chain on missing tool).

All mail letters share the sender label `i18n[mail.sender]` ("Your farmhand") and a structured body composed from i18n templates.

---

## Service interaction diagram

See [component-dependency.md](component-dependency.md) for the full Mermaid + text-fallback diagram.

The interaction pattern is **synchronous, top-down**: SMAPI fires events → `ModEntry`'s wiring routes them to one orchestration service → that service calls components in documented order → components return values / mutate state → service may call into another component (`MailDispatcher`, etc.) but never the reverse direction.

**There is no event bus in v1** (per D5). If two components need each other's state, the orchestrator owns the coordination.
