# U-13B — Domain Entities

**Unit**: U-13B — Farmer Worker + Tool Visuals

Lists the new and modified types U-13B introduces. Core types (no Stardew references) live in `Dayswork.Core`; Mod types live in `Dayswork`. U-13B introduces exactly one Core type (`WorkerTool`) and the rest are Mod-layer rendering/movement/appearance types. No `Shifts/`, `Capabilities/`, or state-machine types change in this unit.

---

## 1. New Core type

### 1.1 `WorkerTool` (enum + pure map)
The tool a task visibly uses (drives `ToolSwapAnimator` and the worker's held tool):

```
None        // Harvest crops, Collect fruit (hand-pick)
WateringCan // Water crops
Scythe      // Clear weeds, Clear grass
Pickaxe     // Clear rocks
Axe         // Cut trees
```

Plus a pure mapping function:

```csharp
public static WorkerTool ForTask(TaskKind task);
```

Pure, no Stardew references — lives in `Dayswork.Core` and is unit-testable (table-driven test asserting every `TaskKind` maps to the locked tool, including the animal kinds → `None` for completeness). The verified per-direction **animation frame sets** are an implementation concern of the Mod-layer `ToolSwapAnimator` (they reference `FarmerSprite`), not of this pure enum.

---

## 2. New Mod types

### 2.1 `FarmhandWorker` (M-09, re-founded on `Farmer`)
Replaces `FarmhandNpc`. Wraps/IS a `StardewValley.Farmer`. Responsibilities:
- hold the `Farmer` instance created at 6am;
- apply a `WorkerAppearance` (§2.2) and the captured `ToolSnapshot`;
- expose `Position`, `TilePoint`, `FacingDirection` for the movement driver;
- expose `doEmote(int)` (inherited from `Character`) for stuck/hit emotes;
- never be added to `location.characters` / `location.farmers`; never serialized.

Constructor takes the spawn pixel position, the `WorkerAppearance`, and the `ToolSnapshot`. No XML-serializer parameterless ctor is required (unlike the old NPC) because the worker is never part of any serialized collection — but if the `Farmer` base demands one for instantiation, it is used only transiently and never persisted.

### 2.2 `WorkerAppearance` (record) — randomized look
Captures the character-creation fields used to skin the `Farmer`:

```csharp
public sealed record WorkerAppearance(
    bool  IsMale,
    int   Skin,
    int   Hairstyle,
    Color HairColor,
    int   Shirt,
    int   Pants,
    Color PantsColor,
    int   Accessory,
    Color EyeColor);
```

Purely cosmetic; never affects behaviour.

### 2.3 `WorkerAppearanceRandomizer`
Produces a `WorkerAppearance` deterministically from a contract ID (FD-Q3=A):

```csharp
public WorkerAppearance Generate(ContractId contractId);
```

Seeds its RNG from a stable integer derived from the contract ID (so the same contract yields the same look every day, no serialization). Full randomization across the valid character-creation ranges (FD-Q4=A): gender/body, skin, hair style + colour, shirt, pants + colour, accessory, eye colour. All indices clamped to valid ranges.

### 2.4 `WorkerMovementDriver` (supersedes M-11 `PathFindControllerAdapter`)
Manual path-follower for the `Farmer`. Surface mirrors the old adapter so the orchestrator loop is unchanged:

```csharp
public bool HasArrived       { get; }
public bool NavigationFailed { get; }
public void StartNavigation(TileCoord destination, GameLocation location, FarmhandWorker worker);
public void Clear();
```

Internal state: a `Queue<Point>`/`Stack<Point>` of waypoints (copied from a throwaway `PathFindController.pathToEndPoint` per FD-Q1=A) and the active target. Steps `Farmer.Position` toward the next waypoint each tick at the vanilla player base walk speed, advancing walk-animation frames and facing. The old `PathFindControllerAdapter.cs` file is deleted.

### 2.5 `WorkerRenderer`
Draws the worker manually via the `Display.RenderedWorld` event (FD-Q2=A):

```csharp
public void SetActiveWorker(FarmhandWorker? worker); // null between shifts
public void OnRenderedWorld(object? sender, RenderedWorldEventArgs e);
```

Composites body + held tool (`FarmerRenderer`) + shadow + active emote bubble at the worker's screen position, internally Y-ordered. Draws on top of world foreground (accepted v1 limitation, BR-WORKER-03). No-op when no worker is active or the rendered location is not the worker's location.

### 2.6 `ToolSwapAnimator` (M-10)
Manages the visible tool and swing animation:

```csharp
public void OnTaskChanged(TaskKind previous, TaskKind next); // instant swap (FD-Q6=A)
public void PlaySwing(TaskKind task, int facingDirection);   // FarmerSprite.animateOnce with verified frames
```

Holds the `WorkerTool.ForTask` mapping (from the Core enum) and the per-direction swing frame sets (heavy R12/R9/R7; can R10/R5/R8/R11; scythe R5/R6/R7). Sets the worker's current tool on swap and triggers one-shot swings during tool-using task actions; for `None`-mapped tasks (Harvest / Collect fruit) it triggers the face-and-pause hand-pick beat instead of a swing (FD-Q5=A).

---

## 3. Modified Mod types

### 3.1 `ShiftOrchestrator` (M-12) — entity/movement/draw seams re-pointed
Field `_farmhand` retyped to `FarmhandWorker?`; field `_nav` replaced by a `WorkerMovementDriver`; new fields for `ToolSwapAnimator` and a reference handed to `WorkerRenderer`. The `Game1.warpCharacter` teleport calls (stuck recovery steps 2/3) replaced by direct `Position` set + driver reset. All control flow, work-list build, skip rules, stuck escalation, deposit/exit, and `OnSaving` logic unchanged (see [business-logic-model.md](business-logic-model.md) §1.3 for the exact seam list).

### 3.2 `ModEntry` (M-01) — composition root
- Construct `WorkerRenderer`, `WorkerAppearanceRandomizer`, `ToolSwapAnimator`; pass the renderer/animator/randomizer into the orchestrator (or wire as needed).
- **Add** `helper.Events.Display.RenderedWorld += workerRenderer.OnRenderedWorld`.
- **Remove** the `OnAssetRequested` handler and the `Portraits/DaysworkFarmhand` → Marnie redirect (NPC-only; obsolete under a Farmer).

---

## 4. Removed / superseded types
- **`FarmhandNpc`** (the `NPC` worker) — removed; replaced by `FarmhandWorker`. Its `PlaceholderSpritePath` / `PlaceholderPortraitPath` / `InternalName` constants and the portrait asset redirect go away with it.
- **`PathFindControllerAdapter`** — removed; replaced by `WorkerMovementDriver`.

---

## 5. Unchanged but newly-used / referenced existing types
- `Farmer` (`StardewValley.Farmer`) — the new base for the worker.
- `FarmerRenderer` / `FarmerSprite` — draw the worker body + held tool and play one-shot swings.
- `ToolSnapshot` (Axe / Pickaxe / WateringCan levels) — applied to the worker so its real tools match the player's at spawn (no Scythe upgrade levels exist in Stardew).
- `ContractId` — used as the appearance seed (FD-Q3=A).
- `TaskKind` — the key into `WorkerTool.ForTask`.
- `ShiftContext`, `ShiftStateMachine`, `ShiftIntent`, `WorkItem`, `StuckDetector`, `CapabilityEvaluator`, `TaskPriorityOrderer`, `ObjectTargetClassifier` — all unchanged in U-13B.
