# Components — Dayswork

This document inventories every component in the planned implementation, its purpose, its responsibilities, the project it lives in (per D1 — separate `Dayswork.Core` for pure logic vs `Dayswork` for SMAPI-bound code), and the public interface it exposes. **Method signatures live in [component-methods.md](component-methods.md). Detailed business rules are deferred to per-unit Functional Design in Construction.**

## Solution layout (decided in D1, D2, D6)

```
Dayswork.sln
├── Dayswork.Core/          .NET 6 class library — ZERO SMAPI / StardewValley references
│   ├── Domain/             Records: Contract, Zone, ChestRef, TaskKind, etc.
│   ├── Pricing/            Rate / deposit / refund calculation
│   ├── Shifts/             State machine, task queue, priority ordering
│   ├── Capabilities/       Tool-snapshot evaluation
│   ├── Geometry/           Zone-tile math
│   ├── Inventory/          Item buffer + deposit planning
│   ├── Persistence/        Save DTOs + JSON serialization (Newtonsoft, already in SMAPI's transitive deps)
│   └── Config/             IConfigSnapshot + defaults
├── Dayswork/               SMAPI mod — references Dayswork.Core + StardewModdingAPI + StardewValley + Harmony
│   ├── ModEntry.cs         Composition root (D2 — hand-wired)
│   ├── Patches/            Harmony patches, one per file (NFR-MAINT-04)
│   ├── UI/                 IClickableMenu subclasses + zone-draw overlay (D6 — four menus + coordinator)
│   ├── Worker/             FarmhandNpc + animator + path-find adapter
│   ├── Orchestration/      ShiftOrchestrator + RecurringContractScheduler + CalendarHandlers
│   ├── Integration/        SMAPI adapters: persistence, mail, GMCM, tool-level reader, chest resolver
│   ├── Guards/             MultiplayerGuard
│   ├── i18n/default.json   English strings
│   └── manifest.json
└── Dayswork.Tests/         xUnit + FsCheck — references ONLY Dayswork.Core
```

---

## Core components (pure, `Dayswork.Core`)

### C-01 RateCalculator
- **Purpose**: Compute hourly rate given an enabled-task set and a config snapshot.
- **Responsibilities**: Pure function over `(IReadOnlySet<TaskKind>, IConfigSnapshot) → int`. Handles rain-day exclusion of the Water Crops surcharge (FR-PAY-07).
- **PBT obligations**: Invariant `rate = base + sum(enabled task increments)` (PBT-03).
- **Interface**: `IRateCalculator`

### C-02 DepositCalculator
- **Purpose**: Compute deposit amount from estimated hours + hourly rate.
- **Responsibilities**: Pure function `(decimal hours, int rate) → int`, integer-rounded toward ceiling (NFR-SAFE-02). Handles "zero work" edge case → 0 (FR-PAY-06).
- **PBT obligations**: `deposit ≥ 0`; `deposit ≥ hoursWorked * rate` for all valid inputs (PBT-03).
- **Interface**: `IDepositCalculator`

### C-03 RefundCalculator
- **Purpose**: Compute refund `deposit − (actualHoursWorked × rate)`, clamped to `[0, deposit]`.
- **PBT obligations**: `0 ≤ refund ≤ deposit`; `deposit − refund == hoursWorked × rate` modulo integer rounding (PBT-03; NFR-SAFE-02).
- **Interface**: `IRefundCalculator`

### C-04 HoursEstimator
- **Purpose**: Estimate hours required for a given contract (zones × tasks × avg-speed constant).
- **Responsibilities**: Pure function `(IReadOnlyList<Zone>, IReadOnlySet<TaskKind>, IConfigSnapshot) → decimal`. Excludes unreachable tile area (FR-WORK-08).
- **Interface**: `IHoursEstimator`

### C-05 ZoneGeometry
- **Purpose**: Tile-rectangle math: union, intersection, contains, count reachable tiles given a passability oracle.
- **Responsibilities**: Pure operations over `Zone` records. Takes a `Func<TileCoord, bool>` passability oracle (injected by the SMAPI side from `Game1` location data).
- **PBT obligations**: Round-trip serialization of zones (PBT-02); invariants on rectangle union (PBT-03).
- **Interface**: `IZoneGeometry`

### C-06 CapabilityEvaluator
- **Purpose**: Map tool upgrade levels → "what objects the worker can break/chop/water/scythe".
- **Responsibilities**: Pure function `ToolSnapshot → CapabilityMatrix`. The matrix is a struct of bools per object class (small stump, large log, small boulder, large boulder, meteorite, etc.). Hardcodes the always-skip rule for fruit trees (FR-SKIP-03).
- **Interface**: `ICapabilityEvaluator`

### C-07 TaskPriorityOrderer
- **Purpose**: Sort `(TaskKind, TileCoord)` work items into the spec's priority order (FR-WORK-03).
- **Responsibilities**: Pure stable sort. Deterministic given identical input ordering, which matters for test reproducibility.
- **Interface**: `ITaskPriorityOrderer`

### C-08 ShiftStateMachine
- **Purpose**: Per the D3 decision — the pure state machine driving the worker's shift.
- **Responsibilities**: States `WaitingForSpawn`, `Working`, `Stuck`, `Recovering`, `Depositing`, `Exiting`, `Done`. Transition function `(State, Event) → State` is pure and free of side effects. Events come in from the SMAPI-side `ShiftOrchestrator` (a sub-service in the Mod project) and the state machine emits intent records (e.g., `IntentMoveToTile`, `IntentEmote`, `IntentTeleportHome`, `IntentDepositAtChest`) that the orchestrator carries out.
- **PBT obligations**: Invariants on legal transitions (PBT-03); the state graph should never leave `Done` once entered.
- **Interface**: `IShiftStateMachine`

### C-09 StuckDetector
- **Purpose**: Track no-progress windows and fire stuck events (FR-WORK-11/12).
- **Responsibilities**: Per-shift counter, advanced by tick events with a "made-progress this tick" flag. Emits `StuckDetected` after the configured threshold. Reset on progress.
- **Interface**: `IStuckDetector`

### C-10 ItemBuffer
- **Purpose**: Hold all collected items for the shift, indexed by destination key (Chest|ShippingBin|Mail).
- **Responsibilities**: Mutable but bounded type. Methods to `Add(Item)`, `TakeAllFor(DestinationKey)`, `Snapshot()`. Snapshot used for save persistence on early-sleep fast-forward (FR-DAY-02).
- **PBT obligations**: Round-trip on `Snapshot()` (PBT-02); invariant `Add + TakeAllFor preserves total item count` (PBT-03).
- **Interface**: `IItemBuffer`

### C-11 DepositPlanner
- **Purpose**: Given the buffer state and a set of chest assignments, produce the ordered list of deposit trips (FR-WORK-05 — one trip per unique destination, items consolidated).
- **Responsibilities**: Pure planner over `(IItemBuffer.Snapshot, ChestAssignmentMap) → IReadOnlyList<DepositTrip>`. Trip ordering minimizes total walking distance given a tile-distance oracle.
- **Interface**: `IDepositPlanner`

### C-12 ContractStore
- **Purpose**: In-memory authoritative list of all contracts (one-time + recurring) for the active save.
- **Responsibilities**: CRUD-ish (`Add`, `Get`, `Update`, `Cancel`, `Pause`, `Resume`, `List`). Stable identifiers (GUID per contract). Hydrated by `ContractPersistenceAdapter` at save-load; flushed back at save-write.
- **Interface**: `IContractStore`

### C-13 SaveDataSerializer
- **Purpose**: Convert `ContractStore`'s in-memory state to/from a versioned JSON DTO.
- **Responsibilities**: Pure serialization. Tolerates absent data (NFR-SAFE-03). Versioned schema for future migrations.
- **PBT obligations**: Round-trip `deserialize(serialize(x)) == x` for all valid x (PBT-02 — primary obligation).
- **Interface**: `ISaveDataSerializer`

### C-14 ConfigSnapshot
- **Purpose**: Immutable record of all tunable config values per D4.
- **Responsibilities**: Captured at shift-start; passed to all Core components that need config. Fields: base rate, per-task increments, average-speed constant, 8pm cap (in-game minutes), stuck initial threshold, stuck post-teleport threshold.
- **Interface**: `IConfigSnapshot` (record)

### C-15 ConfigDefaults
- **Purpose**: Static factory producing the default `ConfigSnapshot` matching the spec's pricing table.
- **Interface**: `ConfigDefaults.Build() → IConfigSnapshot`

---

## SMAPI-bound components (`Dayswork` project)

### M-01 ModEntry
- **Purpose**: SMAPI entry point. Composition root per D2.
- **Responsibilities**: Implements `Mod` interface; in `Entry()` constructs every singleton, wires SMAPI events, applies Harmony patches, registers GMCM. Loads `IConfigSnapshot` defaults + user overrides from `config.json`. Holds references to top-level services for the mod lifetime.

### M-02 BulletinBoardPatch (Harmony)
- **Purpose**: Inject "Hire a Farmhand" option into the vanilla bulletin-board menu (FR-HIRE-01).
- **Responsibilities**: Single Harmony postfix on the bulletin board menu's draw + click handler. Calls `HiringFlowCoordinator.OpenHiringFlow()` when the entry is clicked. Suppresses itself in multiplayer per `MultiplayerGuard`.

### M-03 HiringFlowCoordinator
- **Purpose**: Drives screen-to-screen transitions for the 4-screen hiring flow per D6.
- **Responsibilities**: Owns the in-progress `ContractDraft`. Opens `TaskSelectionMenu`; on advance, swaps to `ZoneAndChestMenu`; etc. On final confirm, persists the contract to `ContractStore` and deducts the deposit via SMAPI's `Game1.player.Money` API.

### M-04 TaskSelectionMenu  *(extends `IClickableMenu`)*
- **Purpose**: Screen 1 — task toggles with live rate display (FR-HIRE-04).
- **Responsibilities**: Renders toggles, calls `RateCalculator` on every change, emits the resulting `EnabledTasks` set back into `ContractDraft`.

### M-05 ZoneAndChestMenu  *(extends `IClickableMenu`)*
- **Purpose**: Screen 2 — zone drawing + chest assignment (FR-HIRE-05/06/07).
- **Responsibilities**: Hosts the building-chest dropdown panel; delegates to `ZoneDrawOverlay` for tile-rectangle drawing; uses `ChestResolver` to enumerate buildings and their chests for the dropdown.

### M-06 ScheduleMenu  *(extends `IClickableMenu`)*
- **Purpose**: Screen 3 — one-time vs recurring selection (FR-HIRE-11).

### M-07 SummaryMenu  *(extends `IClickableMenu`)*
- **Purpose**: Screen 4 — summary + confirm (FR-HIRE-13/14).
- **Responsibilities**: Renders the summary using `HoursEstimator` + `DepositCalculator`. The Confirm action is only enabled when `Game1.player.Money ≥ deposit`.

### M-08 ZoneDrawOverlay
- **Purpose**: Renders the in-progress zone rectangle overlay on the farm map; handles drag input.
- **Responsibilities**: Hooks `Display.RenderedWorld` to draw the rectangle preview while the player is in draw-mode.

### M-09 FarmhandNpc  *(extends Stardew's `NPC`)*
- **Purpose**: The visible worker NPC (FR-WORK-01/02, FR-NPC-01).
- **Responsibilities**: Sprite + animation state. Has an `Update(GameTime)` override that delegates intent execution to `ShiftOrchestrator`. Holds a reference to the current `IShiftStateMachine` (constructed at spawn).
- **Notes**: Placeholder sprite for v1 (Q9). Invulnerable: overrides damage hooks to plays `OuchEmote` and returns 0 damage (FR-NPC-02).

### M-10 ToolSwapAnimator
- **Purpose**: Manages the visual tool-swap when the farmhand changes task class (FR-WORK-10).
- **Responsibilities**: Brief animation when the current task type differs from the previous tick's task type. Owns the asset references for axe / can / scythe / pickaxe overlays.

### M-11 PathFindControllerAdapter
- **Purpose**: Thin wrapper around Stardew's `PathFindController` so the orchestrator depends on an interface, not a Stardew class.
- **Responsibilities**: `PathTo(TileCoord)`, `IsPathing`, `OnArrived` event. Translates `IntentMoveToTile` records from the state machine into actual pathfinding.

### M-12 ShiftOrchestrator
- **Purpose**: The SMAPI-side execution arm of the per-shift state machine. The state machine emits intents; the orchestrator carries them out against the game.
- **Responsibilities**: Subscribes to `GameLoop.UpdateTicked` and `GameLoop.TimeChanged`. On each tick: feeds events into `IShiftStateMachine.Step(...)`, dispatches resulting intents (move, emote, deposit, teleport, exit). Coordinates with `FarmhandNpc`, `PathFindControllerAdapter`, `ToolSwapAnimator`, `ChestResolver`, `MailDispatcher`, and `RefundCalculator`. Handles the 8pm cap → forced state transition. Calls into `RecurringContractScheduler` when shift ends.

### M-13 RecurringContractScheduler
- **Purpose**: Daily lifecycle for recurring contracts (FR-HIRE-11/12, FR-PAY-04).
- **Responsibilities**: Subscribed to `GameLoop.DayStarted`. For each recurring contract: check festival/can-afford/multiplayer guards; deduct deposit; trigger `ShiftOrchestrator.StartShift(contract)`. Sends mail on can't-afford via `MailDispatcher`.

### M-14 CalendarHandlers
- **Purpose**: Festival skip + rain detection + sleep fast-forward (FR-DAY-01/02, FR-PAY-07).
- **Responsibilities**: Festival check (queries `Game1.weatherForTomorrow` / `Utility.isFestivalDay`); rain check (`Game1.IsRainingHere`); sleep handler subscribes to `GameLoop.Saving` and triggers atomic shift fast-forward before the day rolls over.

### M-15 ContractPersistenceAdapter
- **Purpose**: Bridge `ContractStore` ↔ SMAPI's `Helper.Data.WriteSaveData` per-save API (FR-PERSIST-01).
- **Responsibilities**: Subscribed to `GameLoop.SaveLoaded` (hydrate) and `GameLoop.Saving` (flush). Uses `SaveDataSerializer` to convert.

### M-16 MailDispatcher
- **Purpose**: Send mail letters for overflow items + warning notices (FR-OUT-05, FR-PAY-04, FR-TOOL-03).
- **Responsibilities**: Adapter over the **Mail Framework Mod (MFM)** API (per V9 decision in [design-verification-notes.md](design-verification-notes.md)). MFM is acquired via `Helper.ModRegistry.GetApi<...>` at mod-startup and supports multi-item attachments per letter (the vanilla `%item id` token does not). Reads body strings from `I18nHelper`. Single sender label ("Your farmhand", i18n-routed). Warning-only letters (can't-afford, missing-tool) skip MFM and use vanilla `Game1.addMailForTomorrow(mailId)` since they carry no items.
- **Required dependency**: MFM declared in `manifest.json` `Dependencies` (UniqueID confirmed during Construction).

### M-17 GMCMRegistrar
- **Purpose**: Register Dayswork's config schema with GMCM if installed (FR-CFG-01, FR-WORK-13).
- **Responsibilities**: Optional-dependency probe for `GenericModConfigMenu`; if present, enumerates all `IConfigSnapshot` fields and registers each with appropriate validators and i18n labels.

### M-18 MultiplayerGuard
- **Purpose**: Refuse activity in multiplayer (FR-MP-01).
- **Responsibilities**: Single boolean query `IsSinglePlayerSession()`. Called by `ModEntry`, `BulletinBoardPatch`, and `RecurringContractScheduler` before activating any feature.

### M-19 ToolLevelReader
- **Purpose**: Read the player's tool upgrade levels into a `ToolSnapshot` (FR-TOOL-01).
- **Responsibilities**: Pure query against `Game1.player` tools. Used once at shift start; the resulting snapshot is passed into `CapabilityEvaluator`.

### M-20 ChestResolver
- **Purpose**: Resolve a stored `ChestRef` (location name + tile coords) to the live `Chest` object at runtime (FR-HIRE-08).
- **Responsibilities**: Lookup by location + tile. Returns `null` if the chest is missing/destroyed (triggers FR-OUT-03 fallback). Also enumerates building chests for the Screen 2 dropdown (FR-HIRE-07).

### M-21 I18nHelper
- **Purpose**: Thin wrapper around `Helper.Translation` for typed-key access (NFR-UX-02, S-20).
- **Responsibilities**: Single point of contact for i18n strings; surfaces a `Get(string key, object args)` method. Internal lint pass to detect hardcoded English strings in tests is a recommended Build-and-Test gate.

---

## Components total: 14 Core + 21 Mod = 35

Most Mod components are <100 LOC stubs that translate between SMAPI APIs and Core abstractions. The complexity budget concentrates in Core (where it's testable) and in M-03/M-05/M-12 (the user-facing flow + worker orchestrator).
