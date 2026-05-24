# U-18 — Contract Terms Foundation: NFR Requirements Plan

**Unit**: U-18 — Contract Terms Foundation  
**Phase**: CONSTRUCTION — NFR Requirements  
**Builds on**: approved Functional Design for `U-18`. See [functional-design/](../u-18-contract-terms-foundation/functional-design/).

---

## Plan Checklist

- [x] Collect answers to NFR-Q1 through NFR-Q4
- [x] Analyze answers for ambiguity or contradictions and create clarification questions if needed
- [x] Generate `nfr-requirements.md`
- [x] Generate `tech-stack-decisions.md`
- [x] Present completion message and await approval

---

## Context Summary

U-18 is a **pure Core foundation unit**. It does not talk to SMAPI, the live game world, or persistence APIs directly. Its NFR surface is therefore narrower than a runtime-heavy unit and is mostly about:
- deterministic pricing and preview behavior
- live preview responsiveness in the hiring flow
- robustness when config is incomplete or stale
- maintainability and test rigor for the new pure pricing/energy seam

**Inherited NFRs that already apply and do not need re-decision here**:
- `NFR-UX-04` pricing should be easier to understand than the old deposit/refund model
- `NFR-PERF-03` hiring UI must remain responsive on large farm scopes
- `NFR-MAINT-02` Property-Based Testing extension is enabled in partial mode with FsCheck
- `NFR-MAINT-03` pure business logic stays separated from SMAPI/game integration
- `NFR-SAFE-02` no hidden pricing leakage beyond the explicit contract price
- `NFR-SEC-01` Security Baseline extension is disabled project-wide

**Important U-18-specific quality concerns**:
- `ContractTermsBuilder.BuildPreview(...)` will be called repeatedly as the player toggles tasks and changes scope, so latency matters.
- `PricingSnapshot` ordering and aggregation need to stay deterministic so previews, persistence, and tests do not drift.
- The redesign introduces several config tables (`OutdoorBandPrices`, `AnimalBuildingPrices`, `GreenhousePackagePrices`, `ActionEnergyCosts`); incomplete config handling needs an explicit reliability rule.
- Because U-18 is one of the cleanest pure units in the redesign, it is the best place to carry strong example-based and property-based test coverage.

**Already decided / not re-decided here**:
- fixed-price model instead of hourly billing
- unioned outdoor pricing scope
- additive animal-building pricing
- per-service greenhouse packages
- invalid preview only when there are zero chargeable scope-task pairs overall
- full action-cost table snapshotted into one-time terms

---

## NFR Questions

> Option **A** is the recommendation. A letter is enough; add a sentence if you want to steer the detail.

### NFR-Q1 — Live preview latency target

`BuildPreview(...)` will run often while the player is configuring the contract. We should lock the responsiveness target now so later implementation can trade simplicity vs. caching/debouncing correctly.

**A) Immediate synchronous preview (Recommended).** The pure preview build should normally complete fast enough to refresh in the same interaction frame for typical contract edits, with no debounce/background work required. This keeps the UI simple and predictable.

**B) Small debounce allowed.** Preview may be delayed slightly (for example 50-100ms) during rapid input bursts, as long as it still feels live to the player.

**C) Background or deferred preview.** Preview can lag behind input and may update asynchronously, prioritizing simplicity or safety over same-frame feedback.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q2 — Determinism strictness for pricing snapshots

The unit already defines deterministic ordering conceptually, but we should decide how strong that requirement is for implementation and testing.

**A) Strict deterministic stability (Recommended).** Equivalent input must produce identical `PricingSnapshot` line ordering, quantities, totals, and serialized structural content across runs and machines.

**B) Visual determinism only.** Totals and user-visible ordering must be stable, but internal structural ordering may vary if later layers normalize it.

**C) Totals-only determinism.** Only the total math must be stable; line ordering can vary as long as UI code sorts it later.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q3 — Reliability behavior for incomplete or stale config tables

`U-18` depends on multiple keyed config tables. If one is incomplete or stale after a future config edit, we need a reliability rule.

**A) Fall back to defaults and log (Recommended).** Missing or stale individual price/action keys fall back to `ConfigDefaults`, the preview remains usable, and a warning is logged for maintainers.

**B) Fail the preview for affected contracts.** If any required key for the current contract is missing, preview becomes invalid and terms cannot be built until config is fixed.

**C) Fail mod initialization.** Any missing/stale key in these tables is treated as a startup-time fatal configuration error.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q4 — Test-rigor expectation for this pure foundation unit

Because U-18 owns the cleanest pure logic in the redesign, we should decide how much explicit test rigor it carries beyond the minimum extension rules.

**A) Strong property + example coverage (Recommended).** U-18 gets focused example tests plus a meaningful FsCheck suite for zone union, pricing determinism, breakdown reconciliation, invalid-preview behavior, and energy-profile snapshot invariants.

**B) Example tests first, light property coverage.** Keep a few key FsCheck properties for extension compliance, but lean mainly on conventional unit tests.

**C) Minimal coverage.** Rely mostly on later integration/runtime tests; pure-unit property coverage stays shallow.

**X) Other (please describe after the tag).**

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-18-contract-terms-foundation/nfr-requirements/nfr-requirements.md`
- `aidlc-docs/construction/u-18-contract-terms-foundation/nfr-requirements/tech-stack-decisions.md`
