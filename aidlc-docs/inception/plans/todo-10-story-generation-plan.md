# Story Generation Plan - TODO-10 SVE Grandpa's Shed Greenhouse

**Stage**: User Stories - Part 1 (Planning). This plan defines how the TODO-10 story updates will be made. Generation runs only after all answers are complete, ambiguities are resolved, and the plan is explicitly approved.

**How to use this file**: answer each `[Answer]:` tag with a letter choice. If none of the options match, choose `X` and describe the preference after the tag. Reply "done" when finished.

## Inputs
- Requirements: [todo-10-requirements.md](../requirements/todo-10-requirements.md)
- User story assessment: [todo-10-user-stories-assessment.md](todo-10-user-stories-assessment.md)
- Existing stories: [stories.md](../user-stories/stories.md)
- Existing personas: [personas.md](../user-stories/personas.md)
- Existing SVE story plan: [sve-compatibility-story-generation-plan.md](sve-compatibility-story-generation-plan.md)

## Established Project Conventions
- Story format: `As [persona], I want [capability], so that [benefit]`.
- Acceptance criteria: Gherkin for state transitions; bullets for UI, visual, or manual-verification notes.
- Traceability: stories include `Implements` references to requirement IDs.
- Organization: current project uses journey-based sections plus a feature-based SVE compatibility section.
- Personas are reviewed for each change and only expanded when a genuinely new stakeholder type appears.

## Story Breakdown Options
- **Refine Existing Story**: revise S-25 in place and update related traceability. Best when TODO-10 clarifies a previously broad story.
- **Add New Story**: create S-27 for TODO-10 while leaving S-25 mostly historical. Best when old and new behavior should remain separately visible.
- **Split Player and Maintainer Stories**: make player-facing route/work criteria live in S-25 while maintainer route-provider/testability criteria are added to S-26 or a new story.
- **Coverage-Only Minimalism**: update only the story text needed to remove contradictions and keep detailed route choices for design.

## Planning Checklist
- [x] Load User Stories rule details and question-format rules.
- [x] Load TODO-10 requirements and extension configuration.
- [x] Review existing SVE stories and personas.
- [x] Create User Stories assessment for TODO-10.
- [x] Create this story-generation plan with context-appropriate questions.
- [x] Collect answers for every `[Answer]:` tag in this file.
- [x] Analyze answers for ambiguity, contradiction, and missing generation details.
- [x] Create follow-up clarification questions if needed. No follow-up file required; answers are complete and unambiguous.
- [x] Get explicit approval for the resolved story-generation plan.

## Generation Checklist
- [x] Load this approved plan and identify the next unchecked generation step.
- [x] Update `stories.md` using the selected story-scope approach.
- [x] Ensure updated stories remain Independent, Negotiable, Valuable, Estimable, Small, and Testable.
- [x] Include acceptance criteria for shed greenhouse work scope, multi-hop route validation, graceful route failure, deposit routing, item safety, and manual SVE playtest expectations.
- [x] Update `personas.md` if the answers require a new or revised persona mapping.
- [x] Update the coverage summary so TODO-10 requirements are traced to the refined story set.
- [x] Update `aidlc-state.md` and append to `audit.md`.

## Mandatory Artifacts
- [x] `stories.md` updated with TODO-10 user stories or story refinements that meet INVEST and include acceptance criteria.
- [x] `personas.md` reviewed or updated with persona-to-story mapping.

## Generation Summary

- Refined S-25 in place as "Grandpa's Shed greenhouse is a selectable crop-work location."
- Updated S-25 acceptance criteria for single greenhouse selection, source-grounded multi-hop navigation, no direct farm-to-greenhouse success warp, runtime route validation, graceful skip/continue behavior, item-safe shed/main-shed deposit routing, vanilla invariance, and manual SVE playtest expectations.
- Updated S-26 to include explicit SVE multi-hop route provider data and pure route-model property-test expectations.
- Reviewed personas and kept P-01, P-02, and P-03 unchanged; updated mappings to mention the TODO-10 shed-greenhouse and route-provider responsibilities.
- Updated the coverage summary to trace TODO-10 requirements across S-25 and S-26.

## Planning Questions

## Question 1
What story update scope should TODO-10 use?

A) Refine existing S-25 and add targeted traceability updates to S-26 or S-22 only where needed. (Recommended)
B) Add a new S-27 for TODO-10 and leave S-25 mostly as historical broad SVE compatibility wording.
C) Split TODO-10 into a player-facing route/work story and a maintainer route-provider story, revising S-25 and S-26 accordingly.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2
How should personas be handled?

A) Keep existing P-01 Player, P-02 Farmhand, and P-03 Mod Maintainer; update mappings only if needed. (Recommended)
B) Add a distinct P-04 SVE Shed Greenhouse Player persona.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3
What acceptance-criteria style should the TODO-10 story update use?

A) Use Gherkin for route, scope, deposit, and failure behavior; use bullets for UI selection, manual playtest, and PBT traceability. (Recommended)
B) Keep story criteria broad and leave route-specific details to later design artifacts.
C) Write criteria mainly as a manual SVE playtest checklist with a short story summary.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 4
How much traceability should be updated around S-25?

A) Update S-25 and the coverage summary; adjust S-26 for route-provider and route-model testability criteria; add an S-22 cross-reference only if needed. (Recommended)
B) Update only S-25 and the coverage summary.
C) Update S-19, S-22, S-25, and S-26 so every pure route-model and SVE route concern is called out wherever related.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Extension Rule Compliance

| Extension | Status | Compliance / Rationale |
|---|---|---|
| Security Baseline | Disabled | Skipped per TODO-10 extension configuration. |
| Property-Based Testing | Enabled - Partial | Applicable. This plan explicitly asks how to reflect route-model PBT obligations and includes generation steps for pure route invariants where story criteria need them. |

## Content Validation
- Markdown only.
- No Mermaid diagrams.
- No ASCII diagrams.
- Question format uses multiple-choice options with `X) Other` as the last option and `[Answer]:` tags.

## Answer Analysis

| Question | Answer | Decision | Ambiguity Check |
|---|---|---|---|
| Q1 | A | Refine existing S-25 and add targeted traceability updates to S-26 or S-22 only where needed. | Clear; no contradiction. |
| Q2 | A | Keep P-01, P-02, and P-03; update mappings only if needed. | Clear; no new persona required. |
| Q3 | A | Use Gherkin for route, scope, deposit, and failure behavior; bullets for UI selection, manual playtest, and PBT traceability. | Clear; aligns with TODO-10 requirements. |
| Q4 | A | Update S-25, coverage summary, S-26 route-provider/testability criteria, and S-22 only if needed. | Clear; matches minimal story update mode. |

**Analysis result**: All answers are complete, valid, and mutually consistent. No clarification question file is required.

## Resolved Generation Approach

- Refine existing S-25 rather than adding S-27.
- Preserve the existing persona set.
- Make S-25 specific to selected `Custom_GrandpasShedGreenhouse` crop work, with `Custom_GrandpasShed` used only for deposit destinations.
- Update S-26 so the provider seam covers explicit SVE multi-hop route data and pure route-model testability.
- Update the coverage summary for TODO-10 traceability.
