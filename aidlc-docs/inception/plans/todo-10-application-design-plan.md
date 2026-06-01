# Application Design Plan - TODO-10 SVE Grandpa's Shed Greenhouse

**Stage**: Application Design. This plan defines how the high-level route-provider, navigation, scope, and deposit components will be designed for TODO-10.

**How to use this file**: answer each `[Answer]:` tag with a letter choice. If none of the options match, choose `X` and describe the preference after the tag. Reply "done" when finished.

## Inputs
- Requirements: [todo-10-requirements.md](../requirements/todo-10-requirements.md)
- User stories: [stories.md](../user-stories/stories.md), especially S-25 and S-26
- Personas: [personas.md](../user-stories/personas.md)
- Workflow plan: [todo-10-execution-plan.md](todo-10-execution-plan.md)
- Existing SVE application design: [sve-compatibility-application-design.md](../application-design/sve-compatibility-application-design.md)
- Current code seams: `IExpansionProfile`, `SveExpansionProfile`, `ExpansionCompatService`, `BuildingLocationResolver`, `BuildingWorkNavigator`, `LegacyScopeBootstrapper`, `ChestResolver`, and `ShiftOrchestrator`

## Design Scope
- Identify high-level components and interfaces for explicit SVE multi-hop route data.
- Define which component owns pure route definitions versus live route validation.
- Define which component coordinates cross-location route execution for work entry and deposit trips.
- Define how shed greenhouse selection and shed/main-shed chest discovery fit the existing UI/runtime surfaces.
- Preserve the existing single `GreenhouseSelection(LocationName)` model and avoid save-schema changes.

## Planning Checklist
- [x] Load Application Design rule details.
- [x] Load content-validation and question-format rules.
- [x] Load TODO-10 requirements, stories/personas, and execution plan.
- [x] Review existing route, scope, compat, and deposit seams.
- [x] Create this application design plan with context-appropriate questions.
- [x] Collect answers for every `[Answer]:` tag in this file.
- [x] Analyze answers for ambiguity, contradiction, and missing design details.
- [x] Add follow-up questions if needed. No follow-up questions required; all answers are complete and unambiguous.
- [x] Generate `components.md` addendum.
- [x] Generate `component-methods.md` addendum.
- [x] Generate `services.md` addendum.
- [x] Generate `component-dependency.md` addendum.
- [x] Generate consolidated `todo-10-application-design.md`.
- [x] Validate design completeness and consistency.
- [x] Update `aidlc-state.md` and append to `audit.md`.

## Mandatory Design Artifacts
- [x] `components.md` or a TODO-10 component addendum with component definitions and high-level responsibilities.
- [x] `component-methods.md` or a TODO-10 method addendum with high-level signatures and input/output expectations.
- [x] `services.md` or a TODO-10 service addendum with orchestration patterns.
- [x] `component-dependency.md` or a TODO-10 dependency addendum with dependency relationships and communication patterns.
- [x] `todo-10-application-design.md` consolidated design summary.

## Design Questions

## Question 1
Where should explicit SVE multi-hop route definitions live?

A) Extend the existing pure `IExpansionProfile` / `SveExpansionProfile` with route-definition lookups, keeping route data in `Dayswork.Core` and live validation in `ExpansionCompatService`. (Recommended)
B) Add a separate pure `IExpansionRouteProvider` selected alongside the active expansion profile, keeping route data out of `IExpansionProfile`.
C) Keep route definitions only in the Mod-side `ExpansionCompatService`, avoiding Core interface changes.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2
Which component should coordinate multi-hop route execution once a route validates?

A) Add a narrow `CrossLocationRouteNavigator` service that uses `WorkerMovementDriver` and `BuildingWorkNavigator` primitives while `ShiftOrchestrator` owns phase/state transitions. (Recommended)
B) Fold multi-hop support directly into `BuildingWorkNavigator`.
C) Keep all route-hop execution logic inside `ShiftOrchestrator`.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3
How should shed greenhouse selection and shed/main-shed chest discovery be exposed?

A) Extend the compat seam to provide expansion work/destination locations, then have `ChestResolver`/building-outline enumeration add virtual shed greenhouse and main-shed entries for UI selection and deposit destination discovery. (Recommended)
B) Discover any loaded location whose name contains `Greenhouse` or `GrandpasShed` without explicit expansion data.
C) Do not add UI discovery in this stage; support only already-saved `Custom_GrandpasShedGreenhouse` selections.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 4
Which component should own route-failure decisions?

A) Keep the pure route validator returning a result, with `ShiftOrchestrator` mapping work-route failure to skip-and-continue and deposit-route failure to existing undelivered/overflow handling. (Recommended)
B) Have the route provider suppress unavailable shed greenhouse selections before the shift starts, so the orchestrator rarely sees route failures.
C) Treat route failure like an exceptional building-resolution failure and reuse the existing building skipped warning path only.
X) Other (please describe after [Answer]: tag below)

[Answer]: A

## Extension Rule Compliance

| Extension | Status | Compliance / Rationale |
|---|---|---|
| Security Baseline | Disabled | Skipped per TODO-10 configuration. No security surface is introduced by the design plan. |
| Property-Based Testing | Enabled - Partial | Applicable. Questions favor pure route definitions and validation result models so PBT-02, PBT-03, PBT-07, PBT-08, and PBT-09 can be enforced in later stages where route-model properties apply. |

## Content Validation
- Markdown only.
- No Mermaid diagrams.
- No ASCII diagrams.
- Question format uses multiple-choice options with `X) Other` as the last option and `[Answer]:` tags.

## Answer Analysis

| Question | Answer | Decision | Ambiguity Check |
|---|---|---|---|
| Q1 | A | Extend `IExpansionProfile` / `SveExpansionProfile` with pure route-definition lookups; live validation stays in `ExpansionCompatService`. | Clear; aligns with existing SVE compat seam and TODO-10 centralization requirements. |
| Q2 | A | Add a narrow `CrossLocationRouteNavigator`; `ShiftOrchestrator` keeps phase/state authority. | Clear; separates live route movement from orchestration policy. |
| Q3 | A | Extend compat seam with expansion work/destination locations; `ChestResolver` and building-outline enumeration add virtual shed greenhouse/main-shed entries. | Clear; avoids heuristic name scans and preserves explicit SVE data ownership. |
| Q4 | A | Pure validator returns route result; `ShiftOrchestrator` maps work failure to skip-and-continue and deposit failure to existing undelivered/overflow handling. | Clear; preserves item-safety ownership. |

**Analysis result**: All answers are complete, valid, and mutually consistent. No clarification file is required.

## Resolved Design Approach

- Extend the existing `IExpansionProfile` and `SveExpansionProfile` route/data surface rather than adding an unrelated provider.
- Add `CrossLocationRouteNavigator` as a Mod-side runtime service for executing already-validated route hops.
- Extend `ExpansionCompatService` to bridge pure route/location data to live route validation and virtual work/destination discovery.
- Keep `ShiftOrchestrator` responsible for route-failure policy and state-machine transitions.
