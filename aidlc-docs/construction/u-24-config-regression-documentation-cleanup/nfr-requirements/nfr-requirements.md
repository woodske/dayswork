# U-24 — NFR Requirements

**Unit**: U-24 — Config, Regression, and Documentation Cleanup

U-24 is a final-cleanup retrofit unit. Its NFR surface is centered on **an immediate redesign-era GMCM/config experience**, **strict deterministic redesign-only config resolution**, **per-key fallback reliability for stale or malformed clean-break config input**, **documentation and caveat artifacts that stay fully redesign-native and internally consistent**, and **strong targeted example + property-based verification for the final cleanup sweep**. NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A. Functional-design decisions FD-Q1=A, FD-Q2=C, FD-Q3=A, FD-Q4=A, FD-Q5=A, and FD-Q6=A apply throughout.

---

## Performance

### PERF-U24-01 — GMCM/config interaction remains comfortably immediate (NFR-Q1=A)
Browsing redesign sections, editing values, validating them, and saving/publishing should feel effectively instantaneous in normal use, with no noticeable hitching.

This unit is not permitted to depend on:
- heavyweight recalculation loops during ordinary GMCM interaction
- repeated full-surface rebuilds when only one small config value changes
- asynchronous config publishing machinery or background UI workers
- documentation or lint checks in the live config interaction path

### PERF-U24-02 — U-24 must reuse the existing config interaction shell
This cleanup should achieve its behavior by simplifying and re-pointing the existing config surface:
- `GMCMRegistrar` remains the optional registration shell
- `ModConfigManager` remains the save/publish seam
- `RuntimeConfigSnapshotMapper` remains the normalization / immutable snapshot builder

No second config subsystem or parallel settings surface should be introduced.

### PERF-U24-03 — Documentation and caveat generation stay bounded
The rewritten build/test docs and reviewer-facing deviations note should remain a bounded documentation refresh, not a sprawling new review/reporting framework.

---

## Reliability & Correctness

### REL-U24-01 — Redesign-only config resolution is strictly deterministic (NFR-Q2=A)
Equivalent redesign-era config input must produce the same:
- normalized saved-config result
- immutable runtime snapshot
- validation/fallback outcomes
- effective published pricing, stamina, and worker-behavior tuning

across runs and machines.

### REL-U24-02 — Determinism must not depend on incidental ordering
The effective redesign snapshot must not vary because of:
- incidental dictionary or collection ordering
- mixed legacy field leftovers that are no longer authoritative
- GMCM registration order
- non-deterministic documentation/checklist ordering

### REL-U24-03 — Player-facing config and documentation must tell the same story
After U-24:
- GMCM wording
- saved-config expectations
- build/test docs
- reviewer-facing caveat notes

must all describe the redesign-era fixed-price / worker-energy model rather than partially reverting to hourly/deposit language.

### REL-U24-04 — Active-day runtime lock semantics remain consistent
Config edits may affect future previews, recurring rebuilds, and future shifts, but they must not mutate an already-started shift's committed runtime snapshot.

---

## Safety & Data Integrity

### SAFE-U24-01 — Stale or malformed clean-break config must degrade safely (NFR-Q3=A)
Missing or invalid redesign-era keys should fall back to defaults at the smallest practical scope, keep the mod usable, and emit a clear maintainer-facing warning rather than forcing immediate manual repair.

### SAFE-U24-02 — Fallback handling must stay narrow and predictable
Malformed config recovery must not silently:
- re-authorize old hourly/deposit-era fields as player-facing truth
- wipe unrelated valid redesign sections unnecessarily
- publish partially invalid runtime snapshots
- change already-committed active-shift behavior

### SAFE-U24-03 — Targeted regression cleanup must not destabilize unchanged safety invariants
The U-24 regression sweep must preserve the previously established safety guarantees for:
- output routing and overflow fallback
- tool snapshot / skip behavior
- stuck recovery
- invulnerability continuity
- multiplayer refusal

These remain regression blockers, not optional cleanup niceties.

---

## Usability & Interaction Quality

### USAB-U24-01 — The redesign-era config surface must remain understandable
The final GMCM page should feel like one coherent tuning model:
- pricing
- worker stamina
- worker behavior / recovery

It should not feel like a historical mixture of redesign concepts and leftover hourly terminology.

### USAB-U24-02 — Rewritten build/test docs must stay fresh and internally consistent (NFR-Q4=A)
The rewritten instruction files and reviewer-facing deviations note must be:
- clearly redesign-native
- free of stale hourly/deposit guidance
- free of contradictory verification notes
- aligned with the actual shipped behavior

Stale redesign docs are treated as regression failures, not harmless drift.

### USAB-U24-03 — Reviewer-facing deviations/caveats must stay concise and actionable
The consolidated note should help a reviewer quickly understand:
- accepted redesign-era deviations still relevant to behavior
- known verification caveats still worth manual attention
- what should not be mistaken for a bug

It should not become a dump of every historical implementation event.

---

## Maintainability & Testability

### MAINT-U24-01 — Config normalization and validation stay concentrated in deterministic seams
The highest-value U-24 rules should remain practical to test outside the full SMAPI runtime:
- redesign config normalization
- per-key fallback behavior
- runtime snapshot publication
- targeted unchanged-behavior regressions
- i18n / documentation coherence checks where automated

### MAINT-U24-02 — Strong targeted example + property coverage is required (NFR-Q5=A)
Because U-24 is the final cleanup pass, it requires:
- focused example-based tests for key redesign config and regression scenarios
- meaningful FsCheck coverage where practical for deterministic config-mapping and cleanup invariants
- explicit regression coverage for the player-visible i18n boundary and redesign-doc coherence expectations

### MAINT-U24-03 — Property coverage must target the cleanup invariants
At minimum, FsCheck-friendly coverage for U-24 should exercise:
- redesign config normalization determinism
- per-key fallback stability for generated malformed/missing inputs where modeled
- targeted unchanged-behavior invariants still holding after cleanup
- documentation/config coherence checks where a deterministic seam exists

### MAINT-U24-04 — No new config or documentation framework is required
The quality bar should be met by clearer seam ownership and stronger tests, not by introducing:
- a new settings subsystem
- a custom in-world config UI
- a new documentation generator
- a second localization/lint pipeline

---

## Availability / Security / Infrastructure

### AVAIL-U24-01 — No availability-specific requirements
U-24 is an in-process cleanup seam with no external uptime, failover, or disaster-recovery surface.

### SEC-U24-01 — Security Baseline is N/A
Security Baseline is disabled project-wide. U-24 has no network, auth, or PII surface, so Security Baseline rules are N/A for this unit.

### INFRA-U24-01 — No infrastructure decisions introduced
U-24 requires no cloud, container, service, or deployment mapping beyond the existing `.NET 6` / SMAPI mod runtime.

---

## Property-Based Testing Obligations

### PBT-U24-01 — Redesign config determinism invariants
Equivalent redesign-era config input should normalize into equivalent runtime snapshot output regardless of incidental ordering or mixed saved-value layout.

### PBT-U24-02 — Per-key fallback invariants
Generated stale or malformed redesign-era inputs should fall back at the narrowest supported scope without changing unrelated valid config values.

### PBT-U24-03 — Targeted regression invariants
The unchanged high-risk behaviors selected for the final sweep should continue satisfying their earlier deterministic invariants after the cleanup changes land.

### PBT-U24-04 — Player-visible boundary invariants
Player-visible redesign text should remain i18n-routed while approved technical/debug literals remain outside the enforced boundary.

### PBT-U24-05 — Documentation coherence invariants
Where deterministic automated checks are practical, the rewritten build/test outputs should no longer describe the hourly/deposit/refund model as the active system of record.
