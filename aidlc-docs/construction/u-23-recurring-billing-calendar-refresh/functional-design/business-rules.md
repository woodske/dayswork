# U-23 — Recurring Billing + Calendar Refresh: Business Rules

**Unit**: U-23 — Recurring Billing + Calendar Refresh  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A

Enforceable rules for the redesign-era recurring lifecycle. See [business-logic-model.md](business-logic-model.md) for recurring-day flows, [domain-entities.md](domain-entities.md) for lifecycle data shapes, and [frontend-components.md](frontend-components.md) for bulletin-board and notice-facing effects.

---

## No deviations introduced at U-23

U-23 is the unit that formalizes:
- fixed recurring billing from rebuilt saved scope
- silent normal rain/low-work behavior
- same-day notice support only for blocking or courtesy skip cases
- next-morning config-change adoption through day-start rebuild

It intentionally does not reintroduce:
- deposits
- refunds
- variable per-morning pricing based on actionable tile count
- same-day mid-shift rollback

---

## Architecture and ownership boundaries

**BR-ARCH-01 — Pure Core remains the source of truth for recurring term rebuilds.** Rebuilding today's recurring price and energy snapshot from saved scope plus current config must continue to live in the existing pure contract-terms seams, not in ad hoc scheduler math. *(FD-Q1=A, U-18/U-19 carry-forward)*

**BR-ARCH-02 — The scheduler owns sequencing, not pricing formulas.** Day-start orchestration may choose the order of rebuild, festival gate, affordability, persistence, and shift start, but it must not become the place that recalculates fixed recurring pricing directly. *(FD-Q1=A)*

**BR-ARCH-03 — U-23 builds on typed-scope authority from U-22.** Recurring execution must not reopen legacy runtime fallback or zone-only contract guessing. *(U-22 carry-forward)*

**BR-ARCH-04 — U-23 preserves U-21's no-refund runtime model.** Once a recurring day has successfully charged and started, later stop reasons remain runtime/output-settlement concerns only. *(FD-Q7=A, U-21 carry-forward)*

---

## Recurring eligibility and morning rebuild

**BR-RECUR-01 — Only active recurring contracts scheduled for today enter the recurring 6am path.** Paused, cancelled, executed, and one-time contracts are outside this unit's recurring billing flow. *(unit boundary)*

**BR-RECUR-02 — Today's recurring billing authority comes from a fresh 6am rebuild.** The authoritative price and worker energy for a recurring day are rebuilt from saved scope, saved enabled tasks, and current config at that morning's decision point. *(FR-PAY-07, FD-Q1=A)*

**BR-RECUR-03 — Rebuild failure blocks the day.** If saved scope/config cannot produce a valid supported recurring contract, no charge is taken and no worker spawns. *(FD-Q2=A)*

**BR-RECUR-04 — Rebuild-invalid recurring contracts stay active until the player changes them.** U-23 does not auto-pause invalid recurring contracts. *(FD-Q2=A)*

**BR-RECUR-05 — Successful rebuilds replace the saved recurring terms snapshot of record.** Once a rebuild produces valid refreshed terms, those terms become the persisted snapshot for that contract. *(U-19 carry-forward, FD-Q1=A, FD-Q3=A)*

**BR-RECUR-06 — Rebuild-invalid or unsupported recurring contracts must never run from stale saved terms.** U-23 does not fall back to the previous snapshot when the new rebuild failed. *(FD-Q2=A)*

---

## Festival, affordability, and charge timing

**BR-FEST-01 — Festival days never charge recurring contracts.** Even when rebuild succeeds, no recurring daily price is deducted on festival days. *(FR-DAY-01, FD-Q3=A)*

**BR-FEST-02 — Festival days do not start the worker.** A festival-day recurring contract is skipped operationally. *(FR-DAY-01, FD-Q3=A)*

**BR-FEST-03 — Festival days still refresh recurring terms when rebuild succeeds.** The contract's saved snapshot stays current with scope/config even though the worker was skipped. *(FD-Q3=A)*

**BR-AFF-01 — Affordability is checked against today's rebuilt recurring fixed price.** The scheduler must not check affordability against old saved deposit/rate fields or an outdated terms snapshot. *(FD-Q1=A, FR-PAY-07)*

**BR-AFF-02 — Unaffordable recurring days are skipped without charge.** If the farmer lacks today's rebuilt fixed price, the worker does not appear and the contract remains active for future retry. *(FR-PAY-08, FD-Q1=A)*

**BR-AFF-03 — Valid but unaffordable recurring days still keep the latest rebuilt terms snapshot.** Successful rebuild and inability to pay are separate decisions; the inability to pay does not erase valid refreshed terms. *(U-19 carry-forward, FD-Q1=A)*

**BR-CHARGE-01 — A normal recurring day charges at 6am before shift start.** The worker begins only after today's rebuilt recurring price has been successfully deducted. *(FR-PAY-07, FD-Q1=A)*

**BR-CHARGE-02 — The charged price and the started shift use the same rebuilt terms snapshot.** Billing and runtime must not diverge on the same recurring day. *(FD-Q1=A, FR-PAY-12)*

---

## Rain and low-work behavior

**BR-WEATHER-01 — Rain does not change recurring contract price.** Outdoor watering may naturally vanish from actionable work, but the rebuilt recurring price is unchanged. *(FR-PAY-10, FD-Q4=A)*

**BR-WEATHER-02 — Rain does not trigger a recurring day-start courtesy note.** Normal rainy recurring behavior is silent. *(FD-Q4=A, FD-Q8=A)*

**BR-WEATHER-03 — Low-work or no-work mornings do not change recurring contract price.** The normal recurring charge still applies because that day's labor capacity was reserved. *(FR-PAY-09, FD-Q5=A)*

**BR-WEATHER-04 — Low-work or no-work mornings do not trigger special lifecycle mail.** Ordinary low-work days are intentionally silent. *(FD-Q5=A, FD-Q8=A)*

---

## Edit, pause, and cancel timing

**BR-EDIT-01 — Pre-6am recurring edits apply to the next eligible day immediately.** If the player confirms an edit before 6am, that morning's rebuild and charge use the edited saved scope/settings. *(FR-HIRE-16, FD-Q6=A)*

**BR-EDIT-02 — Pre-6am pause or cancel suppresses that morning's recurring run.** If the status change is persisted before the day-start decision point, no charge or worker applies for that day. *(FR-HIRE-12, FD-Q7=A)*

**BR-EDIT-03 — After-6am pause or cancel does not unwind the current day.** No same-day rollback, proration, or refund behavior is introduced by U-23. *(FR-HIRE-15, FD-Q7=A)*

**BR-EDIT-04 — After-6am lifecycle changes affect only future eligible mornings.** The already-started recurring day continues under the existing runtime stop rules. *(FD-Q7=A)*

---

## Notice support and precedence

**BR-MAIL-01 — U-23 supports only three recurring day-start notice kinds.** Festival skip, cannot afford, and rebuild-invalid/needs-attention are the only supported same-day recurring lifecycle notices. *(FD-Q8=A)*

**BR-MAIL-02 — Ordinary successful recurring days stay silent.** U-23 does not add same-day lifecycle mail for normal charge-and-run days. *(FD-Q8=A)*

**BR-MAIL-03 — Courtesy notices must not hide blocking actionable problems.** When more than one theoretical notice reason exists, the actionable blocking reason wins. *(FD-Q2=A, FD-Q3=A, FD-Q8=A)*

**BR-MAIL-04 — Notice precedence is `NeedsAttention > CannotAfford > FestivalSkip`.** This preserves one authoritative message per contract/day-start path. *(FD-Q8=A)*

**BR-MAIL-05 — Needs-attention mail must explain that the contract requires player intervention before it can run again.** The message should not imply auto-pause or auto-cancellation. *(FD-Q2=A)*

---

## Persistence and state safety

**BR-STORE-01 — Terms refresh must not mutate saved scope or recurring destinations.** Replacing the saved terms snapshot is a narrow recurring refresh, not a broader contract rewrite. *(U-19 carry-forward)*

**BR-STORE-02 — Invalid rebuilds do not overwrite the last known valid saved terms snapshot.** If no valid refreshed terms exist, the prior snapshot remains the latest saved valid reference. *(FD-Q2=A)*

**BR-STORE-03 — Festival skip and cannot-afford skip preserve contract activity state.** These skip reasons do not silently pause, cancel, or execute recurring contracts. *(FD-Q2=A, FD-Q3=A)*

**BR-STORE-04 — Sleep-stop does not reopen recurring billing state.** Once today's recurring day charged and started, sleeping affects only shift wrap-up and output safety. *(U-21 carry-forward)*

---

## Property-based testing obligations

Property-Based Testing is enabled in partial mode. U-23 should preserve deterministic recurring/day-start seams whose invariants can be carried into code generation planning and tests.

| Rule | Required property / invariant |
|---|---|
| PBT-03 invariant | Equivalent saved scope, enabled tasks, config, calendar flags, and gold inputs produce equivalent recurring day-start outcomes and identical refreshed terms when valid. |
| PBT-03 invariant | Festival paths never deduct gold or start shifts, regardless of valid refreshed pricing. |
| PBT-03 invariant | Rain and low-work states do not change the rebuilt recurring price or create extra lifecycle mail by themselves. |
| PBT-03 invariant | Successful terms refresh mutates only the saved terms snapshot; invalid refresh does not mutate saved scope/status/destinations. |
| PBT-07 generator quality | Generators must cover festival vs non-festival mornings, valid vs invalid rebuilds, exact-affordability boundaries, and pre-6am vs after-6am lifecycle timing. |
| PBT-08 shrinking | Counterexamples should shrink to one recurring contract, one day-start context, and the smallest conflicting notice or charge decision. |
| PBT-09 framework | FsCheck remains the property-based testing framework for these pure recurring lifecycle seams. |

Recommended example-based companion tests for later code generation:
- valid festival-day rebuild that refreshes terms but charges nothing
- valid non-festival rebuild that exactly matches available gold
- invalid rebuild on a rainy morning that sends only needs-attention notice
- after-6am cancellation request that leaves the current recurring shift committed

Security Baseline is disabled project-wide, so its rules are N/A here.
