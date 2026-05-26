# U-24 — Config, Regression, and Documentation Cleanup: NFR Requirements Plan

**Unit**: U-24 — Config, Regression, and Documentation Cleanup  
**Phase**: CONSTRUCTION — NFR Requirements  
**Builds on**: approved Functional Design for `U-24`. See [functional-design/](../u-24-config-regression-documentation-cleanup/functional-design/).

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

U-24 is a **final cleanup retrofit unit**. Unlike U-18 through U-23, its main quality risk is not new pricing or runtime behavior; it is whether the redesign finishes with a coherent, dependable player-facing config surface, a stable clean-break config shape, a credible regression bar for unchanged high-risk behavior, and documentation that no longer tells the old hourly/deposit story.

Its NFR surface is therefore mostly about:
- keeping the optional GMCM surface responsive even though the redesign-era tuning surface is broader than the old hourly page
- making redesign-only config resolution and published runtime snapshots deterministic across equivalent inputs
- deciding how resilient the mod should be when an older or malformed config meets the clean-break saved shape
- ensuring the rewritten build/test docs and reviewer-facing caveat notes stay trustworthy and do not drift back into stale hourly/deposit wording
- locking the regression/test bar for the final cleanup so unchanged critical behaviors remain credibly protected

**Inherited NFRs that already apply and do not need re-decision here**:
- `NFR-MAINT-02` Property-Based Testing extension remains enabled in partial mode with FsCheck
- `NFR-MAINT-03` pure business logic should stay separated from SMAPI/runtime dependencies where practical
- `NFR-SAFE-01` no collected items or gold are lost
- `NFR-SEC-01` Security Baseline is disabled project-wide
- U-18 through U-23 already own fixed-price terms, saved terms persistence, hire preview flow, worker stamina/pacing, typed runtime scope, and recurring rebuild/charge behavior
- U-24 does not add a new runtime framework, config framework, or in-world settings UI

**Important U-24-specific quality concerns**:
- The final GMCM page will expose larger structured price/action-cost tables and pacing controls, so the interaction quality bar should be explicit.
- The saved config shape becomes redesign-only after this unit, so we should be deliberate about what “safe enough” means for stale or malformed config input.
- The build/test docs and reviewer-facing caveat note become the final cleanup narrative, which raises the bar for consistency and freshness.
- This unit intentionally targets unchanged-but-risky behavior, so regression rigor matters more than broad feature expansion.

**Pre-decided tech stack / no question needed**:
- no new config or UI framework is being introduced
- the existing `ModConfig`, `ModConfigManager`, `RuntimeConfigSnapshotMapper`, and `GMCMRegistrar` seams remain in place
- the existing `Dayswork.Tests` stack remains `xUnit` + `FsCheck.Xunit`
- documentation outputs stay in `aidlc-docs/construction/build-and-test/`

---

## NFR Questions

> Option **A** is the recommendation. A letter is enough; add a sentence if you want to steer the detail.

### NFR-Q1 — GMCM/config responsiveness target

U-24 replaces the old hourly controls with a broader redesign-era pricing, stamina, and worker-behavior surface. We should lock the expected interaction quality now.

**A) Keep GMCM/config interaction comfortably immediate (Recommended).** Browsing the redesign sections, changing values, validating them, and saving/publishing should feel effectively instantaneous in normal use, with no noticeable lag or hitching.

**B) Small interaction delays are acceptable.** Minor pauses while sections update or values validate/save are fine if that keeps the implementation simpler.

**C) Responsiveness is secondary.** A heavier config interaction path is acceptable if it reduces implementation complexity or cleanup risk.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q2 — Determinism strictness for redesign config resolution

U-24 finalizes the redesign-only config surface, so equivalent saved config input should ideally resolve the same way every time.

**A) Strict deterministic config resolution (Recommended).** Equivalent redesign-era config input should produce the same normalized runtime snapshot, the same validation outcomes, and the same effective published tuning behavior across runs and machines.

**B) Behavioral determinism only.** The mod should generally settle on the right effective tuning, but exact normalization or validation details may vary somewhat.

**C) Low determinism requirement.** As long as the mod broadly works with the saved config, exact normalized output stability is not important.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q3 — Reliability target for stale or malformed config after the clean break

U-24 intentionally breaks the saved `config.json` shape over to redesign-only fields. This question sets the resilience bar when players still have stale, partially missing, or malformed config data.

**A) Per-key fallback and keep-going reliability (Recommended).** Missing or invalid redesign-era keys should fall back to defaults at the smallest practical scope, the mod should remain usable, and maintainers should get a clear warning signal.

**B) Section-level reset is acceptable.** If part of the redesign config is malformed, it is fine to reset a larger section or the broader redesign surface to restore a valid state.

**C) Fail-fast/manual-repair is acceptable.** If the clean-break config is stale or malformed, it is acceptable for the mod to require the player to regenerate or manually repair the file before normal use.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q4 — Freshness and consistency bar for rewritten build/test docs

U-24 rewrites the build/test instructions and adds a reviewer-facing deviations/caveats note. We should decide how strong the documentation quality bar needs to be.

**A) Treat stale redesign docs as regression failures (Recommended).** The rewritten instruction files and caveat note should be internally consistent, clearly redesign-native, and free of stale hourly/deposit guidance or contradictory verification notes.

**B) Summary accuracy is the priority.** The high-level summary must be correct, but some stale detail in lower-level instruction files or caveat wording is acceptable for one pass.

**C) Best-effort documentation is enough.** As long as the code/tests are right, some drift or leftover historical wording in the docs is acceptable.

**X) Other (please describe after the tag).**

[Answer]: A

---

### NFR-Q5 — Test-rigor expectation for the final cleanup sweep

Because U-24 is the final cleanup pass, this question sets how strong the automated verification bar should be for config mapping, targeted regressions, and the doc/i18n boundary.

**A) Strong targeted example + property coverage (Recommended).** U-24 should add or refresh focused example tests plus meaningful FsCheck coverage where practical for redesign config mapping, targeted unchanged-behavior regressions, the player-visible i18n boundary, and documentation/config coherence checks.

**B) Example tests first, lighter property coverage.** Keep only the minimum property coverage needed for extension compliance and rely mostly on conventional regression tests.

**C) Minimal direct coverage.** Rely mainly on manual review and later playtest validation for this cleanup unit.

**X) Other (please describe after the tag).**

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-24-config-regression-documentation-cleanup/nfr-requirements/nfr-requirements.md`
- `aidlc-docs/construction/u-24-config-regression-documentation-cleanup/nfr-requirements/tech-stack-decisions.md`
