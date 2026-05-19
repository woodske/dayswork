# U-07 Capability & Priority Core — Functional Design Plan

## Unit Summary

**Purpose**: Introduce the two remaining pure-logic Core primitives:
- **C-06 CapabilityEvaluator** — answers "can this worker's tools process this game object?"
- **C-07 TaskPriorityOrderer** — answers "in what order should enabled tasks be executed?"

Plus domain types: `ToolSnapshot` (read at spawn; locked for shift) and `CapabilityMatrix` (used by the evaluator).

**Code organization** (as approved in unit-of-work.md):
- `Dayswork.Core/Capabilities/` — `ICapabilityEvaluator.cs`, `CapabilityEvaluator.cs`, `CapabilityMatrix.cs`
- `Dayswork.Core/Shifts/` — `ITaskPriorityOrderer.cs`, `TaskPriorityOrderer.cs`
- `Dayswork.Core/Domain/ToolSnapshot.cs`

**Key requirements**:
- FR-SKIP-01: skip stumps/logs if axe level too low
- FR-SKIP-02: skip boulders if pickaxe level too low
- FR-SKIP-03: fruit trees always skipped regardless of axe level (hard rule)
- FR-TOOL-01..04: tool snapshot locked at 6am spawn; level 0 = basic; not owned = level 0 + mail warning
- FR-WORK-03: task priority order is fixed: Feed animals → Pet animals → Collect animal products → Water crops → Harvest crops → Collect fruit → Clear weeds → Clear grass → Clear rocks → Cut trees

---

## Execution Checklist

- [x] Step 1: Analyze unit context (done — see above)
- [x] Step 2: Create functional design plan (this file)
- [x] Step 3: Generate clarifying questions (below)
- [x] Step 4: Store plan (this file)
- [x] Step 5: Collect and analyze answers (Q1=B, Q2=B, Q3=default Basic, Q4=B, Q5=A, Q6=A, Q7=A, Q8=A — no ambiguities)
- [x] Step 6: Generate functional design artifacts (domain-entities.md, business-logic-model.md, business-rules.md)
- [x] Step 7: Present completion message
- [ ] Step 8: Wait for explicit approval
- [ ] Step 9: Record approval and update progress

---

## Clarifying Questions

> **Instructions**: Answer each question by replacing the `[Answer]:` line with your choice
> (e.g. `[Answer]: A`). Some questions accept a short free-text addition after the letter.
> When you're done, reply "continue" and the workflow will resume.

---

### Q1 — ObjectClass enum: granularity

The `CapabilityEvaluator` is in `Dayswork.Core` so it cannot reference Stardew Valley
types directly. It needs a normalized `ObjectClass` enum (or equivalent) that the Mod
layer translates to before calling the evaluator.

Which granularity should `ObjectClass` use?

**A) Fine-grained flat enum** — one value per distinct skip/capability case:

```
SmallRock, LargeBoulder, Meteorite,
SmallStump, LargeStump, LargeLog, StandingTree, FruitTree,
Crop, TrellisCrop
```

**B) Tool-grouped** — separate enums per tool type, each with its own cases:

```
AxeTarget  { SmallStump, LargeStump, LargeLog, StandingTree, FruitTree }
PickTarget { SmallRock, LargeBoulder, Meteorite }
```
`CapabilityEvaluator` takes the appropriate target enum per method.

**C) Single flat enum, capability-relevant cases only** — only cases that produce a
different answer for any tool level. "Always-OK" objects (weeds, grass, crops for
watering) are never passed to the evaluator; the evaluator only receives objects
where the tool level actually matters.

[Answer]:

---

### Q2 — Tool level representation in `ToolSnapshot`

Stardew Valley stores upgrade level as an `int` (0 = Basic, 1 = Copper, 2 = Steel,
3 = Gold, 4 = Iridium). How should `ToolSnapshot` represent levels?

**A) Plain `int` per tool** — matches SV's internal value directly; `CapabilityMatrix`
defines internal constants for comparison (no enum wrapping):

```csharp
public record ToolSnapshot(int AxeLevel, int PickaxeLevel, int WateringCanLevel);
```

**B) Dedicated `ToolLevel` enum** — wraps the int in a domain type; adds safety against
passing raw ints in wrong positions:

```csharp
public enum ToolLevel { Basic = 0, Copper = 1, Steel = 2, Gold = 3, Iridium = 4 }
public record ToolSnapshot(ToolLevel AxeLevel, ToolLevel PickaxeLevel, ToolLevel WateringCanLevel);
```

**C) `int` level + `bool OwnsX` flags** — distinguishes "owns tool at level 0" from
"does not own tool at all" without an out-of-range sentinel:

```csharp
public record ToolSnapshot(int AxeLevel, bool OwnsAxe, int PickaxeLevel, bool OwnsPick, int WateringCanLevel, bool OwnsWateringCan);
```

[Answer]:

---

### Q3 — "Tool not owned" sentinel (FR-TOOL-03)

FR-TOOL-03: if the player has sold a tool, treat its level as 0 and skip all tasks
requiring it; a mail warning is sent the following morning. The mail warning is
dispatched by `ShiftOrchestrator` in U-13 when it discovers the tool is absent.

For `CapabilityEvaluator` to let the orchestrator distinguish "tool absent → queue
mail" from "tool present but too low level → silent skip", the evaluator needs to
communicate that difference. How should it do this?

**A) Sentinel int** — store `int ToolAbsent = -1` as a constant in `ToolSnapshot`.
Callers compare level against `ToolAbsent` before calling the evaluator. The
evaluator's return is always `bool`.

**B) Nullable int** — `int? AxeLevel` where `null` means not owned. The evaluator
checks for null internally and returns `false`; the orchestrator also inspects the
nullable directly to decide whether to queue mail.

**C) Rich result enum** — `CapabilityEvaluator.Evaluate(snap, obj)` returns
`CapabilityResult { CanProcess, ToolAbsent, LevelTooLow }`. The orchestrator checks
`ToolAbsent` to trigger the mail path; the evaluator encapsulates the distinction.

[Answer]:

---

### Q4 — `CapabilityMatrix` role

The unit definition lists `CapabilityMatrix` as a type in `Dayswork.Core/Capabilities/`.
Which of these roles fits best?

**A) Pre-computed snapshot** — `CapabilityEvaluator` exposes a factory:
`CapabilityMatrix Build(ToolSnapshot snap)`. The matrix pre-computes `bool CanChop`,
`bool CanBreak`, etc. for each `ObjectClass`. The `ShiftOrchestrator` (U-10) builds
the matrix once at spawn and calls `matrix.CanProcess(obj)` per tile (no per-tile
re-evaluation of the snapshot).

**B) Static lookup table** — `CapabilityMatrix` is a `static readonly` class with
methods like `bool CanChop(int axeLevel, AxeTarget target)` that `CapabilityEvaluator`
calls internally. The matrix is never instantiated; it's just a named container for
the spec table.

**C) Data record** — `CapabilityMatrix` is a plain record holding the tool levels and
the hard-coded capability thresholds. `CapabilityEvaluator.Evaluate(CapabilityMatrix m,
ObjectClass obj)` is a pure static function with no dependency injection.

[Answer]:

---

### Q5 — FR-SKIP-04 / FR-SKIP-05 scope boundary

FR-SKIP-04: trellis crops skip if no reachable adjacent tile exists.
FR-SKIP-05: crops not yet ready to harvest are skipped.

Both require runtime game state (pathfinding reachability; crop growth stage). The
Core layer has no SMAPI references, so these decisions can't live in `CapabilityEvaluator`.
Confirming the intended scope boundary:

**A) Pure tool-level scope** — `CapabilityEvaluator` handles only tool-level capability
checks (Q1's ObjectClass cases). Trellis adjacency and crop readiness are filtered by
the `ShiftOrchestrator` in the Mod layer, before objects are even passed to the evaluator.
`ObjectClass` has no trellis or readiness variants.

**B) Mixed scope** — add `TrellisCrop` and `UnreadyCrop` to `ObjectClass` anyway
(for test coverage purposes), and let `CapabilityEvaluator` return `false` for both.
The actual reachability/readiness flags are injected by the Mod layer when it classifies
the object.

**C) Separate method** — `CapabilityEvaluator` has a distinct
`bool IsCropActionable(bool isReady, bool isTrellis, bool hasReachableAdjacentTile)`
method, keeping crop-skip logic visible in Core even though the Mod layer supplies the
boolean flags.

[Answer]:

---

### Q6 — `TaskPriorityOrderer` input/output contract

FR-WORK-03 gives a fixed 10-task priority list. What should
`ITaskPriorityOrderer.Order(...)` look like?

**A) `IReadOnlyList<TaskKind> Order(IEnumerable<TaskKind> enabledTasks)`** — takes the
set of tasks the player enabled on the contract (a subset of all 10) and returns them
in FR-WORK-03 order. The `ShiftOrchestrator` separately builds (TaskKind, TileCoord[])
pairs; the orderer only answers "which task type comes first".

**B) `IReadOnlyList<TaskKind> Order(IReadOnlySet<TaskKind> enabledTasks)`** — same
semantics but uses `IReadOnlySet` to make clear that duplicates are invalid input.

**C) `int Priority(TaskKind task)`** — returns the priority rank (0 = highest). Callers
do their own sort. The orderer is a lookup helper rather than a sort executor.

[Answer]:

---

### Q7 — `CapabilityEvaluator` DI: interface or static?

Prior Core units (RateCalculator, ZoneGeometry, ContractStore) all follow the interface
+ implementation pattern, registered via DI in U-09/U-10. Should `ICapabilityEvaluator`
follow the same pattern?

**A) Interface + implementation** — `ICapabilityEvaluator` with
`CapabilityEvaluator : ICapabilityEvaluator`. The `ShiftOrchestrator` (U-10) receives
it via constructor injection. Consistent with the rest of Core.

**B) Static class** — `CapabilityEvaluator` is a static utility (like `CapabilityMatrix`
in option B of Q4). No interface needed; the orchestrator calls it directly. Valid
because it's pure logic with no state and no test-double need.

**C) Extension method on `ToolSnapshot`** — `toolSnap.CanProcess(ObjectClass obj)`.
No separate evaluator class; capability logic lives on the snapshot itself.

[Answer]:

---

### Q8 — Watering can capability check

The spec says the watering can works at any upgrade level (no capability gate). Should
`CapabilityEvaluator`:

**A) Not handle watering can at all** — `WaterCrops` never produces an `ObjectClass`
that is passed to the evaluator. The task always executes if enabled (rain handling is
at the `RecurringContractScheduler` / U-15 level, not here).

**B) Include a trivial `WaterableObject` class that always returns `true`** — for
completeness and so the test suite has uniform coverage of all ten tasks through the
evaluator.

**C) Handle only the water-can-absent case** — if `OwnsWateringCan = false` (or level
sentinel), return `false` so the no-tool mail path works. Otherwise not checked.

[Answer]:

---

*When all eight questions are answered, reply "continue" and Functional Design artifact
generation will begin.*
