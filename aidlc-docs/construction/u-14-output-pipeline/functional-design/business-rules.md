# U-14 — Output Pipeline: Business Rules

**Unit**: U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A

Enforceable rules for U-14. Each cites its source requirement/story/decision. See [business-logic-model.md](business-logic-model.md) for the flows and [domain-entities.md](domain-entities.md) for the types.

---

## Buffering & destination resolution

**BR-OUT-01 — Every drop is tagged with its producing task.** When the worker collects a drop, it is buffered as a `BufferedItem` carrying the current `TaskKind`. No item is ever buffered untagged. *(FD-Q1=A)*

**BR-OUT-02 — Destination resolution.** A buffered item's destination is `TaskDestinations[SourceTask]`:
- `ChestDestination` or `ShippingBinDestination` → that target.
- key absent, or `MailDestination` → **mail next morning**, reason `NoChestAssigned`.
*(FD-Q2=A, FR-OUT-04, S-04)*

**BR-OUT-03 — One trip per unique walkable destination.** The planner groups all items sharing a resolved walkable destination (a specific chest, or the shipping bin) into a single `DepositTrip`; items from multiple tasks bound for the same chest deposit in one trip. *(FR-WORK-05)*

**BR-OUT-04 — Trip ordering minimizes walking.** Trips are ordered by nearest-neighbor greedy from the worker's tile at deposit start, using a Manhattan distance oracle. The exact order is otherwise unspecified. *(FD-Q3=A, FR-WORK-05, S-10)*

---

## Deposit execution

**BR-OUT-05 — Shipping bin never overflows.** A shipping-bin trip always deposits every item; it can never generate overflow or mail. *(FR-OUT-06)*

**BR-OUT-06 — Missing chest → mail.** A chest is resolved (`ChestResolver.ResolveChest`) when the worker arrives at its tile. If it returns null (chest moved/destroyed), every item of that trip moves to overflow with reason `ChestMissing`, and the worker continues other trips normally. *(FR-OUT-03, S-04 orphan case, S-10)*

**BR-OUT-07 — Full chest → partial deposit + mail remainder.** If a live chest cannot hold all of a trip's items, the worker deposits as many as physically fit and moves the remainder to overflow with reason `ChestFull`. *(FR-OUT-02, S-10)*

**BR-OUT-08 — Unassigned output is never walked.** Items resolved to mail (BR-OUT-02) are seeded into the overflow set during planning and never produce a deposit trip. *(FD-Q2=A, FR-OUT-04)*

**BR-OUT-09 — Deposits complete past the 8pm cap.** Reaching the 8pm cap ends *working*, not *depositing*. All planned deposit trips run to completion; items are never abandoned. *(FR-WORK-06, NFR-SAFE-01)*

**BR-OUT-10 — No item filtering within a task.** All of a task's drops go to that task's single resolved destination; no sorting/splitting by item type. *(FR-OUT-07)*

---

## State machine

**BR-SM-01 — Depositing drives zero-or-more trips, then exits.** Within the `Depositing` phase the orchestrator issues one deposit intent per trip via `SetIntent` (`IntentDepositAtChest` for chests, `IntentDepositInShippingBin` for the bin) and transitions `Depositing → Exiting` only after the final trip. With no walkable trips, `Depositing` is a pass-through to `Exiting`. The transition table and its PBT invariants (never leaves `Done`; illegal transitions throw) are otherwise unchanged. *(extends C-08; FR-WORK-05/06)*

**BR-SM-02 — New intent type.** `IntentDepositAtChest(ChestRef)` is added to the intent set; it is an active-phase intent (carries the target chest). *(extends ShiftIntent)*

---

## Refund & exit (unchanged, restated for completeness)

**BR-OUT-11 — Refund at exit only.** The integer-clamped refund `deposit − (hoursWorked × rate)` is applied when the worker exits via the farm entrance; deposit-run walking time is not billed. U-14 does not alter this. *(FR-PAY-05, NFR-SAFE-02)*

---

## Mail

**BR-MAIL-01 — Exactly one overflow letter per shift.** If the overflow set is non-empty at shift end, exactly one letter is queued carrying the union of all overflow items as MFM multi-item attachments. If overflow is empty, no overflow letter is sent. *(S-11, FR-OUT-05, V9)*

**BR-MAIL-02 — No fee, no penalty.** The overflow letter charges nothing and applies no rate adjustment. *(S-11, FR-OUT-05, NFR-SAFE-02)*

**BR-MAIL-03 — Sender label.** All farmhand mail is from "Your farmhand", i18n-routed (`mail.sender`). *(S-11, NFR-UX-02)*

**BR-MAIL-04 — Body lists each applicable reason.** The overflow letter body conditionally includes one line per distinct `OverflowReason` present this shift (`ChestFull`, `ChestMissing`, `NoChestAssigned`, `NotDelivered`). *(FD-Q6=A, S-11)*

**BR-MAIL-05 — Tool-missing warning is a separate vanilla letter.** If `ToolMissingWarnings` is non-empty at shift end, one combined no-item letter is queued via vanilla mail listing every tool-gated task; it is independent of the overflow letter. *(FD-Q7=A, FR-TOOL-03, S-09 carryover)*

**BR-MAIL-06 — Queued for tomorrow; platform-persisted.** Both letters are queued for next-morning delivery at shift end and rely on Stardew/MFM persistence; Dayswork keeps no custom mail save data. *(FD-Q4=A)*

---

## Safety / interruption

**BR-SAFE-01 — No items lost (conservation).** Every item the worker collects is either deposited (chest or bin) or mailed. The union of {deposited} ∪ {overflow-mailed} always equals {collected}. This is the unit's primary PBT obligation on the planner and the deposit loop. *(NFR-SAFE-01)*

**BR-INT-01 — Sleep before deposits finish → mail everything.** If the player sleeps/saves before the deposit run completes, all still-buffered items are mailed next morning with reason `NotDelivered`; nothing is force-dumped into the shipping bin. The existing on-save refund behaviour (partial on normal end, full on genuine mid-day interruption) is unchanged. *(FD-Q5=A, NFR-SAFE-01)*

---

## Dependencies / configuration

**BR-DEP-01 — MFM is a required dependency.** `manifest.json` declares Mail Framework Mod as a required `Dependencies` entry (UniqueID + minimum version confirmed at code generation). Item-bearing mail is sent through MFM's multi-attachment API; no-item warnings use vanilla mail. *(V9, FR-OUT-05, NFR-COMPAT-04)*

---

## PBT obligations (Property-Based Testing extension — enabled)

| Rule | Property |
|---|---|
| PBT-03 (conservation) | For any buffer + assignment map, the multiset of items across `DepositPlan.Trips[*].Items` ∪ `PreMailedOverflow` equals the input snapshot. *(BR-SAFE-01)* |
| PBT-03 (trip count) | `Trips.Count` equals the number of distinct walkable destinations present in the buffer. *(BR-OUT-03)* |
| PBT-03 (no empty trips) | No `DepositTrip` has an empty `Items` list; no trip targets `MailDestination`. *(BR-OUT-03/08)* |
| Sanity (not a hard property) | Nearest-neighbor ordering never produces a longer total route than the trivial as-grouped order for small N. *(BR-OUT-04 — unit test, per unit-of-work.md)* |

Security Baseline extension is **disabled** project-wide (no network/PII/auth surface); all its rules are N/A for U-14.
