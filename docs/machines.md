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
farmhand visit each selected machine to collect finished output and reload it with input.
Fish ponds are a separate, later phase (they are **buildings**, not `Data/Machines` objects — see end).

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
  — the canonical 1.6 load entry. **`probe: true` tests acceptance without mutating anything** (no consume, no animation) — use it for "would this machine take this item?" checks. `probe: false` performs the load: consumes `RequiredCount` from `inputItem` (+ any `AdditionalConsumedItems`) and starts processing.
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
- `static Item MachineDataUtility.GetOutputItem(Object machine, MachineItemOutput outputData, Item inputItem, Farmer who, bool probe, out int? overrideMinutesUntilReady)` — compute the output item (probe-able) — useful for **best-effort UI preview**.
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
- **Casks** (next phase after v1) — they *are* `Data/Machines` objects (`MachineItemOutput.CustomData` carries `AgingMultiplier`), but "ready" is **quality aging**, not a binary `readyForHarvest` flip — the player chooses *when* to pull for higher quality. The cask phase must verify cask `readyForHarvest`/pull semantics against the live data + `Cask`-specific `Object` code before relying on this file's general ready-state rules.
- **Incubators (`IsIncubator`)** — egg→animal, not item→item; out of scope.
- **Per-entry recipes are NOT verified here.** The fish smoker (fish + coal), dehydrator (×5),
  preserves jar, keg flavoring, etc. must be read from the live `Data/Machines` at implementation
  time to confirm whether a secondary input lives on the trigger (`RequiredCount`) vs.
  `AdditionalConsumedItems`. The schema supports both; the specific entry decides.

## Fish ponds (deferred phase — buildings, not machines)

Fish ponds are `StardewValley.Buildings.FishPond`, **not** `Data/Machines` objects, so none of the
above applies to them. They accumulate produce in a building-owned output and are collect-only (their
"input" is the initial fish stocking, out of scope). Their API (`FishPond.output`, population, the
optional capacity-quest item request) must be verified separately when that phase begins.
