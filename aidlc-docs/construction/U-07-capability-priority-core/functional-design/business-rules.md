# U-07 — Business Rules

## Capability Rules

### BR-CAP-01 — Fruit trees are always skipped (FR-SKIP-03)
`CapabilityEvaluator.CanChop(snap, FruitTree)` returns `false` regardless of
`snap.AxeLevel`. This is a hard-coded rule with no configuration toggle in v1.
It is implemented as an unconditional `return false` branch in `CapabilityMatrix.CanChop`
before the level threshold check, so no axe level can ever override it.

### BR-CAP-02 — Axe level thresholds (FR-SKIP-01, FR-TOOL-02)
- Basic/Copper (0–1): can chop standing trees and small stumps; cannot chop large stumps or large logs.
- Steel (2+): additionally can chop large stumps.
- Gold/Iridium (3+): additionally can chop large logs.

### BR-CAP-03 — Pickaxe level thresholds (FR-SKIP-02, FR-TOOL-02)
- Basic/Copper (0–1): can break small rocks; cannot break large boulders or meteorites.
- Steel (2+): additionally can break large boulders.
- Gold/Iridium (3+): additionally can break meteorites (all rocks).

### BR-CAP-04 — Tool absent defaults to Basic (FR-TOOL-03)
If the player does not own a tool (sold it), the Mod layer (`ToolLevelReader`, U-10)
stores `ToolLevel.Basic` (0) in the snapshot. `CapabilityEvaluator` is unaware of
absence — it only sees the level. The Mod layer separately records tool absence and
queues the mail warning in U-13.

**Consequence**: a player who sold their axe gets `AxeLevel = Basic`, so the worker
can still chop standing trees and small stumps but not large stumps/logs. The mail
warning informs the player their axe was missing.

### BR-CAP-05 — Watering can has no capability gate
`CapabilityEvaluator` is never called for `WaterCrops` tasks. Any watering can level
(including Basic) enables full watering. Rain exclusion is handled by
`RecurringContractScheduler` (U-15), not here.

### BR-CAP-06 — Tool snapshot is locked for the shift (FR-TOOL-01)
`ToolSnapshot` is built once at 6am and passed to `CapabilityEvaluator` for the
entire shift. Mid-day tool changes by the player do not affect the worker's capability.

### BR-CAP-07 — Trellis and crop-readiness skips are not evaluated here (FR-SKIP-04, FR-SKIP-05)
These require runtime game state (pathfinding reachability, crop growth stage) that
Core cannot access. The `ShiftOrchestrator` (U-10, Mod layer) filters trellis and
unready crops before any call to `CapabilityEvaluator`.

---

## Priority Rules

### BR-PRI-01 — Fixed priority order (FR-WORK-03)
Task execution order is fixed and non-configurable in v1:
FeedAnimals → PetAnimals → CollectAnimalProducts → WaterCrops → HarvestCrops →
CollectFruit → ClearWeeds → ClearGrass → ClearRocks → CutTrees.

### BR-PRI-02 — Only enabled tasks are returned
`TaskPriorityOrderer.Order` returns a list containing only the tasks present in the
input. Disabled tasks are absent from output, not represented with any placeholder.

### BR-PRI-03 — Stable, deterministic sort
Same enabled task set always produces identical output. No randomness or
secondary sort key.

### BR-PRI-04 — Unknown TaskKind values throw
An unrecognized `TaskKind` value passed to `Order` throws `ArgumentOutOfRangeException`.
This is a defensive rule: in normal operation all values come from the `TaskKind` enum
and this case should never occur.
