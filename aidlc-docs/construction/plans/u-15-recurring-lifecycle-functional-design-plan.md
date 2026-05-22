# U-15 — Recurring Lifecycle + Calendar Handlers: Functional Design Plan

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers
**Stories**: S-12 (completes — deposit-deduction-each-morning + can't-afford → cannot-afford mail), S-14 (full — festival skip, rainy-day Water-Crops rate exclusion, empty-zone full refund), S-15 (full — player sleeps before the worker finishes; shift fast-forwards atomically)
**Phase**: CONSTRUCTION — Functional Design

---

## Plan Checklist

- [x] FD-Q1–Q9: Collect answers to design questions (Q1=A, Q2=A, Q3=C→Clar-1a=C, Q4=B, Q5=A, Q6=C, Q7=A, Q8=C, Q9=C)
- [x] Resolve any ambiguities/contradictions in answers (clarification round: 1a=C skip+letter, 2a/2b/2c=A, 3=A — see u-15-recurring-lifecycle-functional-design-clarification-questions.md)
- [x] Generate `business-logic-model.md`
- [x] Generate `domain-entities.md`
- [x] Generate `business-rules.md`
- [x] Present completion message and await approval — **APPROVED 2026-05-21**

---

## Context Summary

U-15 promotes the **one-time scheduler stub** into the full **daily recurring lifecycle** and adds the **calendar edge cases** (festivals, rain, empty zones) plus the **early-sleep fast-forward**. After this unit, a recurring contract just *runs* day after day on its own, and the player can go to bed before the worker finishes without losing items or refunds.

**Components owned (new files)**:
- **M-14 CalendarHandlers** (`Dayswork/Orchestration/`) — exposes `IsFestivalToday()` / `IsRainyToday()` to the scheduler and orchestrator, and subscribes to `GameLoop.Saving` to drive the sleep fast-forward (Service S-C "Sleep fast-forward sequence", Service S-D steps 2 & 4).

**Components extended**:
- **M-13 RecurringContractScheduler** — today (U-10 stub) it only fires shifts at `DayStarted` and marks one-time contracts Executed. U-15 adds the full per-recurring-contract daily sequence (Service S-D): festival skip (no deposit, no mail), today's config/rate (rain flag), hours estimate, deposit compute, can't-afford → mail + skip, deduct + StartShift, tool-missing warning.
- **M-12 ShiftOrchestrator** — today (U-14) `OnSaving` mid-work just refunds the full deposit and mails collected items. U-15 replaces that stub with the proper synthesized fast-forward that completes the remaining shift, deposits to real chests, applies the partial refund, and queues overflow mail — all before the day rolls over (FR-DAY-02).
- **i18n/default.json** — festival-skip log message, cannot-afford mail body (`mail.cannot_afford.*`), any rain/empty-zone log strings.

**What already exists that U-15 builds on**:
- `RecurringContractScheduler.OnDayStarted` — already multiplayer-guarded; already splits `OneTime` (mark Executed → StartShift) vs `Recurring` (StartShift, stay Active). U-15 inserts the deposit/festival/rain/affordability logic around the `Recurring` branch (and decides the one-time festival case per FD-Q3).
- `ContractStore.ListActiveForDate(day, season, year)` — returns the contracts due today.
- Pricing Core (U-05): `RateCalculator` already takes a **rainy-day flag** that excludes the Water Crops surcharge (FR-PAY-07 math); `HoursEstimator`, `DepositCalculator`, `RefundCalculator` are pure and already used by U-09/U-10.
- `ShiftOrchestrator.StartShift(contract)` — spawns the worker, snapshots tools, builds the work list, runs the shift. Already capable of an empty shift (no detected tasks → exits → refund), which is the empty-zone case (FR-PAY-06).
- `ShiftOrchestrator.OnSaving` (U-14) — currently force-handles the mid-shift save: `ShiftEndTime` set → mail undelivered + partial refund; unset → mail collected + full deposit refund. The U-14 in-code comment explicitly flags "proper sleep fast-forward is U-15."
- `ShiftContext` carries `DepositAmount`, `ComputeRefund()`, `Overflow`, the state machine, and the deposit plan; `FlushShiftMail()` queues the consolidated overflow letter; `AppendUndeliveredToOverflow()` moves still-buffered items into the overflow set.
- `MailDispatcher` (U-14) exposes `QueueOverflowMail(items, reasons)` and `QueueToolMissingWarning(tasks)`. **`QueueCannotAffordNotice(...)` does not yet exist** — U-15 adds it (text-only letter, same MFM/vanilla pattern as the tool-missing warning per DEV-U14-01).
- The 8pm hard cap (`OnTimeChanged` → `ClockReached8pm`) and the state machine's Depositing/Exiting flow already exist and are PBT-protected.

**Already decided / not in scope for questions**:
- **Recurring deposit timing** (FR-PAY-03): the daily deposit is deducted at 6am (`DayStarted`) on each contract day — not at hire time. Fixed.
- **Config lock-in** (FR-PAY-08): the day's deposit and refund use the config/rates in effect when that day began; GMCM changes apply starting the next morning. Fixed — the scheduler snapshots the live `IConfigSnapshot` at `DayStarted`.
- **Refund formula** (FR-PAY-05): unchanged — `refund = deposit − (hoursWorked × rate)`, integer-clamped (NFR-SAFE-02), applied at worker exit; deposit-run walking not billed.
- **Cancel-after-6am blocked** (FR-HIRE-15): already enforced in U-12's contract-management UI; U-15 does not re-implement it (it only delivers the *behavioral* S-12 clauses: daily deduction + can't-afford mail).
- **No new persisted save structure** for mail/refund: the fast-forward settles everything into *today's* live game state (gold, chests, queued mail) before save, mirroring U-14's "no custom mail save data" (DEV-U14-03). Festival/contract status changes persist through the existing `ContractStore` save segment.
- **No items lost** (NFR-SAFE-01) and **tolerate absent data** (NFR-SAFE-03) continue to hold across every new branch.

---

## Design Questions

### FD-Q1 — Concurrent / multiple active contracts on the same day (resolves TODO-04)

`RecurringContractScheduler.OnDayStarted` loops over **all** contracts due today and calls `ShiftOrchestrator.StartShift(contract)` for each. But `ShiftOrchestrator` manages a **single** worker (`_farmhand` / `_ctx`): a second `StartShift` in the same morning would clobber the first worker's state. So today, two active contracts on the same day silently break. TODO-04 reserved this decision for U-15 (recurring + one-time conflict; multiple recurring contracts targeting the same tasks/tiles). v1 needs a rule.

**A) v1 supports exactly one active contract at a time — enforce uniqueness at hire time (Recommended).** The hiring flow refuses to create a second contract while one is Active/Paused (or the bulletin board only offers "Hire" when none exists). The scheduler's loop then trivially handles ≤1 contract per day. Simplest, ships a correct v1, and defers true multi-worker/merge mechanics to a post-v1 unit. Documented as a deviation narrowing FR-HIRE behavior for v1.

**B) Allow multiple active contracts; each spawns its own independent worker that runs in parallel.** Closest to "hire a crew," but requires the orchestrator to manage a *collection* of concurrent workers (separate NPCs/Farmers, separate buffers, separate deposit runs, separate stuck/sleep handling) — a large expansion of U-10/U-13/U-13B scope inside U-15.

**C) Allow multiple active contracts but merge them into a single combined shift.** One worker; the shift's zones = union of all contracts' zones, tasks = union of enabled tasks, with a documented conflict rule when two contracts assign the same task to different chests (e.g., first-contract-wins by contract creation order). One deposit deduction *per contract* still applies. Avoids multi-worker rendering but adds non-trivial merge + conflict-resolution logic.

**D) Allow multiple active contracts but run them strictly sequentially with one worker (contract A fully, then contract B).** Each contract's deposit deducted at 6am; worker completes A's deposit + exit, then starts B. Simpler than parallel, but the second contract may not finish before the 8pm cap, and the model of "exit then re-enter" needs definition.

**E) Other (please describe after [Answer]: tag below)**

[Answer]: A

---

### FD-Q2 — Sleep fast-forward execution model and the 8pm cap (S-15 / FR-DAY-02)

When the player sleeps mid-shift, FR-DAY-02 says the worker "completes the rest of the shift off-screen instantly" at sleep-confirm: deposit run atomic, refund applied, overflow mail queued — all before the day rolls over. The worker isn't physically walking during a save event, so the fast-forward must execute work **headlessly** (perform each remaining task directly, skip walk/animation, collect drops), then run the existing deposit plan to real chests and apply the refund. The open question is whether the fast-forward respects the in-game **time budget** (player slept at, say, 10am → ~10 working hours until the 8pm cap).

**A) Time-budgeted headless fast-forward (Recommended).** Execute remaining detected tasks in priority order, charging each task's estimated in-game-minutes against the remaining window (sleep time → 8pm cap). Stop when work runs out **or** the cap is reached; then deposit, refund unused time (`deposit − hoursWorked × rate`), and queue overflow mail. Most faithful to what a real un-slept shift would have produced — a huge zone slept-on at 7am is *not* magically fully cleared.

**B) Complete-all headless fast-forward.** Instantly finish **all** remaining detected work regardless of the 8pm cap, then deposit, refund unused time, and queue overflow mail. Simpler (no per-task time accounting), and the player always gets the full zone done — but it can complete more work than the worker could have physically done before 8pm, slightly over-delivering.

**C) Other (please describe after [Answer]: tag below)**

[Answer]: A

---

### FD-Q3 — One-time contract whose scheduled day turns out to be a festival

FR-DAY-01 ("on festival days the worker does not show up; for recurring contracts the daily deposit is not deducted; no mail") is written around recurring contracts. But a **one-time** contract is scheduled for "next morning only" and that morning could be a festival. A one-time deposit was already deducted at confirmation (FR-PAY-03), so the festival skip needs a deposit answer.

**A) Festival skips every contract type; the one-time contract's already-paid deposit is fully refunded that morning and the contract is marked Executed (Recommended).** The worker never shows on a festival (consistent player-facing rule), and since zero work was done the player is made whole (FR-PAY-06 spirit). Recurring: no deposit taken, contract stays Active for the next non-festival day.

**B) Festival skip applies to recurring only; a one-time contract still runs on a festival day.** Honors the "deposit already committed, work it off" view, but means the worker is on the farm during a festival — odd, and the player can't attend with the farmhand mid-task. Contradicts the plain reading of FR-DAY-01.

**C) Other (please describe after [Answer]: tag below)**

[Answer]: C, don't skip festival days

---

### FD-Q4 — Rainy-day Water-Crops handling mechanism (FR-PAY-07)

On rainy days the Water Crops surcharge is excluded from the day's rate (handled by `RateCalculator`'s rain flag, which U-15 wires via `CalendarHandlers.IsRainyToday()`). The question is whether the worker should *also* actively drop Water Crops from the shift's task set, since rain auto-waters crops anyway.

**A) Both — exclude the surcharge from the rate AND remove Water Crops from the shift's enabled tasks on rainy days (Recommended).** Belt-and-suspenders: the rate is correct *and* the worker never wastes a beat trying to water already-wet crops. If Water Crops was the only enabled task, the shift is empty → worker doesn't show → full refund (FR-PAY-06), exactly matching "the worker still shows up only if any other task is enabled" (FR-PAY-07).

**B) Rate-only — drop the surcharge but leave Water Crops enabled.** Rely on the worker naturally finding no dry crops to water (rain wets them). Less code, but depends on live game watering state and could leave the worker briefly scanning for nonexistent watering work.

**C) Other (please describe after [Answer]: tag below)**

[Answer]: B, even on rainy days, the worker might need to water plants in a building

---

### FD-Q5 — Can't-afford recurring contract: contract state and mail repetition (FR-PAY-04 / S-12)

When the player can't afford a recurring contract's daily deposit at 6am, the worker doesn't show and a cannot-afford mail is sent. The contract presumably keeps trying on later mornings (it's "recurring"). The open question is the day-after-day behavior.

**A) Stay Active; retry every morning; send the cannot-afford mail each day the deposit is unaffordable (Recommended).** Simplest and spec-literal (FR-PAY-04 sends "a mail notification" per occurrence). The worker resumes automatically the first morning the player can afford it again.

**B) Stay Active and retry, but de-duplicate the mail — send the cannot-afford notice only once and not again until the player has afforded a day in between.** Same retry behavior, gentler mailbox. Costs a small "already-warned" flag (in-memory for the run, or persisted).

**C) Auto-pause the contract after the first can't-afford morning; the player must manually Resume from the bulletin board.** Stops silent daily retries, but adds a state transition and the player might forget to resume.

**D) Other (please describe after [Answer]: tag below)**

[Answer]: A

---

### FD-Q6 — Empty-zone recurring day: deduct-then-refund vs pre-scan-and-skip (FR-PAY-06)

A recurring contract's drawn zone may have no actionable objects on a given day (everything already cleared/harvested). FR-PAY-06 says the player is "fully refunded (effectively no charge)." Two ways to reach net-zero:

**A) Deduct the day's deposit at 6am, spawn the worker who finds no tasks, immediately exits, and the normal exit refund returns the full deposit — net zero (Recommended).** Reuses the existing empty-shift → exit → refund path (already works in U-10). No special-casing in the scheduler; the worker simply has a very short, empty day. Refund = deposit − 0 = deposit.

**B) Pre-scan the zone at 6am; if there are zero actionable objects, skip the deposit deduction and don't spawn the worker at all.** No money ever moves and no worker appears, but it duplicates the worker's task-detection logic in the scheduler (and the scan can disagree with the live shift's detection), adding a second source of truth for "is there work."

**C) Other (please describe after [Answer]: tag below)**

[Answer]: C, deduct the day's deposit at 6am, spawn worker, exits, and mail refund

---

### FD-Q7 — Who triggers the sleep fast-forward, and the Saving-handler ordering (S-C / S-D / FR-DAY-02)

`GameLoop.Saving` currently runs two Dayswork handlers: `ContractPersistenceAdapter.OnSaving` (writes the contract save segment) and `ShiftOrchestrator.OnSaving` (the U-14 mid-shift stub). Service S-A/S-C specify that the **new CalendarHandlers** subscribes to `Saving` and drives the fast-forward. FR-DAY-02 requires the fast-forward's refund + mail to land in *today's* state **before** the day rolls over and before the contract segment is persisted.

**A) CalendarHandlers owns the Saving hook and calls a `ShiftOrchestrator.FastForwardAndSettle()` method; ordering is fast-forward → mail/refund/deposit settle → persist contracts (Recommended).** Matches Service S-A's wiring (`Saving → CalendarHandlers.OnSavingHook`) and S-C, centralizes calendar/sleep logic in M-14, and turns the orchestrator's old `OnSaving` into an explicit method invoked in a guaranteed order. The orchestrator stops subscribing to `Saving` directly.

**B) Keep `ShiftOrchestrator.OnSaving` subscribed directly and just upgrade its body from the U-14 stub to the full fast-forward; CalendarHandlers only exposes the `IsFestivalToday()/IsRainyToday()` predicates.** Smaller change, but splits the "what happens at save" responsibility away from M-14 (contradicts S-A/S-C) and relies on SMAPI handler registration order to keep fast-forward before persistence.

**C) Other (please describe after [Answer]: tag below)**

[Answer]: A

---

### FD-Q8 — Tool-missing warning on recurring shifts (FR-TOOL-03 / S-D step 9)

`ShiftContext.ToolMissingWarnings` is populated during every shift's work-list build (U-13) and dispatched by `MailDispatcher.QueueToolMissingWarning(...)` at shift end (U-14). Since each recurring day runs an ordinary shift, that flow already fires for recurring contracts. S-D step 9 lists tool-missing warning explicitly, so U-15 should confirm the scope.

**A) No additional U-15 work — the existing per-shift queue (U-13) + dispatch (U-14) already covers recurring shifts; U-15 just verifies it (Recommended).** Each recurring morning spawns a normal shift, so a missing tool produces the same next-morning warning as a one-time contract. Mark FR-TOOL-03/S-D-step-9 satisfied by reuse.

**B) Add a recurring-specific pre-check in the scheduler: if every enabled task is gated by a missing tool (the worker would do nothing), skip spawning and send only the tool-missing warning (no empty shift).** Avoids spawning a worker that can do nothing, but duplicates capability evaluation in the scheduler and adds a second code path for the warning.

**C) Other (please describe after [Answer]: tag below)**

[Answer]: C, Missing tools doesn't matter. The NPC worker defaults to the lowest tier of tool if it's missing from the player farmer.

---

### FD-Q9 — Refund delivery mechanism: direct gold at exit vs. mailed gold next morning

Today the unused deposit is credited **directly to player gold at worker exit** (`Game1.player.Money += refund`), per FR-PAY-05. You raised mailing the refund back instead for immersion — "the farmhand sends your change with the morning post." Stardew mail is a *next-morning* mechanism (same-day delivery means fighting the framework and is moot for the sleep case anyway), so "mailed refund" means the gold arrives in the next morning's mail. This is a **deviation from FR-PAY-05** and touches the already-built exit-refund path in `ShiftOrchestrator` (U-10/U-14), which fires on *every* shift — not only U-15 — but U-15 is the natural home since it is already building the cannot-afford/overflow mail machinery.

Trade-offs: mailing introduces a **one-day cash-flow lag** (deposit paid today, change back tomorrow), but it **simplifies the sleep fast-forward** (FD-Q2: the save-time path no longer mutates gold, it just queues a letter) and reads consistently with the overflow/cannot-afford letters this unit adds.

**A) Keep direct gold credit at exit (current FR-PAY-05 behavior).** No deviation, no lag, no new mail. Refund appears instantly when the worker leaves; the sleep fast-forward still mutates gold during the save event.

**B) Mail the refund as gold-bearing mail (next morning) for the normal shift-exit refund only.** The everyday refund arrives by post; the sleep fast-forward also queues it (uniform). Empty-zone full refund (FD-Q6) and the one-time-festival refund (FD-Q3) keep direct crediting so the rarer make-whole refunds stay instant.

**C) Mail the refund as gold-bearing mail (next morning) for ALL refund cases — normal exit, sleep fast-forward, empty-zone full refund, and one-time-festival refund (Recommended for immersion).** Fully consistent "all settlement comes by mail" model. Maximizes immersion and uniformity; the one-day lag applies to every refund including the full make-whole cases.

**D) Other (please describe after [Answer]: tag below)**

[Answer]: C, we are not skipping festival days though

**Sub-note (answer only if B or C):** A gold-bearing letter needs a money attachment. Vanilla mail supports this natively; MFM money support will be confirmed at code time. If MFM cannot cleanly attach money, the fallback is a text-only "here is your change" letter that credits the gold at the moment it is *collected* (still next-morning, still immersive). Is that fallback acceptable?  [Answer]:

---

## Artifact output (after answers collected)

- `aidlc-docs/construction/u-15-recurring-lifecycle/functional-design/business-logic-model.md`
- `aidlc-docs/construction/u-15-recurring-lifecycle/functional-design/domain-entities.md`
- `aidlc-docs/construction/u-15-recurring-lifecycle/functional-design/business-rules.md`
