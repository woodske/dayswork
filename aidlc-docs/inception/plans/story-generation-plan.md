# Story Generation Plan — Dayswork

**Status**: Part 1 — Planning. Answer the embedded questions below, then approve the plan to move into Part 2 — Generation.

**Assessment**: See [user-stories-assessment.md](user-stories-assessment.md). Decision: **Execute User Stories**.

---

## How to use this document

1. Answer each `[Answer]:` line below with the letter of your choice (or `X` plus a custom description for Other).
2. When all answers are filled, reply with "done" or "approve".
3. I'll re-read your answers, raise any ambiguities, and then ask for explicit plan approval before generating the stories.
4. If a question's recommendation already matches what you want, just pick it.

---

## Planning Questions

Each question shapes how stories will be written and organized.

### Question 1 — Persona scope

How many user personas should we define?

A) **One unified "Stardew player" persona** — fastest, but loses nuance about why different players want different things
B) **3 playstyle-based personas** (e.g., "The Efficient Farmer", "The Animal Keeper", "The Time-Crunched Player") — moderate detail; recommended for a feature with this breadth
C) **5+ fine-grained personas** including narrative/roleplay personas, mining-focused, completionist, etc. — more detail, more story-to-persona mapping work
D) **2 personas: player + worker NPC** treated as a system actor — useful if we want stories like "as the worker, I path-find around obstacles"
X) Other (describe after [Answer]: tag below)

[Answer]: A — one unified "Stardew player" persona (combined with Farmhand and Mod Maintainer added separately in Q6/Q7 → total persona set = 3)

> Recommendation: **B**. The mod's surface area is big enough that distinct playstyles motivate genuinely different stories (an animal-keeper wants tight feed-loop guarantees; an efficient farmer wants the deposit/refund math airtight).

---

### Question 2 — Story breakdown approach

How should stories be organized in `stories.md`?

A) **User journey-based** — stories follow the temporal flow: discover the feature → first hire → daily life → handling edge cases → uninstall
B) **Feature-based** — stories grouped by component: Hiring UI / Worker Behavior / Payment / Mail / Config
C) **Persona-based** — stories grouped under each persona, with that persona's full journey under their section
D) **Epic-based** — a small number of epics (e.g., "Hire a Farmhand", "Watch them work", "Get paid") each broken into sub-stories
X) Other

[Answer]: A

> Recommendation: **A** (user journey-based). It naturally surfaces the order in which a Construction unit-of-work breakdown should happen, and journey order maps to the spec's "Suggested build order" (§Technical architecture).

---

### Question 3 — Story format

What template should each story use?

A) **Standard agile** — `As a [persona], I want [capability], so that [benefit]` + acceptance criteria
B) **Job story** — `When [situation], I want to [motivation], so I can [expected outcome]` (more context-driven, less actor-focused)
C) **Hybrid** — standard format for player-facing stories, job-story format for cross-cutting/system stories
X) Other

[Answer]: A

> Recommendation: **A**. Most readable for the broader Stardew modding community if you later open the docs.

---

### Question 4 — Acceptance criteria format

How should acceptance criteria be written?

A) **Gherkin (Given / When / Then)** — verbose but maps directly to xUnit + FsCheck test scaffolding
B) **Bullet checklist** — concise; easier to skim; mapping to tests is manual
C) **Hybrid** — Gherkin for behaviors involving state transitions (worker shift, deposit/refund); bullet lists for UI and visual rules
X) Other

[Answer]: C

> Recommendation: **C** (hybrid). Pays back during Construction when we feed criteria into PBT (which loves Given/When/Then framing) for the math-heavy stuff, while keeping the UI stories readable.

---

### Question 5 — Story granularity

How big should each story be?

A) **Thin slices** — each story is independently shippable; maybe 25–35 small stories total
B) **Moderate** — fewer, slightly larger stories (maybe 12–20) each covering a coherent capability
C) **Thick epics + sub-stories** — 4–6 epics, each with 3–6 sub-stories
X) Other

[Answer]: B (12–20 moderate stories)

> Recommendation: **B**. Avoids both extremes — granular enough for clear acceptance criteria, not so granular that Construction units fragment.

---

### Question 6 — Worker as a persona?

Should the worker NPC have stories written from its perspective (a "system actor" persona)?

A) **Yes** — add a "Farmhand" persona; stories like "as the Farmhand, I path-find around obstacles" make AI/state-machine behavior concrete
B) **No** — keep all stories from the player's perspective; encode worker behavior as acceptance criteria on player stories
X) Other

[Answer]: A

> Recommendation: **A**. The worker's behavior is rich enough (stuck escalation, capability snapshot, deposit runs, festival skip) that "as the Farmhand" framing reads better than "as the player I expect the worker to…" for many requirements.

---

### Question 7 — Developer / operator stories?

Should we include any stories from the developer/maintainer perspective (you, future-you, contributors)?

A) **No** — developer onboarding is captured in NFR-ONBOARD-01/02; not modeled as user stories
B) **Yes, lightly** — 1–2 stories like "as the mod maintainer, I want pure logic separated from SMAPI integration so I can unit-test without launching the game" to anchor architectural choices
X) Other

[Answer]: B (yes, lightly — 1–2 maintainer stories)

> Recommendation: **B**. A small number of these makes the testability/maintainability decisions traceable from stories to code.

---

### Question 8 — Story prioritization signal

Do you want a priority indicator on each story (Must / Should / Could / Won't, or P0/P1/P2)?

A) **Yes, MoSCoW** (Must / Should / Could / Won't)
B) **Yes, simple P0/P1/P2**
C) **No prioritization** — all v1 stories are equally must-have; out-of-scope items already live in §4 of requirements.md
X) Other

[Answer]: C (no prioritization)

> Recommendation: **C**. All FRs in v1 are already in-scope; explicit prioritization would just duplicate information. Items deferred to v2 are already captured in `§4 Out of Scope` of requirements.md.

---

## Story Plan Checklist (executes in Part 2 after approval)

When you approve, Part 2 will execute the following:

- [x] Generate `aidlc-docs/inception/user-stories/personas.md` with the chosen persona set (per Q1) — 3 personas: Player, Farmhand, Mod Maintainer
- [x] Generate `aidlc-docs/inception/user-stories/stories.md` with stories grouped per the chosen breakdown approach (per Q2) — journey-based: Discovery → First day → Daily life → Edge cases → Maintainability
- [x] Each story uses the chosen template (per Q3) — standard agile
- [x] Each story has acceptance criteria in the chosen format (per Q4) — hybrid Gherkin/bullets
- [x] Story sizing follows the chosen granularity (per Q5) — 20 stories, within the 12–20 moderate band
- [x] Worker NPC persona/stories included per Q6 — P-02 Farmhand persona drives S-08, S-09, S-15, S-16, S-17
- [x] Developer/maintainer stories included per Q7 — S-19 (testable pure logic), S-20 (i18n)
- [x] Prioritization applied per Q8 (if any) — none, as agreed
- [x] All stories tagged with the FR-IDs they implement (traceability) — see Coverage Summary in stories.md
- [x] Stories satisfy INVEST (Independent, Negotiable, Valuable, Estimable, Small, Testable)
- [x] Personas mapped to stories in personas.md (which personas care about which stories) — Persona → Story Coverage Matrix in personas.md
- [x] Story plan checkboxes updated to `[x]` as each artifact lands

---

## Out of scope for this stage

Per Stage 11 in the user-stories rules: this stage does **not** include sprint planning, sequencing into units of work, or technical implementation details. Story-to-unit decomposition happens in **Application Design** and **Units Generation** later in Inception.
