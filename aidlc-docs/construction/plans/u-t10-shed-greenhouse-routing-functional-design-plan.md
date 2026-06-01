# Functional Design Plan - u-t10-shed-greenhouse-routing

**Unit**: `u-t10-shed-greenhouse-routing`
**Change**: TODO-10 SVE Grandpa's Shed greenhouse routing
**Stage**: Construction / Functional Design
**Status**: Functional Design complete - review required

## Context

This unit implements `Custom_GrandpasShedGreenhouse` as a selected alternative greenhouse work location for SVE farms. It keeps crop work limited to the existing greenhouse Water and Harvest services, uses explicit SVE route data for multi-hop movement, validates the live route instead of reading SVE quest flags, and preserves item safety for shed greenhouse and main shed deposit destinations.

Approved upstream decisions:

- Requirements: shed greenhouse crop work only; main `Custom_GrandpasShed` is deposit-only; standard greenhouse behavior remains unchanged.
- Application Design: route data belongs in `IExpansionProfile` / `SveExpansionProfile`; live validation belongs in `ExpansionCompatService`; movement belongs in `CrossLocationRouteNavigator`; route failure policy stays in `ShiftOrchestrator`.
- Workflow Planning: single construction unit, `u-t10-shed-greenhouse-routing`; next stages after Functional Design are NFR Requirements, NFR Design, Code Generation, then Build and Test.

## Functional Design Execution Plan

- [x] Load Functional Design rule details from `.aidlc-rule-details/construction/functional-design.md`.
- [x] Load common content-validation and question-format rules.
- [x] Load TODO-10 requirements, user-story, workflow-plan, and application-design context.
- [x] Review current code seams for expansion profiles, scope discovery, greenhouse routing, chest discovery, and shift failure handling.
- [x] Check enabled extension configuration for TODO-10.
- [x] Validate this plan content before creation: markdown only, no Mermaid diagrams, no ASCII diagrams, and all questions use `[Answer]:` tags with `X) Other` as the last option.
- [x] Create this Functional Design plan and question gate.
- [x] Collect answers from this file.
- [x] Validate every answer for completeness, invalid choices, ambiguity, and contradiction.
- [x] Evaluate clarification need: no clarification file required because all answers are valid, complete, and mutually consistent.
- [x] Generate `aidlc-docs/construction/u-t10-shed-greenhouse-routing/functional-design/business-logic-model.md`.
- [x] Generate `aidlc-docs/construction/u-t10-shed-greenhouse-routing/functional-design/business-rules.md`.
- [x] Generate `aidlc-docs/construction/u-t10-shed-greenhouse-routing/functional-design/domain-entities.md`.
- [x] Generate `aidlc-docs/construction/u-t10-shed-greenhouse-routing/functional-design/frontend-components.md` because scope and destination discovery affect the existing selection UI.
- [x] Update `aidlc-docs/aidlc-state.md` and `aidlc-docs/audit.md`.
- [x] Present the standardized Functional Design completion message and wait for review approval.

## Planned Functional Design Artifacts

| Artifact | Purpose |
|---|---|
| `business-logic-model.md` | Defines the route availability, route execution, shed greenhouse work, deposit routing, and route-failure decision flows. |
| `business-rules.md` | Captures scheduling, validation, work-scope, deposit, logging, item-safety, vanilla-invariance, and PBT-relevant rules. |
| `domain-entities.md` | Defines route, hop, request, validation result, expansion location, virtual outline, destination, and failure entities. |
| `frontend-components.md` | Documents how the existing scope/destination UI consumes the virtual shed greenhouse and deposit locations without adding a new UI control. |

## Questions

Please answer each question by filling in the letter choice after the `[Answer]:` tag. If none of the options match, choose `X) Other` and describe the preferred behavior after the tag.

### Question 1
When should Dayswork consider the shed greenhouse route available?

A) Discovery validates the route shape only - active SVE profile, target locations, configured route, and configured hop tiles exist - while the shift revalidates reachability and passability before movement. Recommended.
B) Discovery validates the full live route, including reachability and passability, so the shed greenhouse is hidden whenever the worker could not currently walk the whole path.
C) Discovery shows the shed greenhouse whenever the SVE profile supports it, and all route availability decisions happen only during the shift.
X) Other (please describe after the [Answer]: tag below)

[Answer]: A

### Question 2
How should each multi-hop route step be modeled in the functional design?

A) Model every hop as explicit route data: route id, source location, approach tile, target location, and arrival tile. The navigator walks to the approach tile, transitions locations, and never bypasses the intermediate shed path with a direct farm-to-greenhouse success warp. Recommended.
B) Use the existing building navigator for the farm-to-shed portion and model only the shed-to-greenhouse portion as custom route data.
C) Keep route sequencing as an orchestrator-owned special case with per-farm-map branch logic instead of a reusable route-hop model.
X) Other (please describe after the [Answer]: tag below)

[Answer]: A

### Question 3
What should happen if a configured route cannot validate during a shift?

A) Skip only the shed greenhouse batch or deposit trip that needed the route, continue other shift work, preserve collected items through the existing undelivered or overflow path, and write one maintainer-facing warning with route id, first failing hop, and reason. Recommended.
B) Skip all greenhouse work for the day, but continue non-greenhouse work and write one maintainer-facing warning.
C) Treat selected shed greenhouse route failure as a player-visible needs-attention condition.
X) Other (please describe after the [Answer]: tag below)

[Answer]: A

### Question 4
How should shed greenhouse and main shed chests be exposed as output destinations?

A) Expose shed greenhouse and main shed chests only for output from the selected shed greenhouse work scope; failed deposit routing is item-safe and falls back to undelivered or overflow handling. Recommended.
B) Once discovered, expose shed greenhouse and main shed chests as general chest destinations for any output scope that supports farm chests.
X) Other (please describe after the [Answer]: tag below)

[Answer]: A

### Question 5
Where should route failure policy live in the functional model?

A) `ShiftOrchestrator` owns policy decisions such as skip, continue, deposit fallback, and warning emission; route services return pure validation or navigation outcomes. Recommended.
B) `CrossLocationRouteNavigator` owns both route execution and high-level skip/deposit policy so the orchestrator sees only success or failure.
C) `ExpansionCompatService` owns availability, validation, and skip/deposit policy so SVE-specific route behavior stays behind the compat bridge.
X) Other (please describe after the [Answer]: tag below)

[Answer]: A

### Question 6
What property-based testing boundary should Functional Design define for this route model?

A) Define pure invariants for route lookup determinism, hop-order preservation, validation totality, no direct shortcut success path, skip/continue result mapping, and item-safety decision mapping; Code Generation will use FsCheck domain generators for these models. Recommended.
B) Define PBT only for route table shape and deterministic route selection; route failure and deposit/item-safety behavior are example-tested only.
C) Leave PBT details for NFR and Code Generation, with Functional Design documenting only example scenarios.
X) Other (please describe after the [Answer]: tag below)

[Answer]: A

## Extension Compliance at Question Gate

| Extension | Status | Functional Design question-gate result |
|---|---|---|
| Security Baseline | Disabled | Skipped per TODO-10 configuration; this plan introduces no network, authentication, secrets, or PII surface. |
| Property-Based Testing | Enabled - Partial | No blocking finding at question gate. PBT-02, PBT-03, PBT-07, PBT-08, and PBT-09 remain blocking where applicable; Question 6 will lock the design boundary for route invariants and FsCheck generators. |

## Content Validation

- Markdown tables and lists only.
- No Mermaid diagrams.
- No ASCII diagrams.
- No unescaped code fences with generated syntax.
- Questions use the required `[Answer]:` tag format.
- `X) Other` is the last option for every question.
