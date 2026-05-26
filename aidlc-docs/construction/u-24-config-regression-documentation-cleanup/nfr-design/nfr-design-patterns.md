# U-24 — NFR Design Patterns

**Unit**: U-24 — Config, Regression, and Documentation Cleanup

NFR design decisions applied: no additional question round required. NFR requirements NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, and NFR-Q5=A apply, along with functional-design decisions FD-Q1=A, FD-Q2=C, FD-Q3=A, FD-Q4=A, FD-Q5=A, and FD-Q6=A.

---

## Applicability Scope

| Category | Applicability |
|---|---|
| Security | **N/A** — Security Baseline is disabled project-wide and U-24 has no network/auth/PII surface |
| Scalability / HA | **N/A** — local in-process cleanup seam; no replicas, shards, queues, or distributed scale mechanisms |
| Distributed infrastructure | **N/A** — no service deployment, queue, cache server, async worker runtime, or external config service |
| Resilience | **Applicable** — clean-break config fallback, warning reliability, and regression-failure treatment for stale docs |
| Performance | **Applicable** — immediate synchronous GMCM/config interaction with bounded normalization/publish work |
| Determinism / correctness | **Applicable** — strict deterministic redesign-only config resolution and doc/config coherence |
| Maintainability / testability | **Applicable** — narrow normalization/fallback seams plus strong targeted example/property coverage |

---

## PAT-U24-01 — Immediate Synchronous Config Publication Loop

**What**: Redesign-era config edits flow through one bounded synchronous interaction path so the GMCM surface remains effectively immediate without introducing async machinery.

**Applies to**:
- `PERF-U24-01` GMCM/config interaction remains comfortably immediate
- `PERF-U24-02` reuse the existing config interaction shell
- `TS-U24-01` stay on the existing config/runtime publication shell

**How**:
- the optional GMCM surface edits `ModConfig`
- `ModConfigManager` remains the save/publish seam
- normalization and immutable snapshot publication stay synchronous and bounded
- documentation checks, lint sweeps, and broader regression work stay out of the live interaction path

**Why this pattern**:
- it preserves a responsive player-facing tuning experience
- it avoids adding a second config workflow or delayed publish pipeline
- it keeps the cleanup incremental and easier to reason about

---

## PAT-U24-02 — Deterministic Redesign-Only Normalization Authority

**What**: One deterministic normalization authority resolves redesign-only config into the effective runtime snapshot, excluding old hourly/deposit-era fields from player-facing truth.

**Applies to**:
- `REL-U24-01` redesign-only config resolution is strictly deterministic
- `REL-U24-02` determinism must not depend on incidental ordering
- `TS-U24-02` keep redesign config authority in the existing redesign-era fields
- `TS-U24-07` keep cleanup logic close to pure seams

**How**:
- saved config values are normalized through one authoritative mapper path
- incidental dictionary or registration ordering cannot change effective output
- compatibility leftovers are excluded from the authoritative player-facing model
- the published runtime snapshot remains the single effective tuning product

**Why this pattern**:
- it keeps the final cleanup from accidentally reviving mixed-authority config behavior
- it gives property testing one stable seam to target
- it keeps runtime behavior, GMCM, and docs aligned on the same config truth

---

## PAT-U24-03 — Per-Key Fallback Barrier with Maintainer-Facing Warning Path

**What**: Stale or malformed redesign-era config input is absorbed by a narrow per-key fallback barrier rather than by global reset or fail-fast player repair.

**Applies to**:
- `SAFE-U24-01` stale or malformed clean-break config degrades safely
- `SAFE-U24-02` fallback handling stays narrow and predictable
- `TS-U24-03` preserve deterministic normalization and per-key fallback

**How**:
- missing or invalid values fall back to defaults at the smallest practical scope
- unrelated valid redesign settings are preserved
- warnings remain maintainer-facing instead of surfacing as hard player-facing failures
- active-day committed runtime state is not retroactively mutated

**Why this pattern**:
- it preserves usability through the clean-break config transition
- it avoids section-wide over-reset behavior
- it keeps fallback behavior deterministic and testable

---

## PAT-U24-04 — Player-Visible Boundary Enforcement Through Existing i18n + Lint Gate

**What**: Player-visible redesign text remains enforced through the existing i18n-routing and lint boundary instead of through a new localization framework or all-literals ban.

**Applies to**:
- `USAB-U24-01` redesign-era config surface remains understandable
- `MAINT-U24-02` strong targeted verification includes the player-visible i18n boundary
- `PBT-U24-04` player-visible boundary invariants
- `TS-U24-04` keep the player-visible string boundary on the existing i18n + lint path

**How**:
- GMCM labels/tooltips and other player-visible cleanup text stay i18n-routed
- technical/debug/internal literals remain outside the enforced boundary
- the existing lint test remains the authoritative regression gate for this surface

**Why this pattern**:
- it preserves a practical, reviewable boundary
- it prevents localization cleanup from expanding into an unrelated architecture pass
- it keeps the enforcement seam stable for regression coverage

---

## PAT-U24-05 — Redesign-Native Documentation Coherence Gate

**What**: The rewritten build/test docs and reviewer-facing caveat note are treated as a coherence surface with regression-failure consequences when they drift back into stale hourly/deposit wording.

**Applies to**:
- `REL-U24-03` player-facing config and documentation must tell the same story
- `USAB-U24-02` rewritten docs stay fresh and internally consistent
- `PBT-U24-05` documentation coherence invariants
- `TS-U24-05` keep build/test docs in the existing artifact set and rewrite in place

**How**:
- the existing build/test artifact set is rewritten in place to the redesign model
- review/caveat notes remain bounded and aligned with shipped behavior
- stale hourly/deposit guidance is treated as a regression, not a harmless addendum artifact

**Why this pattern**:
- U-24 is the final cleanup pass, so documentation drift becomes a real quality issue
- it keeps reviewer-facing artifacts trustworthy
- it avoids split-brain behavior between code and docs

---

## PAT-U24-06 — Focused Regression Support for Unchanged High-Risk Behavior

**What**: Final cleanup verification stays intentionally targeted at unchanged but redesign-sensitive behavior rather than expanding into a full replay of every earlier unit.

**Applies to**:
- `SAFE-U24-03` targeted regression cleanup preserves unchanged safety invariants
- `MAINT-U24-02` strong targeted example + property coverage
- `MAINT-U24-03` property coverage targets cleanup invariants
- `TS-U24-06` keep final verification targeted and deterministic
- `TS-U24-08` tests stay on `xUnit` + `FsCheck`

**How**:
- focused example tests pin key config/fallback and regression stories
- FsCheck targets deterministic config mapping and selected unchanged invariants where practical
- output routing, tool snapshot/skip, stuck recovery, invulnerability, multiplayer guard, i18n boundary, and doc coherence remain explicit verification surfaces

**Why this pattern**:
- it closes the redesign with credible verification without turning U-24 into another feature unit
- it keeps the test bar proportional to the real final-cleanup risks
- it aligns well with the project's partial property-based testing mode

---

## Pattern Summary

U-24's NFR design stays intentionally focused:
- one immediate synchronous config publication loop
- one deterministic redesign-only normalization authority
- one narrow per-key fallback barrier with maintainer-facing warnings
- one existing i18n + lint enforcement path for player-visible text
- one redesign-native documentation coherence gate
- one focused regression-support strategy for unchanged high-risk behavior

That gives the final cleanup pass a strong responsiveness, determinism, resilience, and reviewability bar without adding new infrastructure or reopening the redesign's core gameplay architecture.
