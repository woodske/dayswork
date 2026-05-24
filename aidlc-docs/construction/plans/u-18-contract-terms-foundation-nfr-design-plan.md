# U-18 — Contract Terms Foundation: NFR Design Plan

**Unit**: U-18 — Contract Terms Foundation  
**Phase**: CONSTRUCTION — NFR Design  
**Builds on**: approved NFR Requirements for `U-18`. See [nfr-requirements/](../u-18-contract-terms-foundation/nfr-requirements/).

---

## Plan Checklist

- [x] Collect answers to NFR-DES-Q1 through NFR-DES-Q4
- [x] Analyze answers for ambiguity or contradictions and create clarification questions if needed
- [x] Generate `nfr-design-patterns.md`
- [x] Generate `logical-components.md`
- [x] Present completion message and await approval

---

## Context Summary

U-18 is a **pure deterministic Core unit**, so its NFR design is much lighter than a runtime or integration-heavy stage. Most classic non-functional design categories are either already settled or explicitly N/A:

- **Resilience patterns** — Applicable. We need to decide how default-backed config fallback is represented in design, and how invalid-preview outcomes stay handled rather than exceptional.
- **Scalability patterns** — Mostly N/A. This is a local in-process Core seam with bounded per-preview inputs; no queues, shards, replicas, or distributed scale mechanisms are relevant.
- **Performance patterns** — Applicable. Immediate synchronous preview is required, so we need to decide whether design relies on pure recompute only or introduces memoization/caching structure.
- **Security patterns** — N/A. Security Baseline is disabled project-wide and this unit has no network/auth/PII surface.
- **Logical components** — Applicable. Deterministic ordering, config resolution, and test-support structure all affect how many helper seams we introduce around the existing pricing builders.

The approved NFRs already lock the quality bar:
- immediate synchronous preview
- strict deterministic pricing snapshots
- per-key fallback to `ConfigDefaults` with logging
- strong xUnit + FsCheck coverage

So this NFR Design stage is about the **pattern and seam choices** used to realize those requirements cleanly, not about changing the requirements themselves.

> Option **A** is the recommendation. A letter is enough; add a sentence if you want to steer the detail.

---

## NFR Design Questions

### NFR-DES-Q1 — Default-backed config fallback pattern (resilience + logical components)

U-18 needs a reliable way to resolve missing/stale keyed values while preserving per-key fallback behavior (REL-U18-03/04, TS-U18-04). Where should that pattern live?

**A) Dedicated config-resolution seam (Recommended).** Introduce one small pure helper/component that resolves keyed prices and action costs with `ConfigDefaults` fallback and returns both the effective value and "fallback used" metadata. Builders consume that seam instead of each doing its own dictionary probing.

**B) Inline fallback logic inside each builder.** `OutdoorServiceBandClassifier`, `ContractPriceCalculator`, and `WorkerEnergyProfileBuilder` each handle missing keys themselves. Fewer types, but duplicated fallback behavior and warning semantics.

**C) Pre-normalize the whole `ConfigSnapshot` once before any build.** A normalization pass eagerly replaces all missing/stale keys with defaults, and later builders assume config is already fully healed.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-DES-Q2 — Preview performance pattern (performance)

Immediate synchronous preview is required, but we still need to decide whether the design should rely on straightforward recomputation or acknowledge memoization as part of the unit design.

**A) Pure recompute, no cache component (Recommended).** Each preview build recomputes terms directly from the draft input. Performance comes from bounded linear work and simple data flow, not from cache state.

**B) Optional in-memory memoization inside `ContractTermsBuilder`.** The builder may cache the most recent input/output pair for repeated identical calls during menu interaction.

**C) Dedicated preview cache component.** Introduce a small logical cache keyed by normalized draft input so repeated previews can reuse prior results.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-DES-Q3 — Deterministic ordering ownership (determinism + logical components)

Strict deterministic stability is required for `PricingSnapshot`. Where should the canonical ordering logic live?

**A) Explicit ordering policy owned by `PriceBreakdownBuilder` (Recommended).** `PriceBreakdownBuilder` is solely responsible for canonical family/service/key ordering before emitting `PricingSnapshot.LineItems`, using an internal comparer/helper as needed.

**B) Separate reusable ordering component.** Introduce a dedicated pure comparer/policy seam consumed by `PriceBreakdownBuilder` and potentially later persistence/test helpers.

**C) Upstream ordering in calculators/classifiers.** Earlier pipeline stages emit already-ordered contributions, and `PriceBreakdownBuilder` mostly preserves that order.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-DES-Q4 — Property-test support structure (maintainability + logical components)

U-18 carries a stronger-than-minimum FsCheck bar. We should decide how much explicit test-support structure to call for in the design.

**A) Dedicated U-18 test helpers/generators (Recommended).** Add focused test-side builders/generators for overlapping zones, repeated building tiers, mixed scope families, and invalid-preview cases so the property suite expresses contract invariants clearly and reproducibly.

**B) Reuse only generic/shared test helpers.** Keep U-18 tests lightweight by composing whatever generic generators already exist, even if they are less domain-specific.

**C) Mostly example tests, minimal helper structure.** Keep special test-support structure to a minimum and let properties use ad hoc inline generators.

**X) Other (please describe after the tag).**

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-18-contract-terms-foundation/nfr-design/nfr-design-patterns.md`
- `aidlc-docs/construction/u-18-contract-terms-foundation/nfr-design/logical-components.md`
