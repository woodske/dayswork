# U-05 Pricing Core — Functional Design Plan

**Unit**: U-05 — Pricing Core
**Stage**: Functional Design
**Status**: Answers collected — generating artifacts

**Context loaded**:
- [unit-of-work.md](../../inception/application-design/unit-of-work.md) — U-05 definition
- [requirements.md](../../inception/requirements/requirements.md) — FR-PAY-01..09, NFR-SAFE-02
- [source-spec.md](../../inception/source-spec.md) — pricing model section
- [u-04-code-summary.md](../U-04-geometry-domain-primitives/code/u-04-code-summary.md) — TaskKind, TileCoord, Zone available
- [u-03 config](../U-03-config-foundation/) — IConfigSnapshot available

## What this unit produces

Four pure classes in `Dayswork.Core/Pricing/`:

| Component | Responsibility |
|---|---|
| C-01 RateCalculator | Computes the hourly rate given the enabled task set and rain flag |
| C-02 DepositCalculator | Computes the upfront deposit: `rate × estimatedHours` |
| C-03 RefundCalculator | Computes the refund at shift end: `deposit − (hoursWorked × rate)` |
| C-04 HoursEstimator | Estimates hours from zone tile count, task count, and a speed constant |

All four are stateless, have no SMAPI references, and accept `IConfigSnapshot` for their configuration values.

## Plan Checklist

- [x] Collect and analyze answers to all questions below
- [x] Resolve any ambiguities in follow-up
- [x] Generate `domain-entities.md`
- [x] Generate `business-logic-model.md`
- [x] Generate `business-rules.md`

---

## Questions

### Q1 — HoursEstimator formula structure

The spec says estimated hours depend on "zone size, number of tasks, and a configurable average speed constant." Three plausible formulas:

A) **Sum-of-tiles, task-multiplicative** — `estimatedHours = (totalTiles × numTasks × avgMinutesPerTilePerTask) / 60`
   - Each task adds a full pass over the zone tiles
   - Total work scales linearly with both tile count and task count
   - e.g., 100 tiles, 3 tasks, 1.5 min/tile/task → 100 × 3 × 1.5 / 60 = 7.5 hrs

B) **Shared-tile-pass** — `estimatedHours = (totalTiles × avgMinutesPerTile) / 60`, where `avgMinutesPerTile` already bakes in multi-task overhead via a single configurable constant
   - Simpler: the worker covers the zone once, doing all tasks as it goes
   - Task count is not an explicit factor

C) **Per-task density** — `estimatedHours = sum over enabled tasks of (zonesWithThatTask.TileCount × taskSpeedConstant[task]) / 60`
   - Each task has its own configurable speed constant (faster to water than to chop)
   - Per-task constants stored in `IConfigSnapshot` alongside per-task rates

D) **(Recommended)** **Single constant, tile × task additive** — `estimatedHours = (totalZoneTiles × numEnabledTasks × avgMinutesPerTileTask) / 60`
   - One configurable constant `avgMinutesPerTileTask` (default e.g. 2 min/tile/task)
   - Same as A but uses a single shared constant instead of per-task constants
   - Easy to tune; adds more tasks → deposit rises proportionally

[Answer]: D — `estimatedHours = (totalTiles × numEnabledTasks × AverageSpeedConstant) / 60`, single shared constant

---

### Q2 — "Hours" unit in the formula

Stardew Valley's in-game clock runs fast: roughly 0.75 real seconds per in-game minute. The pricing formula uses "hours" — which clock should this be?

A) **In-game hours** (most natural for lore) — the rate is in g per in-game hour; `estimatedHours` is in in-game hours; deposit = rate × estimatedHours both in in-game time. The player sees "~6 in-game hours" which corresponds to the shift from 6am to noon.

B) **(Recommended) Real-time hours** — the rate is g per real hour; `estimatedHours` is expressed in real-time hours. Simpler to reason about for the developer; the speed constant is in real minutes per tile.

C) **In-game minutes** stored internally, displayed as hours — the formula computes in in-game minutes and divides by 60 for display and billing. Rate denominator is in-game hours but the unit is cosmetic.

[Answer]: B — Real-time hours; AverageSpeedConstant unit = real minutes per tile per task

---

### Q3 — Integer arithmetic and rounding (NFR-SAFE-02)

NFR-SAFE-02 requires "integer-clamped" gold values to avoid floating-point leakage. Three approaches:

A) **All-integer, ceiling estimation** — store rates as `int` (g/hr), estimated hours as `int` (whole hours only, ceiling), deposit = `rate × hours` as `int`. Crude but never loses gold.

B) **(Recommended) Integer rates, decimal hours, rounded deposit** — rates are `int` g/hr; `estimatedHours` is a `double` (can be 2.5 hrs); deposit = `(int)Math.Ceiling(rate × estimatedHours)`; refund = `Math.Clamp(deposit - (int)Math.Ceiling(rate × hoursWorked), 0, deposit)`. NFR-SAFE-02's "no gold leakage" is satisfied by always rounding up the billable amount and clamping refund to [0, deposit].

C) **Integer rates, integer fixed-point hours (×100)** — estimated hours stored as `int` in hundredths-of-an-hour (e.g., 250 = 2.5 hrs); arithmetic is `rate × hours / 100`. Avoids floating-point entirely; requires a wrapper type.

[Answer]: B — Integer rates (int), double estimated hours, Math.Ceiling for deposit, refund clamped to [0, deposit]

---

### Q4 — RateCalculator rain handling

FR-PAY-07 says that on rainy days the Water Crops rate increment is excluded. How should `RateCalculator` receive this information?

A) **Caller pre-filters** — the caller (e.g. `RecurringContractScheduler`) removes `TaskKind.WaterCrops` from the enabled task set before calling `RateCalculator`. The calculator never knows about rain.

B) **(Recommended) Calculator takes `bool isRaining`** — `RateCalculator.Calculate(IEnumerable<TaskKind> enabledTasks, IConfigSnapshot config, bool isRaining)`. Internally, if `isRaining` is true, Water Crops contribution is excluded even if it's in `enabledTasks`. This keeps the rain-exclusion rule co-located with rate logic, easier to test.

C) **Separate overload** — two methods: `Calculate(tasks, config)` and `CalculateRainyDay(tasks, config)`. Same as B but without a parameter.

[Answer]: B — Calculate(enabledTasks, config, bool isRaining); calculator handles Water Crops exclusion internally

---

### Q5 — Refund clamping edge cases

The refund formula is `deposit - (hoursWorked × rate)`. NFR-SAFE-02 requires the result is clamped to `[0, deposit]`. Two edge cases to clarify:

**Upper bound**: Can `hoursWorked × rate > deposit`? (Would mean the worker worked more hours than estimated — in practice shouldn't happen since the worker stops when done or at 8pm, but could happen due to rounding.)

**Lower bound**: Can deposit = 0? FR-PAY-06 says zero estimated hours yields zero deposit. In that case, `refund = 0 - (0 × rate) = 0`. That's fine. But is a 0-deposit contract allowed to proceed to confirmation, or is there a minimum deposit enforced by the UI (not by the calculator)?

Please choose one option for each:

**Upper bound**:
- A) Clamp: `refund = Math.Clamp(deposit - billable, 0, deposit)` always — safe even if billing would exceed deposit
- B) Assert: throw/log error if `hoursWorked × rate > deposit + 1` (rounding tolerance); never silently eat the overrun

**Lower bound**:
- A) 0-deposit is valid; calculator returns 0 for both deposit and refund; UI layer decides whether to block or allow
- B) 0-deposit should be blocked at the calculator level (return an error or a special result)

[Answer]: Upper A (always clamp silently); Lower B (DepositCalculator returns special Zero result when estimatedHours <= 0)

---

### Q6 — HoursEstimator input: raw zone area vs. task-filtered tile count

At hire time the player draws zone rectangles before the shift runs. The actual number of actionable tiles is unknown until the worker scans during the shift. HoursEstimator must estimate blindly.

A) **(Recommended) Raw zone area** — `totalTiles` = sum of all Zone rectangle areas (`width × height` per Zone). Simple; the player drew the zone, they implicitly understand overstating. The speed constant calibration accounts for typical actionable density.

B) **Actionable fraction heuristic** — `totalTiles` = zone area × a configurable `estimatedActionableFraction` constant (e.g. 0.6 = "60% of tiles in a typical zone have something to do"). Adds one more config knob.

C) **Task-specific tile count** — player sees separate estimated hours per task based on typical density. Complex; deferred to post-v1.

[Answer]: A — Raw zone area; totalTiles = sum of (zone.Width * zone.Height) per Zone
