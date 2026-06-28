# Verified game-content reference — Machines (SDV 1.6)

> Confirmed by decompiling the installed game with `ilspycmd` against
> **Stardew Valley 1.6.15.24356** (`Stardew Valley.dll` and `StardewValley.GameData.dll`,
> `X:\Steam\steamapps\common\Stardew Valley\`). This file exists so the machine data model and
> runtime API never have to be re-derived from memory (AGENTS.md hard rule 7). When you touch
> machine code, confirm any **per-entry** data (a specific machine's exact recipe) against the live
> `Data/Machines` asset at runtime — only the *schema* and *API surface* below are confirmed here.

"Machine" is the game's own term (`Data/Machines`, `MachineData`) for a placed object that turns
input into output over time — mayonnaise machine, keg, preserves jar, bee house, fish smoker,
dehydrator, furnace, tapper, crystalarium, etc. Dayswork's "Manage Machines" feature has the
farmhand visit each selected machine to collect finished output and reload it with input. A machine
batch (one per location) services its groups **one at a time, as a full collect→reload cycle per
group** — collect a group's ready output, then fetch from that group's input chest and reload its
machines, and only then move on to the next group (not all collects across every group followed by
all reloads). Fish ponds are a separate, later phase (they are **buildings**, not `Data/Machines`
objects — see end).

## What a machine is

A machine is any placed `StardewValley.Object` whose qualified id has an entry in the `Data/Machines`
data asset. Confirmed in `Object.GetMachineData()`:

```csharp
public MachineData GetMachineData()
    => DataLoader.Machines(Game1.content).GetValueOrDefault(base.QualifiedItemId);
```

So **enumerating machines in a location** = iterate `location.objects.Values` (and, for buildings'
interiors, that interior's `objects`) and keep every object where `GetMachineData() != null`.
`DataLoader.Machines(Game1.content)` returns `Dictionary<string, MachineData>` keyed by qualified
item id; it can also be read through SMAPI as `helper.GameContent.Load<Dictionary<string, MachineData>>("Data/Machines")`.

Most machines are big-craftables (`(BC)…`) but the data model keys on qualified id, so don't assume
`bigCraftable.Value` — trust `GetMachineData() != null`.

## Machine runtime state (`StardewValley.Object` net fields)

| Field | Type | Meaning |
|---|---|---|
| `readyForHarvest` | `NetBool` | Output is finished and waiting to be collected. |
| `heldObject` | `NetRef<Object>` | The item currently held — the in-progress or finished output. |
| `lastInputItem` | `NetRef<Item>` | The item most recently loaded (used by some recipes). |
| `MinutesUntilReady` | `int` (property) | In-game minutes left before `readyForHarvest`. Decrements on time/day update. |

State reading for the shift loop:
- **Has finished output to collect** → `readyForHarvest.Value == true` (and `heldObject.Value != null`).
- **Idle / empty / ready to load** → `heldObject.Value == null` (nothing held, not processing).
- **Busy / processing** → `heldObject.Value != null && !readyForHarvest.Value` (`MinutesUntilReady > 0`).

Machines advance on the time/day update regardless of whether the player is present, so async
processing across days needs no special off-screen handling (unlike felled trees).

## `Data/Machines` schema (`StardewValley.GameData.Machines`)

### `MachineData`
- `bool HasInput` / `bool HasOutput` — capability flags (also surfaced as `machine_input` / `machine_output` context tags). `HasInput` is implied when any output rule uses the `ItemPlacedInMachine` trigger.
- `string InteractMethod` — *advanced*; a C# method run when the player interacts and no output is ready. Most machines are null here.
- `List<MachineOutputRule> OutputRules` — the input→output conversion rules (see below).
- `List<MachineItemAdditionalConsumedItems> AdditionalConsumedItems` — **machine-level** extra items that must be present (and are consumed) before *any* output rule runs. One way coal-style secondary inputs are modeled.
- `bool AllowLoadWhenFull` — **the crystalarium case**: a new item can be dropped in before the previous one finishes, **destroying** the previous. Machine reload logic must **never** reload these (it would discard a finished/in-progress gem). Treat `AllowLoadWhenFull == true` as collect-only.
- `bool IsIncubator` — incubators/ostrich incubators in coops/barns. Out of scope for v1 (egg→animal, not item→item).
- `bool OnlyCompleteOvernight`, `string ClearContentsOvernightCondition` — timing/expiry nuances; relevant for HUD accuracy, not core flow.
- Cosmetic/aux: `LoadEffects`, `WorkingEffects`, `InvalidItemMessage`, `InvalidCountMessage`, `ReadyTimeModifiers`, `ExperienceGainOnHarvest`, `StatsToIncrementWhen*`, `CustomFields`.

### `MachineOutputRule`
- `string Id`
- `List<MachineOutputTriggerRule> Triggers` — when this rule applies.
- `List<MachineItemOutput> OutputItem` — candidate outputs (random unless `UseFirstValidOutput`).
- `int MinutesUntilReady` / `int DaysUntilReady` — processing time (days win if both set; instant if neither).
- `bool RecalculateOnCollect` — **the bee-house case**: output is regenerated at collection time (e.g. honey flavored by nearby flowers). The real output id isn't known until collected.

### `MachineOutputTriggerRule` — *this is where input requirements live*
- `MachineOutputTrigger Trigger` (default `ItemPlacedInMachine`).
- `string RequiredItemId` — qualified or unqualified id the input must match (optional).
- `List<string> RequiredTags` — context tags the input must match; **`!tag` negates** (e.g. `!large_egg`). The "any X" concept is native here (e.g. a `fish` tag = "any fish"). (optional)
- `int RequiredCount` — required input stack size, default **1**. This is the dehydrator's "5 fruit" and similar. (optional)
- `string Condition` — a game-state query gating the match. (optional)
- An input must match **all** specified fields; if none are specified the rule always matches.

### `MachineOutputTrigger` (`[Flags]`)
`None=0`, `ItemPlacedInMachine=1`, `OutputCollected=2`, `MachinePutDown=4`, `DayUpdate=8`.
For reload-from-input we care about **`ItemPlacedInMachine`**. `OutputCollected` rules re-trigger off
the previous output without consuming new input (chained outputs). `MachinePutDown` (worm bin) and
`DayUpdate` (input-less producers like the soda machine / bee house) start without a player drop.

### `MachineItemOutput : GenericSpawnItemDataWithCondition`
- `string OutputMethod` — *advanced*; a C# method `(Object machine, GameLocation location, Farmer player, Item? inputItem, bool probe) → Item`. **When set, the output item is computed in code** (mayonnaise, cheese, etc.) and the static `ItemId`/`RandomItemId` fields are ignored. **Consequence:** we cannot always statically preview the output item — but we never need to, because collection takes whatever `heldObject` actually is. Static output info is **UI-preview-only and best-effort**.
- Inherited spawn fields: `ItemId`, `RandomItemId`, `MinStack`/`MaxStack`, `Quality`, `Condition`, `PerItemCondition`, modifiers.
- `CopyColor` / `CopyPrice` / `CopyQuality`, `PreserveType` (`Jelly`/`Juice`/`Pickle`/`Roe`/`AgedRoe`/`Wine`), `PreserveId` (`DROP_IN` = use input id) — these drive flavored outputs (blueberry wine, etc.).

### `MachineItemAdditionalConsumedItems`
- `string ItemId`, `int RequiredCount` (default 1), `string InvalidCountMessage`. The machine-level secondary-input list referenced by `MachineData.AdditionalConsumedItems`.

## Runtime API (`StardewValley.Object` + `MachineDataUtility`)

### Loading input (reload)
- `bool Object.PlaceInMachine(MachineData machineData, Item inputItem, bool probe, Farmer who, bool showMessages = true, bool playSounds = true)`
  — the canonical 1.6 load entry. `probe: false` performs the load. **`probe: true` is NOT a full acceptance check** — it returns `true` immediately after `TryGetMachineOutputRule` succeeds, *before* calling `OutputMachine` or the output delegate. Use `PlaceInMachine(probe:true)` only to confirm the trigger-rule level matches; call `GetOutputItem` separately for delegate-level filtering (see below).
- `bool Object.performObjectDropInAction(Item dropInItem, bool probe, Farmer who, bool returnFalseIfItemConsumed = false)`
  — the general drop-in entry that routes into `PlaceInMachine` for machines; also probe-able.
- `Task<bool> Object.AttemptAutoLoad(Farmer who)` and **`bool Object.AttemptAutoLoad(IInventory inventory, Farmer who)`**
  — the hopper/auto-load logic: scans an inventory, finds a valid input, and loads it. A chest's
  `Items` is an `IInventory`. Auto-load **auto-picks** any valid item, so to honor the player's
  input filter we either pass a *filtered* inventory view or probe-then-commit specific ids (preferred — full control over "specific ids vs any").
- `static void Object.ConsumeInventoryItem(Farmer who, Item drop_in, int amount)` — low-level consume helper.

### Checking requirements
- `static bool MachineDataUtility.HasAdditionalRequirements(IInventory inventory, IList<MachineItemAdditionalConsumedItems> requirements, out MachineItemAdditionalConsumedItems failedRequirement)`
  — does the inventory hold the machine's extra required items (e.g. coal)? Check before committing a load so a load is **atomic** (never consume the fish without the coal).
- `static bool MachineDataUtility.CanApplyOutput(Object machine, MachineOutputRule rule, MachineOutputTrigger trigger, Item inputItem, Farmer who, GameLocation location, out MachineOutputTriggerRule triggerRule, out bool matchesExceptCount)`
- `static bool MachineDataUtility.TryGetMachineOutputRule(Object machine, MachineData machineData, MachineOutputTrigger trigger, Item inputItem, Farmer who, GameLocation location, out MachineOutputRule rule, out MachineOutputTriggerRule triggerRule, out MachineOutputRule ruleIgnoringCount, out MachineOutputTriggerRule triggerIgnoringCount)`
  — the matching machinery; `ruleIgnoringCount` lets you detect "right item, not enough of it."

### Producing / collecting output
- `bool Object.OutputMachine(MachineData machine, MachineOutputRule outputRule, Item inputItem, Farmer who, GameLocation location, bool probe, bool heldObjectOnly = false)` — produces the output into `heldObject` (normally called internally by the load path).
- `static MachineItemOutput MachineDataUtility.GetOutputData(Object machine, MachineData machineData, MachineOutputRule outputRule, Item inputItem, Farmer who, GameLocation location)` — returns the first matching `MachineItemOutput` by delegating to the list overload below.
- `static MachineItemOutput MachineDataUtility.GetOutputData(List<MachineItemOutput> outputs, bool useFirstValidOutput, Item inputItem, Farmer who, GameLocation location)` — filters output items by their `Condition` (GSQ). When `useFirstValidOutput: true`, returns the first match; when `false`, **collects all condition-matches and picks one at random** (`Game1.random.ChooseFrom`). The machine-rule overload above passes `outputRule.UseFirstValidOutput`, which defaults to `false` in JSON — so multi-item output lists are **randomly sampled**. Call this overload directly with `useFirstValidOutput: true` when you need a deterministic result, or skip it entirely and pass a specific `MachineItemOutput` directly to `GetOutputItem`.
- `static Item MachineDataUtility.GetOutputItem(Object machine, MachineItemOutput outputData, Item inputItem, Farmer who, bool probe, out int? overrideMinutesUntilReady)` — compute the output item (probe-able). When `outputData.OutputMethod` is set, calls the named delegate (e.g. `StardewValley.Object.OutputGeodeCrusher`), which can return null to reject the input. **Null propagates correctly** — `ApplyItemFields(null, …)` returns null, so the full call returns null. Useful for **authoritative input acceptance** when the delegate is the real filter (Geode Crusher pattern).
- `bool Object.checkForAction(Farmer who, bool justCheckingForActivity = false)` — the player-interaction entry; on a `readyForHarvest` machine it **collects** the held output (gives it to `who`, plays sound, clears `readyForHarvest`/`heldObject`, may re-trigger an `OutputCollected` rule). `justCheckingForActivity: true` probes.

### Worker-action guarding (reuse existing pattern)
Like crop/tool actions, machine load/collect routes items and sounds through `Game1.player`. Wrap
worker machine actions with the existing snapshot/redirect/HUD-trim guard
(`InvokeTaskActionGuarded` / `CreateWorkerActionFarmer` in `ShiftOrchestrator.TaskActions`): build a
fake worker-action Farmer, populate its inventory from the worker's carried input buffer for loads,
call the API, then read back what was consumed / redirect collected output into the worker's output
`ItemBuffer`.

## Special cases & caveats (verified or flagged)

- **Computed outputs (`OutputMethod`)** — output id not statically known; preview best-effort, collection exact. (verified field)
- **Bee houses (`RecalculateOnCollect`)** — input-less producer; collect-only; real output resolved at collect. (verified field)
- **Crystalarium (`AllowLoadWhenFull`)** — never reload; collect-only. (verified field)
  - **Collect must re-trigger production, or the loaded mineral vanishes.** (verified 2026-06-25 against the installed `Stardew Valley.dll` decompile). The crystalarium's `heldObject` is the *produced copy*, **not** a persistent input — there is no separate "input slot". On collect, vanilla `Object.CheckForActionOnMachine` clears `heldObject`/`readyForHarvest` **unconditionally**, then re-fires the `OutputCollected` output rule via `OutputMachine(...)`, which re-derives the next gem from `machine.lastInputItem.Value` (the originally-placed mineral, remembered across collects) and sets a fresh `MinutesUntilReady`. That re-trigger is what makes the crystalarium "keep the mineral". Any custom collect path that nulls `heldObject` **without** running the `OutputCollected` re-trigger leaves the machine empty — this was the bug fixed in `ShiftOrchestrator.Machines.cs` `CollectMachine` off-location branch. The re-trigger is **silent when the player is elsewhere**: `OutputMachine → minutesElapsed(0)` only plays `"dwop"` when `MinutesUntilReady <= 0` (crystalarium production time is > 0), and `addWorkingAnimation` early-returns when `!location.farmers.Any()` (the fake worker farmer is not in `location.farmers`). The player-present branch already got this right because it calls `checkForAction`. `Object.OutputMachine` is `public virtual`; `lastInputItem`/`showNextIndex` are public net fields.
- **Casks** (next phase after v1) — they *are* `Data/Machines` objects (`MachineItemOutput.CustomData` carries `AgingMultiplier`), but "ready" is **quality aging**, not a binary `readyForHarvest` flip — the player chooses *when* to pull for higher quality. The cask phase must verify cask `readyForHarvest`/pull semantics against the live data + `Cask`-specific `Object` code before relying on this file's general ready-state rules.
- **Incubators (`IsIncubator`)** — egg→animal, not item→item; out of scope.
- **Per-entry recipes are NOT verified here.** The fish smoker (fish + coal), dehydrator (×5),
  preserves jar, keg flavoring, etc. must be read from the live `Data/Machines` at implementation
  time to confirm whether a secondary input lives on the trigger (`RequiredCount`) vs.
  `AdditionalConsumedItems`. The schema supports both; the specific entry decides.

## Reader implementation (Dayswork) — verified by compile

`Dayswork/Orchestration/MachineReader.cs` is the live-world → pure adapter (enumerate / resolve /
classify / build load candidates). The following API surface is **confirmed to compile against the
installed 1.6.15 `Stardew Valley.dll` + `StardewValley.GameData.dll`** (the build links the real
assemblies, so a wrong signature fails the build):

- `Object.GetMachineData() : MachineData?` — null ⇒ not a machine.
- `GameLocation.objects.Pairs` enumerates `KeyValuePair<Vector2, Object>`; `objects.TryGetValue(vec, out obj)`.
- `Object.readyForHarvest.Value : bool`, `Object.heldObject.Value : Object?` (net fields, `.Value`).
- `MachineData.AllowLoadWhenFull`, `.IsIncubator`, `.AdditionalConsumedItems` (`List<MachineItemAdditionalConsumedItems>`).
- `MachineItemAdditionalConsumedItems.ItemId : string`, `.RequiredCount : int`.
- `MachineDataUtility.TryGetMachineOutputRule(Object machine, MachineData data, MachineOutputTrigger trigger, Item input, Farmer who, GameLocation location, out MachineOutputRule rule, out MachineOutputTriggerRule triggerRule, out MachineOutputRule ruleIgnoringCount, out MachineOutputTriggerRule triggerIgnoringCount) : bool` — `rule`/`triggerRule` are the count-matching results; `triggerRule.RequiredCount : int` is the recipe's primary-input count.
- `MachineOutputTrigger.ItemPlacedInMachine` (in `StardewValley.GameData.Machines`).
- `ItemRegistry.Create(string qualifiedId, int amount, int quality = 0, bool allowNull = false) : Item?` — `allowNull: true` returns null for an invalid id (avoids the "Error Item" landmine when probing).

Reader behavior decisions:
- **Reload candidates are discovered by probing**, not hard-coded recipes. For each chest item allowed
  by the group filter, `TryGetMachineOutputRule(ItemPlacedInMachine, …)` confirms acceptance and yields
  `RequiredCount`; `MachineData.AdditionalConsumedItems` supplies secondary inputs (coal-style). The
  probe item is created with a large stack so the count-matching `rule` binds and `RequiredCount` is
  readable; the pure `MachineInputPlanner` then clamps to real supply.
- **Collect-only falls out naturally**: input-less producers (bee houses, tappers) yield zero load
  candidates because no item satisfies an `ItemPlacedInMachine` trigger. `AllowLoadWhenFull`
  (crystalarium) and `IsIncubator` are excluded explicitly via `MachineReader.IsReloadable`.

**Still pending live per-entry confirmation (do in the M8 in-game smoke pass):** whether the fish
smoker models coal on the trigger vs `AdditionalConsumedItems`, the dehydrator's ×5
`RequiredCount`, and keg/jar flavoring. The reader handles **both** placements, so it is correct
either way; the smoke pass only needs to confirm the worker actually loads/collects these in-world.
**Furnace specifically:** confirm its accepted ores and that coal is an `AdditionalConsumedItems`
entry (the auto-locked companion in the input picker relies on this) — record the result here.

### Enumerating a machine type's accepted inputs (UI authoring)

The Manage Machines input picker shows *what the chosen machine type accepts*, derived from data, not
from a chest's contents. `MachineReader` exposes (all probe-only):

- `AcceptsInput(MachineData?)` — true when some output rule fires on `ItemPlacedInMachine` and the
  machine is reloadable. Cheap (inspects the rules; no catalog sweep). Drives "collect-only" types
  (bee houses, tappers) that have no input picker.
- `EnumerateAcceptedInputs(machine, data, who, location)` — sweeps the **whole object catalog**
  (`Game1.objectData.Keys` → `(O)<key>`), creates each with `ItemRegistry.Create(id, 999, allowNull:true)`,
  and keeps those that the machine would actually accept. Two-stage filter:
  1. `TryGetMachineOutputRule(ItemPlacedInMachine,…)` — the game's own matcher resolves `RequiredItemId`
     and `RequiredTags` rules; no need to reimplement context-tag matching.
  2. **Catch-all trigger check** (no `RequiredItemId`, no `RequiredTags`): for machines like the Geode
     Crusher whose trigger accepts everything, the real input filter is an `OutputMethod` delegate on one
     of the rule's `OutputItem` entries. `EnumerateAcceptedInputs` finds the first such delegate item
     and calls `GetOutputItem(probe:true)` directly. This avoids `GetOutputData`'s non-deterministic
     random selection (`UseFirstValidOutput:false` + multiple items → `Game1.random.ChooseFrom`),
     which would let non-delegate items (e.g. mineral-conditioned fallback) randomly pass wrong inputs.
  ~900 probes, **cached per machine type** for the session. Needs a placed instance of the type.
- `EnumerateRequiredCompanions(MachineData?)` — `AdditionalConsumedItems` → the coal-style companions
  the load engine already consumes automatically; surfaced in the picker as auto-selected/locked rows
  (informational: "stock coal in the input chest"), not added to the `MachineInputFilter`.

`Data/Machines` input rules confirmed in practice (SVE `[CP] …/code/Items/Machines.json`): concrete
`RequiredItemId` (Galdoran Gem, Goose Egg, Camel Wool) **and** `RequiredTags` (winery keg
`category_fruits`/`keg_wine`, butter churner `milk_item`+`quality_*`) — hence the probe approach.
`GetMachineDataForType(qualifiedId)` reads `Data/Machines` for a type without a placed instance.

### Flavored inputs must be the *real* item — never a rebuilt id (roe → Aged Roe / Caviar)

A machine input that carries flavor cannot be reconstructed from its qualified id. **All roe is
`(O)812`**; the flavor lives in `Object.preservedParentSheetIndex` and surfaces only as the context
tag `preserve_sheet_index_<fishId>` (verified in `Object` context-tag generation — the
`preserve`-type switch emits only `honey_item`/`jelly_item`/`juice_item`/`wine_item`/`pickle_item`,
**not** a roe/aged-roe tag, so the *only* flavored-roe tag is `preserve_sheet_index_*`). A fresh
`ItemRegistry.Create("(O)812")` has none of it. Consequences, all verified against the 1.6.15
decompile:

- **Matching** (`MachineDataUtility.CanApplyOutput`, `ItemPlacedInMachine`): an input must satisfy
  `Condition` (a `GameStateQuery`), `RequiredItemId` (via `ItemRegistry.HasItemId`, base-id match),
  **all** `RequiredTags` (`ItemContextTagManager.DoAllTagsMatch`), and `RequiredCount`. The Preserve
  Jar's Sturgeon-Roe→Caviar rule keys on the sturgeon flavor tag, so a flavorless probe never reaches
  it.
- **Output flavoring** (`MachineDataUtility.GetOutputItem`): Aged Roe uses the `DROP_IN` /
  `DROP_IN_PRESERVE` preserve mechanism, which copies the flavor from **`inputItem`** onto the output
  (`obj.preservedParentSheetIndex = inputItem.GetPreservedItemId()`). A flavorless input yields
  generic Aged Roe — never the correct flavored Aged Roe / Caviar.

Therefore the reload pipeline carries the **real withdrawn chest items** through
`ShiftSession.CarriedInputs` (`Dictionary<string, List<Item>>`, not a count) and feeds them straight
into `PlaceInMachine`; the acceptance probe (`MachineReader.TryBuildRequirement`) clones a real chest
**sample** (`ReadChestSupply` keeps one per id) instead of `ItemRegistry.Create(id)`. This is the
input-side analog of the output-side `FlavorItemRegistry` capture-and-clone (built 2026-06-26). The
pure `MachineInputPlanner` still allocates by count keyed on `(O)812` — pooling roe flavors for
allocation is correct; only the executor must keep the real items. Leftovers/overflow preserve flavor
via the same `Session.Flavors` token path.

## Fish ponds (separate phase — buildings, not machines)

Fish ponds are `StardewValley.Buildings.FishPond`, **not** `Data/Machines` objects, so none of the
machine reader/planner above applies to them. They are **collect-only** (their "input" is the initial
fish stocking + the optional capacity-quest item — both player-handled, out of scope). API confirmed
by decompiling `Stardew Valley.dll` (1.6.15) → `StardewValley.Buildings.FishPond`:

- **Enumerate:** ponds live in `location.buildings`, not `location.objects` — `location.buildings.OfType<FishPond>()`.
  (Vanilla = Farm only, but scan worker-traveled locations like machines do, since mods/SVE can place
  them elsewhere.)
- **Identity / resolve:** a pond has no qualified item id; identity is `(location, tileX, tileY)`.
  Re-resolve at shift start by matching a `FishPond` whose `tileX.Value`/`tileY.Value` equals the
  stored tile (same skip-if-gone contract as machine tiles).
- **Ready state:** `output` is a `NetRef<Item>`; **`output.Value != null` ⇒ ready to collect**. No
  `readyForHarvest`/`heldObject` (those are `Object` fields, absent on buildings). Produce is rolled in
  `dayUpdate` into `output.Value`; it holds **one** item stack at a time.
- **Collect (verified in `doAction`, lines ~300–320):** vanilla takes `output.Value`, sets
  `output.Value = null`, adds to the player, plays `"coin"`, grants fishing xp. For the worker, the
  clean path is **direct field manipulation**: capture `output.Value`, null it, push the stack into the
  deposit `ItemBuffer`. This avoids the player-inventory/HUD/xp entanglement entirely — **no fake
  `Farmer` or `InvokeTaskActionGuarded` needed** (simpler than machine collect, which must go through
  `checkForAction`). Duplication-safe: only credit the buffer after `output.Value` is nulled.
  Pond roe is usually a flavored `ColoredObject` (Sturgeon Roe etc.); its identity/price is preserved
  through deposit by the per-shift `FlavorItemRegistry` (capture-and-clone keyed by `BufferedItem.FlavorId`)
  rather than reconstructed from `(O)812` — see `docs/plans/fish-ponds.md` → "Flavored roe is preserved".
- **Nav/facing:** footprint is `tilesWide`×`tilesHigh` (5×5) and the interior tiles are water
  (impassable). `GetItemBucketTile()` = `(tileX+4, tileY+4)` is where the output bucket visually sits;
  worker should stand on a walkable tile adjacent to the footprint (not on water) — pick a stand tile
  around the building perimeter, not the machine single-tile `+1` heuristic.
- **Out of scope (confirmed fields, do not touch):** `neededItem`/`neededItemCount`/`needsMutex`
  (capacity-gate quest), `sign`, `goldenAnimalCracker`, `currentOccupants`/`fishType` (stocking).
- `GetFishProduce()`/`CatchFish()` exist but are the game's own roll/removal helpers — the worker only
  reads the already-produced `output`, never re-rolls.
