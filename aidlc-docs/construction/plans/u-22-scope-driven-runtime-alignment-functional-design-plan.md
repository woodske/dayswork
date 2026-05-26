# U-22 — Scope-Driven Runtime Alignment: Functional Design Plan

**Unit**: U-22 — Scope-Driven Runtime Alignment  
**Stories**: S-03, S-04, S-08, S-10, S-11  
**Phase**: CONSTRUCTION — Functional Design  
**Status**: Answers reviewed, no clarification round needed, and functional-design artifacts generated. Pending user review.

---

## Plan Checklist

- [x] Load unit definition, story map, refreshed requirements, refreshed application design, and relevant brownfield runtime/output files
- [x] Inspect current typed-scope consumption, animal targeting, greenhouse handling, deposit routing, and overflow mail behavior
- [x] Draft FD-Q1 through FD-Q9
- [x] Collect answers to FD-Q1 through FD-Q9
- [x] Analyze answers for ambiguity or contradictions and create clarification questions if needed
- [x] Generate `business-logic-model.md`
- [x] Generate `domain-entities.md`
- [x] Generate `business-rules.md`
- [x] Generate `frontend-components.md` (if warranted by the approved answers)
- [x] Present question file and await user answers

---

## Context Loaded

- [unit-of-work.md](../../inception/application-design/unit-of-work.md) — U-22 definition and definition of done
- [unit-of-work-story-map.md](../../inception/application-design/unit-of-work-story-map.md) — story ownership for `S-03`, `S-04`, `S-08`, `S-10`, and `S-11`
- [requirements.md](../../inception/requirements/requirements.md) — typed-scope, greenhouse, animal-building, and output fallback requirements
- [stories.md](../../inception/user-stories/stories.md) — runtime expectations for scope-aware execution, deposit behavior, and overflow/unassigned output
- [application-design.md](../../inception/application-design/application-design.md) — redesign summary and typed-scope architectural boundaries
- [components.md](../../inception/application-design/components.md) — target responsibilities for `ZoneGeometry`, `DepositPlanner`, `ShiftOrchestrator`, `ChestResolver`, and `MailDispatcher`
- Brownfield implementation review:
  - [AnimalTaskHandler.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Orchestration/AnimalTaskHandler.cs)
  - [ShiftOrchestrator.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Orchestration/ShiftOrchestrator.cs)
  - [ChestResolver.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Integration/ChestResolver.cs)
  - [DepositPlanner.cs](/C:/Users/kwood/Repos/dayswork/Dayswork.Core/Inventory/DepositPlanner.cs)
  - [MailDispatcher.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/Integration/MailDispatcher.cs)
  - [ZoneAndChestMenu.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/UI/ZoneAndChestMenu.cs)
  - [OutputDestinationsMenu.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/UI/OutputDestinationsMenu.cs)

---

## What This Unit Must Define

U-22 is the runtime-alignment retrofit that makes live execution and output routing consume the redesign-era typed scope model consistently.

This unit owns the functional behavior of:
- `C-08 ZoneGeometry`
- `C-10 TaskPriorityOrderer`
- `C-14 DepositPlanner`
- `M-05 ZoneAndChestMenu`
- `M-12 ShiftOrchestrator`
- `M-16 MailDispatcher`
- `M-20 ChestResolver`

This unit must define:
- which saved scope representation is authoritative at runtime for redesign-era contracts
- how selected barns/coops become building-owned animal scopes independent of outdoor zone geometry
- how greenhouse work becomes a dedicated crop-work execution scope rather than generic building geometry
- whether output destinations remain task-owned or become more granular under typed scope
- how overflow and unassigned-output mail behavior should stay stable under typed scope execution
- how older compatibility-era contracts should enter the same typed runtime path
- whether any small scope-page wording updates are needed so the UI still matches the runtime rules

---

## Already Decided And Not Re-Decided Here

- U-18 already defined the typed scope model: outdoor work areas, selected animal buildings, and greenhouse crop scope.
- U-19 already established persistence around authoritative `ScopeSelection` plus `ContractTermsSnapshot`, with compatibility projections for older fields where needed.
- U-20 already made typed scope the live hire/edit-flow source of truth and kept output destinations task-owned in the player-facing UI.
- U-21 already switched the active runtime to energy-limited labor, finish-current-unit stop behavior, and no refund/debt settlement.
- Fixed pricing, recurring billing rules, and worker energy math are not being redesigned here.
- This unit should prefer the smallest possible UI impact; any frontend work must be in service of runtime clarity, not a new menu architecture.

This plan focuses only on the remaining scope-consumption and output-alignment choices needed to make the redesign-era runtime coherent.

---

## Design Questions

> Answer each question by writing after its `[Answer]:` tag. Pick the letter that best matches your preference. If none fit, choose `X` and describe your preference after the tag.

## Question 1
What should be the authoritative runtime scope source for a redesign-era contract?

A) Use authoritative `Contract.ScopeSelection` whenever it exists, and only fall back to compatibility-derived scope for older contracts that still lack it (Recommended)

B) Always merge `ScopeSelection` and legacy compatibility `Zones`, even for redesign-era contracts

C) Keep runtime planning primarily driven by legacy `Zones` for now, and treat `ScopeSelection` as preview/persistence metadata only

X) Other (please describe after [Answer]: tag below)

[Answer]: A, but we don't need a fallback, there are no older contracts

---

## Question 2
How should selected animal buildings behave at runtime?

A) Selected barns/coops fully own animal-service eligibility: the worker services animals assigned to those homes indoors or anywhere on the farm, independent of outdoor zones (Recommended)

B) Selected barns/coops only count while animals are indoors; outdoor animals must also be inside selected outdoor zones

C) Selected barns/coops are only hints, and runtime animal eligibility should still be derived mainly from zone geometry

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 3
How should outdoor work zones interact with selected animal-building scope?

A) Outdoor zones never restrict or expand animal-building scope; animal tasks remain building-owned only (Recommended)

B) Outdoor zones can further restrict which selected-building animals are serviced when they are outside

C) Outdoor zones can expand animal service to any animal currently standing inside the selected zones

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 4
How should the greenhouse behave in live runtime planning?

A) Treat greenhouse selection as its own dedicated crop-work scope using greenhouse-compatible crop services only, independent of outdoor zone geometry (Recommended)

B) Merge greenhouse tiles into outdoor crop work as generic zone geometry

C) Treat greenhouse as a generic interior building and allow any outdoor clearing/crop services to be planned there

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 5
When greenhouse scope is selected alongside outdoor crop/clearing scope, how should runtime batching prioritize it?

A) Keep U-21's broad family order, but treat greenhouse work as its own crop batch that runs before outdoor crop/clearing batches when greenhouse scope is selected (Recommended)

B) Keep U-21's broad family order, but always process outdoor crop/clearing batches before greenhouse work

C) Let saved scope order or selection order decide whether greenhouse or outdoor crop work happens first

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 6
How granular should output destinations remain under typed-scope runtime alignment?

A) Keep destinations task-owned, not scope-owned: one destination per task applies to outdoor, greenhouse, and selected-building outputs alike (Recommended)

B) Split destinations by scope family, such as separate outdoor vs greenhouse vs animal-building destinations

C) Split destinations per selected building or selected work area

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 7
How should overflow and unassigned-output behavior work under typed-scope runtime alignment?

A) Reuse the existing generic overflow/unassigned fallback reasons and mail behavior for all typed scopes; do not add scope-specific overflow categories now (Recommended)

B) Add new scope-specific overflow and unassigned-output mail categories now, such as greenhouse-specific or building-specific messages

C) Keep overflow mail for outdoor work only, but route some building/greenhouse fallback cases directly to the shipping bin instead

X) Other (please describe after [Answer]: tag below)

[Answer]: B

---

## Question 8
How should older contracts that still lack authoritative typed scope enter the runtime?

A) Derive a best-effort typed runtime scope from compatibility data once, then run the same typed-scope execution path as redesign-era contracts (Recommended)

B) Refuse to execute older contracts until they are manually edited and resaved

C) Keep a separate legacy runtime path alongside the new typed-scope path

X) Other (please describe after [Answer]: tag below)

[Answer]: X, there will be no older contracts, this project is not yet live

---

## Question 9
Should this unit make any small wording or summary changes on the scope-selection page so the UI still matches the runtime rules?

A) Yes: keep the current page structure, but make any minimal wording/summary updates needed to reinforce that animal tasks are building-owned and greenhouse is a dedicated crop scope (Recommended)

B) No: leave the existing wording untouched, even if some runtime rules become more specialized than the page currently implies

C) Go further and redesign scope/output UI structure again in this unit

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-22-scope-driven-runtime-alignment/functional-design/business-logic-model.md`
- `aidlc-docs/construction/u-22-scope-driven-runtime-alignment/functional-design/domain-entities.md`
- `aidlc-docs/construction/u-22-scope-driven-runtime-alignment/functional-design/business-rules.md`
- `aidlc-docs/construction/u-22-scope-driven-runtime-alignment/functional-design/frontend-components.md` (if required by the approved answers)
