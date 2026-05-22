# U-15 — NFR Requirements

**Unit**: U-15 — Recurring Lifecycle + Calendar Handlers

Inherits U-10/U-13/U-14's worker and output NFRs; adds requirements for the daily recurring lifecycle, calendar predicates, the early-sleep fast-forward, and mailed refunds. FD decisions (Q1=A, Q2=A, Q3→Clar-1a=C, Q4=B, Q5=A, Q6=C, Q7=A, Q8=C+Clar-2, Q9=C+Clar-3) and deviations DEV-U15-01..05 apply throughout.

---

## Safety & Data Integrity

### SAFE-U15-01 — No items/refunds lost across every new branch (NFR-SAFE-01)
Conservation must hold on each new path: festival skip, can't-afford skip, empty-zone day, and both fast-forward branches. Every collected item is deposited (live chest/bin) or mailed; every owed refund is mailed; nothing is dropped at the `Saving` event or across the day-rollover. *(BR-SAFE-01, BR-FF-05, BR-REF-01)*

### SAFE-U15-02 — Refund math unchanged; only delivery moved (NFR-SAFE-02)
The refund stays `clamp(deposit − hoursWorked × rate, 0, deposit)` with integer arithmetic; deposit-run walking is unbilled. U-15 changes *how* it reaches the player (gold-bearing mail next morning, DEV-U15-04), not the amount. *(BR-REF-01)*

### SAFE-U15-03 — No new persisted Dayswork data (NFR-SAFE-03)
Refund, festival, and cannot-afford letters are queued for next-morning delivery via the platform/MFM "deliver tomorrow" mechanism — no new Dayswork-namespaced save structure, no new round-trip surface. Contract status changes (one-time → Executed) persist through the existing `ContractStore` save segment. Festival/weather data is read live, never persisted by Dayswork. *(SAFE-U14-03 carryover, BR-MAIL-03)*

### SAFE-U15-04 — Tolerate absent calendar/weather/contract data (NFR-SAFE-03)
Missing or unexpected festival/weather/contract state degrades gracefully — treated as non-festival / non-rain / no-contract — never a crash. *(BR-SAFE-02)*

### SAFE-U15-05 — Fast-forward collects only self-caused drops (NFR-SAFE-04)
Headless fast-forward performs the same task actions as the live shift and buffers only the debris those actions create; it never sweeps pre-existing world items. *(BR-FF-03, retained from U-13B)*

---

## Performance

### PERF-U15-01 — Scheduler is a once-per-day cost (NFR-PERF-01/02)
`OnDayStarted` runs the lifecycle once per contract day: O(1) festival/rain checks, one rate/estimate/deposit computation, one affordability check. No per-frame work. *(BR-DAY-*)*

### PERF-U15-02 — Calendar predicates are O(1) (NFR-PERF-01)
`IsFestivalToday()` / `IsRainyToday()` are constant-time live-state lookups, called a handful of times per day (scheduler) — never per frame.

### PERF-U15-03 — Sleep fast-forward is a bounded one-time cost (NFR-PERF-02)
The headless completion loop runs **once**, inside the `Saving` event, during the sleep fade. It is bounded by (a) the remaining detected task count in the zone and (b) the in-game time budget to the 8pm cap. It performs no walking/animation and touches no per-frame path. Whether to cap per-frame mutation volume (for very large zones) is a deferred NFR-Design engineering detail; the **product** rule is "settle the remaining shift atomically before day-rollover". *(BR-FF-02/03)*

---

## Usability

### UX-U15-01 — All new mail/log strings localizable (NFR-UX-02)
New user-visible strings — the settlement refund line, cannot-afford body, festival bodies (text + refund variants), and the festival-skip log line — route through `I18nHelper` / `i18n/default.json`; no hardcoded user-visible text. New keys: `mail.settlement.refund_line`, `mail.cannot_afford.body`, `mail.festival.body`, `mail.festival.refund_body`, `log.festival.skipped`. Existing `mail.sender` + `mail.overflow.*` are reused. *(BR-MAIL-02)*

### UX-U15-02 — One-day refund lag is the accepted UX (DEV-U15-04)
Mailed refunds intentionally arrive the morning after the shift. This is the chosen immersive behaviour (FD-Q9=C), not a defect; the deposit still leaves immediately at 6am/confirmation. *(BR-REF-01/02)*

### UX-U15-03 — At most one settlement letter per shift (mailbox tidiness)
To avoid daily mailbox spam for recurring contracts, overflow items and refund gold are combined into a single settlement letter; no letter is sent when there is neither overflow nor a positive refund. *(BR-REF-03)*

---

## Reliability

### REL-U15-01 — Festival/weather predicate robustness
A predicate that cannot determine state returns the safe default (non-festival / non-rain) and logs, rather than throwing. *(BR-SAFE-02)*

### REL-U15-02 — Atomic at-save settlement ordering
`CalendarHandlers.OnSavingHook` completes the fast-forward and full settlement (chest deposits, refund mail, overflow mail) **before** `ContractPersistenceAdapter.OnSaving` writes contract state and before the day rolls over. Handler ordering is owned by the ModEntry composition root, not left to chance. *(BR-FF-01/02, FR-DAY-02)*

### REL-U15-03 — Cannot-afford and festival skips never abort the day
Skipping a contract (festival or affordability) is an expected, handled outcome: no shift starts, the appropriate letter is queued, the day proceeds normally. *(BR-DAY-02/03, BR-AFF-01)*

### REL-U15-04 — Mailed-refund money fallback (Clar-3=A)
If MFM cannot attach money to a letter, the refund is delivered as a text-only "here's your change" letter that credits the gold when collected (still next morning). Items always use MFM's multi-attachment path. The exact mechanism is finalized in NFR Design / Code Generation. *(BR-REF-04)*

### REL-U15-05 — Single-active-contract guard is authoritative
The hire-time guard (DEV-U15-01) is the single source of truth that keeps the scheduler's single-worker assumption valid; the scheduler does not need its own multi-contract reconciliation. *(BR-CTR-01/02)*

---

## Maintainability

### MAINT-U15-01 — New logic confined to the Mod layer (NFR-MAINT-03)
`CalendarHandlers` (M-14), the scheduler lifecycle, the `FastForwardAndSettle` orchestration, and the `ToolLevelReader` missing-tool change all live in `Dayswork` and hold Stardew/SMAPI/MFM references behind existing seams. Pure pricing (`RateCalculator`/`HoursEstimator`/`DepositCalculator`/`RefundCalculator`) is reused unchanged from Core.

### MAINT-U15-02 — Dead code removed, not left dangling (NFR-MAINT-05)
The now-unreachable tool-missing warning path (`ShiftContext.ToolMissingWarnings`, `MailDispatcher.QueueToolMissingWarning`, U-14 BR-MAIL-05) is deleted, per DEV-U15-03 — no inert dead code retained.

### MAINT-U15-03 — No new Harmony patches (NFR-MAINT-04)
U-15 introduces no Harmony patches; everything rides existing SMAPI events (`DayStarted`, `Saving`) and the MFM API. *(NFR-MAINT-04 N/A for new patches.)*

### MAINT-U15-04 — .NET conventions (NFR-MAINT-05)
Code follows standard .NET conventions (`dotnet format`).

---

## Compatibility

### COMPAT-U15-01 — No new dependency (NFR-COMPAT-04)
U-15 adds no manifest dependency; MFM is already required from U-14. Mailed refunds reuse the existing MFM adapter (with the money-attachment fallback of REL-U15-04). *(COMPAT-U14-01 carryover)*

---

## Property-Based Testing Obligations (PBT Extension — Partial mode)

U-15 is largely SMAPI-event-driven (play-tested). PBT/unit coverage applies where logic reduces to Core/pure helpers; PBT-08 (seed logging) is blocking for any new property.

### PBT-U15-01 — Refund formula invariants (reuse U-05)
The mailed refund amount equals `clamp(deposit − hoursWorked × rate, 0, deposit)` for all generated inputs — reuses RefundCalculator's existing PBT-03 invariants. *(SAFE-U15-02)*

### PBT-U15-02 — Fast-forward time budget
The headless fast-forward never charges more in-game-minutes than the remaining window, and tasks completed never exceed tasks available. *(BR-FF-03)*

### PBT-U15-03 — Conservation across fast-forward (extends U-14)
For the fast-forward path, {deposited} ∪ {mailed} == {collected}; no item or refund is dropped. *(SAFE-U15-01)*

### PBT-U15-04 — Single-active-contract guard (unit test)
Given an existing Active/Paused contract, creating a second contract is rejected. *(BR-CTR-01)*

### PBT-U15-05 — Seed logging (PBT-08 blocking)
All new U-15 properties follow the U-02 seed + shrunk-input logging convention. *(PBT-08)*

**Not PBT (unit-tested / play-tested instead):** festival/weather predicates, the at-save ordering, MFM money attachment, and the scheduler lifecycle read live game/SMAPI state and are integration- or play-tested.

---

## Security
Security Baseline extension is **disabled** project-wide (NFR-SEC-01): no network, PII, auth, or external-input surface. All Security Baseline rules are **N/A** for U-15.
