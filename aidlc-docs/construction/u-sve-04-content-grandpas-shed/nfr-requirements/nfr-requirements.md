# NFR Requirements — U-SVE-04 New Content + Grandpa's Shed

Quality bar for content classification, Grandpa's Shed work-location handling, and unique building keying, mapped to the change-level `NFR-SVE-*`. Answers: Q1=A (on-demand detection, no caching), Q2=A (fail-safe to vanilla, never throw), Q3=A (reuse existing stack).

## Performance
- **NFRU4-01 (→ NFR-SVE-02) On-demand detection, no caching.** Category-based product detection runs once per ground object inside the existing scan loop — same call site and `O(1)` cost as the `HashSet` whitelist it replaces. No added scan passes, no caching/state.
- **NFRU4-02 (→ NFR-SVE-02) O(1) lookups.** `TryClassifyContentOverride` and `IsExpansionWorkLocation` are constant-time profile lookups. Grandpa's Shed adds at most one small indoor location to scan when present.

## Reliability / Resilience
- **NFRU4-03 (→ NFR-SVE-04) Never throws into the shift.** A missing/odd item category, descriptor, or location degrades to "no override / not a work location / skip"; unclassifiable content is skipped without crashing. Item-safety (overflow-to-mail) is preserved.

## Determinism & Correctness
- **NFRU4-04 (→ NFR-SVE-03) Deterministic pure logic.** Category membership, content-override lookup, and work-location membership are pure, deterministic functions.
- **NFRU4-05 (→ NFR-SVE-03) Grounded data.** The animal-product category set (Egg -5, Animal Goods -18, Truffle/forage), SVE custom-clump/tree overrides, and Grandpa's Shed location id(s) are verified from SVE source before encoding; nothing assumed.

## Isolation / Vanilla invariance
- **NFRU4-06 (→ NFR-SVE-01) Vanilla unchanged.** Null-object profile → content-override table empty, `IsExpansionWorkLocation` false, no Grandpa's Shed; category detection reproduces the legacy whitelist's vanilla coverage (parity asserted at code-gen). Byte-for-byte vanilla behavior.
- **NFRU4-07 (→ NFR-SVE-07) SVE ids isolated.** All SVE identifiers (custom product/clump/tree ids, Grandpa's Shed location names) live only in `SveExpansionProfile`.

## Testability
- **NFRU4-08 (→ NFR-SVE-05) Pure logic PBT-tested.** Category-detection totality/parity, override passthrough/determinism, work-location membership, and unique building keys are covered by xUnit + FsCheck without SMAPI. SMAPI-touching parts (live scan, Grandpa's Shed entry/deposit) validated by manual SVE playtest.

## Security
- **N/A** — no network, PII, auth, or external-input surface.

## Extension Compliance

| Extension | Status | NFR-requirements compliance |
|---|---|---|
| Security Baseline | Disabled | N/A. |
| Property-Based Testing | Enabled, full | Compliant — NFRU4-04/05/08 set the FsCheck obligations (category totality/parity, override determinism, work-location membership, unique keys) carried into NFR Design and Code Generation. |
