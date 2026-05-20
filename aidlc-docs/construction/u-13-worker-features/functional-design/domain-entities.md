# U-13 — Domain Entities

**Unit**: U-13 — Worker Features: Priority + Stuck + Tool Swap + Invulnerability

Lists the new and modified types U-13 introduces. Core types (no Stardew references) live in `Dayswork.Core`; Mod types live in `Dayswork`.

---

## 1. Modified Core types

### 1.1 `ShiftPhase` (enum) — extended
Add two members:

```
WaitingForSpawn
Working
Stuck         ← new
Recovering    ← new
Depositing
Exiting
Done
```

### 1.2 `ShiftStateMachine` (C-08) — transition rules changed
- Successor map becomes **multi-valued** (`Dictionary<ShiftPhase, HashSet<ShiftPhase>>`):
  - `WaitingForSpawn → {Working}`
  - `Working → {Depositing, Stuck}`
  - `Stuck → {Recovering}`
  - `Recovering → {Working, Depositing}`
  - `Depositing → {Exiting}`
  - `Exiting → {Done}`
  - `Done → {}` (terminal)
- **Active phases** (must carry a non-null intent): `Working`, `Stuck`, `Recovering`, `Depositing`, `Exiting`.
  (`Stuck` carries an emote intent; `Recovering` carries a teleport intent.)
- `Transition` / `SetIntent` guards otherwise unchanged. Illegal transitions still throw; `Done` remains terminal.

### 1.3 `ShiftIntent` (records) — new subtypes
Existing: `IntentMoveToTile`, `IntentPerformTaskAt`, `IntentDepositInShippingBin`, `IntentExitFarm`.
Add (aligned with the component-methods.md intent vocabulary):

```csharp
public sealed record IntentPlayEmote(int EmoteId) : ShiftIntent;        // Stuck step 1 ("?"); also reused for ouch
public sealed record IntentTeleportToTile(TileCoord Destination) : ShiftIntent;  // Recovering step 2
public sealed record IntentTeleportHome : ShiftIntent;                   // Recovering step 3 (end shift early)
```

`IntentQueueMail` from the component vocabulary is **not** materialized in U-13 (mail is U-14); the tool-missing warning is tracked as a pending-warning set (see 1.6), not an intent.

### 1.4 `WorkItem` — gains a separate navigation tile
Today: `(TileCoord Tile, TaskKind Task)`. To support trellis adjacency (FR-SKIP-04):

```csharp
public sealed record WorkItem(
    TileCoord NavTile,    // where the worker stands to act (== TaskTile for normal tiles)
    TileCoord TaskTile,   // the object's tile the action targets
    TaskKind  Task);
```

For all non-trellis work `NavTile == TaskTile`. Existing call sites that used `Tile` map to `TaskTile`; navigation uses `NavTile`.

### 1.5 `StuckDetector` (C-09) — new Core type
```csharp
public interface IStuckDetector
{
    void RecordTick(bool madeProgressThisTick, int inGameMinutesElapsed);
    bool ShouldFireStuck();
    void Reset();
}
```
Internal state: `int _noProgressMinutes`, `int _threshold`. Constructed with the active threshold (initial vs post-teleport supplied by the orchestrator from `ConfigSnapshot`). `RecordTick` resets to 0 on progress, else adds elapsed minutes; `ShouldFireStuck` is `_noProgressMinutes >= _threshold`. Pure, no Stardew refs — PBT target.

### 1.6 `ShiftContext` — gains escalation + warning state
Add:
- `int RecoveryAttempts` (escalation counter; 0, 1, then end-shift) — FD-Q2=A places this here, not in the state machine.
- `HashSet<TaskKind> ToolMissingWarnings` — task kinds skipped entirely for lack of a tool; consumed by U-14's mail dispatcher.

---

## 2. New Core types

### 2.1 `WorkerTool` (enum)
The tool a task visibly uses (drives ToolSwapAnimator + Farmer `CurrentTool`):
```
None        // Harvest crops, Collect fruit (hand)
WateringCan // Water crops
Scythe      // Clear weeds, Clear grass
Pickaxe     // Clear rocks
Axe         // Cut trees
```
Plus a pure mapping `WorkerTool ForTask(TaskKind task)`.

### 2.2 Object-target classification helpers
Pure helpers that map detected work to the existing `AxeTarget` / `PickTarget` enums (see [business-logic-model.md](business-logic-model.md) §3 table). The classification *inputs* are Stardew objects, so the **classifier** lives in the Mod layer (it reads `Tree`/`FruitTree`/`ResourceClump`/`Object`), but it calls into the pure `CapabilityEvaluator` to decide skip/keep.

---

## 3. New Mod types

### 3.1 `WorkerAppearance` (record) — randomized look
Captures the character-creation fields used to skin the `Farmer`:
```csharp
public sealed record WorkerAppearance(
    int   Skin,
    int   Hairstyle,
    Color HairColor,
    int   Shirt,
    int   Pants,
    Color PantsColor,
    int   Accessory,
    Color EyeColor,
    bool  IsMale);
```
A `WorkerAppearanceRandomizer` produces one per shift (or per worker identity) using the valid ranges the character-creation menu exposes.

### 3.2 Worker entity (M-09, re-founded on `Farmer`)
The class currently named `FarmhandNpc` is re-implemented as a `Farmer`-backed worker (rename during code generation, e.g. `FarmhandWorker`). Responsibilities: hold the `Farmer` instance, apply `WorkerAppearance` + `ToolSnapshot`, expose position/facing for the movement driver, draw itself with depth sorting, and play emotes. Never serialized into the save.

### 3.3 Worker movement driver (supersedes M-11 PathFindControllerAdapter for the Farmer)
Computes a tile path and steps the `Farmer` along it each tick (set `Position`, set facing, advance walk animation). Exposes `HasArrived` / `NavigationFailed` signals matching the old adapter's surface so the orchestrator loop is unchanged. See [business-logic-model.md](business-logic-model.md) §1.3.

### 3.4 `ToolSwapAnimator` (M-10)
`OnTaskChanged(TaskKind previous, TaskKind next)` and triggers `FarmerSprite.animateOnce(...)` swings during task actions. Holds the `WorkerTool ForTask` mapping and the per-direction swing frame sets.

---

## 4. Unchanged but newly-used existing types
- `ToolSnapshot` (Axe / Pickaxe / WateringCan levels) — captured in U-10, now consumed by the scan. (Scythe has no upgrade levels in Stardew, so it is not in the snapshot.)
- `CapabilityEvaluator` / `CapabilityMatrix` (C-06) — wired into `BuildWorkList`.
- `TaskPriorityOrderer` (C-07) — wired into `BuildWorkList`.
- `ConfigSnapshot` (C-14) — `StuckInitialThresholdMinutes`, `StuckPostTeleportThresholdMinutes` now read.
