# U-20 — Logical Components

**Unit**: U-20 — Hiring Flow Preview Refresh

NFR requirements NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A apply. Functional-design decisions FD-Q1=A through FD-Q9=A apply throughout.

---

## Component Map

```text
Dayswork / Hiring Flow
  HiringFlowCoordinator                [existing owned seam, expanded]
  LegacyScopeBootstrap                 [narrow compatibility helper]
  DraftPreviewState                    [shared state carrier]
  ServiceContributionRow               [screen-1 view-model carrier]
  ScopeSummaryModel                    [screen-2 / screen-4 view-model carrier]
  SummaryReviewModel                   [screen-4 view-model carrier]

Dayswork / Menus
  TaskSelectionMenu                    [existing presentation seam]
  ZoneAndChestMenu                     [existing presentation seam]
  ScheduleMenu                         [existing presentation seam]
  SummaryMenu                          [existing presentation seam]

Dayswork.Core
  ContractTermsBuilder                 [existing dependency, unchanged source of truth]

Dayswork.Tests / Hiring
  U20ExampleTests                      [test-side grouping]
  U20PropertyGenerators                [test-side helper]
  U20PropertyTests                     [test-side grouping]
```

No new runtime plugin, UI framework, async subsystem, or infrastructure component is introduced. The design deliberately keeps the retrofit inside the existing coordinator/menu architecture plus small compatibility and test-side helpers.

---

## LC-U20-01 — HiringFlowCoordinator (Expanded Ownership)

**Layer**: App / existing hiring flow orchestration seam  
**Kind**: Existing production seam with expanded redesign ownership

**Purpose under U-20**:
- own draft mutation boundaries
- own synchronous preview refresh rules
- own canonical view-model shaping
- own review-first edit re-entry behavior
- own invalid-preview confirm gating

**Responsibilities**:
1. Create a new draft or hydrate one from an existing contract
2. Route task/scope changes through Core preview recomputation
3. Avoid unnecessary price recomputation for destination/schedule edits
4. Produce stable `ServiceContributionRow`, `ScopeSummaryModel`, and `SummaryReviewModel` output
5. Reject confirmation when the current preview is invalid

**Important design constraints**:
- no UI-embedded pricing logic
- no async preview worker
- no retained preview cache subsystem
- no hidden whole-farm scope insertion

This component is the natural owner of responsiveness, determinism, and screen-to-screen orchestration quality.

---

## LC-U20-02 — LegacyScopeBootstrap

**Layer**: App/Core boundary helper, narrow compatibility seam  
**Kind**: Small explicit helper behavior

**Purpose**:
- convert older compatibility-era `Zones` into best-effort typed scope when authoritative `ScopeSelection` is absent

**Responsibilities**:
1. Prefer authoritative scope when available and do nothing in that case
2. Derive supported outdoor/animal/greenhouse scope only when justified by legacy data
3. Return an honest incomplete result when scope cannot be safely inferred

**Not responsible for**:
- inventing whole-farm fallback behavior
- fabricating unsupported buildings or scope families
- computing price or worker energy

This stays intentionally narrow so it can be deleted once compatibility-zone hydration is no longer needed.

---

## LC-U20-03 — Shared Draft/View-Model Carriers

**Layer**: App / menu-facing state carriers  
**Kind**: Lightweight coordination models

**Members**:
- `DraftPreviewState`
- `ServiceContributionRow`
- `ScopeSummaryModel`
- `SummaryReviewModel`

**Purpose under NFR design**:
- separate stable screen-ready state from raw draft mutation
- carry deterministic ordering and explicit invalid-preview semantics cleanly to menus
- make coordinator-level tests possible without heavy UI harnesses

**Responsibilities**:
1. Represent current preview validity and reasons
2. Represent selected-service states before scope is complete
3. Represent typed scope summaries as separate families
4. Represent Screen 4 review content including payment timing and energy summary

These are not business-logic owners; they are the transport layer that keeps menu rendering thin and deterministic.

---

## LC-U20-04 — Presentation Menus (Thin Seams)

**Layer**: SMAPI menu layer  
**Kind**: Existing presentation seams with constrained redesign ownership

**Members**:
- `TaskSelectionMenu`
- `ZoneAndChestMenu`
- `ScheduleMenu`
- `SummaryMenu`

**Purpose under U-20**:
- render coordinator-provided state
- capture input and emit user actions
- preserve existing navigation/gamepad patterns

**Responsibilities**:
1. Display deterministic rows and summaries from shared models
2. Call back into the coordinator when the player changes tasks, scope, destination, or schedule
3. Reflect confirm-disabled state and recovery messaging on Screen 4

**Important constraint**:
- menus must not regain ownership of pricing math, energy interpretation, or scope inference

That separation is central to both maintainability and property-test coverage.

---

## LC-U20-05 — ContractTermsBuilder (Dependency Boundary)

**Layer**: Core / existing dependency  
**Kind**: Existing pure dependency reused by U-20

**Purpose in this design**:
- remain the sole source of truth for preview pricing, validation reasons, and worker-energy inputs

**Why it is listed here**:
- U-20's NFR bar depends on keeping business semantics anchored in Core
- coordinator responsiveness and determinism are only safe if the menus are not duplicating these rules

**Constraint**:
- U-20 may shape the returned information for presentation
- it may not replace or fork the pricing/validation logic locally

---

## LC-U20-06 — Test-Side Support Components

**Layer**: `Dayswork.Tests` only  
**Kind**: Dedicated regression-support helpers

### `U20PropertyGenerators`

**Purpose**:
- generate equivalent-draft variants, reordered typed-scope selections, and legacy-bootstrap scenarios

**Responsibilities**:
- equivalent task/scope sets expressed in different orderings
- drafts that differ only by schedule or destination
- compatibility-zone bootstrap inputs for legacy edit scenarios

### `U20ExampleTests`

**Purpose**:
- capture key concrete behaviors with readable examples

Examples include:
- selected service stays visible before scope is complete
- outdoor tasks without zones remain invalid rather than gaining whole-farm fallback
- edit flow opens at review first
- invalid preview disables confirm only on Screen 4

### `U20PropertyTests`

**Purpose**:
- express U-20 orchestration invariants with FsCheck

Examples include:
- equivalent-draft deterministic output
- no-whole-farm-fallback behavior
- schedule-change-does-not-change-price
- destination-change-does-not-change-price

These are explicit logical components because U-20’s NFR bar is about stable orchestration behavior, not just menu rendering.

---

## Interaction Summary

```text
New hire flow
  menu action
    -> HiringFlowCoordinator
         -> ContractTermsBuilder (task/scope changes only)
         -> DraftPreviewState + screen view models
    -> menus render updated state

Edit flow
  existing contract
    -> authoritative scope if present
    -> otherwise LegacyScopeBootstrap
    -> HiringFlowCoordinator refreshes preview
    -> SummaryMenu opens first

Non-pricing edits
  schedule/destination change
    -> HiringFlowCoordinator narrow refresh path
    -> review copy/state updates without price recompute
```

---

## Why no additional runtime components were introduced

The NFR design intentionally does **not** add:
- a new UI framework
- a background preview worker
- a preview cache subsystem
- an event-bus/observer layer
- a UI automation infrastructure component

Reason:
- draft size is tiny
- preview needs to feel immediate
- existing coordinator/menu structure is already sufficient
- the highest-value quality work is deterministic shaping plus regression tests

That keeps U-20’s redesign sharp, testable, and consistent with the rest of the retrofit.
