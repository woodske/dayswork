# U-14 — Output Pipeline: Domain Entities

**Unit**: U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A

This file defines the data shapes U-14 introduces or extends. All types described here are technology-agnostic (Core `Dayswork.Core` types). SMAPI/Stardew classes (`Chest`, mail APIs) are named only to anchor the model and live behind the orchestrator/dispatcher seams.

---

## Existing types reused (no change)

| Type | Shape | Role in U-14 |
|---|---|---|
| `TaskKind` (enum) | Water / Harvest / CollectFruit / ClearWeeds / ClearGrass / ClearRocks / CutTrees / … | The producing task of every collected drop; the key into `TaskDestinations`. |
| `DestinationKey` (sum type) | `ChestDestination(ChestRef)` \| `ShippingBinDestination` \| `MailDestination` | The resolved target for a task's output. |
| `ChestRef` | `(string LocationName, TileCoord Tile)` | Location + tile of an assigned chest; resolved to a live chest at deposit time. |
| `Contract.TaskDestinations` | `IReadOnlyDictionary<TaskKind, DestinationKey>` | The per-task assignment map set at hire time. Absent key ⇒ unassigned ⇒ mail (FD-Q2=A). |
| `TileCoord` | `(int X, int Y)` | Used by the distance oracle and trip tiles. |

---

## New / extended types

### `BufferedItem` (extends C-10 ItemBuffer — FD-Q1=A)

Replaces the current flat `(string itemId, int quantity)` tuple. Each buffered drop now records the task that produced it, so the planner can resolve its destination later.

```
BufferedItem
  QualifiedItemId : string     // Stardew qualified item id (per V6)
  Quantity        : int        // > 0
  SourceTask      : TaskKind   // the task whose action produced this drop
```

**Invariants**: `Quantity > 0`; `QualifiedItemId` non-empty; `SourceTask` is always set (no untagged items).

**ItemBuffer interface change**: `Add(string itemId, int quantity, TaskKind sourceTask)`; `Snapshot()` / `TakeAll()` return `IReadOnlyList<BufferedItem>`. *(Recorded as a deviation: the component matrix listed C-10 as not-extended; U-14 must extend it to carry `SourceTask`.)*

---

### `ItemStack` (consolidation unit)

A destination-agnostic quantity of one item, used inside trips and overflow sets after consolidation.

```
ItemStack
  QualifiedItemId : string
  Quantity        : int   // > 0; same itemId summed during consolidation
```

---

### `DepositTrip` (output of C-11 DepositPlanner)

One physical visit to one walkable destination, carrying every item bound for it.

```
DepositTrip
  Destination : DestinationKey     // ChestDestination | ShippingBinDestination only (never MailDestination)
  Tile        : TileCoord          // ChestRef.Tile, or the shipping-bin tile
  Items       : IReadOnlyList<ItemStack>   // consolidated; non-empty
```

**Invariants**: `Destination` is never `MailDestination` (mail items are not walked to); `Items` non-empty (no empty trips planned).

---

### `DepositPlan` (return of `IDepositPlanner.Plan(...)`)

The complete routing result for a shift's buffer.

```
DepositPlan
  Trips             : IReadOnlyList<DepositTrip>   // ordered nearest-neighbor (FD-Q3=A)
  PreMailedOverflow : IReadOnlyList<ItemStack>     // items whose destination resolved to MailDestination (FD-Q2=A)
```

**Conservation invariant (PBT)**: the multiset of `(itemId, qty)` across `Trips[*].Items` ∪ `PreMailedOverflow` equals the multiset of the input buffer snapshot. Nothing is created, dropped, or duplicated.

**Trip-count invariant (PBT)**: `Trips.Count` equals the number of *distinct walkable destinations* present in the buffer (each unique chest + the shipping bin if used).

---

### `OverflowReason` (enum)

Why an item ended up mailed rather than deposited. Drives the letter body (FD-Q6=A).

| Value | Cause | Body line (i18n) |
|---|---|---|
| `NoChestAssigned` | Task had no assignment ⇒ `MailDestination` (FD-Q2=A). | `mail.overflow.no_chest_assigned` |
| `ChestFull` | Assigned chest could not hold all items (FR-OUT-02). | `mail.overflow.chest_full` |
| `ChestMissing` | Assigned chest was moved/destroyed; `ChestResolver.ResolveChest` returned null (FR-OUT-03). | `mail.overflow.chest_missing` |
| `NotDelivered` | Player slept/saved before the deposit run finished (FD-Q5=A). | `mail.overflow.not_delivered` |

---

### `OverflowItem` (accumulated during deposit execution)

The orchestrator collects these as trips fail or partially fail; the union becomes the single overflow letter.

```
OverflowItem
  Stack  : ItemStack
  Reason : OverflowReason
```

The **mail letter** carries the union of all `OverflowItem.Stack`s (S-11: exactly one letter, all items). The distinct set of `OverflowItem.Reason`s selects which body lines appear (FD-Q6=A).

---

### `IDepositPlanner` (C-11 interface)

```
IDepositPlanner
  Plan(
    snapshot     : IReadOnlyList<BufferedItem>,
    assignments  : IReadOnlyDictionary<TaskKind, DestinationKey>,
    shippingBin  : TileCoord,                  // representative tile for the bin
    workerStart  : TileCoord,                  // worker position when deposits begin
    distance     : Func<TileCoord, TileCoord, int>   // pure distance oracle (Manhattan in v1)
  ) : DepositPlan
```

Pure: no Stardew references, no static/game state. The `distance` oracle keeps Core free of pathfinding while letting the orchestrator supply Manhattan (consistent with U-13's nearest-task routing, DEV-02).

---

### `IMailDispatcher` (M-16 interface)

Designed so U-15 can reuse it for the can't-afford letter without reshaping it.

```
IMailDispatcher
  // MFM, multi-item, delivered next morning, no fee (S-11). One call per shift.
  QueueOverflowMail(items : IReadOnlyList<ItemStack>, reasons : IReadOnlySet<OverflowReason>)

  // Vanilla mail, no items, delivered next morning. One call per shift when warnings exist (FD-Q7=A).
  QueueToolMissingWarning(skippedTasks : IReadOnlySet<TaskKind>)
```

Both queue **for tomorrow** at shift end and rely on platform persistence (FD-Q4=A) — no Dayswork-owned mail save data.

---

### Shift state additions

| Holder | Field | Purpose |
|---|---|---|
| `ShiftContext` | `TaskDestinations : IReadOnlyDictionary<TaskKind, DestinationKey>` | Threaded from the contract at `StartShift`; consumed by the planner. (New — context does not carry it today.) |
| `ShiftContext` | `Overflow : List<OverflowItem>` | Accumulates undeliverable items across the deposit run; flushed to one letter at exit / on save. |
| `ShiftIntent` | `IntentDepositAtChest(ChestRef Chest)` | New intent, issued once per chest trip alongside the existing `IntentDepositInShippingBin`. |

`ShiftContext.ToolMissingWarnings` (already present) is read once at exit to drive `QueueToolMissingWarning`.
