# U-07 — Business Logic Model

## CapabilityEvaluator

### `CanChop(ToolSnapshot snap, AxeTarget target) → bool`

Delegates to `CapabilityMatrix.CanChop(snap.AxeLevel, target)`.

#### Axe capability table (from spec §Tool inheritance)

| AxeTarget     | Basic (0) | Copper (1) | Steel (2) | Gold (3) | Iridium (4) |
|---------------|-----------|------------|-----------|----------|-------------|
| StandingTree  | true      | true       | true      | true     | true        |
| SmallStump    | true      | true       | true      | true     | true        |
| LargeStump    | false     | false      | true      | true     | true        |
| LargeLog      | false     | false      | false     | true     | true        |
| FruitTree     | false     | false      | false     | false    | false       |

Threshold rules encoded in `CapabilityMatrix.CanChop`:
- `FruitTree` → always `false` (FR-SKIP-03 hard rule; no threshold)
- `StandingTree` | `SmallStump` → `axeLevel >= Basic` (always `true` when tool owned)
- `LargeStump` → `axeLevel >= Steel` (>= 2)
- `LargeLog` → `axeLevel >= Gold` (>= 3)

---

### `CanBreak(ToolSnapshot snap, PickTarget target) → bool`

Delegates to `CapabilityMatrix.CanBreak(snap.PickaxeLevel, target)`.

#### Pickaxe capability table (from spec §Tool inheritance)

| PickTarget    | Basic (0) | Copper (1) | Steel (2) | Gold (3) | Iridium (4) |
|---------------|-----------|------------|-----------|----------|-------------|
| SmallRock     | true      | true       | true      | true     | true        |
| LargeBoulder  | false     | false      | true      | true     | true        |
| Meteorite     | false     | false      | false     | true     | true        |

Threshold rules encoded in `CapabilityMatrix.CanBreak`:
- `SmallRock` → `pickLevel >= Basic` (always `true` when tool owned)
- `LargeBoulder` → `pickLevel >= Steel` (>= 2)
- `Meteorite` → `pickLevel >= Gold` (>= 3)

---

### Tasks not evaluated by CapabilityEvaluator

The following tasks either require no tool-level gate or are handled elsewhere:

| TaskKind                | Reason not evaluated |
|-------------------------|----------------------|
| WaterCrops              | Watering can works at any level (spec §Tool inheritance) |
| HarvestCrops            | No tool-level gate; readiness filter is Mod-layer concern (FR-SKIP-05) |
| CollectFruit            | No tool-level gate |
| FeedAnimals             | No tool; uses silo inventory |
| PetAnimals              | No tool |
| CollectAnimalProducts   | No tool |
| ClearWeeds              | Scythe at any level; no capability gate |
| ClearGrass              | Scythe at any level; no capability gate |

`CutTrees` and `ClearRocks` are the only tasks that route through `CapabilityEvaluator`.

---

## TaskPriorityOrderer

### `Order(IEnumerable<TaskKind> enabledTasks) → IReadOnlyList<TaskKind>`

Returns only the enabled tasks (input subset), in the fixed FR-WORK-03 priority order.
Tasks not present in the input are absent from the output — not included with any default.

#### FR-WORK-03 priority table

| Priority | TaskKind                |
|----------|-------------------------|
| 0        | FeedAnimals             |
| 1        | PetAnimals              |
| 2        | CollectAnimalProducts   |
| 3        | WaterCrops              |
| 4        | HarvestCrops            |
| 5        | CollectFruit            |
| 6        | ClearWeeds              |
| 7        | ClearGrass              |
| 8        | ClearRocks              |
| 9        | CutTrees                |

#### Sort guarantee

The sort is **stable and deterministic**: the same input set always produces the same
output order. No secondary sort key is needed — each `TaskKind` has exactly one
priority rank and duplicates are not valid input.

#### Edge cases

| Input | Output |
|---|---|
| All 10 tasks enabled | All 10 in FR-WORK-03 order |
| Single task | That task (list of 1) |
| Empty set | Empty list |
| Unknown `TaskKind` value | Implementation throws `ArgumentOutOfRangeException` (defensive; should not occur in practice) |
