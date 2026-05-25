# U-20 — Hiring Flow Preview Refresh: Business Logic Model

**Unit**: U-20 — Hiring Flow Preview Refresh  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=A, FD-Q8=A, FD-Q9=A

This unit refreshes the four-screen hire/edit flow so it behaves like the redesign-era contract model instead of the historical hourly/deposit model.

The earlier flow:
- stored work area mainly as legacy `Zones`
- used a whole-farm fallback when no zone was chosen
- computed hourly rate / estimated hours / deposit inside the menus
- confirmed using deposit-first semantics

U-20 replaces that with:
- authoritative typed draft scope
- live `ContractPreview` refresh through `ContractTermsBuilder`
- fixed-price contribution preview
- typed scope summaries for outdoor zones, animal buildings, and greenhouse
- summary/confirm behavior driven by `ContractTermsSnapshot`

---

## 0. Where this plugs into the redesign

U-18 already introduced:
- `ContractScopeSelection`
- `ContractPreview`
- `ContractTermsSnapshot`
- `WorkerEnergyProfile`
- `ContractTermsBuilder`

U-19 already introduced:
- persistence of authoritative typed scope
- persistence of authoritative terms snapshot
- compatibility projection back to legacy `Zones`

U-20 is the player-facing switchover:

```text
player changes draft
  -> HiringFlowCoordinator.RefreshPreview(...)
  -> ContractTermsBuilder.BuildPreview(...)
  -> current ContractPreview + screen view models
  -> TaskSelection / ZoneAndChest / Schedule / Summary screens

player confirms
  -> HiringFlowCoordinator.ConfirmDraft(...)
  -> final Contract with ScopeSelection + TermsSnapshot
  -> ContractStore.Add / Update
```

---

## 1. Live draft model

The refreshed flow treats `ContractDraft` as the player's authoritative in-progress contract.

The authoritative draft fields are:
- enabled tasks
- typed selected scope
- output destinations
- schedule
- optional edit identity

Important consequence:
- legacy `Zones` are no longer the working source of truth inside the flow
- they are only derived later when building the final persisted/runtime `Contract`

This gives the flow a stable redesign-era mental model:
- outdoor zones mean outdoor work scope
- selected barns/coops mean animal-care scope
- greenhouse selection means greenhouse crop-work scope

---

## 2. Preview refresh orchestration

`HiringFlowCoordinator` owns preview refresh.

### 2.1 What triggers a fresh core preview

Any change to:
- enabled tasks
- selected outdoor zones
- selected animal buildings
- greenhouse selection

causes the coordinator to call:

```text
ContractTermsBuilder.BuildPreview(
    draft.ScopeSelection,
    draft.EnabledTasks,
    currentConfig)
```

The resulting `ContractPreview` becomes the current shared preview state for the active screen.

### 2.2 What does not require a fresh core preview

Changes to:
- output chest assignments
- schedule choice

do not change fixed price or worker energy.

So:
- destination changes do not call `ContractTermsBuilder`
- schedule changes reuse the same preview terms and only refresh schedule-sensitive copy on Screen 4

### 2.3 Screen-specific derived view state

The coordinator derives lightweight screen models from the current `ContractPreview`, including:
- Screen 1 service contribution rows
- Screen 2 typed scope summary sections
- Screen 4 review model with pricing, worker energy, and schedule-sensitive payment explanation

This keeps menus presentation-focused rather than pricing-aware.

---

## 3. Screen 1 — Task selection with live contribution states

Screen 1 still lets the player toggle tasks first, but the redesign preview no longer pretends every selected task is already fully priced.

### 3.1 Selected services always remain visible

If a service is selected, it appears in the Screen 1 preview panel even when no compatible scope exists yet.

That panel uses three broad row states:
- `Charged`
  - the current preview includes one or more pricing contributions for that service
- `NeedsCompatibleScope`
  - the service is selected, but no compatible scope is currently selected
- `SelectedButCurrentlyUnpriced`
  - reserved for other future mismatch cases, but still selected

In practice for U-20 the main “needs scope” reasons are:
- outdoor service with no outdoor zone
- animal service with no selected barn/coop
- greenhouse crop service with no greenhouse selected

### 3.2 Forward navigation

Screen 1 remains usable as an early planning step.

So:
- the player may continue forward once at least one task is selected
- missing compatible scope does **not** trap the player on Screen 1
- the invalid state is carried forward until the player fixes scope or reaches Screen 4

This preserves the current hire-flow rhythm while making the preview more honest.

---

## 4. Screen 2 — Typed scope selection and output setup

Screen 2 is where the redesign-era scope model becomes visible to the player.

### 4.1 No whole-farm fallback

If the player selects outdoor services and no outdoor zone:
- no implicit whole-farm scope is inserted
- outdoor work remains unscoped
- the preview remains invalid for those services until a real outdoor zone is selected

### 4.2 Supported building selections

Only currently supported pricing/runtime work-scope buildings remain selectable:
- barns/coops
- greenhouse

Unsupported buildings are not offered as selectable work scope in U-20.

### 4.3 Typed scope summary sections

Screen 2 no longer flattens everything into generic “zones/buildings selected” language.

Instead it shows distinct sections such as:
- outdoor zones
- animal buildings
- greenhouse

This makes the price logic legible:
- outdoor tasks price from outdoor zones
- animal tasks price from selected animal buildings
- greenhouse crop work prices from greenhouse package selection

### 4.4 Output destination behavior

Output chest assignment remains on Screen 2 and behaves as before.

Destination assignment:
- still applies only to output-producing task families
- still defaults to mail where applicable if the player leaves it unset
- does not affect price preview or worker energy preview

---

## 5. Screen 3 — Schedule selection

Schedule selection still chooses:
- one-time
- recurring

But under the redesign:
- the fixed price does not change just because the schedule changes
- only the meaning of payment timing changes

So Screen 3 updates draft schedule, and the coordinator refreshes the summary review copy, not the pricing core itself.

---

## 6. Screen 4 — Review and confirm

Screen 4 becomes the redesign-era review gate.

It shows:
- selected tasks
- typed scope summary
- pricing breakdown
- worker energy summary
- schedule-sensitive payment explanation

### 6.1 Invalid preview gating

If the current `ContractPreview` is invalid:
- Screen 4 remains reachable
- validation reasons are displayed
- confirm action is disabled

Typical reasons include:
- outdoor services selected with no outdoor zone
- animal services selected with no barn/coop
- greenhouse service selected with no greenhouse
- zero chargeable scope-task pairs overall

### 6.2 Valid preview confirmation

If the preview is valid:
- confirmation uses the current `ContractTermsSnapshot`
- Screen 4 never mentions hours, deposits, or refunds

### 6.3 Schedule-sensitive copy

Screen 4 explains payment timing differently by schedule:

- `OneTime`
  - the fixed contract price is charged now
- `Recurring`
  - the shown fixed daily price becomes the next eligible daily charge
- `Recurring edit`
  - the revised fixed daily price applies on the next eligible contract day

The worker energy summary also explains the high-level contract:
- worker keeps going until work is finished, the day ends, or energy is exhausted

---

## 7. Edit flow

U-20 shortens recurring edit friction.

### 7.1 Entry point

Editing opens directly to Screen 4 with a prefilled draft and the current preview already built.

The player can then move backward through:
- schedule
- scope/output
- tasks

only if they need to change something.

This satisfies the goal of not forcing the full four-screen replay every time.

### 7.2 Draft hydration

When the stored contract already has authoritative `ScopeSelection`:
- hydrate the draft directly from that authoritative scope

When it does not:
- derive a best-effort typed draft from compatibility `Zones` once
- continue the edit session in redesign mode from there

That derivation is a one-time edit bootstrap, not the new source of truth.

---

## 8. Confirm / update flow

### 8.1 New one-time contract

On valid confirmation:
- charge the fixed total immediately
- build the final contract from authoritative draft scope + confirmed terms snapshot
- derive compatibility `Zones` for transitional runtime consumers
- store the contract

### 8.2 New recurring contract

On valid confirmation:
- persist authoritative draft scope
- persist the confirmed terms snapshot
- do not charge a same-day one-time amount at confirmation
- let day-start recurring lifecycle own the next eligible daily charge

### 8.3 Edit existing contract

On valid confirmation:
- build the updated contract from the edited authoritative draft scope
- attach the revised confirmed terms snapshot immediately
- keep persistence aligned with U-19 so the revised fixed daily price is ready for the next eligible day

---

## 9. Flow summary

```text
Open hire/edit flow
  -> hydrate or create ContractDraft
  -> RefreshPreview(...)
  -> open active screen

Task change
  -> update EnabledTasks
  -> RefreshPreview(...)

Scope change
  -> update ContractScopeSelection
  -> RefreshPreview(...)

Destination change
  -> update TaskDestinations only

Schedule change
  -> update Schedule
  -> refresh summary copy only

Open Screen 4
  -> show tasks + typed scope + pricing breakdown + energy summary
  -> if preview invalid: show reasons, disable confirm
  -> if preview valid: confirm using ContractTermsSnapshot
```

---

## 10. What U-20 explicitly does not decide

- exact runtime energy spending or visible in-world bar behavior during the shift
- typed-scope runtime execution for animals/greenhouse beyond what the preview already assumes
- recurring day-start billing rules
- GMCM exposure of the new price/energy knobs

Those belong to later retrofit units, especially U-21, U-22, U-23, and U-24.
