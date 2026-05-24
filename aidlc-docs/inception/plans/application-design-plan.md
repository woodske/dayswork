# Application Design Plan — Pricing Model Redesign

**Status**: Answers reviewed, no clarification round needed, and refreshed application-design artifacts generated. Pending user review.

**Scope**: This pass refreshes the existing application design for the brownfield pricing overhaul. We are not redesigning the entire mod from scratch. We are updating the component model, method seams, orchestration boundaries, and dependencies affected by the move from hourly deposit/refund billing to fixed contract pricing plus worker energy.

**Previously approved design decisions still assumed unless changed here**:
- Separate `Dayswork.Core` pure-logic project and `Dayswork` SMAPI project
- Hand-wired composition root in `ModEntry`
- Explicit shift state machine
- Immutable config snapshot semantics
- Direct orchestration calls instead of an event bus
- Four-screen menu structure with `HiringFlowCoordinator`

---

## Context Loaded
- [requirements.md](../requirements/requirements.md) — pricing, energy, recurring, greenhouse, animal-scope, pacing, and config redesign rules
- [stories.md](../user-stories/stories.md) — updated player/admin journeys for the redesign
- [personas.md](../user-stories/personas.md) — revised player/farmhand/maintainer motivations
- [execution-plan.md](execution-plan.md) — brownfield retrofit sequence and affected units
- Existing application-design artifacts in [application-design](/C:/Users/kwood/Repos/dayswork/aidlc-docs/inception/application-design/)

---

## Plan Checklist
- [x] Review pricing-redesign requirements, stories, personas, and existing application-design artifacts
- [x] Identify which existing components and services are outdated because they still assume hourly deposits/refunds
- [x] Prepare targeted application-design questions for the redesign delta instead of redoing settled architecture choices
- [x] Analyze your answers for ambiguity or contradictions and add follow-up questions if needed
- [x] Generate refreshed `components.md` with updated responsibilities and project placement
- [x] Generate refreshed `component-methods.md` with redesigned pricing/energy/service interfaces
- [x] Generate refreshed `services.md` with updated orchestration flows
- [x] Generate refreshed `component-dependency.md` with updated dependency and data-flow relationships
- [x] Validate the refreshed application design for completeness and consistency
- [x] Generate refreshed `application-design.md` consolidating the redesign architecture

---

## Redesign Questions

### Question AD-R1 — Pricing core component boundaries
The old design had `RateCalculator`, `DepositCalculator`, `RefundCalculator`, and `HoursEstimator` as first-class pricing components. The redesign needs a new pure-logic pricing surface.

A) **Split pricing into focused pure components (Recommended)** — introduce components such as `ScopeClassifier` / `ServiceBandCalculator`, `ContractPriceCalculator`, and `PriceBreakdownBuilder`, with hourly deposit/refund components removed from the architecture
B) **One unified pricing engine** — replace the old pricing components with a single `ContractPricingEngine` that returns total price and breakdown in one call
C) **Minimal rename/refactor of the old components** — keep a calculator-heavy shape and repurpose the existing pricing components even if some names become less exact
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

### Question AD-R2 — How contract price should be represented on saved contracts
We need a consistent design for one-time and recurring contracts, especially because recurring prices are derived from saved scope plus current config, while one-time contracts are prepaid at confirmation.

A) **Persist both scope and a computed pricing snapshot (Recommended)** — save the scope/config-derived price breakdown used when the contract is created or edited, while still allowing recurring repricing workflows to rebuild a new snapshot when appropriate
B) **Persist scope only; always recompute price on demand** — menus and day-start flows derive price fresh each time from saved scope and current config
C) **Persist only a flat total price** — keep the saved contract price minimal and rebuild any breakdown purely for UI display when possible
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

### Question AD-R3 — Work-scope modeling for zones, barns/coops, and greenhouse
The redesign now has three distinct pricing/work anchors: outdoor zones, animal buildings, and the greenhouse.

A) **Explicit typed work scopes (Recommended)** — represent them as distinct scope records/components, such as outdoor-zone scope, animal-building scope, and greenhouse scope, then build pricing and runtime behavior on top of that common abstraction
B) **Keep today’s mixed representation** — zones and building selections stay in ad hoc contract fields, and pricing/runtime logic interpret those fields directly
C) **Pricing-only typed scopes** — add a typed scope model only inside the pricing layer, while the rest of the runtime continues to use the existing contract representation
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

### Question AD-R4 — Energy accounting ownership
Worker energy now affects execution, visibility, and contract feel. We need to decide whether energy is embedded in shift state or given its own component boundary.

A) **Dedicated pure energy component (Recommended)** — add a focused `WorkerEnergyLedger` / `EnergyCostCalculator` style component in `Dayswork.Core`, and let the shift/orchestrator consume it
B) **Energy lives inside shift state only** — no separate component; the state machine/context owns all energy tracking and action-cost math directly
C) **Hybrid** — a pure component defines action costs while the mutable remaining-energy tracking lives inside shift state/context
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

### Question AD-R5 — Hiring preview orchestration
The updated UI needs live price contribution breakdowns, scope-sensitive messaging, and worker-energy summary information. We should decide whether menus assemble this themselves or rely on a dedicated seam.

A) **Dedicated preview/query service (Recommended)** — introduce a `ContractPreviewService` or similar orchestrator/facade that menus call to get a single preview model containing price breakdown, scope classification, and energy summary
B) **Menus compose pricing pieces directly** — `TaskSelectionMenu` and `SummaryMenu` call the underlying pricing/scope/energy components themselves through the coordinator
C) **Coordinator-owned preview assembly** — `HiringFlowCoordinator` computes all preview models and pushes them into the menus
X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

### Question AD-R6 — Transition strategy for old saved contracts
The codebase and possibly player saves may still contain active contracts shaped around the hourly deposit/refund system.

A) **Add an explicit migration path (Recommended)** — keep a compatibility seam in persistence/load logic that upgrades old saved contracts into the new pricing model or marks them for safe repricing
B) **Invalidate old active contracts on load** — if a saved contract predates the redesign, clear or deactivate it and surface a player-facing explanation
C) **Best-effort reinterpretation with no formal migration type** — load old contracts through the current DTO path and reprice them from whatever saved scope still exists
X) Other (please describe after [Answer]: tag below)

[Answer]: X, this project has not been released yet so we do not need to transition old contracts. Just delete old ones and do not surface an explanation.

---

## Artifact Goals After Approval

When the answers are complete and approved, this stage will refresh these artifacts in `aidlc-docs/inception/application-design/`:

- [ ] `components.md` — updated pricing, scope, energy, preview, persistence, and runtime component inventory
- [ ] `component-methods.md` — revised method signatures reflecting fixed-price and energy-oriented interfaces
- [ ] `services.md` — updated sequences for hiring preview, shift runtime, recurring charging, and persistence/migration
- [ ] `component-dependency.md` — refreshed dependency map and communication/data-flow patterns
- [ ] `application-design.md` — consolidated brownfield redesign architecture and rationale
