# U-19 — Contract Snapshot Persistence + Legacy Cleanup: NFR Requirements Plan

**Unit**: U-19 — Contract Snapshot Persistence + Legacy Cleanup  
**Phase**: CONSTRUCTION — NFR Requirements  
**Builds on**: approved Functional Design for `U-19`. See [functional-design/](../u-19-contract-snapshot-persistence-legacy-cleanup/functional-design/).

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

U-19 is a **persistence retrofit unit**. Unlike U-18, it is not purely an in-memory pricing seam: it defines the long-lived saved shape for redesigned contracts and the bridge behavior that keeps older runtime consumers working while later retrofit units switch over.

Its NFR surface is therefore mostly about:
- save/load responsiveness during normal SMAPI lifecycle events
- deterministic schema-v2 serialization and round-trip behavior
- resilience when save data contains legacy, malformed, or mixed contract entries
- temporary compatibility quality for bridge fields still read by downstream consumers
- maintainability and regression-test rigor for one of the highest-risk retrofit seams

**Inherited NFRs that already apply and do not need re-decision here**:
- `NFR-MAINT-02` Property-Based Testing extension remains enabled in partial mode with FsCheck
- `NFR-MAINT-03` Core persistence logic stays separated from SMAPI/runtime dependencies
- `NFR-SEC-01` Security Baseline extension is disabled project-wide
- existing serializer safety expectations from `U-06` still apply for null/invalid/future-schema cases
- later units still own the final removal of compatibility bridge fields once all consumers migrate

**Important U-19-specific quality concerns**:
- Save/load is still synchronous in the mod lifecycle, so schema-v2 persistence cannot add noticeable hitching.
- Typed scope plus terms snapshot now become the authoritative saved model, so deterministic output matters for debugging, review, and property-based round trips.
- The legacy-drop policy is intentionally destructive for schema v1, which raises the bar for being very safe with valid schema-v2 sibling contracts.
- The temporary bridge fields (`Zones`, `DepositAmount`, `HourlyRate`) are not authoritative anymore, but they still need to remain reliable enough for not-yet-retrofitted consumers.

**Pre-decided tech stack / no question needed**:
- JSON library stays `Newtonsoft.Json`
- test stack stays `xUnit` + `FsCheck.Xunit`
- no new migration framework or background persistence subsystem is being introduced here
- save data remains human-readable JSON

---

## NFR Questions

> Option **A** is the recommendation. A letter is enough; add a sentence if you want to steer the detail.

### NFR-Q1 — Save/load latency target

U-19 runs during normal save/load lifecycle events, so we should lock the responsiveness expectation now.

**A) Keep persistence synchronous and lightweight (Recommended).** Schema-v2 serialization and deserialization should remain cheap enough to run inline during normal save/load without background work, chunking, or noticeable hitching for the small contract counts Dayswork supports.

**B) Small extra save/load cost is acceptable.** A modest synchronous delay is fine if it keeps the implementation simpler or more explicit.

**C) Heavier persistence work is acceptable.** Correctness/readability matters more than responsiveness; save/load can take noticeably longer if needed.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q2 — Determinism strictness for schema-v2 persistence

U-19 now persists typed scope, structured pricing lines, and action-cost tables. We should decide how strict deterministic save output must be.

**A) Strict deterministic structural output (Recommended).** Equivalent current-schema contracts should serialize to the same structural content and stable ordering across runs and machines, aside from explicit envelope metadata like mod version.

**B) Logical round-trip determinism only.** The deserialized contract must be equivalent, but raw JSON ordering may vary as long as behavior is unchanged.

**C) Totals-and-behavior determinism only.** Only loaded behavior must match; serialized structure itself can drift.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q3 — Reliability target for mixed legacy / malformed / valid saves

The functional design already says schema-v1 is dropped and malformed schema-v2 contracts are skipped individually. This question sets how strong that resilience requirement is.

**A) Best-effort preservation is mandatory (Recommended).** Valid schema-v2 contracts must survive even when the same save payload also contains legacy or malformed entries; one bad contract must not cause avoidable loss of unrelated valid redesign contracts.

**B) Simpler all-or-nothing behavior is acceptable.** If implementation complexity climbs, it is acceptable to fail the whole load for safety rather than preserve valid siblings.

**C) Minimal resilience is acceptable.** Dropping extra contracts is okay as long as the mod does not crash.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q4 — Compatibility bridge quality bar

U-19 keeps legacy-facing fields alive temporarily for downstream consumers that have not switched to `TermsSnapshot` yet. We should decide how strict that bridge must be during the retrofit window.

**A) Full bridge reliability until cutover (Recommended).** Compatibility projections and bridge fields must remain complete and dependable for remaining consumers until the later retrofit units remove that dependency; no partial/null bridge behavior should leak into normal flows.

**B) Best-effort bridge only.** Bridge fields should usually work, but occasional edge-case gaps are acceptable if the redesign source-of-truth fields are correct.

**C) Minimal bridge.** The redesign fields are all that matter; any remaining old consumers should be updated immediately if the bridge is awkward.

**X) Other (please describe after the tag).**

[Answer]: C, there are no active consumers yet, this is still a work in progress

---

### NFR-Q5 — Test-rigor expectation for the persistence retrofit

Because U-19 is the seam where authoritative redesign data actually becomes long-lived save data, we should decide how heavy the regression bar needs to be.

**A) Strong example + property coverage (Recommended).** U-19 gets focused example tests plus meaningful FsCheck coverage for schema-v2 round-trip, schema-v1 drop behavior, mixed valid/malformed survival, compatibility-bridge projections, and `ReplaceTermsSnapshot` invariants.

**B) Example tests first, lighter property coverage.** Keep a few key FsCheck properties for extension compliance, but lean mainly on conventional serializer/store unit tests.

**C) Minimal direct coverage.** Rely mostly on later integration tests and keep persistence-specific property tests shallow.

**X) Other (please describe after the tag).**

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-19-contract-snapshot-persistence-legacy-cleanup/nfr-requirements/nfr-requirements.md`
- `aidlc-docs/construction/u-19-contract-snapshot-persistence-legacy-cleanup/nfr-requirements/tech-stack-decisions.md`
