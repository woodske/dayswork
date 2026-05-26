# U-24 — Logical Components

**Unit**: U-24 — Config, Regression, and Documentation Cleanup

NFR requirements NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, and NFR-Q5=A apply. Functional-design decisions FD-Q1=A, FD-Q2=C, FD-Q3=A, FD-Q4=A, FD-Q5=A, and FD-Q6=A apply throughout.

---

## Component Map

```text
Dayswork / Player-Facing Config Surface
  GMCMRegistrar                         [existing optional registration shell]
  ModConfigManager                      [existing editable/save/publish seam]
  ModConfig                             [existing saved config source]

Dayswork / Deterministic Config Resolution
  RuntimeConfigSnapshotMapper           [existing normalization + immutable snapshot authority]

Dayswork.Core / Fallback & Defaults
  ConfigValueResolver                   [existing narrow fallback/value-resolution seam]
  ConfigDefaults                        [existing redesign-era defaults authority]

Dayswork.Tests / Cleanup Regression Support
  RuntimeConfigSnapshotMapperTests      [existing test seam, expanded]
  ConfigValueResolverTests              [existing test seam, expanded]
  HardcodedUserFacingStringLintTests    [existing lint regression gate]
  U24ExampleTests                       [new or equivalent test-side grouping]
  U24PropertyGenerators                 [new or equivalent test-side helper]
  U24PropertyTests                      [new or equivalent test-side grouping]

aidlc-docs / Build and Test
  build-instructions.md                 [existing artifact, rewritten in place]
  unit-test-instructions.md             [existing artifact, rewritten in place]
  integration-test-instructions.md      [existing artifact, rewritten in place]
  performance-test-instructions.md      [existing artifact, rewritten in place]
  build-and-test-summary.md             [existing artifact, rewritten in place]
  U24 deviations/caveats note           [reviewer-facing documentation output]
```

No new config framework, UI framework, documentation generator, async publish worker, or alternate localization subsystem is introduced.

---

## LC-U24-01 — GMCMRegistrar (Optional Player-Facing Registration Shell)

**Layer**: App / optional config UI registration seam  
**Kind**: Existing production seam with preserved authority

**Purpose under U-24**:
- remain the single optional player-facing registration shell for the redesign-era settings surface

**Responsibilities**:
1. Register only redesign-era pricing, stamina, and worker-behavior controls
2. Keep the grouping aligned to the approved redesign mental model
3. Route player-visible field labels/tooltips through i18n keys
4. Avoid reviving hourly/deposit-era player-facing controls

**Important design constraints**:
- no second settings surface
- no player-facing mixed-authority legacy controls
- no heavy logic beyond field wiring/grouping

This seam remains the UI shell, not the owner of normalization or fallback behavior.

---

## LC-U24-02 — ModConfigManager (Bounded Edit / Save / Publish Seam)

**Layer**: App / config lifecycle seam  
**Kind**: Existing production seam with preserved constrained ownership

**Purpose under U-24**:
- keep redesign-era config editing, saving, and publication on one bounded synchronous path

**Responsibilities**:
1. Hold the editable config surface
2. Trigger normalization before persistence/publication
3. Publish the immutable runtime snapshot used by future previews, recurring rebuilds, and future shifts
4. Preserve the already-committed active-day runtime lock semantics

**Not responsible for**:
- defining the fallback rules themselves
- inventing a second config authority
- executing documentation or lint checks during live UI interaction

This seam is what keeps the config interaction path immediate and bounded.

---

## LC-U24-03 — RuntimeConfigSnapshotMapper (Deterministic Normalization Authority)

**Layer**: App / deterministic config mapping seam  
**Kind**: Existing production seam with strengthened authority

**Purpose under U-24**:
- remain the single authoritative path from saved redesign-era config to the effective immutable runtime snapshot

**Responsibilities**:
1. Normalize redesign-era saved config deterministically
2. Exclude hourly/deposit-era leftovers from the player-facing authoritative path
3. Produce equivalent snapshot output for equivalent input
4. Preserve valid ordering and value invariants before publication

**Important design constraints**:
- no incidental ordering dependence
- no parallel alternate normalization path
- no partial invalid snapshot publication

This is the primary owner of U-24's determinism bar.

---

## LC-U24-04 — ConfigValueResolver (Narrow Fallback / Value-Resolution Seam)

**Layer**: Core / pure fallback helper seam  
**Kind**: Existing production seam with preserved narrow ownership

**Purpose under U-24**:
- remain the narrow per-key value-resolution and default-fallback seam for redesign-era config input

**Responsibilities**:
1. Resolve missing or invalid values to redesign-era defaults at the smallest practical scope
2. Preserve unrelated valid settings
3. Support deterministic fallback behavior that is practical to property test

**Important design constraint**:
- it should not silently widen into section-wide reset or broad compatibility-era authority

This seam is what makes the clean-break config shape resilient without becoming fragile.

---

## LC-U24-05 — ConfigDefaults (Redesign-Era Default Authority)

**Layer**: Core / defaults authority  
**Kind**: Existing production seam with preserved ownership

**Purpose under U-24**:
- remain the canonical source of redesign-era default values used during clean-break fallback

**Responsibilities**:
1. Supply stable defaults for pricing, stamina, action costs, and worker behavior
2. Keep fallback behavior coherent across normalization and tests
3. Avoid hidden divergence between “reset” behavior and “missing key” fallback behavior

This seam matters because U-24's reliability bar depends on predictable fallback semantics.

---

## LC-U24-06 — HardcodedUserFacingStringLintTests (Player-Visible Boundary Gate)

**Layer**: `Dayswork.Tests` only  
**Kind**: Existing test-side enforcement seam with preserved authority

**Purpose under U-24**:
- remain the authoritative regression gate for player-visible hardcoded-string violations

**Responsibilities**:
1. Enforce the approved player-visible boundary
2. Preserve exemptions for technical/debug/internal literals
3. Catch redesign-surface regressions in GMCM, UI, mail, and other player-visible cleanup text

**Important design constraint**:
- this seam enforces the boundary; it does not require a new localization subsystem

---

## LC-U24-07 — Cleanup Regression Test Support

**Layer**: `Dayswork.Tests` only  
**Kind**: Dedicated final-cleanup verification support

### `RuntimeConfigSnapshotMapperTests`

**Purpose**:
- expand concrete coverage for redesign-only normalization, ordering, and active published snapshot expectations

### `ConfigValueResolverTests`

**Purpose**:
- expand concrete and generated coverage for narrow fallback behavior and malformed/missing input handling

### `U24ExampleTests`

**Purpose**:
- pin concrete final-cleanup stories such as:
  - redesign-only config publication ignoring legacy player-facing fields
  - malformed value falling back without wiping unrelated valid redesign settings
  - targeted unchanged-behavior regressions still holding after cleanup
  - documentation/i18n coherence checks where examples are more practical than properties

### `U24PropertyGenerators`

**Purpose**:
- generate redesign-era config and targeted cleanup contexts with varied:
  - threshold ordering cases
  - missing/invalid key placement
  - price/action-cost map variation
  - unchanged regression surface combinations where deterministic pure seams exist

### `U24PropertyTests`

**Purpose**:
- express invariants with FsCheck:
  - normalization determinism
  - narrow per-key fallback stability
  - unchanged high-risk behavior invariants still holding after cleanup
  - player-visible boundary or doc/config coherence invariants where deterministic checks are practical

These test-side components are explicit because U-24's quality bar is driven by cleanup trustworthiness more than by new gameplay behavior.

---

## LC-U24-08 — Build-and-Test Documentation Set (Rewritten In-Place Coherence Surface)

**Layer**: Documentation artifacts under `aidlc-docs`  
**Kind**: Existing artifact set with strengthened coherence ownership

**Purpose under U-24**:
- remain the single reviewer-facing build/test instruction set while being rewritten to the redesign-native model

**Responsibilities**:
1. Describe the fixed-price / worker-energy system as the active behavior
2. Include the explicit final regression checklist
3. Stay internally consistent across summary and detailed files
4. Avoid stale hourly/deposit/refund guidance

This surface matters in NFR design because the approved bar treats stale redesign docs as regression failures.

---

## LC-U24-09 — U24 Deviations / Caveats Note

**Layer**: Documentation artifact under `aidlc-docs`  
**Kind**: Reviewer-facing summary output

**Purpose under U-24**:
- provide one bounded reviewer-facing note for still-relevant redesign deviations and verification caveats

**Responsibilities**:
1. Summarize accepted redesign-relevant deviations still important to shipped behavior
2. Surface known verification caveats still worth manual attention
3. Avoid turning into a historical dump that duplicates `aidlc-state.md` or `audit.md`

This component exists because reviewability is part of the final cleanup quality bar.

---

## Interaction Summary

```text
player edits redesign-era config
  -> GMCMRegistrar exposes only approved redesign controls
  -> ModConfigManager collects/save-publishes the edited values
  -> RuntimeConfigSnapshotMapper normalizes deterministically
  -> ConfigValueResolver + ConfigDefaults supply narrow fallback where needed
  -> future previews / recurring rebuilds / future shifts consume the published snapshot

cleanup verification
  -> config and regression tests verify deterministic mapping + unchanged invariants
  -> HardcodedUserFacingStringLintTests enforce the player-visible string boundary
  -> rewritten build/test docs and U24 caveat note stay redesign-native and coherent
```

---

## Why no new config or documentation infrastructure was introduced

The NFR design intentionally does **not** add:
- a new settings framework
- an async config publication worker
- a custom in-world config UI
- a separate localization enforcement subsystem
- a documentation generator or parallel reviewer-reporting system

Reason:
- the cleanup is local and bounded by design
- the hardest risks are deterministic config authority, reliable fallback, and coherence across tests/docs/player-facing text
- the existing seams are sufficient if their ownership is clarified and the regression gates are strengthened

That keeps U-24's final cleanup incremental, testable, and consistent with the rest of the redesign.
