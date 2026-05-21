# U-14 — Output Pipeline: Business Logic Model

**Unit**: U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A
**Stories completed**: S-04 (orphan → mail), S-10 (multi-trip deposit + fallbacks + refund), S-11 (overflow mail)

This describes the *behaviour* U-14 adds, technology-agnostically. See [domain-entities.md](domain-entities.md) for the data shapes and [business-rules.md](business-rules.md) for the enforceable rules.

---

## The shape of the change

Today the worker collects drops into a flat, destination-blind buffer and, at shift end, walks to the shipping bin once and dumps everything. U-14 splits this into four cooperating responsibilities:

1. **Collection-time tagging** — every drop is buffered with the task that produced it.
2. **Deposit planning** (pure Core) — the buffer is grouped by resolved destination into an ordered set of deposit trips, plus a set of items that go straight to mail.
3. **Deposit execution** (orchestrator) — the worker walks each trip in order and deposits; anything that can't land (chest full, chest gone) joins the overflow set.
4. **Mail at shift end** — one no-fee letter delivers all overflow items next morning; a separate no-item letter reports tool-gated tasks.

The contract between (2) and (3): the planner decides *where to go and what to carry*; the orchestrator decides *what actually fits* once it sees the live game state.

---

## Flow 1 — Collection-time tagging (FD-Q1=A)

When the worker completes a task action and the game produces drops, the orchestrator already knows the current task (`_pendingTask`). Each drop is buffered as a `BufferedItem { QualifiedItemId, Quantity, SourceTask = _pendingTask }`.

- The buffer never stores an untagged item.
- The destination is **not** resolved here — only the producing task is recorded. Resolution is deferred to planning so the buffer stays a dumb, pure record and all routing logic lives in one PBT-testable place.
- This is the only change to the collection path; *what* gets collected (NFR-SAFE-04: only worker-caused drops) is unchanged from U-13B.

---

## Flow 2 — Deposit planning (C-11 DepositPlanner, pure)

Triggered when the shift ends (work list exhausted, 8pm cap, or stuck-step-3). Inputs: the buffer snapshot, the contract's `TaskDestinations`, the shipping-bin tile, the worker's current tile, and a Manhattan distance oracle.

**Step A — resolve each item's destination.** For each `BufferedItem`, look up `TaskDestinations[SourceTask]`:
- `ChestDestination(chestRef)` → walkable; grouped under that chest.
- `ShippingBinDestination` → walkable; grouped under the bin.
- `MailDestination`, **or the task is absent from the map** → not walkable; the item goes to `PreMailedOverflow` with reason `NoChestAssigned` (FD-Q2=A).

**Step B — consolidate.** Within each walkable destination, merge identical item ids into `ItemStack`s. One destination = one trip carrying all its items (FR-WORK-05: items from multiple tasks to the same chest are one trip).

**Step C — order the trips (FD-Q3=A).** Nearest-neighbor greedy: starting from the worker's current tile, repeatedly pick the unvisited destination whose tile is closest (by the supplied distance oracle), append it, and advance the "current" tile to it. Produces `DepositPlan.Trips` in walk order.

**Output**: `DepositPlan { Trips, PreMailedOverflow }`. Conservation holds: every buffered item is in exactly one trip or in `PreMailedOverflow`.

The planner is pure — no `Chest` resolution, no liveness check, no game state. Whether a chest still exists or is full is discovered later, in execution.

---

## Flow 3 — Deposit execution (M-12 ShiftOrchestrator, multi-trip)

The `Depositing` phase now drives **one deposit intent per trip** instead of a single shipping-bin trip. The state machine's transition table is unchanged: the orchestrator re-issues intents within `Depositing` via `SetIntent`, and transitions to `Exiting` only after the final trip (see [business-rules.md](business-rules.md) BR-SM-01).

Seed `ShiftContext.Overflow` with the plan's `PreMailedOverflow` (these are already known to be mail-bound). Then, for each trip in order:

**Shipping-bin trip** → issue `IntentDepositInShippingBin`, walk to the bin tile, deposit **all** items. The shipping bin has no capacity (FR-OUT-06), so nothing overflows.

**Chest trip** → issue `IntentDepositAtChest(chestRef)`, walk to the chest tile. On arrival, resolve the live chest via `ChestResolver.ResolveChest(chestRef)`:
- **Null (moved/destroyed)** → the worker "reaches the last known location and finds nothing"; move every item of this trip to `Overflow` with reason `ChestMissing` (S-10, FR-OUT-03). Continue to the next trip.
- **Live but cannot hold everything** → deposit as many items/stacks as fit; move the remainder to `Overflow` with reason `ChestFull` (S-10, FR-OUT-02).
- **Live with room** → deposit all.

After the last trip, transition `Depositing → Exiting`. If the plan had **no walkable trips** (everything was mail-bound, or the buffer was empty), the deposit phase is a pass-through straight to `Exiting` — the worker still walks to the entrance to leave.

All deposit trips run to completion **even past the 8pm cap** (FR-WORK-06): the cap ends *working*, not *depositing*. Items are never abandoned.

---

## Flow 4 — Exit, refund, and mail (shift end)

The exit/refund path is **unchanged** (FR-PAY-05): the worker walks to the farm entrance, the integer-clamped refund `deposit − (hoursWorked × rate)` is applied at exit, and deposit-run walking is not billed.

Immediately around exit, the orchestrator flushes the two letters via `MailDispatcher` (FD-Q4=A — queued for *tomorrow*, platform-persisted):

- **Overflow letter** — if `ShiftContext.Overflow` is non-empty, call `QueueOverflowMail(items, reasons)` exactly once. It carries the union of all overflow items (S-11) as MFM multi-item attachments, from sender "Your farmhand", no fee. The body lists each distinct reason present (FD-Q6=A): full chests, missing chests, unassigned tasks, and/or "ran out of time". If the only overflow is shipping-bin items — impossible, since the bin never overflows (FR-OUT-06) — no letter is sent.
- **Tool-missing warning** — if `ShiftContext.ToolMissingWarnings` is non-empty, call `QueueToolMissingWarning(skippedTasks)` once. This is a **vanilla**, no-item letter (FD-Q7=A), separate from the overflow letter, listing every task skipped for insufficient tool level.

Both letters are independent: a shift can produce zero, one, or both.

---

## Flow 5 — Player sleeps/saves mid-deposit (FD-Q5=A)

If `Saving` fires before the deposit run finishes, the worker is not at any chest and cannot complete its trips. The previous behaviour force-dumped the whole buffer into the shipping bin — wrong now that items are routed per task. Instead:

- Take all remaining `BufferedItem`s, add them to `Overflow` with reason `NotDelivered`, and queue the single overflow letter for next morning. **No shipping-bin dump.** This honours NFR-SAFE-01 (nothing lost) and respects the player's routing intent (rocks bound for a chest are not sold).
- The existing refund handling on save is unchanged (partial refund for a normally-ended shift; full refund for a genuine mid-day interruption).
- Already-`PreMailedOverflow` and any items already moved to `Overflow` during the run are included — the leftover set is simply "whatever is still buffered plus whatever already overflowed", mailed as one letter.

---

## End-to-end example (S-10 happy path)

Contract assigns: Harvest → chest C1, Clear rocks → chest C2, Clear weeds → shipping bin, Cut trees → (unassigned).

1. Worker collects parsnips (Harvest), stone (Clear rocks), fiber (Clear weeds), wood (Cut trees) — each buffered tagged with its task.
2. Planning resolves: parsnips→C1, stone→C2, fiber→bin, wood→mail (`NoChestAssigned`). Three walkable trips ordered nearest-first; wood seeded into overflow.
3. Execution walks the three trips: deposits into C1, C2, and the bin. Suppose C2 is full → leftover stone joins overflow (`ChestFull`).
4. Exit: refund applied at the entrance.
5. Mail next morning: one letter with wood + leftover stone; body notes "no chest assigned" and "chest was full".

This single shift exercises S-10 (multi-trip + chest-full fallback + refund) and S-11 (one no-fee letter, mixed reasons) together.
