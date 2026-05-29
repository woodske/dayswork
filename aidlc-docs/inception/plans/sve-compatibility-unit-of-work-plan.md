# Unit of Work Plan — Stardew Valley Expanded (SVE) Compatibility

**Stage**: Units Generation — Part 1 (Planning). Defines how the SVE-compatibility work decomposes into units. Generation runs only after you approve answers.

**How to use this file**: answer each `[Answer]:` tag with a letter (or `X` + description). Reply "done" when finished. I'll analyze for ambiguities, then ask you to approve before generating the unit artifacts.

**Inputs**: Requirements (FR-SVE-01..16, NFR-SVE-01..07); Stories S-21..S-26; Application Design ([sve-compatibility-application-design.md](../application-design/sve-compatibility-application-design.md)); Execution plan ([sve-compatibility-execution-plan.md](sve-compatibility-execution-plan.md)).

**Existing baseline**: Historical units U-01..U-17 plus retrofit U-18..U-24 and U-WR are the already-built baseline. SVE units are appended after that baseline and depend only on existing built code, not on each other except via the foundation.

---

## Proposed decomposition (for your review; Q1–Q3 refine)

| Unit | Scope | Components (from App Design) | Stories / FRs |
|---|---|---|---|
| **U-SVE-01** Provider foundation + detection | The full compat seam shell + SVE detection/selection + vanilla invariance | C-19, C-20, C-21, C-22 (shell), C-23, M-22, M-23 | S-21, S-26; FR-SVE-01/02/03/04; NFR-SVE-01/02/03/05/06/07 |
| **U-SVE-02** Farm maps + worker entrance | Per-map entrance overrides; orchestrator delegates to the seam | C-22 entrance table; `ShiftOrchestrator` call site | S-22; FR-SVE-05/06/15 |
| **U-SVE-03** Animal buildings | Data-driven capacity/feeding; premium→vanilla-tier mapping in scope | C-23 consumption; `AnimalTaskHandler` + scope | S-23; FR-SVE-07..11 |
| **U-SVE-04** New content + Grandpa's Shed | Content-classification overrides; Grandpa's Shed as a work location | C-22 content/work-location tables; `ObjectTargetClassifier` + building navigators | S-24, S-25; FR-SVE-12/13/14/15/16; NFR-SVE-04 |

**Dependencies**: U-SVE-01 is the foundation; U-SVE-02, U-SVE-03, U-SVE-04 each depend only on U-SVE-01 and are independent of each other.

---

## Planning Questions

## Question 1 — Unit count / granularity
A) **(Recommended)** Four units as proposed (U-SVE-01 foundation; U-SVE-02 maps/entrance; U-SVE-03 animal buildings; U-SVE-04 content + Grandpa's Shed). Each is independently playtestable.
B) Three units — foundation + two combined slices (maps+animals; content+shed).
C) Two units — foundation + everything else.
D) Five units — split Grandpa's Shed (building navigation) out of content into its own U-SVE-05.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2 — Execution order of the override units (all depend on U-SVE-01)
A) **(Recommended)** U-SVE-01 → U-SVE-02 (entrance) → U-SVE-03 (animal buildings) → U-SVE-04 (content + shed). Entrance first, because the worker must spawn correctly on an SVE map before any other SVE behavior is observable/playtestable.
B) Order by risk/uncertainty: foundation → content + shed → animal buildings → maps.
C) Other order (describe).
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3 — Foundation scope (what U-SVE-01 ships)
A) **(Recommended)** U-SVE-01 delivers the complete seam (C-19..C-23, M-22/M-23) with the **Vanilla profile fully working** and the **SVE profile present but with its override tables filled in incrementally by U-SVE-02..04**. This makes U-SVE-01 independently verifiable: vanilla is provably unchanged, SVE is detected, and no SVE override behavior exists yet.
B) Merge the foundation with the first override (entrance) so U-SVE-01 also delivers SVE farm-map spawning.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Execution Checklist (generation — runs after answers approved)

- [x] Append U-SVE-01..04 (per Q1) to `application-design/unit-of-work.md` with scope, components, stories/FRs, and primary expected files.
- [x] Append the SVE dependency sub-graph + execution order (per Q2/Q3) to `application-design/unit-of-work-dependency.md`.
- [x] Append SVE story→unit mapping (S-21..S-26) to `application-design/unit-of-work-story-map.md`.
- [x] Validate: every SVE story/FR assigned to a unit; no unit depends on a later SVE unit; foundation-first preserved.
- [x] Update `aidlc-state.md` and append to `audit.md`.

## Mandatory artifacts (Step 2)
- [x] `unit-of-work.md`, `unit-of-work-dependency.md`, `unit-of-work-story-map.md` updated with the SVE units.
