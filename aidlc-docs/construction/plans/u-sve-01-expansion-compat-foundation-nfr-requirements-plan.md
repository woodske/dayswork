# NFR Requirements Plan — U-SVE-01 Expansion-Compatibility Provider Foundation

**Stage**: CONSTRUCTION → U-SVE-01 → NFR Requirements (Part 1: plan + questions). Artifacts generated after answers resolve.

**How to use this file**: answer each `[Answer]:` tag with a letter (or `X` + description). Reply "done" when finished.

**Context**: Most NFRs for this change are already fixed by the approved requirements (NFR-SVE-01 isolation/invariance, NFR-SVE-02 extensibility, NFR-SVE-03 grounded correctness, NFR-SVE-05 testability, NFR-SVE-06 performance, NFR-SVE-07 maintainability) and the U-SVE-01 functional design. These questions confirm the unit-level quality bar and tech stack; defaults reflect the existing project conventions.

---

## Question 1 — Reliability posture if expansion detection/seam construction misbehaves
A) **(Recommended)** **Fail safe to the Vanilla profile** — wrap detection/selection in a guard; on any unexpected error, log a warning and run as vanilla. Compatibility detection must never break the mod (supports NFR-SVE-01/04).
B) Treat a detection failure as a hard error that disables Dayswork's worker features for the session with a player-facing message.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2 — Performance envelope for the seam
A) **(Recommended)** Detection runs **once** at `GameLaunched`; the active profile is **cached**; all seam lookups are constant-time with **no per-tile reflection and no per-frame mod-registry queries**; the change introduces no measurable per-shift regression versus the Worker Routing baseline (NFR-SVE-06).
B) A stricter/explicit numeric performance target (please describe).
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3 — Tech stack
A) **(Recommended)** **Reuse the existing stack, no additions**: pure logic in `Dayswork.Core` (zero SMAPI/Stardew refs), Mod adapters in `Dayswork`, tests in `Dayswork.Tests` (xUnit + FsCheck, full PBT). No new libraries or frameworks.
B) Introduce a new library/framework for this unit (please describe).
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Execution Checklist (artifact generation — after answers resolved)

- [x] Create `construction/u-sve-01-expansion-compat-foundation/nfr-requirements/nfr-requirements.md` (reliability/fail-safe, performance envelope, determinism, testability/PBT bar, maintainability/isolation — mapped to NFR-SVE-*).
- [x] Create `.../nfr-requirements/tech-stack-decisions.md` (reuse C#/.NET 6 + xUnit + FsCheck; Core-pure/Mod-adapter split; no new deps; rationale).
- [x] Extension compliance (Security N/A; PBT full).
- [x] Update `aidlc-state.md` and append to `audit.md`.
