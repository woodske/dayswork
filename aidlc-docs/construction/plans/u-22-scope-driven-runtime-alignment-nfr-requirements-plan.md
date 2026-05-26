# U-22 — Scope-Driven Runtime Alignment: NFR Requirements Plan

**Unit**: U-22 — Scope-Driven Runtime Alignment  
**Phase**: CONSTRUCTION — NFR Requirements  
**Builds on**: approved Functional Design for `U-22`. See [functional-design/](../u-22-scope-driven-runtime-alignment/functional-design/).

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

U-22 is a **runtime-alignment retrofit unit**. Unlike U-21, its main quality risk is not per-beat stamina behavior; it is whether the redesign-era typed scope model stays deterministic, safe, and understandable once runtime execution and overflow mail start depending on it.

Its NFR surface is therefore mostly about:
- keeping shift-start scope classification and batch planning lightweight and synchronous
- making normalized scope families, batch order, and scope-aware mail categorization deterministic across equivalent inputs
- ensuring unsupported contracts fail safely instead of partially executing with the wrong scope
- preserving player clarity when overflow/unassigned-output mail becomes more specific about greenhouse, outdoor, and animal-building cases
- keeping the new provenance-aware routing and mail seams testable instead of burying the logic in ad hoc orchestrator branches

**Inherited NFRs that already apply and do not need re-decision here**:
- `NFR-MAINT-02` Property-Based Testing extension remains enabled in partial mode with FsCheck
- `NFR-MAINT-03` pure business logic should stay separated from SMAPI/runtime dependencies where practical
- `NFR-SAFE-01` no collected items are lost
- `NFR-SEC-01` Security Baseline is disabled project-wide
- U-20 already owns the current hire/edit menu architecture
- U-21 already owns the worker's per-tick stamina/pacing quality bar
- U-23 still owns recurring billing and calendar behavior

**Important U-22-specific quality concerns**:
- Runtime scope intake is now intentionally stricter because `Contract.ScopeSelection` is the only supported live source.
- Scope-aware overflow mail adds richer explanation, which raises the bar for deterministic wording/category shaping.
- Animal-building targeting and greenhouse batching must remain predictable regardless of how outdoor zones are shaped.
- Task-owned routing must remain stable even while output provenance becomes richer.

**Pre-decided tech stack / no question needed**:
- no new runtime framework, job system, or background worker is being introduced
- the existing SMAPI runtime shell remains in place
- the existing mail path remains in place; this unit only changes the categorization/content inputs
- test stack stays `xUnit` + `FsCheck.Xunit`
- no legacy runtime compatibility path is planned by default because the project is not yet live

---

## NFR Questions

> Option **A** is the recommendation. A letter is enough; add a sentence if you want to steer the detail.

### NFR-Q1 — Runtime planning performance target

U-22 adds stricter typed-scope intake, greenhouse/animal batch shaping, and richer overflow categorization. We should lock the expected runtime cost now.

**A) Keep scope intake and routing comfortably lightweight (Recommended).** Shift-start classification, batch shaping, and overflow categorization should stay synchronous and cheap enough that they do not add visible hitching or noticeable runtime drag in normal use.

**B) Small extra planning cost is acceptable.** A modest bit of extra work during shift start or wrap-up is fine if it keeps the logic simpler.

**C) Heavier planning work is acceptable.** Correctness/readability matters more than runtime cost for this retrofit.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q2 — Determinism strictness for scope normalization and mail categorization

U-22 now has multiple pure decisions that should ideally stay stable: normalized scope families, runtime batch order, and scope-aware overflow categories.

**A) Strict deterministic pure-output behavior (Recommended).** Equivalent contracts and live inputs should produce the same normalized scope set, the same batch ordering structure, and the same scope-aware overflow categorization across runs.

**B) Behavioral determinism only.** The worker should generally do the right work and send the right items, but internal grouping/order/category structure may vary.

**C) Low determinism requirement.** As long as the player broadly gets the right outcomes, exact internal structure does not need to stay stable.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q3 — Reliability target for unsupported contracts without typed scope

The approved functional design explicitly avoids a legacy runtime fallback path. This question sets the quality bar for what happens if a contract somehow still reaches runtime without `ScopeSelection`.

**A) Fail fast and safely before live work begins (Recommended).** Unsupported contracts should be rejected predictably, with maintainer-facing diagnostics and no partial execution under guessed scope.

**B) Best-effort execution is acceptable.** If typed scope is missing, the runtime may still attempt a limited or compatibility-flavored execution path rather than stopping outright.

**C) Silent no-op is acceptable.** If typed scope is missing, it is fine for the worker simply not to perform work with minimal diagnostics.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q4 — Player-facing clarity target for scope-aware overflow mail

U-22 makes overflow and unassigned-output letters more specific about greenhouse, outdoor, and animal-building cases. We should decide how strong the clarity bar needs to be.

**A) Clear and concise scope-aware explanation is mandatory (Recommended).** The player should be able to quickly understand why the mail arrived and which scope family it came from, without the letter becoming noisy or bloated.

**B) Correctness matters more than wording elegance.** The categories should be present, but some awkward or repetitive wording is acceptable during the retrofit.

**C) Minimal clarity requirement.** As long as the items are delivered safely, the explanatory text can remain rough or highly technical.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q5 — Test-rigor expectation for the scope-alignment retrofit

Because U-22 introduces stricter scope authority, scope provenance, and scope-aware mail shaping, we should decide how strong the regression bar needs to be.

**A) Strong example + property coverage (Recommended).** U-22 should get focused example tests plus meaningful FsCheck coverage for scope normalization determinism, animal-zone independence, greenhouse/outdoor separation, task-owned routing invariants, and scope-aware mail categorization where property testing is practical.

**B) Example tests first, lighter property coverage.** Keep only the minimum properties needed for extension compliance and lean mainly on conventional unit tests.

**C) Minimal direct coverage.** Rely mostly on later integration/playtest validation for this unit.

**X) Other (please describe after the tag).**

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-22-scope-driven-runtime-alignment/nfr-requirements/nfr-requirements.md`
- `aidlc-docs/construction/u-22-scope-driven-runtime-alignment/nfr-requirements/tech-stack-decisions.md`
