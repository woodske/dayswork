# Story Generation Plan — Stardew Valley Expanded (SVE) Compatibility

**Stage**: User Stories — Part 1 (Planning). This plan defines *how* SVE-compatibility stories will be created. Generation (Part 2) runs only after you approve this plan.

**How to use this file**: answer each `[Answer]:` tag with a letter (or `X` + description). Reply "done" when finished. I'll analyze for ambiguities, ask follow-ups only if needed, then ask you to approve the plan before generating `stories.md` / `personas.md` updates.

**Inputs**:
- Requirements: [sve-compatibility-requirements.md](../requirements/sve-compatibility-requirements.md) (FR-SVE-01..16, NFR-SVE-01..07)
- Existing story set: [stories.md](../user-stories/stories.md) (S-01..S-20, journey-based) and [personas.md](../user-stories/personas.md) (P-01 Player, P-02 Farmhand, P-03 Maintainer)
- Assessment: [sve-compatibility-user-stories-assessment.md](sve-compatibility-user-stories-assessment.md)

---

## Established conventions in this project (proposed to carry forward)

- **Story format**: `As [persona], I want [capability], so that [benefit]`.
- **Acceptance criteria**: Gherkin (Given/When/Then) for state transitions; bullets for UI/visual rules.
- **Organization**: grouped by user journey; stories carry **Implements: FR-IDs** for traceability.
- **INVEST** compliance; **no prioritization markers** (consistent with the existing set).
- Personas reviewed each change; new personas added only if a genuinely new user type appears.

---

## Story breakdown options (Step 5 — for your awareness; Q1/Q3 below choose)

- **User Journey-Based** *(current project default)* — stories follow how a player encounters/uses the feature over time. Pro: continuity with S-01..S-20; con: SVE concerns are cross-cutting.
- **Feature-Based** — stories grouped by compatibility surface (maps, buildings, content, shed, detection). Pro: maps cleanly to FR-SVE groups; con: slight divergence from journey framing.
- **Persona-Based** — grouped by P-01/P-02/P-03. Pro: clarifies who benefits; con: splits related behavior.
- **Hybrid (recommended here)** — add one **feature-based "Expansion Compatibility (SVE)" section** to the existing journey-based set, plus a maintainer story in the existing maintainability section. Keeps continuity while mapping cleanly to the FR-SVE groups.

---

## Planning Questions

## Question 1 — Where do the SVE stories live?
A) **Hybrid (Recommended)** — add a new **"Section 6 — Expansion Compatibility (SVE)"** with new stories (S-21+), and add brief SVE notes/criteria to a few directly-affected existing stories (e.g., S-07 arrival/entrance, S-08 premium-animal servicing). Existing wording otherwise preserved.
B) New section only — add the new SVE section; do **not** modify any existing story.
C) In-place revision — fold SVE behavior into existing stories; no new section.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2 — Personas
A) **No new persona (Recommended)** — P-01 Player covers the SVE player and P-03 Maintainer covers the provider/extensibility concern; I'll review and confirm personas (matching how the Worker Routing change handled it).
B) Add a distinct **"Expansion-mod Player" persona (P-04)** to represent players running content expansions.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3 — Story granularity for the SVE section
A) **One story per compatibility surface (Recommended)** — roughly six: (a) detection + vanilla-invariance, (b) SVE farm maps + worker entrance, (c) premium barn/coop servicing, (d) new crops/trees/animals/products, (e) Grandpa's Shed as a work location, (f) maintainer extensibility/provider seam.
B) **Fewer, broader** — e.g., one player "Dayswork works on my SVE farm" story + one maintainer story.
C) **Finer-grained** — split surfaces further (e.g., a separate story per supported farm map).
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 4 — Maintainer extensibility story
A) **Include a dedicated P-03 maintainer story (Recommended)** — sibling to S-19/S-20: "adding a new expansion = implement one provider; the vanilla path is unchanged when no expansion is present; pure compatibility logic is xUnit + FsCheck tested." Encodes NFR-SVE-01/02/03/05.
B) Fold extensibility into the detection/vanilla-invariance story; no dedicated maintainer story.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 5 — Acceptance-criteria approach for SVE-dependent behavior
A) **(Recommended)** Keep Gherkin (state) + bullets (UI/visual) as today. For behavior that depends on SVE assets being loaded, mark those criteria as **"validated via manual SVE playtest"**, and add **PBT/xUnit** criteria for the pure compatibility logic (provider selection, entrance resolution, capacity/feeding derivation, content classification) — consistent with NFR-SVE-05.
B) Same formats, but without explicit manual-playtest annotations.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Execution Checklist (Part 2 — runs after plan approval)

- [x] Confirm/extend personas per Q2 (review P-01/P-02/P-03; add P-04 only if chosen).
- [x] Create the SVE stories per the breakdown chosen in Q1/Q3, following INVEST.
- [x] Each story: persona, capability, benefit, **Implements: FR-SVE-IDs**, and acceptance criteria per Q5.
- [x] Add the maintainer extensibility story if Q4=A.
- [x] Add SVE notes/criteria to directly-affected existing stories if Q1=A.
- [x] Update the coverage summary so every FR-SVE-* and NFR-SVE-* is traced to at least one story (the previously docs-only `FR-COMPAT` row is superseded/expanded).
- [x] Update `personas.md` Persona → Story coverage matrix.
- [x] Update `aidlc-state.md` and append to `audit.md`.

## Mandatory artifacts (Step 4)
- [x] `stories.md` updated with SVE user stories meeting INVEST + acceptance criteria.
- [x] `personas.md` reviewed/updated with persona → story mapping.
