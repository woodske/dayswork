# U-SVE-04 — New Content + Grandpa's Shed — NFR Requirements Plan

**Unit**: U-SVE-04 · **Stories**: S-24, S-25 · **Stage**: Construction → NFR Requirements (Part 1: planning)

## Context (from Functional Design)
Category-based animal-product detection (replacing the ID whitelist), a content-classification override seam consulted in `ObjectTargetClassifier`, Grandpa's Shed as a crop-work location, and unique-name building keying. All on existing seams; no new components, no save-schema change, no new dependencies. Pure-Core decision logic + thin Mod adapters; vanilla path via the null-object profile.

### Performance note
Category detection runs once per ground object inside the **existing** scan loop (the old whitelist `HashSet.Contains` ran at the same point) — an `O(1)` category compare, no added passes. Content-override and `IsExpansionWorkLocation` lookups are `O(1)`. Grandpa's Shed is scanned like the greenhouse (one extra small indoor location when present).

---

## Questions (answer each with a lettered option; edit the `[Answer]:` line)

### Q1 — Detection cost / caching
- **A. (Recommended)** Compute category detection on-demand per object during the existing scan; no caching. Same call frequency and `O(1)` cost as the whitelist it replaces — no measurable regression. Content-override/work-location lookups stay `O(1)` and uncached.
- **B.** Cache per-object/per-tile classification. Adds state/invalidation for no benefit at this scale.

[Answer]: A

### Q2 — Failure behavior of new classification & work-location logic
- **A. (Recommended)** Fail-safe to vanilla and never throw: a missing/odd item category, descriptor, or location degrades to "no override / not a work location / skip", so an unexpected object can never interrupt a shift. Item-safety (overflow-to-mail) preserved; unclassifiable content is skipped.
- **B.** Throw/log-and-abort on unexpected content data. Unacceptable for a compatibility layer.

[Answer]: A

### Q3 — Tech stack / dependencies
- **A. (Recommended)** Reuse the existing stack (pure `Dayswork.Core` profile/descriptor logic + thin `Dayswork` adapters; xUnit + FsCheck). No new NuGet/runtime dependencies. Consistent with U-SVE-01/02/03.
- **B.** Introduce a new dependency/framework. Unjustified for this scope.

[Answer]: A

---

## Answers (recorded)
Q1=A · Q2=A · Q3=A (user: "continue", 2026-05-30). No ambiguity → no clarification round.

## NFR-requirements artifacts (Part 2) — generated
- [x] `construction/u-sve-04-content-grandpas-shed/nfr-requirements/nfr-requirements.md` — NFRU4-01..08 mapped to change-level NFR-SVE-*.
- [x] `construction/u-sve-04-content-grandpas-shed/nfr-requirements/tech-stack-decisions.md` — reuse-existing-stack rationale + extension compliance.

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
