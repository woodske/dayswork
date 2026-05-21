# U-14 — Output Pipeline (Multi-Destination Deposit + Overflow Mail): Functional Design Plan

**Unit**: U-14 — Output Pipeline: Multi-Destination Deposit + Overflow Mail
**Stories**: S-04 (completes — orphaned-chest fallback fires: orphan → mail), S-10 (completes — multi-trip deposit, 8pm-cap-still-deposits, chest-full fallback, chest-destroyed fallback, refund at exit), S-11 (completes — overflow-mail flow, shipping-bin-no-overflow case)
**Phase**: CONSTRUCTION — Functional Design

---

## Plan Checklist

- [x] FD-Q1–Q7: Collect answers to design questions (Q1=A, Q2=A, Q3=A, Q4=A, Q5=A, Q6=A, Q7=A)
- [x] Resolve any ambiguities/contradictions in answers (all answers consistent; no clarification needed)
- [x] Generate `business-logic-model.md`
- [x] Generate `domain-entities.md`
- [x] Generate `business-rules.md`
- [ ] Present completion message and await approval

---

## Context Summary

U-14 completes the **output story**. Until now the worker has collected drops into a flat buffer and dumped *everything* into the shipping bin in a single trip. U-14 turns that into real per-task routing: items go to the chest the player assigned, anything that can't be delivered is mailed the next morning with no fee, and "no items are ever lost" (NFR-SAFE-01) becomes a real guarantee.

**Components owned (new files)**:
- **C-11 DepositPlanner** (`Dayswork.Core/Inventory/`) — pure planner: `(buffer snapshot, task→destination map, distance oracle) → ordered list of DepositTrip`. One trip per unique destination, items consolidated, trips ordered to minimize total walking distance.
- **M-16 MailDispatcher** (`Dayswork/Integration/`) — adapter over the **Mail Framework Mod (MFM)** API. Sends the single overflow letter (multi-item attachment) and the no-item warning letters (tool-missing). Reads body strings from `I18nHelper`; single sender label "Your farmhand".

**Components extended**:
- **C-08 ShiftStateMachine** — the `Depositing` phase must now drive **multiple** deposit trips (today it issues exactly one `IntentDepositInShippingBin`).
- **C-10 ItemBuffer** — must now associate each buffered item with the task/destination that produced it (today `Add(itemId, qty)` is destination-blind). *(Note: the component matrix listed C-10 as not-extended; reality requires this extension — to be recorded as a deviation in the FD artifacts.)*
- **M-12 ShiftOrchestrator** — replace the single shipping-bin trip with planner-driven trips; dispatch new deposit intents; resolve each chest via `ChestResolver`; route chest-full / chest-missing / unassigned remainders to `MailDispatcher`.
- **ShiftContext** — must carry the contract's `TaskDestinations` map (today it carries Zones / EnabledTasks but **not** TaskDestinations).
- **ShiftIntent** — add `IntentDepositAtChest(ChestRef)` alongside the existing `IntentDepositInShippingBin`.
- **manifest.json** — add MFM as a required `Dependencies` entry (V9).
- **i18n/default.json** — mail bodies + sender label (`mail.sender`, `mail.overflow.*`, `mail.warning.tool_missing`).

**What already exists that U-14 builds on**:
- `Contract.TaskDestinations : IReadOnlyDictionary<TaskKind, DestinationKey>` — the per-task assignment map, set at hire time. `DestinationKey` is a sum type: `ChestDestination(ChestRef)`, `ShippingBinDestination`, `MailDestination`.
- `ChestResolver.ResolveChest(ChestRef) → Chest?` — returns the live chest, or **null** if it was moved/destroyed (exactly the chest-missing signal U-14 needs). Owned by U-11.
- `ItemBuffer` — flat `Add(itemId, qty)` / `TakeAll()` / `Snapshot()`; populated by the orchestrator while it knows the current `_pendingTask`.
- `ShiftStateMachine` — multi-successor table; `Depositing → {Exiting}`; active phases must carry an intent; illegal transitions throw (PBT-protected). Intents can be re-issued within a phase via `SetIntent`.
- `ShiftOrchestrator.BeginDeposit()` → single `IntentDepositInShippingBin`, walk to `ShippingBinTile`, `HandleDeposit` dumps the whole buffer into the bin, then `Exiting`.
- `ShiftOrchestrator.OnSaving` — if the player sleeps mid-cleanup, the buffer is currently force-dumped into the shipping bin and the partial refund applied.
- `ShiftContext.ToolMissingWarnings : HashSet<TaskKind>` — already populated in U-13's `BuildWorkList`; the in-code comment says it is "read by U-14 to send the warning mail".
- Refund is computed (`ShiftContext.ComputeRefund`, integer-clamped per NFR-SAFE-02) and applied at the moment of exit; deposit-run time is not billed.

**Already decided / not in scope for questions**:
- **Refund timing/formula is unchanged** (FR-PAY-05): refund = `deposit − (hoursWorked × rate)`, applied at worker exit, deposit-run walking not billed. U-14 does not touch this.
- **Shipping bin never overflows** (FR-OUT-06): shipping-bin-destined items always deposit fully; they never generate mail.
- **Chest-full is a partial deposit** (S-10): deposit as many items/stacks as physically fit; the remainder stays buffered for mail. (The fit-checking mechanism — vanilla `addItem` leftover — is a code-stage concern; the *rule* "as many as fit, remainder mailed" is fixed here.)
- **Exactly one overflow letter per shift** carrying *all* leftover items (S-11 Gherkin) — the open question is only about the letter's *body* when reasons are mixed (FD-Q6), not the count.
- **MFM is the mail carrier** for item-bearing letters (V9 decision A); no-item warnings use vanilla mail. MFM UniqueID/min-version confirmed at code-generation time.
- **Can't-afford mail (FR-PAY-04) is U-15, not U-14.** U-14's MailDispatcher must expose a clean enough interface for U-15 to reuse, but U-14 only sends overflow + tool-missing mail.
- **No item filtering / sorting within a task** (FR-OUT-07): all of a task's drops go to that task's single destination.
- **NFR-SAFE-04 unchanged**: the worker only collects drops it caused; U-14 changes routing, not what gets collected.

---

## Design Questions

### FD-Q1 — How should each buffered item be associated with its destination?

Routing items to different chests requires knowing, per buffered item, where it should go. Today `ItemBuffer.Add(itemId, qty)` records nothing about the producing task or destination — everything is indistinguishable and dumped into the shipping bin. The orchestrator *does* know the current task (`_pendingTask`) at the moment it buffers a drop, so the association can be captured at collection time.

**A) Tag each buffered item with its producing `TaskKind`; the `DepositPlanner` resolves `TaskKind → DestinationKey` via the contract's `TaskDestinations` map at deposit time (Recommended).** Matches the `components.md` planner signature `(snapshot, assignmentMap) → trips`. Keeps the buffer a dumb record of "what was collected, for which task"; all destination/grouping logic lives in one pure, PBT-testable planner. Chest *liveness* (moved/destroyed) is still resolved later by the orchestrator via `ChestResolver`, so storing the static assignment is safe.

**B) Resolve `TaskKind → DestinationKey` at collection time and key the buffer directly by `DestinationKey`.** The buffer becomes `Add(itemId, qty, DestinationKey)`; the planner just groups already-resolved entries. Slightly simpler planner, but the buffer now needs the contract's assignment map at collection time and the "what task produced this" information is lost (harder to diagnose/log).

**C) Other (please describe after [Answer]: tag below)**

[Answer]: A

---

### FD-Q2 — Default destination for an output-producing task that has no assignment

`Contract.TaskDestinations` may not contain an entry for every enabled output task — the player is allowed to leave a task's output unassigned (S-04: "A task with output but no assigned destination is allowed"). FR-OUT-04 says such output should be **buffered and mailed** the next morning. But the early thin-slice (U-09) defaulted unassigned output to the *shipping bin*. U-14 must pick the v1 rule.

**A) Unassigned output → mail next morning (Recommended).** A task in `EnabledTasks` but absent from `TaskDestinations` (or explicitly set to `MailDestination`) resolves to mail, per FR-OUT-04 and the S-04 acceptance bullet. The shipping bin is used only when the player *explicitly* chose `ShippingBinDestination`.

**B) Unassigned output → shipping bin.** Treat a missing assignment as "send to the shipping bin" (the U-09 stub behavior). Simpler for the player (gold appears automatically) but contradicts FR-OUT-04 and means items the player expected in a chest silently get sold.

**C) Other (please describe after [Answer]: tag below)**

[Answer]: A

---

### FD-Q3 — Deposit-trip ordering heuristic (minimize total walking distance)

`DepositPlanner` produces one trip per unique destination and orders them to minimize total walking distance (FR-WORK-05). Destination counts are tiny in practice (a handful of chests plus maybe the shipping bin). S-10 says the order "minimizes total walking distance but is otherwise unspecified," and the unit's planned test is a sanity check, not a hard property.

**A) Nearest-neighbor greedy from the worker's position when deposits begin (Recommended).** Repeatedly pick the closest not-yet-visited destination. Consistent with U-13's existing nearest-next work routing (DEV-02), cheap, and good-enough for the small destination counts.

**B) Exact optimal via brute-force permutation.** Since N is tiny (typically ≤ 4–5 destinations), evaluate all orderings and pick the true minimum. Optimal but more code for a difference the player will rarely notice.

**C) Fixed deterministic order (e.g., chests by tile, then shipping bin).** Simplest and fully deterministic; ignores distance entirely.

**D) Other (please describe after [Answer]: tag below)**

[Answer]: A

---

### FD-Q4 — When is the overflow letter queued, and how does it survive the day rollover?

Leftover buffered items (chest full / chest missing / unassigned) must arrive in the player's mailbox the **next morning**. The worker exits the same evening, before the player sleeps, so the leftover set is known at shift end. The question is whether to lean on Stardew/MFM's native "deliver tomorrow" persistence or build our own.

**A) Queue one "deliver-tomorrow" MFM letter at shift end (when the worker finishes its deposit run / exits), and let Stardew + MFM persist and deliver it next morning (Recommended).** No custom Dayswork save data; mirrors how the can't-afford warning is intended to work. The leftover buffer is consumed into the letter at exit.

**B) Persist a pending-overflow record in Dayswork's own save data at shift end; a `DayStarted` handler builds and sends the letter next morning.** Fully under our control and decoupled from MFM's queuing semantics, but adds a new persisted structure (and its own round-trip/SAFE-03 obligations) for something the platform already does.

**C) Other (please describe after [Answer]: tag below)**

[Answer]: A

---

### FD-Q5 — Player sleeps/saves before the worker finishes depositing: where do leftover items go?

Today `OnSaving` force-dumps the *entire* remaining buffer into the shipping bin if the player sleeps mid-cleanup. With per-task routing that's wrong — rocks bound for a chest would be sold. The worker isn't physically at any chest when `Saving` fires, so it can't run a real deposit walk.

**A) Mail the entire leftover buffer next morning as the single overflow letter; no shipping-bin dump (Recommended).** Safe (NFR-SAFE-01), respects assignments, and uniform with the normal overflow path. The player simply receives the items by mail rather than finding rocks in the bin.

**B) Best-effort remote deposit into still-live assigned chests/bin first, then mail the remainder.** Closer to player intent, but "depositing" into a chest the worker never reached is a fiction and complicates the model.

**C) Keep current behavior — dump everything into the shipping bin on save.** Simplest, but breaks per-task routing for the interrupted case and can sell items the player wanted in a chest.

**D) Other (please describe after [Answer]: tag below)**

[Answer]: A

---

### FD-Q6 — Overflow letter body when several reasons apply in one shift

S-11 mandates exactly one letter carrying all leftover items, but a single shift can mix reasons: one chest was full, another was destroyed, a third task had no chest assigned. The letter body should explain why items are attached.

**A) One letter whose body lists each reason that applied this shift (Recommended).** e.g., a short body that conditionally includes "Some chests were full.", "Some chests no longer exist.", "Some tasks had no chest assigned." Clearest for the player; costs a few extra i18n lines and simple body assembly.

**B) One letter with a single generic body regardless of reason.** e.g., "I couldn't deliver everything, so I've sent it to you here." Minimal i18n, but less informative about what went wrong.

**C) Other (please describe after [Answer]: tag below)**

[Answer]: A

---

### FD-Q7 — Tool-missing warning mail (consuming `ShiftContext.ToolMissingWarnings`)

U-13 records tasks skipped because the player's tool level was too low (`ToolMissingWarnings`). U-14's `MailDispatcher` is where that warning is finally sent. It carries **no items**, so it uses vanilla mail rather than MFM.

**A) One combined warning letter per shift listing all skipped task kinds, delivered next morning via vanilla mail, separate from the overflow letter (Recommended).** Keeps item-bearing and warning-only mail cleanly separated; one tidy notice even if several tasks were skipped.

**B) One warning letter per skipped task kind.** More granular, but can flood the mailbox when multiple tasks are gated.

**C) Fold the tool-missing notice into the overflow letter's body when both occur (one letter total when possible).** Fewer letters, but couples a no-item warning to the item letter and needs a fallback when there is no overflow that day.

**D) Other (please describe after [Answer]: tag below)**

[Answer]: A

---

## Artifact output (after answers collected)

- `aidlc-docs/construction/u-14-output-pipeline/functional-design/business-logic-model.md`
- `aidlc-docs/construction/u-14-output-pipeline/functional-design/domain-entities.md`
- `aidlc-docs/construction/u-14-output-pipeline/functional-design/business-rules.md`
