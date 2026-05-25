# U-20 — Hiring Flow Preview Refresh: Functional Design Plan

**Unit**: U-20 — Hiring Flow Preview Refresh  
**Phase**: CONSTRUCTION — Functional Design  
**Status**: Answers reviewed, no clarification round needed, and functional-design artifacts generated. Pending user review.

---

## Plan Checklist

- [x] Load unit definition, story map, refreshed requirements, refreshed user stories, and refreshed application design
- [x] Inspect the current brownfield hire/edit flow implementation in `Dayswork/UI/`
- [x] Collect answers to FD-Q1 through FD-Q9
- [x] Analyze answers for ambiguity or contradictions and create clarification questions if needed
- [x] Generate `business-logic-model.md`
- [x] Generate `domain-entities.md`
- [x] Generate `business-rules.md`
- [x] Generate `frontend-components.md`
- [x] Present completion message and await approval

---

## Context Loaded

- [unit-of-work.md](../../inception/application-design/unit-of-work.md) — U-20 definition and definition of done
- [unit-of-work-story-map.md](../../inception/application-design/unit-of-work-story-map.md) — U-20 story ownership for `S-01`, `S-02`, `S-03`, `S-05`, `S-06`, and `S-12`
- [requirements.md](../../inception/requirements/requirements.md) — fixed-price, typed-scope, and worker-energy requirements
- [stories.md](../../inception/user-stories/stories.md) — player-facing expectations for live pricing preview, typed building scope, edit flow, and summary messaging
- [application-design.md](../../inception/application-design/application-design.md) — redesign summary
- [components.md](../../inception/application-design/components.md)
- [component-methods.md](../../inception/application-design/component-methods.md)
- [services.md](../../inception/application-design/services.md)
- Brownfield implementation review:
  - [HiringFlowCoordinator.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/UI/HiringFlowCoordinator.cs)
  - [TaskSelectionMenu.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/UI/TaskSelectionMenu.cs)
  - [ZoneAndChestMenu.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/UI/ZoneAndChestMenu.cs)
  - [ScheduleMenu.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/UI/ScheduleMenu.cs)
  - [SummaryMenu.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/UI/SummaryMenu.cs)
  - [ContractDraft.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/UI/ContractDraft.cs)
  - [ContractListMenu.cs](/C:/Users/kwood/Repos/dayswork/Dayswork/UI/ContractListMenu.cs)

---

## What This Unit Must Define

U-20 is the player-facing switchover from the old hourly/deposit preview to the redesign terms model introduced by U-18 and persisted by U-19.

This unit owns the functional behavior of:
- `M-03 HiringFlowCoordinator`
- `M-04 TaskSelectionMenu`
- `M-05 ZoneAndChestMenu`
- `M-06 ScheduleMenu`
- `M-07 SummaryMenu`
- `ContractDraft` as the live four-screen working model

This unit must define:
- how the draft stores authoritative typed scope during hire/edit flow
- when and how `ContractPreview` is refreshed across the four screens
- how per-service price contributions appear before all compatible scope is selected
- how Screen 2 reflects outdoor zones vs animal buildings vs greenhouse scope
- how edit flow re-enters the refreshed pipeline without forcing a full four-screen replay
- how summary/confirm uses fixed price + energy preview and schedule-sensitive payment copy

Because this is a UI-heavy unit, frontend interaction behavior is part of the functional design and will be captured in `frontend-components.md`.

---

## Already Decided And Not Re-Decided Here

- Hourly billing, deposit estimates, refunds, and hour previews are gone.
- `ContractTermsBuilder` is the source of truth for fixed pricing, pricing breakdowns, validation issues, and worker energy preview.
- Outdoor work scope comes from outdoor zones, animal care comes from selected barns/coops, and greenhouse crop work comes from the greenhouse scope.
- One-time contracts charge immediately at confirmation; recurring contracts apply their fixed daily price on the next eligible day at 6am.
- Recurring edits apply on the next eligible contract day, not mid-day.
- U-19 already established persistence semantics: authoritative `ScopeSelection` + `TermsSnapshot`, with temporary compatibility projections for older runtime fields.

This plan focuses only on the remaining functional-design choices that still shape the refreshed hire/edit flow.

---

## Design Questions

> Answer each question by writing after its `[Answer]:` tag. Pick the letter that best matches your preference. If none fit, choose `X` and describe your preference after the tag.

## Question 1
What should be the source of truth for live work scope inside `ContractDraft` during the refreshed hire/edit flow?

A) Store authoritative typed scope (`ContractScopeSelection`) in the draft, and only derive compatibility `Zones` when confirming/updating the final `Contract` (Recommended)

B) Keep legacy `List<Zone>` as the draft source of truth and derive typed scope every time preview refreshes

C) Dual-write both `ContractScopeSelection` and legacy `Zones` inside the draft on every user change

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 2
What should happen to the current implicit whole-farm fallback when the player selects outdoor tasks but no outdoor zone?

A) Remove the implicit whole-farm fallback; no outdoor zone means no outdoor scope, and the preview becomes invalid until the player selects a real outdoor work area (Recommended)

B) Keep the current whole-farm fallback for outdoor task families only

C) Ask on confirmation whether the player wants to convert missing outdoor scope into a whole-farm job

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 3
On Screen 1, how should selected task rows behave before the player has chosen compatible scope for them?

A) Always show every selected service in the preview panel, but let rows enter an explicit “needs scope” / “not yet chargeable” state until compatible scope is selected (Recommended)

B) Show only the services that are currently chargeable; selected-but-unscoped services do not appear in the contribution list yet

C) Prevent the player from selecting services until they have already chosen compatible scope on Screen 2

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 4
Which buildings should remain selectable as work scope on Screen 2 in the redesign-era flow?

A) Only barns/coops and the greenhouse are selectable work-scope buildings for now; unsupported buildings are not offered as scope selections (Recommended)

B) Any building may still be selected, but unsupported buildings remain zero-price/compatibility-only selections for now

C) Any building may still be selected, but unsupported buildings make the preview invalid until removed

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 5
How should Screen 2 summarize selected scope once typed scope becomes authoritative?

A) Show separate typed sections such as outdoor zones, animal buildings, and greenhouse selection, each with their own count/list summary (Recommended)

B) Keep one flat combined summary such as generic “zones/buildings selected” counts

C) Show one unified location list only, without distinguishing outdoor zones from animal buildings or greenhouse scope

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 6
How should the player enter the refreshed edit flow for an existing recurring contract?

A) Open directly to the summary/preview screen with the current draft prefilled, and allow Back navigation into schedule, scope, and task screens as needed (Recommended)

B) Always reopen editing at Screen 1 (task selection) with all current values prefilled

C) Reopen editing at Screen 2 (scope/output setup) with all current values prefilled

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 7
When editing an older in-memory contract that still lacks authoritative `ScopeSelection`, how should U-20 seed the refreshed draft?

A) Use authoritative `ScopeSelection` when present; otherwise derive a best-effort typed draft from compatibility `Zones` once and continue in redesign mode from there (Recommended)

B) Refuse edit flow for contracts that do not already carry authoritative typed scope

C) Always reconstruct the draft only from compatibility `Zones`, even when authoritative `ScopeSelection` exists

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 8
What level of schedule-sensitive payment explanation should Screen 4 show alongside fixed price and worker energy?

A) Show explicit schedule-sensitive copy: one-time contracts say the price is charged now, recurring edits say the revised daily price applies on the next eligible day, and both explain worker energy in plain language (Recommended)

B) Show one generic fixed-price explanation shared by one-time and recurring schedules

C) Show only the numbers (price and energy summary) with no additional explanatory copy

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 9
How should invalid `ContractPreview` states gate the four-screen flow?

A) Let intermediate screens remain navigable, but block confirmation on Screen 4 whenever the preview is invalid and show the validation reasons there (Recommended)

B) Block forward navigation immediately on the first screen that produces an invalid preview

C) Allow invalid previews to be confirmed as zero-work contracts

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Artifact Output After Answers Are Collected

- `aidlc-docs/construction/u-20-hiring-flow-preview-refresh/functional-design/business-logic-model.md`
- `aidlc-docs/construction/u-20-hiring-flow-preview-refresh/functional-design/domain-entities.md`
- `aidlc-docs/construction/u-20-hiring-flow-preview-refresh/functional-design/business-rules.md`
- `aidlc-docs/construction/u-20-hiring-flow-preview-refresh/functional-design/frontend-components.md`
