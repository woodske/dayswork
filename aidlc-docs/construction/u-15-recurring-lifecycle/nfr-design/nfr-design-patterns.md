# U-15 — NFR Design Patterns

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers

U-15 changes only the **calendar / scheduler / at-save / refund-delivery seams**. The entire worker behavioural loop (U-10/U-13/U-13B) and the U-14 deposit/overflow-mail seam are retained, except where Patterns S/T/U touch them. New patterns Q–U continue the A–P sequence.

---

## Retained unchanged (from U-10/U-13/U-13B/U-14)

- **Throttled-Tick** (÷4), **Manual Path-Follow Movement** (G), **Farmer-as-Worker Rendering** (F), **Save-Exclusion Guard** (I), **Stuck Detection + 3-Step Escalation** (D/E), **Invulnerability + Swing Emote** (H), **Pure Tool Map + Mod Swing** (K), **Once-Per-Shift Scan**, **Invoke-and-Poll** task effects, **Core-Purity Guard**.
- **Collection-Time Task Tagging** (L), **Pure Deposit Planner** (M), **Multi-Trip Deposit Loop** (N) — reused as-is by the normal shift end. Sleep-stop settlement (Pattern S) mails already-collected undelivered items rather than running a sleep-time deposit plan.
- The refund **math** (integer-clamped, deposit-run unbilled) is unchanged; only its *delivery* moves to mail (Pattern U).

The **Overflow Accumulator** (O) and **Mail Adapter over MFM** (P) are extended by Pattern U (refund gold folded into the single settlement letter), not replaced.

---

## Pattern Q — Calendar Predicate Adapter
**Satisfies**: FD-Q3→Clar-1a=C, FD-Q4=B, BR-CAL, BR-DAY-05, REL-U15-01, MAINT-U15-01, SAFE-U15-04

`M-14 CalendarHandlers` (Mod, `Dayswork/Orchestration/`) is a thin adapter exposing two constant-time predicates over live Stardew state — `IsFestivalToday()` and `IsRainyToday()` — plus the at-save hook (Pattern S). It is the single place that reads festival/weather state, keeping the scheduler and orchestrator free of direct game lookups and easy to reason about. A predicate that cannot determine state returns the **safe default** (non-festival / non-rain) and logs (REL-U15-01 / SAFE-U15-04). `IsRainyToday()` feeds only the rate flag (DEV-U15-05 — the Water Crops task is **not** removed); `IsFestivalToday()` drives the festival gate in Pattern R.

---

## Pattern R — Morning Lifecycle Guard Chain
**Satisfies**: FD-Q1=A, FD-Q5=A, FD-Q6=C, BR-CTR-01/02, BR-DAY-*, BR-AFF-*, BR-EMP-01, REL-U15-03/05, PERF-U15-01

`M-13 RecurringContractScheduler.OnDayStarted` becomes an ordered **fail-safe guard chain**, run once per day for the single due contract (the single-active-contract invariant, DEV-U15-01, is enforced upstream at hire time so the scheduler never reconciles multiples):

1. **Multiplayer guard** → no-op in MP (REL-U10-01).
2. **Festival gate** (Pattern Q) → if festival: skip the shift; recurring = no deposit + text-only festival letter (stays Active); one-time = mark Executed + mailed refund letter (Pattern U). Return.
3. **Config lock** → snapshot the live `IConfigSnapshot` (FR-PAY-08).
4. **Rain-aware rate** → `RateCalculator` with `IsRainyToday()` (surcharge excluded; task kept — DEV-U15-05).
5. **Estimate + deposit** → `DepositHoursPolicy` (current flat 1.0-hour preview policy) → `DepositCalculator`.
6. **Affordability gate** → if `gold < deposit`: queue cannot-afford notice (Pattern U), skip, stay Active, retry tomorrow (each unaffordable day mails again — FD-Q5=A). Return.
7. **Deduct + start** → deduct gold, `ShiftOrchestrator.StartShift(contract)`.

Each gate either short-circuits with a queued letter or falls through; no gate aborts the day (REL-U15-03). The **empty-zone** case (FD-Q6=C) needs no gate — the started shift finds no tasks, exits, and refunds via Pattern U (BR-EMP-01). There is **no tool gate** (DEV-U15-03): missing tools are handled inside the shift (Pattern T).

---

## Pattern S — Ordered At-Save Sleep-Stop Settlement Hook
**Satisfies**: FD-Q7=A, DEV-U15-09, BR-FF-01..05, REL-U15-02, SAFE-U15-01/05, FR-DAY-02

**Ownership & ordering (FD-Q7=A).** `CalendarHandlers.OnSavingHook` is the sole driver of the sleep settlement. `ShiftOrchestrator` no longer subscribes to `GameLoop.Saving`; it exposes `StopForSleepAndSettle()`. ModEntry registers the `Saving` handlers in a guaranteed order: **`CalendarHandlers.OnSavingHook` (stop + settle) → `ContractPersistenceAdapter.OnSaving` (persist)**, so settlement lands in today's state before the contract segment is written and before day-rollover (atomicity, BR-FF-02 / REL-U15-02).

**Branch by phase.** No shift in flight → no-op. Otherwise:
- **(a) Mid-work (`ShiftEndTime` unset).** *Hard stop for v1*: flush pending debris from already-performed actions, set `ShiftEndTime` to the sleep time, perform no remaining task actions, run no sleep-time deposit plan, move already-collected undelivered items to settlement mail, compute the refund, and settle via Pattern U. Remaining world tasks stay in the world.
- **(b) Already-finished (`ShiftEndTime` set).** The U-14 interruption path: remaining buffer → Overflow (`NotDelivered`), no bin dump; settle via Pattern U.

**Bounded one-time cost.** The save hook no longer mutates remaining work tiles or runs the deposit planner, so sleep settlement is bounded by collected buffer/trip contents rather than by the remaining zone size.

---

## Pattern T — Lowest-Tier Tool Fallback (+ warning-path removal)
**Satisfies**: FD-Q8=C, Clar-2a/2b/2c, BR-TOOL-01/02/03, DEV-U15-03, MAINT-U15-02

The single behavioural change: `ToolLevelReader.ReadCurrent()` maps a tool the player **does not own** to the **lowest (basic) tier** instead of "absent/level 0". `CapabilityEvaluator` (Core) is **unchanged** — owned-tool tier gating still applies (basic pickaxe can't break boulders/meteorites; fruit trees always-skip per FR-SKIP-03). Net effect: the worker never skips for a *missing* tool and never warns. The now-unreachable warning path is **deleted** (no inert dead code, MAINT-U15-02): `ShiftContext.ToolMissingWarnings`, `MailDispatcher.QueueToolMissingWarning`, and U-14 BR-MAIL-05 are removed.

---

## Pattern U — Mailed Settlement (one letter: items + refund gold) with money-attachment fallback
**Satisfies**: FD-Q9=C, Clar-3=A, BR-REF-01..05, BR-CAL-03, BR-AFF-01, DEV-U15-04, REL-U15-04, UX-U15-02/03, SAFE-U15-01/02/03

Refund delivery moves from "credit gold at exit" to "gold-bearing mail next morning" for shift-settlement refund cases (normal exit, sleep-stop, empty-zone). The deposit still leaves gold immediately (FR-PAY-03); only the return lags one day (UX-U15-02). Playtest corrected the no-worker mail timing: a one-time-festival refund rides the same-day festival notice, because a festival-day "not working today" letter is stale if it waits until tomorrow.

- **Settlement letter (≤1 per shift).** `IntentApplyRefund` no longer mutates gold; it hands the refund amount to a settlement step that **extends Pattern O's flush**: overflow items (with U-14 reason-line body) and refund gold are combined into a single letter. Neither overflow nor a positive refund → no letter (UX-U15-03 / BR-REF-03).
- **Money attachment + fallback (REL-U15-04).** Preferred: attach gold via the existing `MailFrameworkModApiAdapter` (Pattern P). If MFM 1.20.0's letter API can't carry money (confirmed against the installed DLL at code-gen, as for `RegisterLetter`/DEV-U14-03), fallback to a text-only "here's your change" letter whose collection callback credits `Game1.player.Money`. Items always use MFM's multi-attachment path.
- **Pre-/no-shift letters.** The cannot-afford notice (text-only) and festival notice (text-only for recurring; refund-gold-bearing for a refunded one-time) reuse the same dispatcher/fallback but are delivered same-day. The dispatcher registers them with MFM and adds the letter id to today's mailbox so they are readable during the skipped day. Settlement mail still rides the platform/MFM deliver-tomorrow queue — no Dayswork mail save data (SAFE-U15-03).
- **No empty item slots.** The MFM adapter supplies `dynamicItems` only when the letter has real item attachments. Text-only and refund-only letters are registered without an item provider, and empty settlement letters are suppressed.

---

## Resilience Assessment

| Failure / edge scenario | Handling | Pattern |
|---|---|---|
| Festival day | Skip shift; recurring no-deposit + same-day text letter; one-time Executed + same-day mailed refund | Q / R / U |
| Rainy day | Surcharge excluded from rate; Water Crops task kept (outdoor naturally skipped) | Q / R |
| Cannot afford daily deposit | Skip; same-day cannot-afford letter; stay Active; retry + mail each day | R / U |
| Saved building placeholder zones | Recurring day-start ignores raw zone area for deposit-hours estimation, matching the hire preview | R |
| Empty MFM attachment slot | No-attachment letters register without `dynamicItems`; empty settlement letters are suppressed | U |
| Empty zone | Deduct → run → exit → mailed full refund (net zero) | R / U |
| Calendar/weather data unavailable | Safe default (non-festival/non-rain) + log | Q |
| Player sleeps mid-work | Stop worker; mail collected-but-undelivered items + refund; leave remaining tasks undone | S / U |
| Player sleeps after work done | U-14 interruption path; mailed refund | S / U |
| Save ordering race | Fixed handler order: settle → persist | S |
| Missing tool | Degrade to basic tier; no skip/warning | T |
| MFM can't attach money | Text-only letter, credit gold on collection | U |
| Very large zone sleep | No remaining-work mutation during sleep; only settlement of collected items/refund | S |
| Any U-13/U-13B/U-14 regression | Guarded by retained patterns + green test suites | retained |

## Scalability Assessment
N/A — single-player mod; one contract, one worker, a handful of tasks/destinations per day.

## Security Assessment
N/A — Security Baseline extension disabled (Requirements Analysis Q28). No network, PII, auth, or external-input surface.
