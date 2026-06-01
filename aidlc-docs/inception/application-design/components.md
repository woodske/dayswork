# Components — Dayswork Pricing Model Redesign

This document refreshes the Application Design component inventory for the pricing overhaul. The earlier design's Core-vs-Mod split still stands, but the hourly deposit/refund seam has been replaced with fixed contract pricing, typed work scopes, persisted contract-term snapshots, and worker-energy accounting.

**Still valid from the earlier design**:
- `Dayswork.Core` stays free of SMAPI/Stardew references
- `Dayswork` remains the SMAPI-bound integration layer
- `Dayswork.Tests` continues to reference only `Dayswork.Core`
- The worker still runs through an explicit shift state machine

**Superseded by this redesign**:
- `RateCalculator`
- `DepositCalculator`
- `RefundCalculator`
- `HoursEstimator`

Those components are removed from the refreshed architecture. Their responsibilities are replaced by scope classification, fixed-price calculation, persisted pricing snapshots, and energy-profile modeling.

---

## Solution Layout

```text
Dayswork.sln
├── Dayswork.Core/          Pure logic, zero SMAPI/Stardew refs
│   ├── Domain/             Contract, scopes, snapshots, work/action enums
│   ├── Pricing/            Scope classification, outdoor banding, fixed-price calculation
│   ├── Energy/             Worker energy profile + per-action spend ledger
│   ├── Geometry/           Zone math
│   ├── Capabilities/       Tool capability snapshot evaluation
│   ├── Shifts/             State machine, priority ordering, stuck detection
│   ├── Inventory/          Item buffer + deposit planning
│   ├── Persistence/        Save DTOs + serializer
│   └── Config/             Immutable config snapshot + defaults
├── Dayswork/               SMAPI mod integration layer
│   ├── Patches/
│   ├── UI/
│   ├── Worker/
│   ├── Orchestration/
│   ├── Integration/
│   ├── Guards/
│   ├── i18n/default.json
│   └── manifest.json
└── Dayswork.Tests/         xUnit + FsCheck, references only Dayswork.Core
```

---

## Core Components (`Dayswork.Core`)

### C-01 WorkScopeClassifier
- **Purpose**: Convert the player's selected zones/buildings plus enabled tasks into explicit typed work scopes.
- **Responsibilities**:
  - Distinguish outdoor zone work from animal-building scope
  - Represent greenhouse work as its own dedicated crop-work scope
  - Produce a normalized `WorkScopeSet` that pricing and runtime can both consume
- **Interface**: `IWorkScopeClassifier`

### C-02 OutdoorServiceBandClassifier
- **Purpose**: Assign broad size bands for outdoor crop/clearing services.
- **Responsibilities**:
  - Derive per-service `Small / Medium / Large` style banding from saved outdoor scope
  - Keep band logic deterministic and independent of daily actionable work
  - Support service-sensitive banding without leaking exact-hour math back into the design
- **Interface**: `IOutdoorServiceBandClassifier`

### C-03 ContractPriceCalculator
- **Purpose**: Compute fixed contract price from typed scopes, selected tasks, and config.
- **Responsibilities**:
  - Apply outdoor service-band pricing
  - Apply fixed animal-building pricing
  - Apply fixed greenhouse package pricing
  - Produce raw totals without any deposit/refund concepts
- **Interface**: `IContractPriceCalculator`

### C-04 PriceBreakdownBuilder
- **Purpose**: Build the persisted and UI-friendly pricing breakdown.
- **Responsibilities**:
  - Convert raw price totals into stable line items the UI can show directly
  - Produce a `PricingSnapshot` suitable for contract persistence
  - Preserve the player-facing explanation of what is being paid for
- **Interface**: `IPriceBreakdownBuilder`

### C-05 WorkerEnergyProfileBuilder
- **Purpose**: Build the worker's daily energy profile from config.
- **Responsibilities**:
  - Produce daily energy capacity
  - Produce per-action energy cost mappings
  - Provide a preview-friendly summary for hiring screens
- **Interface**: `IWorkerEnergyProfileBuilder`

### C-06 ContractTermsBuilder
- **Purpose**: Pure preview/query facade selected in AD-R5.
- **Responsibilities**:
  - Orchestrate scope classification, outdoor banding, price calculation, pricing snapshot building, and energy-profile building
  - Return both a persisted `ContractTermsSnapshot` and a UI-ready `ContractPreview`
  - Rebuild recurring contract terms from saved scope plus current config when needed
- **Interface**: `IContractTermsBuilder`

### C-07 WorkerEnergyLedger
- **Purpose**: Track remaining worker energy during a shift.
- **Responsibilities**:
  - Spend energy per work action
  - Clamp remaining energy at zero
  - Support the rule “finish the current work unit, then deposit and leave”
  - Expose state useful to the shift state machine and worker HUD
- **Interface**: `IWorkerEnergyLedger`

### C-08 ZoneGeometry
- **Purpose**: Tile-rectangle math for outdoor zones.
- **Responsibilities**:
  - Union/containment/enumeration
  - Reachable tile counting when needed by pricing/runtime helpers
  - Shared geometry utilities for both UI and runtime
- **Interface**: `IZoneGeometry`

### C-09 CapabilityEvaluator
- **Purpose**: Map the player's tool snapshot to worker capabilities.
- **Responsibilities**:
  - Determine what the worker can chop/break/water/scythe
  - Preserve hard exclusions like fruit-tree felling
  - Keep capability logic independent from the SMAPI runtime
- **Interface**: `ICapabilityEvaluator`

### C-10 TaskPriorityOrderer
- **Purpose**: Order work items according to the approved broad priority rules.
- **Responsibilities**:
  - Prioritize animal work ahead of crop work, then clearing work
  - Preserve deterministic ordering inside equal-priority groups
  - Support future extension without changing worker orchestration shape
- **Interface**: `ITaskPriorityOrderer`

### C-11 ShiftStateMachine
- **Purpose**: Pure state machine driving the worker's daily lifecycle.
- **Responsibilities**:
  - Model working, stuck recovery, deposit, exit, and completion phases
  - React to work-unit completion, energy depletion, time cap, and stuck events
  - Emit runtime intents without knowing about SMAPI/NPC types
- **Interface**: `IShiftStateMachine`

### C-12 StuckDetector
- **Purpose**: Detect lack of movement/progress and trigger stuck recovery.
- **Responsibilities**:
  - Measure no-progress windows in in-game minutes
  - Differentiate “still working” from “truly stuck”
  - Feed stuck events into the shift state machine
- **Interface**: `IStuckDetector`

### C-13 ItemBuffer
- **Purpose**: Hold collected output until deposit or mail fallback.
- **Responsibilities**:
  - Buffer items by destination
  - Snapshot/hydrate buffered state for persistence-safe stop conditions
  - Remain independent of pricing/billing logic
- **Interface**: `IItemBuffer`

### C-14 DepositPlanner
- **Purpose**: Plan deposit trips at shift end.
- **Responsibilities**:
  - Group buffered items by unique destination
  - Produce one trip per unique chest/bin destination
  - Keep deposit ordering separate from billing concerns
- **Interface**: `IDepositPlanner`

### C-15 ContractStore
- **Purpose**: Authoritative in-memory contract registry for the loaded save.
- **Responsibilities**:
  - Store contracts, terms snapshots, schedule state, and status
  - Support add/update/pause/cancel/edit workflows
  - Support daily lookup for one-time and recurring start logic
- **Interface**: `IContractStore`

### C-16 SaveDataSerializer
- **Purpose**: Serialize/deserialize persisted contract data.
- **Responsibilities**:
  - Convert current contract schema to/from versioned DTOs
  - Silently drop legacy hourly/deposit/refund contracts during load, per the pre-release deletion policy chosen in AD-R6
  - Keep save-data handling tolerant of absent or stale data
- **Interface**: `ISaveDataSerializer`

### C-17 ConfigSnapshot
- **Purpose**: Immutable view of all tunable redesign values.
- **Responsibilities**:
  - Hold price tables, energy capacity, per-action costs, pacing knobs, and operational thresholds
  - Provide a stable input snapshot for contract pricing and runtime setup
- **Interface**: `IConfigSnapshot`

### C-18 ConfigDefaults
- **Purpose**: Produce default config values for the redesigned system.
- **Responsibilities**:
  - Define baseline outdoor band prices, animal-building prices, greenhouse package prices, energy capacity, and action costs
  - Centralize balance defaults so GMCM/config overlay has one stable base
- **Interface**: `ConfigDefaults.Build()`

---

## Mod Components (`Dayswork`)

### M-01 ModEntry
- **Purpose**: SMAPI composition root.
- **Responsibilities**:
  - Build Core and Mod singletons
  - Register SMAPI events
  - Wire Harmony, GMCM, persistence, and orchestration

### M-02 BulletinBoardPatch
- **Purpose**: Inject “Hire a Farmhand” entry into the vanilla bulletin board.
- **Responsibilities**:
  - Patch the entry point
  - Respect multiplayer guard
  - Open the hiring flow

### M-03 HiringFlowCoordinator
- **Purpose**: Orchestrate the four-screen hiring/editing flow.
- **Responsibilities**:
  - Hold the in-progress draft
  - Request previews/terms from `ContractTermsBuilder`
  - Persist confirmed contracts with their terms snapshot

### M-04 TaskSelectionMenu
- **Purpose**: Screen 1 — task toggles + live contract preview entry point.
- **Responsibilities**:
  - Show task toggles
  - Trigger live preview refresh
  - Display price contributions in a player-readable way

### M-05 ZoneAndChestMenu
- **Purpose**: Screen 2 — work-scope and output configuration.
- **Responsibilities**:
  - Manage outdoor zones, selected barns/coops, greenhouse selection, and output destinations
  - Use `ChestResolver` and `ZoneDrawOverlay`

### M-06 ScheduleMenu
- **Purpose**: Screen 3 — one-time vs recurring selection.
- **Responsibilities**:
  - Capture schedule choice
  - Support edit/pause/cancel entry points coherently

### M-07 SummaryMenu
- **Purpose**: Screen 4 — confirm fixed price + energy summary.
- **Responsibilities**:
  - Display pricing breakdown, scope summary, and worker energy summary
  - Deduct one-time contract price only on confirm
  - Avoid all deposit/refund/hour terminology

### M-08 ZoneDrawOverlay
- **Purpose**: Farm overlay for rectangle selection.
- **Responsibilities**:
  - Draw in-progress rectangles
  - Return finalized outdoor zone selections

### M-09 FarmhandNpc
- **Purpose**: Visible worker NPC.
- **Responsibilities**:
  - Spawn/despawn on contract days
  - Surface visible energy bar state
  - Remain invulnerable to player attacks

### M-10 ToolSwapAnimator
- **Purpose**: Visual tool/task beat presentation.
- **Responsibilities**:
  - Show tool changes
  - Support slower, readable task cadence

### M-11 PathFindControllerAdapter
- **Purpose**: Adapter around Stardew pathfinding.
- **Responsibilities**:
  - Move the worker toward target tiles/doors
  - Surface arrival/failure signals to orchestration

### M-12 ShiftOrchestrator
- **Purpose**: Execute the worker's day against the live game world.
- **Responsibilities**:
  - Feed events to the state machine
  - Spend energy through `WorkerEnergyLedger`
  - Execute work-unit, deposit, stuck, and exit intents

### M-13 RecurringContractScheduler
- **Purpose**: Daily lifecycle for saved contracts.
- **Responsibilities**:
  - Rebuild recurring terms from saved scope and current config
  - Apply fixed recurring charges
  - Skip/notify on festival or cannot-afford cases

### M-14 CalendarHandlers
- **Purpose**: Calendar and save-time hooks.
- **Responsibilities**:
  - Detect rain/festivals
  - Stop/settle the worker on sleep

### M-15 ContractPersistenceAdapter
- **Purpose**: Bridge `ContractStore` and SMAPI save data.
- **Responsibilities**:
  - Hydrate on save load
  - Flush on save
  - Accept the serializer's silent pre-release legacy-drop behavior

### M-16 MailDispatcher
- **Purpose**: Send next-day output mail and same-day notices.
- **Responsibilities**:
  - Queue overflow/unassigned output mail
  - Queue same-day cannot-afford/festival notices
  - Stay out of pricing/refund settlement logic

### M-17 GMCMRegistrar
- **Purpose**: Expose redesign config through GMCM.
- **Responsibilities**:
  - Register price, energy, pacing, and operational knobs
  - Keep labels/tooltips localized

### M-18 MultiplayerGuard
- **Purpose**: Keep v1 single-player only.
- **Responsibilities**:
  - Short-circuit entry points in multiplayer
  - Surface a friendly log message

### M-19 ToolLevelReader
- **Purpose**: Snapshot player tool levels at shift start.
- **Responsibilities**:
  - Read upgrade tiers
  - Produce `ToolSnapshot` for capability evaluation

### M-20 ChestResolver
- **Purpose**: Resolve stored chest references and enumerate selectable chests.
- **Responsibilities**:
  - Resolve live chests by location + tile
  - Provide building-interior chest lists for configuration

### M-21 I18nHelper
- **Purpose**: Typed access to user-visible strings.
- **Responsibilities**:
  - Centralize translation lookup
  - Keep UI/mail/config labels out of hardcoded English

---

## Important Domain Types Introduced or Reframed

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
- `WorkerEnergyState`
- `WorkActionKind`

These are pure domain/value types rather than top-level orchestrators, but they define the new shape the redesign depends on.

---

## Component Count

- **Core**: 18 components
- **Mod**: 21 components
- **Total**: 39 components

The increase versus the earlier design is intentional: the redesign replaces one big hourly-billing mental model with clearer, explicitly separated responsibilities for typed scope modeling, pricing snapshots, and worker energy.

---

## Addendum — SVE Compatibility Provider Seam (2026-05-29)

The Stardew Valley Expanded compatibility change adds an isolated expansion-compatibility seam. Full design in [sve-compatibility-application-design.md](sve-compatibility-application-design.md). New components:

- **Core (`Dayswork.Core/Compat/`)**: `C-19 IExpansionProfile`, `C-20 ExpansionProfileSelector`, `C-21 VanillaExpansionProfile`, `C-22 SveExpansionProfile`, `C-23 AnimalBuildingCapacityPolicy` (all pure).
- **Mod (`Dayswork/Compat/`)**: `M-22 ExpansionDetector`, `M-23 ExpansionCompatService` (the runtime seam).

Existing components (`ShiftOrchestrator`, `AnimalTaskHandler`, `ObjectTargetClassifier`, building navigators) delegate to `M-23` with no inline SVE branches; the Vanilla profile guarantees unchanged vanilla behavior.
