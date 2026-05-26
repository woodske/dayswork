# U-24 — Tech Stack Decisions

**Unit**: U-24 — Config, Regression, and Documentation Cleanup

NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A. Functional-design decisions FD-Q1=A, FD-Q2=C, FD-Q3=A, FD-Q4=A, FD-Q5=A, and FD-Q6=A apply.

---

## TS-U24-01 — Stay on the existing config/runtime publication shell
U-24 introduces no new config or UI framework. Implementation stays on the established architecture:
- `GMCMRegistrar` as the optional player-facing config registration shell
- `ModConfigManager` as the editable/save/publish seam
- `RuntimeConfigSnapshotMapper` as the normalization and immutable runtime snapshot seam
- `ConfigDefaults` and `ConfigValueResolver` as the redesign-era fallback/value-resolution support seams

This keeps the cleanup incremental instead of creating a second settings model.

## TS-U24-02 — Keep redesign config authority in the existing redesign-era fields
The preferred implementation direction is:
- expose only redesign-era price, stamina, action-cost, and worker-behavior controls
- remove hourly/deposit-era controls from GMCM and the intended saved config shape
- continue treating any still-needed legacy compatibility values as internal implementation details only

No ad hoc reintroduction of hourly/deposit tuning should appear in the player-facing surface.

## TS-U24-03 — Preserve deterministic normalization and per-key fallback through existing mapping seams
The clean-break saved config shape does not remove the need for safe fallback behavior. The preferred path is:
- normalize saved `ModConfig` through `RuntimeConfigSnapshotMapper`
- resolve missing/invalid values through redesign-era defaults at the narrowest supported scope
- keep warning emission maintainers-facing rather than surfacing hard failures to normal players

This is simpler and safer than section-wide blind resets or fail-fast manual repair requirements.

## TS-U24-04 — Keep the player-visible string boundary on the existing i18n + lint path
No new localization subsystem is required. The preferred direction is:
- continue routing player-visible text through `i18n/default.json`
- preserve `HardcodedUserFacingStringLintTests` as the enforcement gate
- keep debug/technical/internal literals outside the enforced player-visible boundary

This satisfies the strict i18n bar without widening into an impractical “no English literals anywhere” policy.

## TS-U24-05 — Keep build/test docs in the existing artifact set and rewrite in place
No new documentation framework is needed. The preferred direction is:
- rewrite the existing build/test instruction files
- keep them redesign-native and internally consistent
- add the explicit regression checklist and reviewer-facing deviations/caveats note within the existing documentation tree

This is preferable to a transitional addendum layered on top of stale hourly/deposit instructions.

## TS-U24-06 — Keep final verification targeted and deterministic
The preferred verification shape remains:
- focused regression coverage for unchanged high-risk behaviors
- deterministic config mapping and fallback tests
- bounded documentation/i18n coherence checks where practical

U-24 should not balloon into an exhaustive replay of every earlier unit.

## TS-U24-07 — Keep cleanup logic close to pure seams
The main U-24 decisions should remain practical to test with pure or near-pure inputs:
- redesign config normalization
- default fallback behavior
- runtime snapshot equivalence
- regression invariants for unchanged behaviors
- player-visible string/documentation boundary checks where deterministic

This is the cleanest way to satisfy the strict determinism bar.

## TS-U24-08 — Tests stay on `xUnit` + `FsCheck`
No new test framework is needed. U-24 should lean on:
- `xUnit` for focused cleanup scenarios and regression examples
- `FsCheck` for deterministic config/fallback invariants and generated cleanup edge cases where property testing is practical

The strongest value comes from generated config and regression contexts rather than from new UI automation infrastructure.
