# Plan — Manage Machines

**Status:** implemented 2026-06-19 (milestones 1–7 built, unit-tested, builds clean);
**milestone 8 (in-game smoke pass) is pending** and must be run before release.
**Game-content reference:** [`docs/machines.md`](../machines.md) (verified `Data/Machines`
schema + API for SDV 1.6.15).

## Group editor redesign (2026-06-19)

The per-group editor was reworked into a **type-first, gated** flow (still a hub editor, not a wizard):

1. **Reload-empty-machines** toggle retained (hidden/forced off for input-less types).
2. **Pick a machine type** first (Furnace, Bee House, …) — `MachineGroup.MachineType` (qualified id);
   a group is now **single-type**. Persisted on `MachineGroupDtoV1` (no migration — feature unreleased).
3. **Select machines on the map**, restricted to that type (`ZoneDrawMenu` `machineTypeFilter`).
4. **Inputs** come from the *type's* accepted-input list (data-derived via
   `MachineReader.EnumerateAcceptedInputs`, **not** chest contents), with "Any &lt;category&gt;" bulk
   shortcuts and **required companions (coal) shown auto-selected + locked**
   (`MachineReader.EnumerateRequiredCompanions`; the load engine already consumes them).
5. **Input chest** (decoupled from the filter; the farmhand skips it at runtime if the material is absent).
6. **Output destination** (unchanged).

Changing the type clears the group's machines + input filter (both are type-scoped). See
`docs/machines.md` → "Enumerating a machine type's accepted inputs". The original chest-derived input
flow described under "UI flow" below is superseded by the above.

## Implementation status (2026-06-19)

Built and unit-tested (Core/persistence/pricing covered by xUnit; the shift engine + UI are
build-verified and await the in-game smoke pass per AGENTS.md):

- **M1** `Dayswork/Orchestration/MachineReader.cs` — enumerate/resolve/classify + probe-built load
  candidates. API verified by compiling against the live game DLLs (see `docs/machines.md` → "Reader
  implementation").
- **M2** `Dayswork.Core/Machines/` — `MachineRef`, `MachineInputFilter`, `MachineGroupMode`,
  `MachineGroup`, `MachineWorkScope`, `RecipeRequirement`/`AdditionalInput`/`MachineLoadCandidate`,
  `MachineLoadPlan`, `MachineInputPlanner`, `MachineActionKind`, `MachineOutputRouter`. Energy
  (`WorkActionKind.CollectMachine`/`LoadMachine` = 1 each in `ConfigDefaults`) + `TaskCategory.Machines`
  (4th in `DefaultCategoryPriority`). Planner + filter unit tests.
- **M3** DTOs (`MachineWorkScopeDtoV1`/`MachineGroupDtoV1`/`MachineRefDtoV1`/`MachineInputFilterDtoV1`)
  + `MachineWorkScopeSerialization`; carried on `Contract.MachineScope`. **No schema bump** — an
  optional field on `ContractDtoV2` (the CropPlan precedent), so old v3 saves load with an empty
  scope. Round-trip + missing→empty migration + malformed-skip tests.
- **M4** `ContractTermsBuilder` gates chargeability on `MachineWorkScope.IsEnabled` (no surcharge —
  energy is the cost; decided with the user). `ContractValidationCode.MachineGroupNeedsInputChest`
  (informational). Pricing tests.
- **M5/M6** Hub spoke → `ManageMachinesMenu` → `MachineGroupEditorMenu` (mode/input-chest/input-filter/
  output pickers) + `MachinePlanDraft`/`MachineGroupDraft`; map selection extends `ZoneDrawMenu`
  (click-toggle, drag-select, location switcher, cross-group exclusivity). Coordinator wiring + live
  reprice.
- **M7** `ShiftOrchestrator.Machines.cs` + `BatchKind.Machines` emit + session state. Group-major
  collect → fetch → load passes; per-group output routed via `OutputScopeProvenance.Machine`;
  carried inputs settled back to chest/overflow on stop (items never lost).

**v1 limitations / open verification (do in M8):**
- **Input chest must be in the same location as the machines.** A cross-location input chest makes
  that group collect-only for that location (dev log) — inputs are never touched/lost. Full
  cross-location fetch trips are a follow-up (see `ManagedShoppingCoordinator` for the pattern).
- **Collect via `checkForAction`** credits the buffer only if the machine actually released its
  output (duplication-safe). Confirm in-world that a fake worker `Farmer` cleanly collects (the
  open question flagged below) — `dayswork_debug_machines` lists machine state to verify.
- **Load via `PlaceInMachine(probe:false)`** with the carry buffer populated on a fake `Farmer`;
  confirm fish-smoker (fish+coal) / dehydrator (×5) actually load in-world.
- Sleep-settle of *collected machine output* still in the buffer routes by the buffer's nominal task
  tag (provenance is honored on the normal deposit path) — same minor imperfection managed crops
  have; items are never lost. Acceptable for v1.

**Smoke pass:** enable `DevLog.Enabled`, build a machine group, run a shift, and use
`dayswork_debug_machines` (current location) + `dayswork_end_shift` to inspect collect/reload.

The farmhand learns to operate **machines** — placed objects that turn input into output over time
(mayonnaise machine, keg, preserves jar, bee house, fish smoker, dehydrator, furnace, tapper, …).
"Machine" is the game's own term (`Data/Machines`, `MachineData`). Each shift the worker **visits**
each selected machine to **collect** finished output and **reload** empty machines with input fetched
— physically — from a chest.

## Design decisions (locked)

1. **Visit model, not tool model.** Machine work = a per-machine *visit* doing up to two things:
   **collect** ready output, then **reload** if empty. Paced like animal care (interaction beats, no
   tool swing), gated by machine ready-state, inherently async across days. Collect-before-reload in
   one visit falls out naturally (never collect what you just loaded).
2. **Per-instance selection, granular.** A machine is selected by `(location, tile, expectedMachineId)`
   and re-resolved at shift start; if it moved/vanished/changed type, skip it (dev log) — same as
   managed-crop missing-tile skips. Cost: rearranging a machine means re-selecting it. Accepted.
3. **Map selection supports click AND drag-select**, extending `ZoneDrawMenu`, with a **location
   switcher** so one selection session can span farm + sheds + greenhouse.
4. **Groups = config dimension; batches = location dimension (orthogonal).** A *group* bundles
   `{selected machines, input filter, input chest, output destination, mode}`. A machine belongs to
   exactly one group. At plan time machines are bucketed **by location** into batches; each machine's
   config is looked up from its owning group. A group may span multiple locations.
5. **Input is a physical fetch trip** (NOT the abstract-pool shortcut crops use). The worker walks to
   the group's input chest, withdraws needed inputs into a carry buffer, walks to the machines, loads
   from the buffer. This makes input/output **symmetric** (both physical), honoring the mod's
   "the farmhand walks for everything" philosophy. *(Crops' abstract seed-consumption is a known
   pre-existing shortcut; retrofitting it is out of scope here — see Follow-ups.)*
6. **One input chest per group.** Player-chosen, any location.
7. **Per-group mode: collect+reload (default) or collect-only.** No-input machines (bee houses,
   tappers, crystalariums, `AllowLoadWhenFull`) are inherently collect-only and ignore the toggle.
8. **Machines is a first-class reorderable `TaskCategory`** (4th), alongside AnimalCare/Crops/Fieldwork.
9. **Energy (integer model):** collect = **1**, load = **1**, per machine-interaction (not per item;
   a 5-fruit dehydrator load is one charge). Withdrawals/walking are unpriced logistics, like
   deposits. Values live in `ConfigDefaults` as tunables.

## Scope

**v1 (this plan):** `Data/Machines` objects in locations the worker already travels to (farm, sheds,
greenhouse, animal buildings); collect + reload; input filter (specific ids or "any"); one input
chest + one output destination + one mode per group; new Machines category + pricing.

**v1.x (next, separate plans):**
- **Casks** (cellar) — quality-aging "ready" semantics + cellar travel. Verify cask state model first.
- **Fish ponds** — collect-only building output; separate executor.

**Explicit non-goals (v1):** auto-buying machine inputs; incubators (`IsIncubator`); crystalarium
reloading; reload-only mode; carry-capacity caps (worker carries unbounded, like the shopping trip);
moving/placing machines.

## Architecture fit (reuse map)

| Need | Reuse / mirror |
|---|---|
| Collect output safely | `ItemBuffer` → `DepositPlanner` → `DepositTripRunner` → overflow. Unchanged. |
| Physical input fetch | mirror `ManagedShoppingCoordinator` (it is already a *walk → acquire into `_carriedItems` → walk → settle* trip; swap the store counter for a chest withdraw). |
| Cross-location travel | `Travel.cs` + `BuildingWorkNavigator` door entry/exit; expansion hops via `ExpansionCompat`. |
| Map selection | extend `ZoneDrawMenu` (it already has single-tile building toggle + drag + a location target). |
| Worker-action guarding | `InvokeTaskActionGuarded` / `CreateWorkerActionFarmer` (machine load/collect leak into `Game1.player`, exactly like crop/tool actions). |
| Scope persistence | new `MachineWorkScope` parallel to `ManagedCropWorkScope`; new `BatchKind.Machines` emitted by `ShiftPlanBuilder`. |
| Output routing | existing `DestinationKey` (`ChestDestination` / `ShippingBinDestination`). |
| Chest mutex / stand tiles | `chest.GetMutex().IsLocked()` guard + `TrySelectChestDepositStandTile` / `DepositStandTilesAround`. |

## Core domain model (`Dayswork.Core`, SMAPI-free)

New types under `Dayswork.Core/Machines/`:

- `MachineRef(string LocationName, TileCoord Tile, string ExpectedQualifiedId)` — one selected
  machine. Re-resolved at shift start.
- `MachineInputFilter` — either `Any` or an ordered set of allowed qualified ids. (The machine's
  own rules still decide ultimate acceptance; the filter only narrows.)
- `MachineGroupMode { CollectAndReload, CollectOnly }`.
- `MachineGroup(string Id, IReadOnlyList<MachineRef> Machines, MachineInputFilter InputFilter,
  ChestRef? InputChest, DestinationKey OutputDestination, MachineGroupMode Mode)`.
- `MachineWorkScope(IReadOnlyList<MachineGroup> Groups)` with `IsEnabled => Groups.Any(...)`.
- **`MachineInputPlanner`** (pure, the meatiest new logic): given a group's machines that need
  loading + their per-machine recipe requirements (input id/tags/count + additional consumed items)
  + the input chest contents + the filter, produce a **withdrawal list** (what to pull from the chest)
  and **per-machine load assignments** (which item, how many, at which machine), clamped to available
  supply. Handles "any"-tag matching, `RequiredCount`, atomic multi-item recipes (fish+coal). This is
  the analog of `ShiftSupplyAggregator`; unit-tested in `Dayswork.Tests`.
  - *Note:* recipe matching that needs live game data (context tags, `MachineDataUtility` checks) is
    resolved in `Dayswork/` and passed into the planner as already-computed `RecipeRequirement`
    snapshots, keeping Core SMAPI-free (reader → planner → executor, per AGENTS.md Core-placement rule).
  - *Naming note:* `Dayswork.Core.Machines` lives alongside the game's `StardewValley.GameData.Machines`;
    alias in `Dayswork/` files that reference both.

Energy: add `WorkActionKind.CollectMachine` and `WorkActionKind.LoadMachine`; add both to the cost
dictionary in `ConfigDefaults` (`= 1` each). Add `TaskCategory.Machines` and include it in
`TaskKindSets.DefaultCategoryPriority`.

## Persistence

- New DTO `MachineWorkScopeDtoV1` (+ `MachineGroupDtoV1`, `MachineRefDtoV1`,
  `MachineInputFilterDtoV1`) under `Dayswork.Core/Persistence/Dto/`.
- Carry it on the contract. Bump the save schema: `DaysworkSaveDataV2` → `V3` (or add an optional
  field to the contract DTO if the serializer treats new optional fields as back-compatible — confirm
  against `SaveDataSerializer` v1→v2 precedent). Old saves load with an empty machine scope.
- **Tests required** (AGENTS.md): round-trip serialization + a v(old)→v(new) migration test that a
  machine-less contract upgrades cleanly. Reuse the `ContractDtoV2` migration test shape.

## UI flow (`Dayswork/UI`)

Mirror the Manage Crops hub spoke:

1. **`HubMenu`** gains a **Manage Machines** entry.
2. **`ManageMachinesMenu`** — lists machine groups (Add / Edit / Delete), like `ManageCropsMenu`.
   Each row shows: machine count, input summary ("Any" / item names), input chest, output, mode.
3. **`MachineGroupEditorMenu`** — per group: pick input filter (specific ids or "Any"), input chest
   (chest picker, reuse `ChestResolver`/`ChestEntry`), output destination (reuse
   `OutputDestinationsMenu` building blocks), mode toggle, and a **Select machines on map** button.
4. **Map selection** — extend `ZoneDrawMenu` into a machine-selection mode:
   - **Location switcher** control (the new piece): cycle the displayed map among candidate locations
     (farm + each shed/greenhouse/animal-building interior that contains ≥1 machine). It already
     swaps `Game1.currentLocation` + recenters; generalize `ResolveDrawLocation` + add a "viewing: X ▸"
     button that re-inits the view for a new target without leaving the session. Selections accumulate
     across locations into the group.
   - **Single-click toggles** the machine under the tile (reuse the building-toggle path, but resolve
     a machine object at the tile instead of a `BuildingOutline`).
   - **Drag-select** adds every machine in the rectangle. To scope which machines, the editor can
     pre-select a machine **type** (optional) so a drag grabs only that type; with no type chosen,
     a drag grabs all machines.
   - **Exclusivity:** machines already claimed by *other* groups render protected/unavailable
     (reuse the `ProtectedZones` concept, applied to machine tiles).
5. **Draft type** `MachinePlanDraft` / `MachineGroupDraft` (transient, mutable), projecting into the
   persisted `MachineWorkScope` on confirm — mirror `CropPlanDraft`/`CropGroupDraft`.
6. **Coordinator** (`HiringFlowCoordinator`) wires the new spoke + back-navigation, and the live
   re-price (`ContractTermsBuilder.BuildPreview`) must reflect a non-empty machine scope.

## Pricing (`ContractTermsBuilder`)

Add Machines as a chargeable service: enabled when `MachineWorkScope.IsEnabled`. Validation: a group
needs ≥1 machine and (if any selected machine has input + mode is reload) an input chest, else flag a
`ContractValidationCode`. Price model: a flat per-shift machine surcharge when the scope is non-empty
(simplest, consistent with how scope×task gates pricing today); machine-count scaling is a later
tuning lever. Add a `ServiceContributionRow` state for "needs input chest." **Tests required**
(pricing/money math).

## Execution model (`Dayswork/Orchestration`)

New partial `ShiftOrchestrator.Machines.cs` + a session-held `MachineTripRunner`, with per-shift
state on `ShiftSession` (machine batch queue, input carry buffer, current group/machine cursor).

**Batch planning** (`ShiftPlanBuilder`): flatten all groups' machines, bucket by location, emit one
`BatchKind.Machines` batch per location with ≥1 selected machine, slotted under the Machines category
in `categoryPriority` order. (Machines carry their refs out-of-band like managed crops carry
`CropZoneAssignment`s — `Tasks` is empty.)

**Per machine batch (group-major within the location):**
For each group that has machines in this location:
1. **Collect pass** — walk machine→machine; at each `readyForHarvest` machine, collect output into the
   output `ItemBuffer` (guarded), spend `CollectMachine` energy. (No input needed — runs for
   collect-only groups too.)
2. **Reload pass** (skipped for collect-only / no-input groups):
   a. Compute load needs for this group's empty machines in this location (reader builds
      `RecipeRequirement`s via `MachineDataUtility`; `MachineInputPlanner` produces withdrawal +
      assignments against the input chest, mutex-checked).
   b. **Fetch trip** — `MachineTripRunner` walks to the input chest (any location, via Travel +
      building doors), withdraws the planned inputs into the carry buffer (mutex re-check per stack),
      walks back to the machine area.
   c. **Load pass** — walk machine→machine; at each empty machine, if the carry buffer holds the
      assigned input(s) (and `HasAdditionalRequirements` is satisfied — **atomic**), load via
      `PlaceInMachine`/`AttemptAutoLoad` (probe-then-commit to honor the filter), consume from the
      carry buffer, spend `LoadMachine` energy.

**Stop/settle:** energy exhaustion, 8pm cap, cancel, and sleep route through the existing Depositing
path. **Leftover carried inputs** must settle safely on stop — return them to the input chest, then
overflow (office Output chest → shipping bin). Reuse `SettleCarriedItems`-style logic; **items are
never lost** (hard rule 4) applies to *inputs* now, not just collected output.

**Partial supply:** fetch what's there, load until the carry buffer runs dry, skip the rest with a
HUD note (mirror the crop seed-shortfall path).

**Off-screen:** collection/reload are direct state mutations + machine timers advance on day update,
so no special off-screen animation handling (contrast with `Debris` tree-fall pumping).

## Milestones / task order

1. **Recipe reader + verify live data** — `Dayswork/` reader over `Data/Machines`
   (`GetMachineData`, trigger requirements, `AdditionalConsumedItems`); confirm fish-smoker/dehydrator
   per-entry recipes against live data. Append findings to `docs/machines.md`.
2. **Core model + planner + tests** — `Machines/` types, `MachineInputPlanner`, energy/category
   additions; unit tests for the planner (any-tag, counts, atomic multi-input, supply clamp).
3. **Persistence + migration + tests** — DTOs, schema bump, round-trip + migration tests.
4. **Pricing + validation + tests** — `ContractTermsBuilder` service line + validation codes.
5. **UI** — Manage Machines hub spoke, group list/editor menus, drafts, chest/output/mode pickers.
6. **Map selection** — `ZoneDrawMenu` machine mode: machine toggle, drag-by-type, location
   switcher, cross-group exclusivity.
7. **Execution** — `ShiftOrchestrator.Machines.cs`, `MachineTripRunner`, batch emit in
   `ShiftPlanBuilder`, session state, collect/fetch/load passes, settle-on-stop, HUD notices.
8. **In-game smoke pass** (`DevLog.Enabled` + a `dayswork_*` console command) — the shift engine is
   verified by play-testing per AGENTS.md, not ritual unit tests.

## Open verification items (do before/within the relevant milestone)

- Per-entry recipes for fish smoker (fish + coal placement: trigger vs `AdditionalConsumedItems`),
  dehydrator (×5), keg/jar flavoring — read live `Data/Machines`.
- Exact collect path for a worker (does `checkForAction` cleanly hand the held output to the
  fake worker Farmer, and does the `OutputCollected` re-trigger fire without a real player?). Confirm
  via the worker-action guard during milestone 7.
- `AttemptAutoLoad` vs `PlaceInMachine` for filtered loads — pick probe-then-commit if auto-load
  can't be constrained to the filter.
- Save schema bump mechanics (`SaveDataSerializer`) — whether an added optional contract field needs
  a full version bump or is back-compatible.

## Risks

- **Map UI across interiors** is the largest *new* UX surface (location switcher) — contain it inside
  `ZoneDrawMenu` rather than a new menu framework.
- **`MachineInputPlanner` correctness** (atomic multi-input, "any"-tag, partial supply) is the
  biggest correctness risk — hence Core + unit tests.
- **Input never lost** is a new invariant surface (carried inputs on stop). Treat with the same rigor
  as output overflow.
- **Mod machines** (custom `Data/Machines` entries from other mods) should "just work" via the data
  model, but exotic `InteractMethod`/`OutputMethod` machines may need collect-only fallbacks.

## Follow-ups (separate work)

- Retrofit managed-crop seed consumption to physical fetch trips (make crops symmetric with
  machines) — deliberately deferred to avoid bloating this feature.
- Cask phase; fish-pond phase.
- Auto-buy machine inputs; carry-capacity realism cap; per-location input chests.
