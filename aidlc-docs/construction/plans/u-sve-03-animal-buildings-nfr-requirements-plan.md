# U-SVE-03 — SVE Animal Buildings — NFR Requirements Plan

**Unit**: U-SVE-03 · **Story**: S-23 · **Stage**: Construction → NFR Requirements (Part 1: planning)

## Context (from Functional Design)
Two narrow data-driven changes on existing seams: (1) replace the hardcoded `FeedCapacity` ladder with `ResolveAnimalFeedCapacity` = `min(troughTiles, MaxOccupants)`; (2) populate `MapPremiumBuildingTier` (Premium→Deluxe) consulted ahead of vanilla tier inference. No new components, no enum/save change, no new dependencies. Pure-Core decision logic + thin Mod adapters; vanilla path via null-object profile.

### Sole new performance consideration
`ExpansionCompatService` derives capacity by counting `Trough` map-property tiles over the interior (`CountTroughTiles`, O(W×H) on a small building interior). The **auto-feed gate runs first** in `CreateFeedWork`, so SVE premium (and vanilla Deluxe) buildings **skip the scan entirely**. Only non-auto-feed buildings scan, and only when producing feed work — a handful of times per shift on a small map.

---

## Questions (answer each with a lettered option; edit the `[Answer]:` line)

### Q1 — Capacity trough-scan: on-demand vs cached
- **A. (Recommended)** Compute capacity on-demand (count `Trough` tiles each time feed work is created). No caching. Justification: auto-feed buildings short-circuit before the scan; non-auto-feed scans are infrequent (per feed-work creation, not per tile/frame) over a small interior. Keeps the logic stateless and trivially correct, matching the U-SVE-02 "no caching needed" stance.
- **B.** Cache the trough count per building/day. Adds invalidation/state for no measurable benefit at this scale.

[Answer]: A

### Q2 — Failure behavior of capacity & tier resolution
- **A. (Recommended)** Fail-safe to vanilla and never throw: if `MaxOccupants`/map data is unavailable, capacity derivation returns a clamped/zero-safe value (already total in `AnimalBuildingCapacityPolicy`); if the profile has no premium mapping, tier resolution returns the supplied vanilla tier unchanged. No exception escapes into the shift runtime.
- **B.** Throw/log-and-abort on unexpected building data. Risks interrupting a shift over a cosmetic data gap — not acceptable for a compatibility layer.

[Answer]: A

### Q3 — Tech stack / dependencies
- **A. (Recommended)** Reuse the existing stack (pure `Dayswork.Core` policy/profile + thin `Dayswork` adapter; xUnit + FsCheck for tests). No new NuGet/runtime dependencies. Consistent with U-SVE-01/02.
- **B.** Introduce a new dependency or framework. Unjustified for this scope.

[Answer]: A

---

## Answers (recorded)
Q1=A · Q2=A · Q3=A (user: "continue", 2026-05-30). No ambiguity → no clarification round.

## NFR-requirements artifacts (Part 2) — generated
- [x] `construction/u-sve-03-animal-buildings/nfr-requirements/nfr-requirements.md` — NFRU3-01..08 mapped to change-level NFR-SVE-*.
- [x] `construction/u-sve-03-animal-buildings/nfr-requirements/tech-stack-decisions.md` — reuse-existing-stack rationale + extension compliance.

## Plan checkboxes
- [x] Step 1 — Analyze functional design
- [x] Step 2 — Create NFR requirements plan
- [x] Step 3 — Generate questions (Q1–Q3)
- [x] Step 4 — Store plan
- [x] Step 5 — Collect & analyze answers (Q1–Q3 = A; no ambiguity)
- [x] Step 6 — Generate NFR-requirements artifacts
- [x] Step 7 — Present completion message
- [ ] Step 8 — Await approval
- [ ] Step 9 — Record approval & update state
