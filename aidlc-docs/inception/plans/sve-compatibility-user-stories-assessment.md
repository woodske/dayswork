# User Stories Assessment — Stardew Valley Expanded (SVE) Compatibility

## Request Analysis

- **Original Request**: Make Dayswork compatible with Stardew Valley Expanded (new farm maps & entrances, premium barn/coop, new crops/trees, Grandpa's Shed, new animals/products) without affecting vanilla, isolating SVE code and architecting for easy addition of other expansion mods.
- **User Impact**: Direct. A player running SVE directly experiences whether the farmhand spawns correctly, feeds a 16-animal premium barn, works new content, and uses Grandpa's Shed.
- **Complexity Level**: Complex. Spans worker spawn/entrance, animal-building capacity/feeding, building navigation, world-content classification, a new mod-detection/provider seam, and the hiring-scope model.
- **Stakeholders**:
  - P-01 Stardew Player (now specifically: a player running SVE)
  - P-02 Farmhand (system actor — its behavior on SVE maps/buildings)
  - P-03 Mod Maintainer (provider seam, isolation, extensibility, testability)

## Assessment Criteria Met

- [x] High Priority: User-experience change to an existing user-facing workflow (the hired farmhand) under a new content context.
- [x] High Priority: Complex behavior with multiple scenarios and acceptance-criteria needs (multiple farm maps, premium buildings, new content kinds, a new building).
- [x] Medium Priority: Integration work (mod detection / provider seam) that affects user workflows.
- [x] Medium Priority: Multiple components involved.
- [x] Benefits: Stories make the desired visible behavior easy to validate in SVE playtesting, and capture the maintainer's isolation/extensibility intent as testable criteria.
- [x] First story coverage of the pre-existing `FR-COMPAT` requirement group (previously docs-only, no story).

## Decision

**Execute User Stories**: Yes

**Reasoning**: This is not internal-only refactoring. It changes what the player observes when running an expansion (where the worker spawns, whether premium buildings are fully serviced, whether new content and Grandpa's Shed are handled) and it establishes an extensibility contract the maintainer must be able to reason about. Stories + acceptance criteria are the clearest way to validate these across the supported SVE maps and to pin the "vanilla unchanged" guarantee.

## Expected Outcomes

- Add SVE-compatibility stories to the existing journey-based set, plus a maintainer story for the provider seam / extensibility.
- Capture vanilla-invariance ("no SVE → identical behavior") as an explicit, testable story.
- Provide testable, playtest-oriented scenarios per supported SVE map and per premium building for later functional design and code generation.
- Review the existing persona model and confirm whether a new persona is needed (expected: no — P-01/P-03 cover it).

## Extension Compliance

| Extension | Status | Assessment-stage compliance |
|---|---|---|
| Security Baseline | Disabled | N/A — no security surface for this compatibility change. |
| Property-Based Testing | Enabled, full | N/A for the assessment itself; later story/design/code stages must preserve full enforcement (provider selection, entrance resolution, capacity derivation, and content classification are pure logic suited to FsCheck). |
