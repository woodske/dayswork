# U-23 — Recurring Billing + Calendar Refresh: NFR Requirements Plan

**Unit**: U-23 — Recurring Billing + Calendar Refresh  
**Phase**: CONSTRUCTION — NFR Requirements  
**Builds on**: approved Functional Design for `U-23`. See [functional-design/](../u-23-recurring-billing-calendar-refresh/functional-design/).

---

## Plan Checklist

- [x] Analyze functional design for applicable NFRs
- [x] Create this NFR requirements plan
- [x] Collect answers to NFR-Q1 through NFR-Q5
- [x] Analyze answers for ambiguity or contradictions and create clarification questions if needed
- [x] Generate `nfr-requirements.md`
- [x] Generate `tech-stack-decisions.md`
- [x] Present question file and await user answers

---

## Context Summary

U-23 is a **recurring lifecycle retrofit unit**. Unlike U-22, its main quality risk is not scope-family execution; it is whether the daily recurring rebuild/charge/skip path stays predictable, lightweight, and resilient once the old deposit/refund model is gone.

Its NFR surface is therefore mostly about:
- keeping the 6am recurring evaluation pass lightweight and synchronous
- ensuring rebuild, affordability, and notice-precedence decisions stay deterministic across equivalent inputs
- isolating one contract's invalid or unaffordable morning from other recurring contracts that may also need evaluation
- preserving same-day visibility for blocking recurring notices without introducing noisy lifecycle messaging
- keeping the rebuild/charge/persist rules testable in pure seams instead of burying them inside scheduler-side branches

**Inherited NFRs that already apply and do not need re-decision here**:
- `NFR-MAINT-02` Property-Based Testing extension remains enabled in partial mode with FsCheck
- `NFR-MAINT-03` pure business logic should stay separated from SMAPI/runtime dependencies where practical
- `NFR-SAFE-01` no items or gold are lost
- `NFR-SEC-01` Security Baseline is disabled project-wide
- U-21 already owns active shift pacing, stamina, and stop-path performance
- U-22 already owns typed-scope runtime alignment and scope-aware overflow categorization
- one-time confirmation billing is already outside this unit

**Important U-23-specific quality concerns**:
- 6am rebuilds now happen before every recurring charge and can succeed, fail invalid, fail unsupported, skip for festival, or skip for affordability.
- Valid terms refresh must stay persistent even when the worker does not run that day because of affordability or festival.
- Same-day recurring notices must remain timely and non-conflicting.
- Invalid recurring contracts must fail safely without poisoning other valid contracts in the same morning evaluation pass.

**Pre-decided tech stack / no question needed**:
- no new background worker, scheduler framework, or async morning pipeline is being introduced
- the existing SMAPI event-driven day-start shell remains in place
- `ContractTermsBuilder` remains the pricing/energy authority
- `ContractStore.ReplaceTermsSnapshot(...)` remains the narrow persistence seam for successful recurring refresh
- test stack stays `xUnit` + `FsCheck.Xunit`

---

## NFR Questions

> Option **A** is the recommendation. A letter is enough; add a sentence if you want to steer the detail.

### NFR-Q1 — 6am recurring-pass performance target

U-23 adds a rebuild-and-decide pass at 6am for every active recurring contract. We should lock the expected cost now.

**A) Keep the morning recurring pass comfortably lightweight and synchronous (Recommended).** Rebuild, affordability, persistence, and notice-decision work should stay cheap enough that normal day start has no visible hitching or delay.

**B) A small one-time morning delay is acceptable.** Some extra synchronous work at 6am is fine if it keeps the implementation simpler.

**C) Correctness matters much more than morning responsiveness.** A heavier recurring pass is acceptable if it reduces implementation complexity or risk.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q2 — Determinism strictness for rebuild, charge, and notice decisions

U-23 now has several pure lifecycle decisions that should ideally stay stable: rebuilt fixed price, affordability outcome, persisted-terms replacement, and notice precedence.

**A) Strict deterministic decision behavior (Recommended).** Equivalent saved contracts, config, calendar flags, and gold inputs should produce the same rebuild result, the same charge/skip outcome, the same persisted terms behavior, and the same notice choice across runs.

**B) Behavioral determinism only.** The contract should usually do the right thing, but exact notice/persistence ordering details may vary somewhat.

**C) Low determinism requirement.** As long as the player generally gets the right charge and worker appearance outcome, internal structure can vary.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q3 — Reliability target for mixed recurring outcomes in the same morning

U-23 may evaluate several recurring contracts whose outcomes differ: one valid, one unaffordable, one festival-skipped, one invalid-needs-attention. We should decide how isolated those paths must be.

**A) Strong per-contract isolation is required (Recommended).** One contract's invalid rebuild, failed persistence refresh, or cannot-afford outcome should not stop other valid recurring contracts from being evaluated correctly that same morning.

**B) Best-effort isolation is acceptable.** We should try to isolate contracts, but a more global failure path is acceptable if the implementation stays simpler.

**C) Global fail-fast is acceptable.** If one contract's recurring evaluation fails badly, it is acceptable for the rest of that morning's pass to stop.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q4 — Player-facing reliability target for same-day recurring notices

The approved design keeps same-day notices only for festival skip, cannot afford, and needs attention. This question sets the quality bar for how reliably visible those notices must be.

**A) Same-day blocking/courtesy notices must be reliably visible that same day (Recommended).** If U-23 chooses to notify, the letter should be available in the same in-game day and not quietly drift to tomorrow.

**B) Near-term visibility is enough.** It is fine if some notices arrive later the same day or next morning as long as the behavior is documented.

**C) Minimal notice-timing guarantee.** Correct worker/charge behavior matters more than exactly when the explanatory notice becomes visible.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q5 — Test-rigor expectation for the recurring lifecycle retrofit

Because U-23 changes the authoritative recurring charge path and its persistence semantics, we should decide how strong the regression bar needs to be.

**A) Strong example + property coverage (Recommended).** U-23 should get focused example tests plus meaningful FsCheck coverage for rebuild determinism, festival/no-charge behavior, affordability boundaries, terms-refresh persistence, notice precedence, and pre-6am vs after-6am timing where property testing is practical.

**B) Example tests first, lighter property coverage.** Keep only the minimum properties needed for extension compliance and lean mainly on conventional unit tests.

**C) Minimal direct coverage.** Rely mostly on later integration/playtest validation for this unit.

**X) Other (please describe after the tag).**

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-23-recurring-billing-calendar-refresh/nfr-requirements/nfr-requirements.md`
- `aidlc-docs/construction/u-23-recurring-billing-calendar-refresh/nfr-requirements/tech-stack-decisions.md`
