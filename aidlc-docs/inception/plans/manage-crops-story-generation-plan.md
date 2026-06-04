# Story Generation Plan — Manage Crops

**Stage**: User Stories — Part 1 (Planning)
**Source**: [manage-crops-requirements.md](../requirements/manage-crops-requirements.md) (FR-MC-01..44, NFR-MC-01..09)
**Existing story set**: S-01..S-26 in [stories.md](../user-stories/stories.md) — journey-organized, `As/I want/so that`, Gherkin (state) + bullets (UI), INVEST, FR-traceable. New stories will continue at **S-27**.
**Personas**: P-01 Player, P-02 Farmhand, P-03 Mod Maintainer ([personas.md](../user-stories/personas.md)).

---

## Methodology (proposed)
- Follow the established project conventions exactly (same story/AC format, INVEST, FR traceability, coverage summary, persona matrix).
- Add a new **journey section** to `stories.md` for the Manage Crops player/farmhand flow, plus a maintainer story in the Maintainability section.
- Map every FR-MC-* / NFR-MC-* to at least one story; extend the coverage summary + persona matrix.

---

## Planning Questions

Please answer each by filling in the letter after the `[Answer]:` tag. Each notes the recommended option and how it fits existing conventions.

### Question 1 — Story breakdown approach
How should the Manage Crops stories be organized?

A) **New "Manage Crops" user-journey section** (authoring → first managed shift → shopping → ongoing/edge cases), plus one maintainer story in Section 5 — consistent with how S-21..S-26 were added for SVE. (Recommended)
B) Feature-based grouping (one story per subsystem: UI, planting, shopping, chests, persistence) regardless of journey.
C) A single large epic with sub-stories.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 2 — Story granularity
How finely should the feature be sliced into stories?

A) **Focused set (~7–9 stories)** — each an independently valuable, testable slice that aligns naturally with likely units of work (authoring UI, draw overlay, planting/maintenance shift behavior, purchasing trip, two cabin chests, output routing, greenhouse/shed, persistence/migration, maintainer seam). (Recommended)
B) Coarse (~3–4 broad stories) — fewer, larger stories.
C) Fine-grained (~12+ small stories) — maximum decomposition.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 3 — Personas
Does this feature need a new persona, or do the existing three suffice?

A) **Reuse existing P-01/P-02/P-03** — Player authors/observes, Farmhand executes, Maintainer owns the new seams; no new persona. (Recommended)
B) Add a new persona (please describe after [Answer]: tag).
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 4 — Maintainer story for the new technical seams
The feature adds genuinely new technical surface: town-store cross-location navigation, headless 1.6 shop transactions (`ShopBuilder`/`Data/Shops`), new pure planning logic (viability, `min(seeds,fertilizer)`, multi-season locking), and the V2→V3 persistence bump + input-chest backfill. How should this be storied?

A) **One dedicated maintainer story** (peer to S-26) anchoring the new navigation/shop seam, the pure-Core planning logic, and the persistence/migration + PBT obligations. (Recommended)
B) Fold these concerns into the relevant player/farmhand stories' acceptance criteria; no separate maintainer story.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 5 — Acceptance-criteria depth for PBT obligations
PBT is enabled in **full mode** for this change. How explicit should PBT obligations be in the stories?

A) **Embed explicit PBT obligation criteria** (deterministic viability, `min(seeds,fertilizer)` completion, multi-season locking, store/fallback resolution, save round-trip, replayable failures) in the maintainer story — mirroring S-19/S-26. (Recommended)
B) Keep PBT obligations out of the stories and defer them entirely to Functional/NFR design.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Mandatory Story Artifacts (to be produced in Part 2 — Generation)
- [x] Append new Manage Crops journey stories (S-27..S-34) to `stories.md` following INVEST + the established AC format.
- [x] Append the maintainer story (S-35) to Section 5.
- [x] Ensure every story is Independent, Negotiable, Valuable, Estimable, Small, Testable.
- [x] Include acceptance criteria (Gherkin for state, bullets for UI) for each story.
- [x] Map every FR-MC-* / NFR-MC-* into the coverage summary table.
- [x] Review personas; update `personas.md` (story interests + persona→story matrix) — confirmed no new persona (Q3=A).

---

## Execution Checklist (Part 2)
- [x] Step A — Add new "Section 7 — Manage Crops" journey stories to `stories.md` (per approved breakdown).
- [x] Step B — Add the maintainer story (S-35) to Section 5.
- [x] Step C — Extend the Coverage Summary table with FR-MC-*/NFR-MC-* rows.
- [x] Step D — Update `personas.md` (story interests + matrix; confirmed persona set).
- [x] Step E — Update `aidlc-state.md` + `audit.md`; present completion for approval.

---

When you've answered the questions above, let me know (e.g. "done"). I'll check the
answers for ambiguity/contradiction and, once the plan is approved, generate the stories.
