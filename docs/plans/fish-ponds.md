# Plan — Manage Fish Ponds

**Status:** implemented 2026-06-23 (Core, persistence, pricing, shift plan, execution, and UI built;
unit-tested; builds clean). **In-game smoke pass PASSED 2026-07-07 — release-ready.**
**Game-content reference:** [`docs/machines.md`](../machines.md) → "Fish ponds" (verified `FishPond`
API for SDV 1.6.15).

## Why a separate subsystem (not folded into Manage Machines)

A fish pond shares only the *collect → route to a destination* half of Manage Machines, and almost
nothing else (decided with the user, 2026-06-23):

| | Machines (`Data/Machines` objects) | Fish ponds (`Building`s) |
|---|---|---|
| Lives in | `location.objects` | `location.buildings` |
| Identity | `(loc, tile, qualifiedItemId)` | `(loc, tileX/tileY)` — **no item id** |
| Ready check | `readyForHarvest` + `heldObject` | `output.Value != null` |
| Collect | `checkForAction` on an `Object` (needs a guarded fake `Farmer`) | take `output.Value`, null it (direct field, **no fake Farmer**) |
| Reload / input | the whole `MachineReader` + `MachineInputPlanner` + fetch trip + carry buffer | **none** — player stocks the fish |

`MachineReader` is built entirely on `Object.GetMachineData()` / `location.objects`; a pond can't be
resolved through it. So fish ponds are a **parallel collect-only subsystem** mirroring the existing
parallel-scope pattern (`ManagedCropWorkScope`, `MachineWorkScope`) — not a machine special case.

## Decisions (locked)

1. **Own hub spoke** ("Manage Fish Ponds") — the machine group editor's input-chest/type/companion
   flow is input-centric and would be empty/confusing for ponds. The pond page is just
   *{select ponds on map, pick one output destination}*.
2. **Own `TaskCategory.FishPonds`** (5th, reorderable) — consistent with the parallel-scope design.
3. **Collect-only.** The player stocks fish and supplies any capacity-quest item (both out of scope).
   No input/reload/fetch machinery.
4. **One output destination for the whole scope** (not per-pond). `FishPondWorkScope(Ponds, OutputDestination)`.
5. **Energy:** collect = **1** per pond visit (`WorkActionKind.CollectFishPond`), mirroring the machine
   collect charge. Gate-only pricing — no surcharge; energy is the cost (same as machines).
6. **Direct-field collect.** Capture `pond.output.Value`, null it, credit the deposit buffer — avoids
   the player-inventory/HUD/xp side effects of `FishPond.doAction`, so **no action guard is needed**
   (simpler than machine collect). Duplication-safe: credit only after the field is nulled.

## What was built (file map)

**Core (`Dayswork.Core`, SMAPI-free):**
- `FishPonds/FishPondRef.cs`, `FishPondWorkScope.cs`, `FishPondOutputRouter.cs`,
  `FishPondWorkScopeSerialization.cs`.
- `OutputScopeFamily.FishPond` + `OutputScopeProvenance.FishPond()`; `TaskCategory.FishPonds` +
  `DefaultCategoryPriority`; `WorkActionKind.CollectFishPond` + cost in `ConfigDefaults`.
- `WorkScopeSet.FishPonds`; `Contract.FishPondScope` (+ back-compat overload); `IntentPerformFishPondAction`.
- `ShiftPlanBuilder.BuildFishPondBatches` → one `BatchKind.FishPonds` batch per location with ≥1 pond.

**Persistence:** `FishPondWorkScopeDtoV1`/`FishPondRefDtoV1`, optional field on `ContractDtoV2`
(no schema bump — same back-compat pattern as `MachineWorkScope`; old saves load with an empty scope),
`SaveDataSerializer` both directions.

**Pricing:** `WorkScopeClassifier.Classify` + `ContractTermsBuilder.BuildPreview` take a
`FishPondWorkScope?`; chargeable when the scope is enabled; gate-only (no surcharge).

**Execution (`Dayswork`):** `FishPondReader.cs` (enumerate/resolve by tile/`HasOutput`/perimeter stand
tiles), `ShiftOrchestrator.FishPonds.cs` (collect pass), session state on `ShiftSession`, batch
dispatch in `ShiftOrchestrator.cs`, nav-arrival + nav-failure handling in `ShiftOrchestrator.Movement.cs`,
deposit destination merge in `ShiftOrchestrator.Deposit.cs`.

**UI (`Dayswork/UI`):** `FishPondPlanDraft.cs`, `ManageFishPondsMenu.cs`, `FishPondMapLocation.cs`,
fish-pond selection mode in `ZoneDrawMenu.cs` (click/drag toggles a whole pond by footprint;
full-footprint highlight), `HubMenu` entry, coordinator wiring (`HiringFlowCoordinator`), i18n keys,
`SummaryMenu`/`TaskPriorityMenu` category label.

**Tests:** `Dayswork.Tests/FishPonds/FishPondScopeTests.cs` (scope normalization, output router,
batch-plan), plus fish-pond round-trip/migration/malformed-skip in `SaveDataSerializerTests` and
pricing in `ContractTermsBuilderTests`.

## Output routing nuance (differs from machines)

`FishPondOutputRouter.BuildDestinationMap` **always** maps the pond provenance — including the
Automatic case (→ `AutomaticOutputDestination`). `MachineOutputRouter` omits Automatic and lets the
deposit planner fall back to the buffer's per-task destination; for ponds that fallback could
mis-route output to an unrelated task's chest (the buffer uses `TaskKind.HarvestCrops` as a nominal
tag). Always-mapping the provenance closes that hole.

## Flavored roe is preserved (2026-06-23 enhancement)

Pond roe is usually a `ColoredObject` carrying a `PreserveId` (fish flavor) + color, whose identity
and sell price (Sturgeon Roe ≫ plain Roe) can't be reconstructed from `(O)812` alone. The deposit
pipeline now preserves it: a per-shift `FlavorItemRegistry` (`Dayswork/Orchestration/`) captures the
real item at collect time under an opaque `FlavorId`; the pure buffer/planner carry only that token
(`BufferedItem`/`RoutedItemStack`/`ItemStack.FlavorId`, consolidated so distinct flavors don't merge);
the three deposit reconstruction sites (chest, shipping bin, automatic overflow) clone the captured
template (`getOne()` copies preserve/color/price) instead of `ItemRegistry.Create`. **This also fixes
flavored machine output** (blueberry wine, aged roe, flavored honey). Plain items are unaffected
(null token → ordinary id path).

**Quality** is a first-class field through the whole chain (planner consolidation key →
`RoutedItemStack.Quality` → rebuild). `getOne()` does *not* copy quality, so it is re-applied per stack
at rebuild from the captured value. Both collect sites now capture it: `CollectFishPond` reads
`output.Quality`; `CollectMachine` reads the collected item's quality (so **cask-aged silver/gold/
iridium wine & cheese keep their star** — a pre-existing gap closed 2026-06-23).

## v1 limitations / open verification — all verified in the 2026-07-07 smoke pass ✓

- **Confirm direct-null collect in-world** — that nulling `pond.output.Value` cleanly clears the
  visible output bucket sprite and the produce ends up deposited (no dupe, no leftover bucket).
- **Perimeter stand-tile selection** around the 5×5 footprint (water tiles are impassable) — confirm
  the worker reaches a pond and faces it plausibly.
- **Expansion ponds** — confirm a pond on an SVE/expansion outdoor map is selected, serviced, and the
  worker returns to the farm (the batch-exit travel mirrors the machine path but is untested for ponds).
- **Flavored roe deposits intact** — collect from a stocked pond (e.g. Sturgeon) and confirm the chest
  receives correctly-named, correctly-priced **Sturgeon Roe** (not generic Roe), via both the chest
  path and the automatic-overflow path (assign an Automatic destination / fill the chest).
- **Cask quality intact** — collect a silver/gold/iridium cask wine via Manage Machines and confirm the
  deposited wine keeps both its flavor (e.g. Blueberry Wine) *and* its quality star.

## Smoke pass

Enable `DevLog.Enabled`, build ≥1 fish pond, stock it, let it produce, hire with a fish-pond scope +
an output chest, run a shift, and watch the `[Dayswork][fishponds]` logs + `dayswork_end_shift`.

## Follow-ups (separate work)

- Casks (cellar) — the remaining machine-family phase (see `docs/machines.md`).
