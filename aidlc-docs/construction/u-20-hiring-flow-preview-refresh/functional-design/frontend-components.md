# U-20 — Hiring Flow Preview Refresh: Frontend Components

**Unit**: U-20 — Hiring Flow Preview Refresh  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A through FD-Q9=A

This unit is heavily UI-oriented, so the screen/view-model behavior is part of the functional design.

---

## Component hierarchy

```text
HiringFlowCoordinator
  -> TaskSelectionMenu
  -> ZoneAndChestMenu
  -> ScheduleMenu
  -> SummaryMenu

ContractListMenu
  -> calls HiringFlowCoordinator.OpenEditFlow(...)

ZoneAndChestMenu
  -> launches ZoneDrawMenu for outdoor zones
```

Key separation:
- `HiringFlowCoordinator` owns draft mutation + preview refresh
- menus render coordinator-provided state and emit user actions
- `ZoneDrawMenu` still owns outdoor rectangle drawing only

---

## Shared UI state

### Coordinator-owned state

`HiringFlowCoordinator` owns:
- current `ContractDraft`
- current `DraftPreviewState`
- current config snapshot
- current edit/new-flow context

Menus receive only the slices they need.

### Shared view-model pieces

The coordinator provides:
- `ServiceContributionRow[]` for Screen 1
- `ScopeSummaryModel` for Screen 2 and Screen 4
- `SummaryReviewModel` for Screen 4

This avoids each menu recomputing business semantics independently.

---

## M-03 HiringFlowCoordinator

### Responsibilities

- Create a new draft or hydrate one from an existing contract
- Refresh preview when tasks or scope change
- Refresh summary copy when schedule changes
- Open the right screen for new-flow vs edit-flow
- Confirm a valid draft into a final `Contract`

### Screen transitions

#### New hire flow

```text
OpenHiringFlow
  -> Screen 1 TaskSelectionMenu
  -> Screen 2 ZoneAndChestMenu
  -> Screen 3 ScheduleMenu
  -> Screen 4 SummaryMenu
```

#### Edit flow

```text
OpenEditFlow
  -> hydrate draft
  -> refresh preview
  -> Screen 4 SummaryMenu
  -> Back -> Screen 3 / Screen 2 / Screen 1 as needed
```

### Important coordinator behaviors

- `RefreshPreview(draft)` is called after task or scope changes
- destination selection does not call preview builder
- schedule changes update review copy without changing price
- confirm is rejected when the current preview is invalid

---

## M-04 TaskSelectionMenu

### Purpose

Screen 1 remains the task-toggle entry point, but now also shows honest redesign-era contribution states.

### Props / state it needs

- current enabled task set
- `ServiceContributionRow[]`
- whether at least one task is selected
- continue / cancel callbacks

### Interaction behavior

- toggling a task updates the draft, then preview refreshes
- selected services remain visible in the preview panel even before scope is chosen
- a row can display:
  - charged amount
  - needs outdoor scope
  - needs animal building
  - needs greenhouse

### Navigation behavior

- continue remains available once there is at least one selected task
- invalid preview due to missing scope does not block moving to Screen 2

---

## M-05 ZoneAndChestMenu

### Purpose

Screen 2 becomes the typed-scope screen plus output-routing screen.

### Props / state it needs

- current typed `ScopeSelection`
- `ScopeSummaryModel`
- supported selectable building outlines
- output task assignment state
- begin-zone-draw callback

### Scope behavior

- outdoor zones come only from `ZoneDrawMenu`
- selectable buildings are limited to:
  - barns/coops
  - greenhouse
- unsupported buildings are not selectable work scope

### Rendering behavior

Show distinct scope sections:
- outdoor zones
- animal buildings
- greenhouse

Do not show a generic combined “zones/buildings” count only.

### Output-routing behavior

- output-producing task families still expose destination selection
- assignments do not refresh pricing preview
- default routing behavior is preserved at confirmation time

---

## M-06 ScheduleMenu

### Purpose

Screen 3 still chooses one-time vs recurring, but the redesign changes what that means in the review copy.

### Props / state it needs

- current `ContractSchedule`
- continue / back callbacks

### Behavior

- toggling schedule updates draft schedule
- no hourly/deposit preview is shown here
- the fixed price itself does not change
- the downstream review copy changes to match:
  - charge now
  - recurring next eligible day
  - recurring edit applies next eligible day

---

## M-07 SummaryMenu

### Purpose

Screen 4 becomes the real redesign review gate.

### Props / state it needs

- `SummaryReviewModel`
- confirm callback
- back callback

### Content shown

- selected tasks
- typed scope summary
- pricing breakdown
- worker energy summary
- schedule-sensitive payment explanation

### Validation behavior

- if `CanConfirm = false`, confirm button is disabled
- validation messages are shown in-place
- the screen never falls back to hours/deposit/refund language

### Confirm behavior

- valid preview only
- one-time: charge now
- recurring: save revised next-day fixed price semantics

---

## ContractListMenu edit-entry adjustment

### Purpose in this unit

`ContractListMenu` remains the bulletin-board manage screen, but its edit action now targets the review-first redesign flow.

### Behavior

- selecting Edit opens `HiringFlowCoordinator.OpenEditFlow(...)`
- the coordinator re-enters at Screen 4 with a prebuilt preview
- player can back into earlier screens only if changes are needed

This is what satisfies the “without going through the full 4-screen flow” expectation.

---

## Validation and disabled states

### Screen 1

- no tasks selected -> continue disabled
- missing compatible scope -> continue still allowed

### Screen 2

- scope may remain incomplete while player assigns outputs
- no fake whole-farm auto-fill occurs

### Screen 3

- always navigable once reached

### Screen 4

- invalid preview -> confirm disabled
- validation reasons shown
- player may backtrack to fix tasks/scope/schedule

---

## Brownfield migration behavior in UI

When editing an older contract:
- if authoritative `ScopeSelection` exists, use it directly
- otherwise derive a best-effort typed draft from compatibility `Zones`

After hydration:
- the rest of the session behaves exactly like a redesign-native draft
- the UI should not expose raw legacy placeholder-zone concepts back to the player

---

## Accessibility / interaction continuity

U-20 preserves:
- mouse support
- keyboard/gamepad snapping and directional navigation
- the existing four-screen mental model for new hires

The redesign changes the information architecture, not the basic interaction modality.
