# U-07 — Domain Entities

## New types introduced in this unit

---

### `ToolLevel` enum
**Location**: `Dayswork.Core/Domain/ToolLevel.cs`

Represents a tool's upgrade level as read from the player's inventory at 6am spawn.
Integer values match Stardew Valley's internal `UpgradeLevel` int directly, making
the Mod-layer translation trivial.

```
ToolLevel
  Basic    = 0   // un-upgraded (copper is 1; basic hand-tools start at 0)
  Copper   = 1
  Steel    = 2
  Gold     = 3
  Iridium  = 4
```

**Note on "tool not owned"**: If the player has sold the tool, the Mod layer
(`ToolLevelReader`, U-10) defaults to `ToolLevel.Basic` when building the
`ToolSnapshot`. The Mod layer separately records whether the tool was absent
and queues the mail warning (U-13). `ToolLevel` itself has no sentinel for absence.

---

### `ToolSnapshot` record
**Location**: `Dayswork.Core/Domain/ToolSnapshot.cs`

Immutable snapshot of the three relevant tool levels captured once at 6am spawn.
Locked for the duration of the shift (FR-TOOL-01).

```
ToolSnapshot
  AxeLevel         : ToolLevel
  PickaxeLevel     : ToolLevel
  WateringCanLevel : ToolLevel
```

- `WateringCanLevel` is stored for completeness (Mod layer reads it) but is never
  consulted by `CapabilityEvaluator` — the watering can has no capability gate.
- Hoe is not included (unused in v1 per FR-TOOL-02).

---

### `AxeTarget` enum
**Location**: `Dayswork.Core/Capabilities/AxeTarget.cs`

Normalized classification of axe-targetable objects. The Mod layer maps Stardew
`ResourceClump` and `Tree` types to these values before calling the evaluator.

```
AxeTarget
  StandingTree   // normal felled tree (any species except fruit)
  FruitTree      // always skipped — FR-SKIP-03 hard rule
  SmallStump     // the small stump left after a player fells a tree
  LargeStump     // large hardwood stump (requires Steel+)
  LargeLog       // large fallen log (requires Gold+)
```

---

### `PickTarget` enum
**Location**: `Dayswork.Core/Capabilities/PickTarget.cs`

Normalized classification of pickaxe-targetable objects.

```
PickTarget
  SmallRock      // standard rocks and small boulders
  LargeBoulder   // large boulders (requires Steel+)
  Meteorite      // meteorite (requires Gold+)
```

---

### `CapabilityMatrix` (static class)
**Location**: `Dayswork.Core/Capabilities/CapabilityMatrix.cs`

Static lookup table encoding the spec's Tool-inheritance table as pure threshold
comparisons. Called internally by `CapabilityEvaluator`; never instantiated.

Exposes:
```
CapabilityMatrix
  static bool CanChop(ToolLevel axeLevel, AxeTarget target)
  static bool CanBreak(ToolLevel pickLevel, PickTarget target)
```

See `business-logic-model.md` for the full threshold table.

---

### `ICapabilityEvaluator` / `CapabilityEvaluator`
**Location**: `Dayswork.Core/Capabilities/`

Interface + implementation following the project's standard DI pattern.
Receives `ToolSnapshot` and an `AxeTarget` or `PickTarget`; delegates to
`CapabilityMatrix` internally.

```
ICapabilityEvaluator
  bool CanChop(ToolSnapshot snap, AxeTarget target)
  bool CanBreak(ToolSnapshot snap, PickTarget target)
```

---

### `ITaskPriorityOrderer` / `TaskPriorityOrderer`
**Location**: `Dayswork.Core/Shifts/`

Interface + implementation. Accepts any subset of enabled `TaskKind` values
and returns them in the fixed FR-WORK-03 priority order.

```
ITaskPriorityOrderer
  IReadOnlyList<TaskKind> Order(IEnumerable<TaskKind> enabledTasks)
```

---

## Types used (not introduced here)

| Type | Introduced in | Role here |
|---|---|---|
| `TaskKind` | U-04 | Input to `TaskPriorityOrderer` |
| `TileCoord` | U-04 | Not directly used; `ShiftOrchestrator` (U-10) pairs tiles with ordered tasks |
