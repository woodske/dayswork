# Unit of Work — Dayswork

## What a "unit" means here

A **unit of work** is one coherent batch of code that goes through the Construction per-unit loop — **Functional Design → NFR Requirements → NFR Design → Code Generation** — as an atomic checkpoint before the next unit starts. Dayswork ships as **one assembly** (a single SMAPI mod), so units are *not* independently deployable services; they are logical, sequential development increments inside the single deployable.

**Component ownership rule (greenfield convention)**: A component is *owned* by the unit that **introduces it** (creates its file in the solution). Subsequent units may *extend* an owned component (add states, methods, callers) — those extensions are listed under the *extending* unit as "Extends" rather than "Owns". This guarantees every component file is created by exactly one unit while keeping the deepening units meaningful.

**Decomposition methodology** (from approved plan [unit-of-work-plan.md](../plans/unit-of-work-plan.md)):
- **U1 = Hybrid**: foundational Core-only units first, then vertical feature slices
- **U2 = ~16 units**: small enough for one-or-two-session Construction loops
- **U3 = Test infrastructure as its own early unit**
- **U4 = Hybrid sequencing**: foundations → thin end-to-end slice → deepen
- **U5 = Explicit Project Scaffold unit first**

## Code organization strategy (greenfield)

The solution layout was decided in Application Design D1/D2/D6 (see [components.md](components.md)). Every unit's code lands in the right project per the layout:

```text
C:\Users\kwood\Repos\dayswork\
├── Dayswork.sln                 ← created in U-01
├── Dayswork.Core\               ← Core foundation units (U-03..U-07) land here
│   ├── Config\                  ← U-03
│   ├── Domain\, Geometry\       ← U-04
│   ├── Pricing\                 ← U-05
│   ├── Persistence\             ← U-06
│   ├── Capabilities\, Shifts\   ← U-07 (priority orderer + capability matrix)
│   ├── Shifts\                  ← state machine introduced in U-10, extended in U-13/U-14
│   └── Inventory\               ← U-10 (buffer), U-14 (deposit planner)
├── Dayswork\                    ← Mod units (U-08..U-17) land here
│   ├── ModEntry.cs              ← scaffolded in U-01, composition root grows each unit
│   ├── Patches\                 ← U-08 (bulletin board)
│   ├── UI\                      ← U-09 (TaskSelection, Summary), U-11 (Zone/Chest + overlay), U-12 (Schedule + board mgmt)
│   ├── Worker\                  ← U-10 (Npc, path adapter), U-13 (tool swap animator)
│   ├── Orchestration\           ← U-10 (shift orch + sched stub), U-15 (full sched + calendar)
│   ├── Integration\             ← U-08 (i18n, mp guard), U-09 (persistence adapter), U-10 (tool reader), U-11 (chest resolver), U-14 (mail), U-17 (gmcm)
│   ├── i18n\default.json        ← created in U-08; grows every UI-introducing unit
│   └── manifest.json            ← created in U-01
└── Dayswork.Tests\              ← created in U-02; every unit drops its test files here
    ├── Generators\              ← FsCheck generators added per unit
    └── (mirrors Dayswork.Core directory structure)
```

**Test placement rule** (from U3 answer): U-02 establishes the test project, FsCheck integration, the shared FsCheck generator namespace, and the seed-logging convention. Every subsequent Core-owning unit drops test files alongside the existing infrastructure rather than re-inventing it. Mod-owning units add light integration tests only where the surface area is testable without launching Stardew.

**Manifest.json grows over time**: U-01 creates the skeleton. U-08 adds `i18n` content. U-14 adds the MFM `Dependencies` entry. U-17 adds the GMCM optional `OptionalDependencies` entry.

**ModEntry composition root grows over time**: U-01 starts as a stub with one log line. Each subsequent Mod unit adds its singleton constructions and SMAPI event wirings in the order documented in [services.md](services.md) Service S-A.

---

## The 17 units

### Foundation phase (units that have no dependencies on later Mod work)

#### U-01 — Project Scaffold

**Purpose**: Stand up a loadable empty SMAPI mod that logs "Dayswork loaded" at startup. Proves Construction is unblocked.

**Components owned**: M-01 ModEntry (stub form only — `Entry()` just logs).

**Code organization**:
- Create `Dayswork.sln` at workspace root
- Create `Dayswork.Core` csproj (.NET 6 class library, no SMAPI/Stardew refs, only Newtonsoft.Json reference per [component-dependency.md](component-dependency.md) rule 1)
- Create `Dayswork` csproj (.NET 6 class library, references `Dayswork.Core`, SMAPI, StardewValley, Harmony; pulls `Pathoschild.Stardew.ModBuildConfig` NuGet; enables `<EnableHarmony>true</EnableHarmony>`)
- Create `manifest.json` skeleton (UniqueID = `Bindicle.Dayswork` per user's modding handle, Version 0.1.0)
- Create empty `i18n/default.json` (will be populated starting in U-08)
- Create `README.md` and `LICENSE` (MIT, decided in Q7)

**Stories implemented**: None directly; foundational scaffolding for S-19 (Core/Mod project separation, Patches namespace).

**Definition of Done**: `dotnet build` succeeds; dropping the compiled mod into `Stardew Valley/Mods/Dayswork/` and launching SMAPI shows "Dayswork loaded" in the SMAPI console.

---

#### U-02 — Test Infrastructure

**Purpose**: Establish the test project and FsCheck/xUnit conventions once so that all subsequent units only need to drop test files into a working framework.

**Components owned**: None (test scaffolding, not production components).

**Code organization**:
- Create `Dayswork.Tests` csproj (.NET 6 class library, references only `Dayswork.Core` per [component-dependency.md](component-dependency.md) rule 2)
- Add NuGet packages: xUnit (chosen in Q4), `FsCheck.Xunit` (chosen per PBT-09), `Microsoft.NET.Test.Sdk`
- Create `Dayswork.Tests/Generators/` namespace stub
- Establish seed-logging convention per PBT-08: a shared xUnit fixture or test-class base that captures the FsCheck `Replay` seed on failure and logs the shrunk minimal failing input to the test output
- Create a stub test that calls `FsCheck.Property.ForAll((int x) => x + 0 == x)` to confirm the framework wires up end-to-end
- Document the testing conventions in a brief `Dayswork.Tests/README.md`

**Stories implemented**: S-19 (PBT-08, PBT-09 infrastructure obligations).

**Definition of Done**: `dotnet test` runs the stub property and passes; deliberately failing a property prints both the seed AND the shrunk input to console.

---

#### U-03 — Config Foundation

**Purpose**: Pure config records that every Core foundation unit needs as input. No game-state dependencies.

**Components owned**: C-14 ConfigSnapshot, C-15 ConfigDefaults.

**Code organization**: `Dayswork.Core/Config/` — `IConfigSnapshot.cs`, `ConfigSnapshot.cs` (immutable record), `ConfigDefaults.cs` (static factory matching the spec's pricing table from [requirements.md](../requirements/requirements.md) FR-PAY-01).

**Test files** (in `Dayswork.Tests/Config/`):
- Unit test verifying `ConfigDefaults.Build()` produces the spec's default values
- FsCheck generator for `IConfigSnapshot` in `Dayswork.Tests/Generators/ConfigSnapshotGen.cs` (used by every later Core unit's PBTs per PBT-07 shared-generator obligation)

**Stories implemented**: Foundation for S-13 (GMCM later exposes these fields).

**Definition of Done**: `IConfigSnapshot` record is immutable; `ConfigDefaults.Build()` returns the spec-defined values; a shared FsCheck generator exists for downstream PBTs.

---

#### U-04 — Geometry & Domain Primitives

**Purpose**: Pure geometry math and the value types that flow through every other component.

**Components owned**: C-05 ZoneGeometry. Plus the value-record types referenced everywhere: `Zone`, `TileCoord`, `ChestRef`, `TaskKind` enum, `DestinationKey` enum.

**Code organization**:
- `Dayswork.Core/Domain/` — `TileCoord.cs`, `Zone.cs`, `ChestRef.cs`, `TaskKind.cs`, `DestinationKey.cs`
- `Dayswork.Core/Geometry/` — `IZoneGeometry.cs`, `ZoneGeometry.cs`

**Test files** (in `Dayswork.Tests/Geometry/`):
- FsCheck generator `ZoneGen` (used by U-05, U-06 downstream)
- PBT-02 round-trip property for Zone JSON serialization
- PBT-03 invariants for rectangle union (commutative, idempotent, area conservation)

**Stories implemented**: Foundation for S-03 (zones), S-19 (PBT obligations).

**Definition of Done**: ZoneGeometry has no SMAPI references (verified by Core project's reference list); PBT-02 and PBT-03 properties pass for ≥1000 generated inputs.

---

#### U-05 — Pricing Core

**Purpose**: The four pure pricing/estimation functions. The math the player will trust their gold to.

**Components owned**: C-01 RateCalculator, C-02 DepositCalculator, C-03 RefundCalculator, C-04 HoursEstimator.

**Code organization**: `Dayswork.Core/Pricing/` — `IRateCalculator.cs` + `RateCalculator.cs`, `IDepositCalculator.cs` + `DepositCalculator.cs`, `IRefundCalculator.cs` + `RefundCalculator.cs`, `IHoursEstimator.cs` + `HoursEstimator.cs`.

**Test files** (in `Dayswork.Tests/Pricing/`):
- PBT-03 invariant: `rate(emptyTasks, config) == config.BaseRate`
- PBT-03 invariant: `rate(taskSet, config) == config.BaseRate + sum(taskSet.map(t => config[t]))`
- PBT-03 invariant: `deposit ≥ 0` and `deposit ≥ hoursWorked × rate`
- PBT-03 invariant: `0 ≤ refund ≤ deposit`
- PBT-03 invariant: `deposit − refund == hoursWorked × rate` modulo integer rounding (NFR-SAFE-02)
- Rain-day branch: with rain flag set, Water Crops increment is excluded from rate (FR-PAY-07)
- Zero-work edge case: deposit for zero estimated hours is 0 (FR-PAY-06)

**Stories implemented**: Foundation for S-02 (live rate display), S-06 (estimate + deposit), S-14 (rain handling), S-19 (PBT obligations PBT-03).

**Definition of Done**: All four calculators are pure (no static state, no Game1 references); all listed PBT properties pass.

---

#### U-06 — Persistence Core

**Purpose**: In-memory contract store + JSON DTO serializer with round-trip safety.

**Components owned**: C-12 ContractStore, C-13 SaveDataSerializer.

**Code organization**:
- `Dayswork.Core/Persistence/` — `IContractStore.cs`, `ContractStore.cs`, `ISaveDataSerializer.cs`, `SaveDataSerializer.cs`
- `Dayswork.Core/Persistence/Dto/` — versioned save DTOs (`ContractDtoV1`, top-level `DaysworkSaveDataV1`)
- `Contract` record itself lives in `Dayswork.Core/Domain/Contract.cs` (added in this unit since prior units don't need it yet)

**Test files** (in `Dayswork.Tests/Persistence/`):
- FsCheck generator `ContractGen` (composes ZoneGen from U-04 + TaskKind set + ChestRef list)
- **PBT-02 primary obligation**: `deserialize(serialize(contract)) == contract` for all generated contracts
- PBT-02 round-trip for full `ContractStore.List()` collections
- Unit test for NFR-SAFE-03: deserializing a save with missing `Dayswork.Contracts` segment yields an empty store, not a crash
- Unit test for schema-version field presence (forward migrations)

**Stories implemented**: S-05 (contracts survive save/load), S-19 (PBT-02 primary obligation).

**Definition of Done**: Round-trip property passes for ≥1000 inputs; schema version field is written; missing-data case returns empty store.

---

#### U-07 — Capability & Priority Core

**Purpose**: The two remaining pure-logic primitives: tool-capability evaluation and task-priority ordering. Bundled because they're both small and both feed into the shift state machine.

**Components owned**: C-06 CapabilityEvaluator, C-07 TaskPriorityOrderer. Plus `ToolSnapshot` and `CapabilityMatrix` record types in `Dayswork.Core/Domain/`.

**Code organization**:
- `Dayswork.Core/Capabilities/` — `ICapabilityEvaluator.cs`, `CapabilityEvaluator.cs`, `CapabilityMatrix.cs`
- `Dayswork.Core/Shifts/` — `ITaskPriorityOrderer.cs`, `TaskPriorityOrderer.cs` (the file directory; `ShiftStateMachine` is added to this same directory in U-10)
- `Dayswork.Core/Domain/ToolSnapshot.cs`

**Test files** (in `Dayswork.Tests/Capabilities/` and `Dayswork.Tests/Shifts/`):
- CapabilityEvaluator: table-driven tests for each tool level × object class combination per spec; explicit test that fruit trees are always-skip regardless of axe level (FR-SKIP-03)
- TaskPriorityOrderer: deterministic stable sort property — same input always yields same output; output is in spec's priority order (FR-WORK-03)

**Stories implemented**: Foundation for S-08 (task priority), S-09 (capability snapshot, skip rules), S-19.

**Definition of Done**: Capability matrix matches the spec's "What gets done" table; priority orderer is stable and matches the FR-WORK-03 order.

---

### Thin vertical slice phase (proves end-to-end happy path with minimum surface area)

#### U-08 — Bulletin Board Hook + i18n + Multiplayer Guard

**Purpose**: First piece of player-visible Mod work. The bulletin board entry shows up, clicking it opens a placeholder dialog, and multiplayer sessions are politely refused.

**Components owned**: M-02 BulletinBoardPatch, M-18 MultiplayerGuard, M-21 I18nHelper.

**Extends**: M-01 ModEntry (adds Harmony patch application + multiplayer short-circuit per Service S-A steps 4–7).

**Code organization**:
- `Dayswork/Patches/BulletinBoardPatch.cs` (Harmony postfix; isolated namespace per NFR-MAINT-04)
- `Dayswork/Guards/MultiplayerGuard.cs`
- `Dayswork/Integration/I18nHelper.cs`
- `Dayswork/i18n/default.json` populated with keys used so far: `bulletin.hire_a_farmhand`, `multiplayer.refused_log_message`

**Test files**: Light xUnit in `Dayswork.Tests` for `I18nHelper` if the helper has any logic worth testing in isolation; otherwise this unit is play-tested.

**Stories implemented**: S-01 (discover the hiring option), S-18 (multiplayer refuses with a friendly log message).

**Definition of Done**: In single-player save, opening the Pelican Town bulletin board shows a "Hire a Farmhand" entry; clicking it logs `"[Dayswork] Hire-flow placeholder opened"` (the real coordinator wires up in U-09). In multiplayer, the entry is absent and the SMAPI log shows the friendly refusal message.

---

#### U-09 — Minimum Hiring Flow

**Purpose**: Player can open the hiring UI, toggle tasks, see the live rate, see the estimate, confirm, and have a contract saved that survives game reload. Intentionally thin: no zone drawing (defaults to whole-farm), no chest assignment (defaults to shipping bin), no schedule menu (one-time only).

**Components owned**: M-03 HiringFlowCoordinator, M-04 TaskSelectionMenu, M-07 SummaryMenu, M-15 ContractPersistenceAdapter.

**Extends**: M-01 ModEntry (wires the coordinator and persistence adapter into Service S-A), `i18n/default.json` (menu labels).

**Code organization**:
- `Dayswork/UI/HiringFlowCoordinator.cs`
- `Dayswork/UI/TaskSelectionMenu.cs` (extends `IClickableMenu`)
- `Dayswork/UI/SummaryMenu.cs` (extends `IClickableMenu`)
- `Dayswork/Integration/ContractPersistenceAdapter.cs`
- New `ContractDraft` mutable type in `Dayswork/UI/` (UI-state only, not a Core domain record)
- i18n keys: `ui.task_selection.title`, `ui.task_selection.<task_name>`, `ui.summary.*`, `ui.error.cant_afford`

**Stories implemented**: S-02 (full — task toggles + live rate), S-06 (full — summary + confirm + insufficient-gold block), S-05 (partial — one-time contracts persist; schedule UI and recurring lifecycle land in U-12 and U-15).

**Definition of Done**: Player can hire a one-time farmhand contract; deposit deducts; contract round-trips through save/load via `Helper.Data.WriteSaveData`/`ReadSaveData`. Gamepad navigation works.

---

#### U-10 — Minimum Worker Shift

**Purpose**: Worker physically arrives at 6am, walks to one task tile, performs the task, deposits the buffered items in the shipping bin, walks to the farm entrance, exits with refund applied. Thin: single zone (whole-farm default from U-09), single-trip deposit (shipping-bin-only), no stuck detection, no tool-swap visuals, no overflow mail.

**Components owned**: C-08 ShiftStateMachine (basic — states: WaitingForSpawn, Working, Depositing, Exiting, Done; no Stuck or Recovering yet), C-10 ItemBuffer, M-09 FarmhandNpc, M-11 PathFindControllerAdapter, M-12 ShiftOrchestrator, M-13 RecurringContractScheduler (stub — wired for one-time contracts only, deduplication logic; full recurring lifecycle lands in U-15), M-19 ToolLevelReader.

**Extends**: M-01 ModEntry (wires worker singletons + DayStarted/UpdateTicked/TimeChanged events per Service S-A), C-08 will grow in U-13 (Stuck/Recovering) and U-14 (multi-trip Depositing).

**Code organization**:
- `Dayswork.Core/Shifts/ShiftStateMachine.cs`, `Dayswork.Core/Shifts/IShiftStateMachine.cs`, intent records (`IntentMoveToTile`, `IntentPerformTask`, `IntentDepositInShippingBin`, `IntentApplyRefund`, `IntentExitFarm`)
- `Dayswork.Core/Inventory/ItemBuffer.cs`, `Dayswork.Core/Inventory/IItemBuffer.cs`
- `Dayswork/Worker/FarmhandNpc.cs` (extends Stardew `NPC`; placeholder sprite per Q9)
- `Dayswork/Worker/PathFindControllerAdapter.cs`
- `Dayswork/Orchestration/ShiftOrchestrator.cs`
- `Dayswork/Orchestration/RecurringContractScheduler.cs` (one-time stub)
- `Dayswork/Integration/ToolLevelReader.cs`

**Test files** (in `Dayswork.Tests/`):
- PBT-03 invariants on `IShiftStateMachine.Step` — never leaves `Done`, illegal transitions throw
- PBT-02 round-trip on `IItemBuffer.Snapshot()`
- PBT-03 invariant on ItemBuffer: `Add(item)` then `TakeAllFor(dest)` preserves total count

**Stories implemented**: S-07 (arrival + walk + tool-swap visuals deferred to U-13), S-08 (partial — single task only; full priority order in U-13), S-09 (basic snapshot; full skip-rule branches in U-13), S-10 (shipping-bin deposit only; multi-trip + overflow in U-14).

**Definition of Done**: Hire a one-time contract for "Clear Weeds" with default whole-farm zone and shipping-bin destination. Next morning at 6am, the placeholder NPC spawns at the farm entrance, walks to the first weed tile, plays the cut animation, deposits the harvested fiber in the shipping bin, walks to the exit, and the player receives a partial refund. Sleep ends the day normally; the cycle completes without crashes.

---

### Deepening phase (each unit takes the thin slice and adds depth)

#### U-11 — Full Hiring UI: Zones & Chests

**Purpose**: Replace U-09's "whole farm + shipping bin default" stub with the real zone-drawing UI and chest-assignment UI. Player can now restrict work areas and route output to specific chests.

**Components owned**: M-05 ZoneAndChestMenu, M-08 ZoneDrawOverlay, M-20 ChestResolver.

**Extends**: M-03 HiringFlowCoordinator (inserts ZoneAndChestMenu between TaskSelectionMenu and SummaryMenu), `i18n/default.json` (zone-drawing labels, chest dropdown labels, fallback chest name format from S-04), M-01 ModEntry (wires ChestResolver singleton).

**Code organization**:
- `Dayswork/UI/ZoneAndChestMenu.cs` (extends `IClickableMenu`)
- `Dayswork/UI/ZoneDrawOverlay.cs` (hooks `Display.RenderedWorld`)
- `Dayswork/Integration/ChestResolver.cs`
- i18n keys: `ui.zone_chest.draw_zone_btn`, `ui.zone_chest.select_building_btn`, `ui.zone_chest.set_output_btn`, `ui.zone_chest.chest_fallback_name`

**Stories implemented**: S-03 (full zone drawing + building selection + unreachable-tile silent skip), S-04 (full chest assignment including building-interior dropdown + Gherkin orphaned-chest fallback).

**Definition of Done**: Player can draw multiple zones, select buildings, assign chests per task. Renaming a chest preserves assignment; moving a chest orphans it (falls back to U-14's mail handling once that ships).

---

#### U-12 — Hiring UI: Schedule + Edit/Pause/Cancel

**Purpose**: Add the schedule-selection screen and the bulletin-board contract-management actions.

**Components owned**: M-06 ScheduleMenu.

**Extends**: M-03 HiringFlowCoordinator (inserts ScheduleMenu between ZoneAndChestMenu and SummaryMenu), M-02 BulletinBoardPatch (adds the contract-list view with Pause/Cancel/Edit actions next to the "Hire a Farmhand" entry), `i18n/default.json`, C-12 ContractStore (Pause/Resume methods are added in this unit if not already present from U-06).

**Code organization**:
- `Dayswork/UI/ScheduleMenu.cs` (extends `IClickableMenu`)
- `Dayswork/UI/ContractListMenu.cs` (new — the in-bulletin-board management UI)
- i18n keys: `ui.schedule.one_time`, `ui.schedule.recurring`, `ui.contract_list.pause`, `ui.contract_list.cancel`, `ui.contract_list.edit`

**Stories implemented**: S-05 (full — schedule UI + state-Gherkin persistence test), S-12 (Pause/Cancel/Edit UI flows; recurring-lifecycle behavioral parts ship in U-15).

**Definition of Done**: Player can choose one-time or recurring at hire time; can pause, cancel, or edit any contract from the bulletin board before 6am; cancel is correctly unavailable during an active shift per FR-HIRE-15.

---

#### U-13 — Worker AI: Priority + Capability/Skip + Stuck + Invulnerability

> **Split note (2026-05-21):** U-13 was split. The tool-swap visuals (S-07) and the full-Farmer re-founding (FD-Q5=B) were carved out into **U-13B — Farmer Worker + Tool Visuals**, which runs immediately after U-13. U-13 keeps the worker as the existing `NPC` and delivers all the *behavior* logic on that proven foundation; U-13B is the isolated, higher-risk visual/architecture upgrade.

**Purpose**: Take U-10's "walks to one tile, does one task (teleport stub)" worker and make it behave like a real worker: full priority queue, full capability/skip rules, real walking, stuck recovery escalation, and invulnerability with ouch emote.

**Components owned**: C-09 StuckDetector, ObjectTargetClassifier (Mod — maps game objects to AxeTarget/PickTarget).

**Extends**: C-08 ShiftStateMachine (adds Stuck and Recovering states + transitions per FR-WORK-11/12), M-11 PathFindControllerAdapter (replaces the U-10 teleport stub with native `PathFindController` walking — required for stuck detection to be meaningful and to verify TODO-01), M-12 ShiftOrchestrator (wires StuckDetector, TaskPriorityOrderer, CapabilityEvaluator + ObjectTargetClassifier, full skip-rule branches, trellis adjacency, and the invulnerability hit-detection emote). M-09 FarmhandNpc remains an `NPC` here (re-founded as a `Farmer` in U-13B).

**Code organization**:
- `Dayswork.Core/Shifts/StuckDetector.cs`, `IStuckDetector.cs`
- `Dayswork/Worker/ObjectTargetClassifier.cs`

**Test files**:
- PBT-03 invariants on extended state machine: still never leaves Done; Stuck → Recovering → (Working | Depositing) only; can never enter Stuck from Done
- StuckDetector PBT/unit tests: counter resets on progress; fires after threshold; reset behavior after teleport

**Stories implemented**: S-08 (full priority order + trellis-side harvest + not-ready-skip), S-09 (full capability snapshot with all skip rules and tool-missing warning queued), S-16 (3-step hybrid stuck escalation: emote → teleport to next tile → teleport home end shift), S-17 (invulnerable + ouch emote). *(S-07 tool-swap moved to U-13B.)*

**Definition of Done**: Worker now walks (no longer teleports) and runs a multi-task zone end-to-end in priority order, applies all capability/skip rules, survives sword swings (ouch emote), and recovers from fence-trap with the 3-step escalation. Tool-missing case queues the warning (mail dispatcher itself lands in U-14). TODO-01 (tree-seed drops) re-checked now that the worker walks at a realistic pace.

---

#### U-13B — Farmer Worker + Tool Visuals

> Created by the 2026-05-21 split of U-13. Runs immediately after U-13. This is the isolated, higher-uncertainty architectural change so that a play-test problem here does not entangle the (already-validated) worker-AI logic.

**Purpose**: Re-found the worker on `StardewValley.Farmer` so it visibly uses tools the way the player does, and add the tool-swap animation. Replaces U-13's NPC + native pathfinding with a Farmer + custom movement/rendering.

**Pre-decided design (carried from U-13's design stage — feed these into U-13B's Functional Design rather than re-deciding)**:
- **FD-Q5=B** — full Farmer (not a hybrid NPC+Farmer). Revises FR-NPC-01 (see DEV-01).
- Manual **render hook** with Y-depth sort; worker `Farmer` kept out of `location.characters`/`location.farmers` and never serialized (BR-WORKER-01/03). Rejected alternative: registering in `location.characters`.
- **Manual movement** driver (compute route via game pathfinding, step `Farmer.Position` + walk anim) replacing the NPC `PathFindController` from U-13 (BR-WORKER-02).
- Tool swings via `FarmerSprite.animateOnce` (verified frames: heavy R12/R9/R7, watering can R10/R5/R8/R11, scythe R5/R6/R7); held tool drawn by `FarmerRenderer`.
- Randomized appearance from character-creation field ranges.

**Components owned**: M-10 ToolSwapAnimator, `WorkerTool` map (Core), FarmhandWorker (Farmer — supersedes the U-10/U-13 FarmhandNpc), WorkerMovementDriver (supersedes M-11 PathFindControllerAdapter), WorkerRenderer, WorkerAppearance(+Randomizer).

**Extends**: M-09 FarmhandNpc (re-founded on Farmer), M-12 ShiftOrchestrator (swap worker entity + movement driver, wire ToolSwapAnimator + render hook), M-01 ModEntry (drop NPC portrait asset redirect, add `Display.RenderedWorld`).

**Stories implemented**: S-07 (tool-swap visuals — completes the story).

**Definition of Done**: The worker is a randomized Farmer that walks, draws with correct depth sorting, and visibly swings the right tool (axe/can/scythe/pickaxe) as it works — all U-13 behavior preserved.

---

#### U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail

**Purpose**: Complete the output story. Worker now plans and executes multi-trip deposit runs to assigned chests; anything that doesn't land in a chest (overflow, missing chest, unassigned task output) is mailed the next morning with no fee.

**Components owned**: C-11 DepositPlanner, M-16 MailDispatcher.

**Extends**: C-08 ShiftStateMachine (Depositing state now consumes the planner's trip list instead of the U-10 single-trip stub; multiple `IntentDepositAtChest` and `IntentDepositInShippingBin` intents per shift), M-12 ShiftOrchestrator (dispatches the new intents; routes overflow / missing-chest / unassigned cases through MailDispatcher), `manifest.json` (adds **MFM** as a required Dependency per V9 decision in [design-verification-notes.md](design-verification-notes.md)), `i18n/default.json` (mail letter bodies, sender label `"Your farmhand"`).

**Code organization**:
- `Dayswork.Core/Inventory/DepositPlanner.cs`, `IDepositPlanner.cs`
- `Dayswork/Integration/MailDispatcher.cs`
- i18n keys: `mail.sender`, `mail.overflow.chest_full`, `mail.overflow.chest_missing`, `mail.overflow.no_chest_assigned`, `mail.warning.tool_missing`

**Test files**:
- DepositPlanner PBT: every buffered item ends up in exactly one trip's destination set (conservation)
- DepositPlanner PBT: trip count = unique destination count
- Unit test for ordering optimization heuristic (not a hard property — sanity check)

**Stories implemented**: S-04 (the orphaned-chest Gherkin case fully fires: orphan → mail), S-10 (full multi-trip deposit + 8pm-cap-still-deposits + chest-full fallback + chest-destroyed fallback + refund at exit), S-11 (full overflow-mail flow + shipping-bin-no-overflow case).

**Definition of Done**: A multi-task shift where the player assigned three different chests results in three deposit trips minimizing walking distance. Filling one chest mid-deposit correctly mails the remainder. Destroying a chest mid-shift correctly mails everything destined for it.

---

#### U-15 — Recurring Lifecycle + Calendar Handlers

**Purpose**: Promote the U-10 one-time scheduler stub into the full daily lifecycle. Add festival/rain/sleep-stop handling. After this unit, a recurring contract just *runs* day after day.

**Components owned**: M-14 CalendarHandlers.

**Extends**: M-13 RecurringContractScheduler (full — daily deposit deduction; festival skip; can't-afford → cannot-afford mail), M-12 ShiftOrchestrator (adds sleep-stop settlement entry point), `i18n/default.json` (festival-skip log message, cannot-afford mail body).

**Code organization**:
- `Dayswork/Orchestration/CalendarHandlers.cs` (subscribes to `GameLoop.Saving` and exposes `IsFestivalToday()` / `IsRainyToday()` to other services)

**Test files**: Largely play-tested — these are SMAPI-event-driven flows. Whatever calendar predicates can be reduced to Core helpers should be PBT-tested in `Dayswork.Core/`.

**Stories implemented**: S-12 (recurring behavioral parts: deposit-deduction-each-morning + cancel-after-6am-blocked + can't-afford-mail), S-14 (festivals + rain rate-exclusion + empty-zone full-refund), S-15 (sleep stops the worker and atomically applies refund/mail to today's state).

**Definition of Done**: Save a recurring contract on day 1; play 7 in-game days including one festival and one rainy day. The mod handles each correctly: sends same-day festival/cannot-afford notices, charges no Water Crops surcharge on rain days, fully refunds the empty-zone day, and stops/settles the worker cleanly when the farmer sleeps before work is done.

---

#### U-16 — Animals & Buildings

**Purpose**: Close TODO-05 by making selected buildings real work areas instead of selection placeholders. The worker can enter barns/coops/greenhouse/building interiors, perform supported indoor tile tasks, and execute the three animal tasks.

**Components owned**: BuildingWorkNavigator (Mod, post-design addition), IndoorWorkScanner (Mod, post-design addition), AnimalTaskHandler (Mod, post-design addition).

**Extends**: M-12 ShiftOrchestrator (building-door warp navigation, indoor location context, animal task detection/invocation), WorkerMovementDriver (door approach + warp handoff), C-07 TaskPriorityOrderer (animal priority slots become executable), M-20 ChestResolver / C-11 DepositPlanner integration (animal-product and greenhouse/building output routing), `i18n/default.json` (animal/building logs or mail/body lines if needed).

**Code organization**:
- `Dayswork/Orchestration/BuildingWorkNavigator.cs`
- `Dayswork/Orchestration/IndoorWorkScanner.cs`
- `Dayswork/Orchestration/AnimalTaskHandler.cs`

**Test files**: Prefer Core/unit tests for any pure mapping or planning helpers (for example building-work ordering and animal-product routing invariants). SMAPI/Stardew animal interaction and building-warp behavior are primarily play-tested.

**Stories implemented**: S-08 (completes animal task execution in priority order), S-03/S-04 deepening (selected buildings now produce real work, and building-interior output routing is exercised), plus FR-WORK-09 and FR-TASK-03/04 runtime behavior.

**Definition of Done**: A selected barn/coop contract feeds animals, pets animals, collects animal products to the configured destination, and exits without losing items. A selected greenhouse/building interior runs supported tile tasks. The worker reaches building doors, transitions inside, resumes pathing indoors, and handles missing/invalid interiors gracefully.

---

#### U-17 — GMCM + i18n Lint + Polish

**Purpose**: Final unit. Expose every configurable value to GMCM so the player can tune the mod; run the hardcoded-string lint pass to confirm S-20 actually holds.

**Components owned**: M-17 GMCMRegistrar.

**Extends**: `manifest.json` (adds GMCM as an `OptionalDependencies` entry — present-but-optional probe via `Helper.ModRegistry.GetApi`), `i18n/default.json` (GMCM labels and tooltips for every config field).

**Code organization**:
- `Dayswork/Integration/GMCMRegistrar.cs`

**Test files**: Add the i18n lint test in `Dayswork.Tests/Lint/` — a Roslyn-based code-search that scans the `Dayswork` assembly for hardcoded user-visible strings outside of `I18nHelper` callsites. Recommended Build-and-Test gate per [components.md](components.md) M-21 notes.

**Stories implemented**: S-13 (full GMCM exposure of all spec-listed configurable values with validators + i18n labels + state-Gherkin "today's deposit uses R1, tomorrow's uses R2"), S-20 (full — i18n lint test passes and proves no hardcoded user-visible strings remain).

**Definition of Done**: GMCM section appears when GMCM is installed; every configurable value is editable with proper validation; lint test passes against the full assembly; `i18n/default.json` is the sole source of truth for user-visible text.

---

## Component ownership matrix

35 components total. Each appears exactly once in the "Owned by" column. The "Extended by" column lists later units that meaningfully add to that component without owning its file.

| Component | Owned by | Extended by |
|---|---|---|
| C-01 RateCalculator | U-05 | — |
| C-02 DepositCalculator | U-05 | — |
| C-03 RefundCalculator | U-05 | — |
| C-04 HoursEstimator | U-05 | — |
| C-05 ZoneGeometry | U-04 | — |
| C-06 CapabilityEvaluator | U-07 | — |
| C-07 TaskPriorityOrderer | U-07 | — |
| C-08 ShiftStateMachine | U-10 | U-13, U-14 |
| C-09 StuckDetector | U-13 | — |
| ObjectTargetClassifier (Mod, post-design addition) | U-13 | — |
| WorkerTool map (Core, post-design addition) | U-13B | — |
| FarmhandWorker (Farmer, supersedes M-09 NPC, post-design addition) | U-13B | — |
| WorkerMovementDriver (supersedes M-11, post-design addition) | U-13B | — |
| WorkerRenderer (post-design addition) | U-13B | — |
| WorkerAppearanceRandomizer (post-design addition) | U-13B | — |
| C-10 ItemBuffer | U-10 | — |
| C-11 DepositPlanner | U-14 | — |
| C-12 ContractStore | U-06 | U-12 (Pause/Resume methods) |
| C-13 SaveDataSerializer | U-06 | — |
| C-14 ConfigSnapshot | U-03 | — |
| C-15 ConfigDefaults | U-03 | — |
| M-01 ModEntry | U-01 | U-08, U-09, U-10, U-11, U-13, U-14, U-15, U-16, U-17 (composition root grows each unit) |
| M-02 BulletinBoardPatch | U-08 | U-12 (contract-list rendering) |
| M-03 HiringFlowCoordinator | U-09 | U-11, U-12 |
| M-04 TaskSelectionMenu | U-09 | — |
| M-05 ZoneAndChestMenu | U-11 | — |
| M-06 ScheduleMenu | U-12 | — |
| M-07 SummaryMenu | U-09 | — |
| M-08 ZoneDrawOverlay | U-11 | — |
| M-09 FarmhandNpc | U-10 | U-13 (invulnerability via hit-detection), U-13B (re-founded on Farmer) |
| M-10 ToolSwapAnimator | U-13B | — |
| M-11 PathFindControllerAdapter | U-10 | U-13 (real walking), U-13B (superseded by WorkerMovementDriver) |
| M-12 ShiftOrchestrator | U-10 | U-13, U-13B, U-14, U-15, U-16 |
| M-13 RecurringContractScheduler | U-10 (stub) | U-15 (full lifecycle) |
| M-14 CalendarHandlers | U-15 | — |
| M-15 ContractPersistenceAdapter | U-09 | — |
| M-16 MailDispatcher | U-14 | — |
| M-17 GMCMRegistrar | U-17 | — |
| M-18 MultiplayerGuard | U-08 | — |
| M-19 ToolLevelReader | U-10 | — |
| M-20 ChestResolver | U-11 | — |
| M-21 I18nHelper | U-08 | — |
| BuildingWorkNavigator (Mod, post-design addition) | U-16 | — |
| IndoorWorkScanner (Mod, post-design addition) | U-16 | — |
| AnimalTaskHandler (Mod, post-design addition) | U-16 | — |

**Verification**: 35 original design components, each owned by exactly one unit. ✓ Plus 9 post-design implementation components introduced by the FD-Q5=B full-Farmer decision, the U-13/U-13B split, and the U-16 Animals & Buildings insertion (ObjectTargetClassifier, WorkerTool map, FarmhandWorker, WorkerMovementDriver, WorkerRenderer, WorkerAppearanceRandomizer, BuildingWorkNavigator, IndoorWorkScanner, AnimalTaskHandler), each owned by exactly one unit.

---

## Validation checks

- **Every story covered**: see [unit-of-work-story-map.md](unit-of-work-story-map.md) for the 20-story coverage matrix. ✓
- **Every component owned exactly once**: see the matrix above. ✓
- **No forward dependencies**: see [unit-of-work-dependency.md](unit-of-work-dependency.md). Every unit's dependencies point to earlier units only. ✓
- **Foundation-first respected (U1 = C, U4 = C)**: U-01 through U-07 are foundation; U-08 through U-10 form the thin end-to-end vertical slice; U-11 through U-17 deepen the slice. ✓
- **Test infra early (U3 = B)**: U-02 ships immediately after the project scaffold; all later units add tests to the established framework rather than re-bootstrapping. ✓
- **Scaffold first (U5 = A)**: U-01 ships a loadable empty mod before any other work. ✓
- **Granularity ~16 (U2 = B)**: 17 units after the playtest-driven Animals & Buildings insertion. ✓

---

## What each unit's Construction loop will look like

Per the approved execution plan in [execution-plan.md](../plans/execution-plan.md), each unit goes through:

1. **Functional Design** (Construction stage) — only if the unit introduces new business logic. Likely EXECUTE for U-03..U-07, U-10, U-13, U-14, U-15, U-16; likely SKIP for U-01, U-02, U-08, U-09, U-11, U-12, U-17 (where the design is largely UI-rendering or SMAPI-event wiring with little new logic).
2. **NFR Requirements** — pull the relevant NFRs from [requirements.md](../requirements/requirements.md) per unit (e.g., U-05's NFR-SAFE-02 integer rounding; U-10's NFR-SAFE-01 no-items-lost; U-14's NFR-SAFE-01 + NFR-SAFE-03 graceful chest-missing fallback; U-16's building/animal pathing safety; U-17's NFR-MAINT-02 i18n).
3. **NFR Design** — design patterns for those NFRs.
4. **Infrastructure Design** — SKIPPED for every unit (no cloud/IaC; SMAPI is the platform).
5. **Code Generation** — always EXECUTE. Plan-and-approve cycle per unit.

Each unit's approval gate hands off to the next unit.
