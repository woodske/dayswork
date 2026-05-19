# U-05 Pricing Core — NFR Design Patterns

**Unit**: U-05 — Pricing Core
**NFRs addressed**: NFR-SAFE-02, NFR-MAINT-03, PBT-03, PBT-07

---

## Pattern 1: Gold Integrity via Ceiling-Clamp Arithmetic

**Addresses**: NFR-SAFE-02 — "No gold is ever lost beyond the contractually-billed hourly rate × hours worked."

### Problem

Floating-point multiplication of `rate × hours` is not exact. A rounding error of ±1g could:
- Create gold from nothing (`refund > deposit` by 1g due to rounding)
- Charge more than billed (`deposit - refund > rate × hoursWorked` by 1g)

Both directions violate NFR-SAFE-02.

### Solution: Ceiling-then-Clamp

**Step 1 — Ceiling rounding on every billable amount**:

```
deposit  = (int)Math.Ceiling(rate * estimatedHours)
billable = (int)Math.Ceiling(rate * hoursWorked)
```

Ceiling always rounds toward the mod's interest (slightly more upfront, slightly more billed). The deposit ceiling at hire time guarantees `deposit ≥ exact billable` for any `hoursWorked ≤ estimatedHours`, because:

```
rate * hoursWorked ≤ rate * estimatedHours
Ceiling(rate * hoursWorked) ≤ Ceiling(rate * estimatedHours) + 1   (rounding can add at most 1)
```

The +1 rounding edge case is absorbed by Step 2.

**Step 2 — Clamp the refund**:

```
refund = Math.Clamp(deposit - billable, 0, deposit)
```

- Lower bound `0`: if `billable > deposit` by at most 1g (rounding), refund is 0 instead of -1 — no gold created
- Upper bound `deposit`: if `billable = 0` (zero hours worked), refund = deposit — full refund, no overshoot

### Invariants satisfied (PBT-03 verified)

```
refund ∈ [0, deposit]                              -- always
deposit - refund ≤ Math.Ceiling(rate × hoursWorked) -- net charge bounded
refund == deposit when hoursWorked == 0.0           -- full refund on empty day
```

### Where applied

| Location | Application |
|---|---|
| `DepositCalculator.Calculate()` | `Math.Ceiling(rate * estimatedHours)` |
| `RefundCalculator.Calculate()` | `Math.Ceiling(rate * hoursWorked)` for billable; `Math.Clamp` for refund |

---

## Pattern 2: Pure Function Isolation

**Addresses**: NFR-MAINT-03 — "Pure business-logic modules are separated from SMAPI/game-engine integration so they can be unit-tested without launching Stardew."

### Problem

If pricing logic calls `Game1.player.Money` or `Game1.IsRainingHere()` directly, tests require a running Stardew instance. SMAPI mods can't easily be unit-tested without the game loaded.

### Solution: Dependency injection of pure inputs

All game-state that influences pricing is extracted *before* calling any calculator and passed as a plain value:

| Game state | Extracted as | Extracted by |
|---|---|---|
| Player's tool levels | `IConfigSnapshot` (rate table) | `ConfigDefaults` / GMCM adapter (U-16) |
| Enabled tasks | `IEnumerable<TaskKind>` | `HiringFlowCoordinator` (U-09) |
| Rain status | `bool isRaining` | `CalendarHandlers.IsRainyToday()` (U-15) |
| Zone rectangles | `IEnumerable<Zone>` | `ZoneAndChestMenu` selection (U-11) |
| Actual hours worked | `double hoursWorked` | `ShiftOrchestrator` timer (U-10) |

The calculators themselves receive only value types and interfaces with no game-engine dependencies. They are instantiated with `new RateCalculator()` in tests — no SMAPI `IModHelper` needed.

### Enforcement mechanism

`Dayswork.Core.csproj` references only `Newtonsoft.Json`. Any accidental `using StardewValley;` or `using StardewModdingAPI;` in `Dayswork.Core/Pricing/` causes a **compile error** — enforcement is automatic and cannot be bypassed.

### Interface boundary diagram

```
[SMAPI / Game Engine]
        |
        | extracts plain values
        v
[ModEntry / Coordinators / Handlers]   <- Dayswork project
        |
        | passes: TaskKind[], Zone[], IConfigSnapshot, bool, double
        v
[IRateCalculator / IHoursEstimator]    <- Dayswork.Core/Pricing/
[IDepositCalculator / IRefundCalculator]
        |
        | returns: int, double, DepositResult
        v
[ModEntry / Coordinators / Handlers]   <- Dayswork project
        |
        | applies: deduct gold, spawn worker, apply refund
        v
[SMAPI / Game Engine]
```

---

## Pattern 3: Typed Discriminated Union for Error Signaling

**Addresses**: Q5 lower:B — degenerate contract detection; NFR-MAINT-03 (explicit over implicit)

### Problem

`DepositCalculator` returns `0` when `estimatedHours ≤ 0`. If the return type is `int`, callers have no way to distinguish "0g deposit for a valid contract" from "no zones configured at all." They might silently proceed with a degenerate contract.

### Solution: `DepositResult` discriminated union

```csharp
public abstract record DepositResult
{
    public sealed record Positive(int Amount) : DepositResult;
    public sealed record Zero : DepositResult;
}
```

The C# compiler enforces exhaustive handling:

```csharp
var result = depositCalculator.Calculate(estimatedHours, rate);
switch (result)
{
    case DepositResult.Positive p:
        // normal flow: deduct p.Amount from player gold
        break;
    case DepositResult.Zero:
        // show "no deposit needed" message or block confirmation
        break;
}
```

Without this type, callers would need to check `amount == 0` after the fact — easily forgotten. With it, the compiler prevents silent omission.

This pattern is the same as the `DestinationKey` hierarchy from U-04 (`ChestDestination`, `ShippingBinDestination`, `MailDestination`), following the established codebase convention.

---

## Summary: patterns × NFRs

| Pattern | NFR-SAFE-02 | NFR-MAINT-03 | PBT-03 | PBT-07 |
|---|---|---|---|---|
| Ceiling-Clamp Arithmetic | Primary | — | Verified by | — |
| Pure Function Isolation | — | Primary | Enabled by | Enabled by |
| Discriminated Union | Secondary (explicit handling) | Supporting | — | — |
