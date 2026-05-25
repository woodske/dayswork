# U-20 — Hiring Flow Preview Refresh: NFR Requirements Plan

**Unit**: U-20 — Hiring Flow Preview Refresh  
**Phase**: CONSTRUCTION — NFR Requirements  
**Builds on**: approved Functional Design for `U-20`. See [functional-design/](../u-20-hiring-flow-preview-refresh/functional-design/).

---

## Plan Checklist

- [x] Analyze functional design for applicable NFRs
- [x] Create this NFR requirements plan
- [x] Collect answers to NFR-Q1 through NFR-Q5
- [x] Analyze answers for ambiguity or contradictions and create clarification questions if needed
- [x] Generate `nfr-requirements.md`
- [x] Generate `tech-stack-decisions.md`
- [x] Present completion message and await approval

---

## Context Summary

U-20 is a **player-facing workflow retrofit unit**. Unlike U-18 and U-19, its main risk is not schema shape or pure pricing math; it is the quality of the hire/edit experience after the pricing redesign is surfaced to the player.

Its NFR surface is therefore mostly about:
- immediate preview responsiveness while the player edits tasks and scope
- deterministic preview/view-model output so the same draft always renders the same pricing and worker-energy story
- resilient edit hydration for older contracts that may still need compatibility-zone bootstrap
- preserving clear, gamepad-friendly interaction despite more honest invalid-preview states
- maintainability and regression-test rigor so pricing logic stays in Core seams instead of leaking back into menus

**Inherited NFRs that already apply and do not need re-decision here**:
- `NFR-MAINT-02` Property-Based Testing extension remains enabled in partial mode with FsCheck
- `NFR-MAINT-03` pricing and persistence business logic stay in pure Core seams
- `NFR-SEC-01` Security Baseline is disabled project-wide
- U-18 owns contract-term computation and worker-energy model correctness
- U-19 owns authoritative persisted scope/terms snapshot shape and compatibility serialization

**Important U-20-specific quality concerns**:
- Task/scope changes should feel immediate because this is an interactive four-screen flow, not a background planning tool.
- Screen 1 contribution rows, Screen 2 typed-scope summary, and Screen 4 review content should be structurally deterministic for the same draft and config.
- Review-first edit flow must remain trustworthy for both authoritative-scope contracts and legacy contracts bootstrapped from compatibility `Zones`.
- Invalid previews must be explicit and recoverable, not silently auto-healed or hidden behind whole-farm fallback behavior.

**Pre-decided tech stack / no question needed**:
- no new UI framework is being introduced
- existing SMAPI menu stack remains in place
- test stack stays `xUnit` + `FsCheck.Xunit`
- no async preview pipeline, background worker, or client-side cache subsystem is desired by default

---

## NFR Questions

> Option **A** is the recommendation. A letter is enough; add a sentence if you want to steer the detail.

### NFR-Q1 — Preview responsiveness target

U-20 makes pricing and worker-energy preview part of an interactive editing flow, so we should lock the expected responsiveness now.

**A) Immediate synchronous preview updates (Recommended).** Task and scope changes should refresh preview inline with no debounce, background work, or visible lag in normal use.

**B) Small UI delay is acceptable.** A short debounce or minor lag is fine if it simplifies orchestration.

**C) Heavier refresh work is acceptable.** Correctness/readability matters more than responsiveness; preview can update more slowly if needed.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q2 — Determinism strictness for preview and review output

U-20 now derives multiple user-facing models from the same draft and config. We should decide how strict deterministic rendering needs to be.

**A) Strict deterministic preview/view-model output (Recommended).** Equivalent drafts and config should produce the same contribution rows, summary ordering, validation reasons, totals, and energy summary structure across runs.

**B) Behavioral determinism only.** Totals and confirmability must match, but row ordering or summary structure may vary.

**C) Confirm-screen determinism only.** Screen 4 must be stable, but earlier screen preview presentation may vary.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q3 — Reliability target for legacy edit hydration

Older contracts may still need one-time typed-scope bootstrap from compatibility `Zones`. This question sets the bar for how graceful that edit path must be.

**A) Best-effort legacy edit resilience is mandatory (Recommended).** Older contracts should still open in the refreshed flow whenever bootstrap is possible; missing or imperfect legacy data should degrade to explicit invalid/reviewable state rather than crash or silently invent scope.

**B) Authoritative-scope contracts are the main priority.** Legacy bootstrap should usually work, but edge-case failures are acceptable if newer contracts remain solid.

**C) Minimal legacy edit support is acceptable.** If old contracts are awkward, it is acceptable to require the player to recreate them.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q4 — Usability and gamepad quality bar

The redesign adds more honest invalid states and review-first edit behavior. We should decide how strict the interaction-quality bar is.

**A) Preserve full current usability expectations (Recommended).** The refreshed flow should remain gamepad-friendly, keep edit flow shorter than fresh hire flow, and show clear recovery paths for invalid preview states without hidden auto-fixes.

**B) Minor interaction roughness is acceptable.** Core logic matters most; some extra friction during edit/review is acceptable.

**C) Mouse/keyboard-first behavior is acceptable during the retrofit.** Gamepad polish can wait for later.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q5 — Test-rigor expectation for the hire-flow retrofit

Because U-20 is where the redesign becomes visible to players, we should decide how strong the regression bar needs to be.

**A) Strong example + property coverage (Recommended).** U-20 gets focused example tests plus meaningful FsCheck coverage for preview determinism, no-whole-farm-fallback behavior, schedule/destination no-price-change invariants, and legacy edit hydration outcomes where property testing is practical.

**B) Example tests first, lighter property coverage.** Keep only the minimum property tests needed for extension compliance and lean mainly on conventional unit tests.

**C) Minimal direct coverage.** Rely mostly on later integration/playtest validation.

**X) Other (please describe after the tag).**

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-20-hiring-flow-preview-refresh/nfr-requirements/nfr-requirements.md`
- `aidlc-docs/construction/u-20-hiring-flow-preview-refresh/nfr-requirements/tech-stack-decisions.md`
