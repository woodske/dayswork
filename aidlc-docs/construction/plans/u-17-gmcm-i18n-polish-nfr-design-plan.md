# NFR Design Plan — U-17 GMCM + i18n Polish

## Depth
Minimal — all U-17 NFR design choices are determined by approved requirements, the existing Core/Mod split, and the current implementation state.
No clarifying questions needed.

## Stages

- [x] Step 1: Analyze U-17 NFR requirements and tech-stack decisions
- [x] Step 2: Identify applicable design patterns
  - PAT-U17-01 Optional Dependency Probe and No-Op Registration
  - PAT-U17-02 Mutable Mod Config → Immutable Runtime Snapshot
  - PAT-U17-03 Single Metadata Table for GMCM Fields
  - PAT-U17-04 I18n-First Registration Surface
  - PAT-U17-05 Deterministic Source-Lint Gate with Explicit Allowlist
  - PAT-U17-06 One-Time Registration / Zero Tick Cost
  - PAT-U17-07 Current-Day Config Lock Preservation
- [x] Step 3: Confirm no questions needed (all ambiguity resolved by approved NFRs and current code)
- [x] Step 4: Generate `nfr-design-patterns.md`
- [x] Step 5: Generate `logical-components.md`
