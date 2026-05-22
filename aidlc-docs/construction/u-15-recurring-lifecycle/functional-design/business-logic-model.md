# U-15 — Recurring Lifecycle + Calendar Handlers: Business Logic Model

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers
**Stage**: CONSTRUCTION — Functional Design
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3→Clarification-1a=C (skip festivals **+ letter**), FD-Q4=B, FD-Q5=A, FD-Q6=C, FD-Q7=A, FD-Q8=C (+Clar-2a/2b/2c=A), FD-Q9=C (+Clar-3=A)
**Stories completed**: S-12 (deposit-deduction-each-morning + can't-afford → cannot-afford mail), S-14 (festival handling, rainy-day Water-Crops rate exclusion, empty-zone full refund), S-15 (early-sleep atomic fast-forward)

This describes the *behaviour* U-15 adds, technology-agnostically. See [domain-entities.md](domain-entities.md) for the data shapes and [business-rules.md](business-rules.md) for the enforceable rules. Deviations introduced here (DEV-U15-01..05) are catalogued in business-rules.md.

---

## The shape of the change

After U-14 the worker can run a one-time shift end-to-end and route its output. U-15 makes a contract *live on its own calendar*:

1. **Daily lifecycle** — each morning the scheduler decides, per contract, whether the worker shows up, charges the right deposit, and starts the shift (Service S-D, promoted from the U-10 one-time stub).
2. **Calendar awareness** — a new `CalendarHandlers` answers "is today a festival?" / "is today rainy?" and owns the at-save hook that drives the sleep fast-forward (Service S-C, M-14).
3. **Early-sleep fast-forward** — if the player sleeps before the worker finishes, the rest of the shift is settled instantly and atomically before the day rolls over.
4. **Mailed settlement** — refunds (and any undeliverable items) now come back by post the next morning instead of as an instant gold credit, for immersion (DEV-U15-04).

Three v1 simplifications/reversals are folded in: **one active contract at a time** (DEV-U15-01, resolves TODO-04), **missing tools degrade to the basic tool instead of skipping** (DEV-U15-03), and **festivals are skipped but the player gets a courtesy letter** (DEV-U15-02).

---

## Flow 1 — Morning lifecycle (M-13 RecurringContractScheduler, full)

Runs on `DayStarted`. Multiplayer-guarded (no-op in MP, REL-U10-01). With **one active contract** (DEV-U15-01), the loop processes at most one contract; the loop shape is retained so the invariant is enforced, not assumed.

For the day's due contract:

**Step 1 — Festival gate (DEV-U15-02, was FD-Q3).** If `CalendarHandlers.IsFestivalToday()`:
- The worker does **not** show up.
- **Recurring**: no deposit is deducted; the contract stays `Active` for the next non-festival day; a **text-only festival-notice letter** is queued ("off for the festival today").
- **One-time**: the already-paid deposit (FR-PAY-03) is **refunded by mail** (DEV-U15-04) and the contract is marked `Executed`; the festival-notice letter **carries the refund gold**.
- Either way a letter is sent — this is the deliberate deviation from FR-DAY-01's "no mail" clause.
- *(Sub-decision surfaced for approval: a one-time contract that lands on a festival is consumed (Executed) with a full mailed refund rather than rolled to the next non-festival day. Flagged in business-rules.md BR-CAL-03.)*

**Step 2 — Lock today's config (FR-PAY-08).** Snapshot the live `IConfigSnapshot` at this moment; today's deposit and refund use these rates regardless of later GMCM edits.

**Step 3 — Today's rate (FR-PAY-07, FD-Q4=B).** Compute the rate via `RateCalculator` passing `CalendarHandlers.IsRainyToday()` as the rain flag, which excludes the Water Crops surcharge on rainy days. **The Water Crops task is *not* removed from the enabled set** (DEV-U15-05): rain auto-waters outdoor crops (so the worker naturally finds none outdoors), but the task stays available for building-interior crops once that work lands (TODO-05).

**Step 4 — Estimate + deposit.** `HoursEstimator` → estimated hours; `DepositCalculator` → today's deposit using the locked config and today's rate.

**Step 5 — Affordability (FR-PAY-04, FD-Q5=A).** If `player gold < deposit`:
- Queue a **cannot-afford notice** (text-only) via `MailDispatcher.QueueCannotAffordNotice(...)`.
- Skip the shift; the contract stays `Active` and retries every morning. The notice is sent **each** unaffordable morning (no de-duplication).
- The worker resumes automatically the first morning the player can afford it.

**Step 6 — Deduct + start.** Deduct the deposit from `player gold` (FR-PAY-03) and call `ShiftOrchestrator.StartShift(contract)`.

**Tools are never a blocker (DEV-U15-03).** There is no tool-missing pre-check and no tool-missing warning; a missing tool is read as the lowest-tier tool inside `StartShift` (Flow 5). The empty-zone case is handled by the shift itself (Flow 2).

**One-time contracts** keep their existing handling: marked `Executed` before `StartShift` so a same-day reload can't re-fire; the festival branch (Step 1) is the only new pre-empt.

---

## Flow 2 — Empty-zone day (FD-Q6=C + FD-Q9=C)

A drawn zone may have no actionable objects on a given day. No special-casing in the scheduler:

1. The deposit is deducted at 6am (Flow 1 Step 6).
2. The worker spawns, its work-list build finds zero tasks, and it goes straight to exit.
3. At exit the refund equals the full deposit (`deposit − 0 hours`) and is **mailed** next morning (DEV-U15-04) — net zero, one-day lag.

This reuses the existing empty-shift → exit → refund path; only the refund delivery changed. *(FR-PAY-06)*

---

## Flow 3 — Calendar predicates (M-14 CalendarHandlers)

`CalendarHandlers` is a thin Mod-side adapter exposing two pure-ish predicates over live game state, plus the save hook (Flow 4):

- `IsFestivalToday()` — true on a festival calendar day. Consumed by Flow 1 Step 1.
- `IsRainyToday()` — true when today's weather is rain/storm. Consumed by Flow 1 Step 3 (rate) only.

The exact game-state sources (festival-day check, weather flags) are code-stage details; the predicates keep that knowledge in one place so the scheduler and orchestrator stay testable.

---

## Flow 4 — At-save hook + sleep fast-forward (Service S-C, FD-Q7=A, FD-Q2=A)

**Ownership (FD-Q7=A).** `CalendarHandlers` subscribes to `GameLoop.Saving` (`OnSavingHook`). `ShiftOrchestrator` **no longer subscribes to `Saving` directly**; instead it exposes an explicit `FastForwardAndSettle()` method that the hook calls. Required ordering inside the save: **fast-forward & settle → then persist contracts** (`ContractPersistenceAdapter.OnSaving`), so the settled state is captured and nothing races the day-rollover (FR-DAY-02).

**What the hook does.** If no shift is in flight, it is a no-op. Otherwise it branches on shift phase:

**(a) Worker still *working* (`ShiftEndTime` unset — phases Working / Recovering / Stuck): time-budgeted headless fast-forward (FD-Q2=A).**
1. Compute the remaining in-game window: from the current `timeOfDay` to the 8pm hard cap (`HardCapTime`), clamped ≥ 0.
2. Execute the remaining detected tasks **headlessly** — perform each task's action directly (remove the object, collect drops into the buffer) with **no walking or animation beats** — in the same priority/nearest-next order the live shift uses.
3. **Charge time per action**: each performed action consumes its estimated in-game-minutes against the remaining window. Stop when the window is exhausted (the worker would have hit the 8pm cap) **or** the work runs out. Unfinished work is simply left undone — a huge zone slept-on early is *not* magically completed.
4. Build and run the deposit plan against the **real** chests/bin exactly as a normal shift end (U-14 Flow 2/3): items land in live chests, leftovers accumulate as overflow.
5. Compute the refund from total hours worked (live + fast-forwarded) and **queue it as mailed gold** (DEV-U15-04); queue the settlement letter for any overflow (Flow 6).

**(b) Worker already *finished* working (`ShiftEndTime` set — phases Depositing / Exiting): U-14 interruption path, refund now mailed.**
- The work is done but the deposit walk didn't finish. As in U-14 BR-INT-01, all still-buffered items are mailed (reason `NotDelivered`); **no shipping-bin dump**. The partial refund is computed and **mailed** (DEV-U15-04) rather than credited directly.

Both branches settle entirely within the `Saving` event, before the contract segment is written and before the day rolls over (atomicity, FR-DAY-02).

---

## Flow 5 — Missing tools degrade to the basic tool (DEV-U15-03, FD-Q8=C + Clar-2)

Inside `StartShift`, `ToolLevelReader.ReadCurrent()` builds the `ToolSnapshot`. The single change: **a tool the player does not own is reported at the lowest tier (basic/starter) instead of "absent / level 0".**

Consequences via `CapabilityEvaluator`:
- The worker **never skips** a task for a *missing* tool, and **no tool-missing warning** is produced (Clar-2a). The whole tool-missing warning path is removed (Clar-2c) — `ShiftContext.ToolMissingWarnings` and `MailDispatcher.QueueToolMissingWarning` are deleted as dead code; U-14's BR-MAIL-05 no longer applies.
- **Tier gating for tools the player *does* own is unchanged** (Clar-2b): a player who owns only a basic pickaxe still can't break boulders/meteorites that need a higher tier, and fruit trees are still always-skip (FR-SKIP-03). Only the *missing* branch flips from "skip" to "basic tier".

This deviates from FR-TOOL-03 and removes the S-09 tool-missing skip/warning behaviour.

---

## Flow 6 — Mailed settlement: refunds and items by post (DEV-U15-04, FD-Q9=C + Clar-3)

Every refund — normal exit, sleep fast-forward, empty-zone full refund, one-time-festival refund — is delivered as **gold-bearing mail the next morning** instead of an instant credit at exit. The deposit still leaves `player gold` immediately at 6am (FR-PAY-03); only the *return* is mailed, giving a one-day cash-flow lag.

**One settlement letter per shift.** To avoid mailbox spam, a shift queues **at most one** end-of-shift settlement letter carrying whatever must come back:
- undeliverable **items** (the U-14 overflow set, with its reason-line body), and/or
- the **refund gold**.

The body covers whichever applies. If a shift has neither overflow nor a positive refund (e.g., the worker spent the entire deposit and delivered everything), **no letter** is sent.

**`IntentApplyRefund` changes meaning.** Where it used to do `player gold += refund` at exit, it now hands the refund amount to the settlement step to be mailed.

**Fallback (Clar-3=A).** If Mail Framework Mod can't cleanly attach money to a letter, the fallback is a text-only "here's your change" letter that credits the gold at the moment the letter is *collected* (still next morning, still immersive). Items always go through MFM's existing multi-attachment path (U-14).

The festival-notice (Flow 1) and cannot-afford (Flow 1 Step 5) letters are separate, pre-/no-shift letters; the one-time-festival letter additionally carries refund gold via the same mailed-refund mechanism.

---

## End-to-end example (recurring contract, rainy mid-week day, early sleep)

Recurring contract: Water Crops + Clear Weeds + Harvest, zone drawn, chest assigned for Harvest.

1. **6am**: not a festival. Today's config locked. Rainy → rate excludes the Water Crops surcharge (DEV-U15-05); Water Crops stays enabled but outdoor crops are already wet. Deposit computed and affordable → deducted; worker starts.
2. The worker clears weeds and harvests for a few in-game hours.
3. **10am**: player goes to sleep. `CalendarHandlers.OnSavingHook` fires → worker is mid-work → time-budgeted fast-forward runs the rest of the weed/harvest work against the 10am→8pm window, stops when work runs out, deposits harvest into the assigned chest, computes the refund.
4. **Next morning**: one settlement letter arrives with the refund gold (no overflow this time). Net cost = hours actually worked × rate. The recurring contract runs again that morning.
