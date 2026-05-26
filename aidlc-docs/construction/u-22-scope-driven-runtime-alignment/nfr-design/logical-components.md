# U-22 — Logical Components

**Unit**: U-22 — Scope-Driven Runtime Alignment

NFR requirements NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A apply. Functional-design decisions FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=B, FD-Q8=X, and FD-Q9=A apply throughout.

---

## Component Map

```text
Dayswork.Core / Scope & Routing
  WorkScopeClassifier                [existing pure normalization seam, becomes runtime-authoritative]
  RuntimeScopeSupportGuard           [new narrow guard/helper seam]
  RuntimeBatchPlanner                [new or extracted logical helper seam]
  DepositPlanner                     [existing pure task-owned routing seam]
  ScopedOutputProvenance             [lightweight runtime carrier]
  ScopeAwareOverflowCategorizer      [new pure or near-pure helper seam]

Dayswork / Runtime Shell & Delivery
  ShiftOrchestrator                  [existing live world adapter, constrained]
  AnimalTaskHandler                  [existing runtime helper, expanded building-owned responsibility]
  MailDispatcher                     [existing delivery seam, expanded wording/categorization input]

Dayswork.Tests / Mixed Scope
  U22ExampleTests                    [test-side grouping]
  U22PropertyGenerators              [test-side helper]
  U22PropertyTests                   [test-side grouping]
```

No new async subsystem, cache layer, compatibility execution path, or second mail pipeline is introduced.

---

## LC-U22-01 — WorkScopeClassifier (Runtime-Authoritative Normalization)

**Layer**: Core / pure normalization seam  
**Kind**: Existing production seam with expanded runtime authority

**Purpose under U-22**:
- become the only supported runtime normalization path for saved typed scope
- produce deterministic outdoor, animal-building, and greenhouse scope families

**Responsibilities**:
1. Accept authoritative `ContractScopeSelection`
2. Normalize outdoor zones deterministically
3. Emit selected animal-building scopes independent of outdoor-zone geometry
4. Emit greenhouse scope separately from outdoor scope

**Important design constraints**:
- no dependence on legacy `Zones` for supported runtime execution
- deterministic ordering and output shape
- no destination-routing logic

This is the primary owner of U-22’s stricter scope authority.

---

## LC-U22-02 — RuntimeScopeSupportGuard

**Layer**: Core/App boundary helper seam  
**Kind**: New narrow production helper or equivalent explicit guard behavior

**Purpose**:
- enforce the supported-contract gate for live runtime execution

**Responsibilities**:
1. Validate that a contract is eligible for U-22 runtime execution
2. Reject contracts that lack authoritative `ScopeSelection`
3. Produce maintainable diagnostics or a structured unsupported result before live work begins

**Not responsible for**:
- normalizing scope
- choosing tasks or destinations
- sending mail

This seam keeps fail-fast unsupported-contract behavior explicit rather than implicit inside broad orchestrator logic.

---

## LC-U22-03 — RuntimeBatchPlanner

**Layer**: Core or near-pure planning helper seam  
**Kind**: New or extracted logical helper behavior

**Purpose**:
- turn normalized scope into the deterministic runtime batch-family structure that U-22 requires

**Responsibilities**:
1. Build the approved batch-family order:
   - animal-building work
   - greenhouse crop work
   - outdoor crop work
   - outdoor clearing work
2. Preserve greenhouse/outdoor separation
3. Preserve animal-zone independence
4. Keep batch shaping lightweight and deterministic

**Important constraint**:
- this component shapes families and ordering, not actual destination routing

---

## LC-U22-04 — DepositPlanner (Preserved Task-Owned Routing Authority)

**Layer**: Core / pure routing seam  
**Kind**: Existing production seam with preserved ownership

**Purpose under U-22**:
- remain the sole authority for turning task-owned assignments into concrete delivery trips

**Responsibilities**:
1. Resolve destination from `TaskKind`
2. Group items by resulting destination key
3. Preserve deterministic trip output

**Important design constraint**:
- provenance must not change destination resolution

This component is listed explicitly because U-22’s NFR bar depends on *not* destabilizing it while richer scope provenance is added elsewhere.

---

## LC-U22-05 — ScopedOutputProvenance

**Layer**: Core/App boundary carrier  
**Kind**: Lightweight new runtime data carrier

**Purpose**:
- attach scope family and optional location identity to buffered output without changing task-owned routing

**Responsibilities**:
1. Record whether output came from outdoor, greenhouse, or animal-building work
2. Optionally retain the selected building/location context needed for richer mail wording
3. Travel with buffered output into categorization

**Why it matters in NFR design**:
- it lets the system improve player-facing explanation without exploding the destination model

---

## LC-U22-06 — ScopeAwareOverflowCategorizer

**Layer**: Core or near-pure helper seam  
**Kind**: New logical helper behavior

**Purpose**:
- transform overflow/unassigned-output records plus provenance into deterministic categorized mail inputs

**Responsibilities**:
1. Group by delivery cause plus scope provenance
2. Produce stable categorized outputs for the mail body
3. Preserve the one-letter-per-shift bounded aggregation model

**Not responsible for**:
- deciding destination keys
- registering mail with SMAPI/MFM
- collecting items from the world

This seam is what keeps richer overflow explanation from turning into ad hoc string logic scattered through `MailDispatcher`.

---

## LC-U22-07 — ShiftOrchestrator (Constrained Runtime Shell)

**Layer**: App / SMAPI runtime integration seam  
**Kind**: Existing production seam with constrained U-22 ownership

**Purpose under U-22**:
- stay the live world adapter while delegating new alignment rules to narrower helpers

**Responsibilities**:
1. Start shifts only for supported typed-scope contracts
2. Request normalized scope and runtime batch structure
3. Execute live world work against those batches
4. Reuse the existing delivery path with categorized overflow inputs

**Important design constraints**:
- do not invent a second compatibility runtime path
- do not become the new source of truth for normalization or categorization rules
- do not add async planning infrastructure to satisfy U-22

---

## LC-U22-08 — AnimalTaskHandler (Expanded Building-Owned Runtime Responsibility)

**Layer**: App / runtime helper seam  
**Kind**: Existing production seam with expanded U-22 ownership

**Purpose under U-22**:
- enforce the building-owned animal service rule in live execution

**Responsibilities**:
1. Resolve animals from selected home buildings
2. Preserve indoor-or-outdoor farm servicing behavior
3. Stay independent from outdoor-zone geometry when determining eligibility

This component is important to the NFR design because its behavior under mixed animal/outdoor scope must be deterministic and testable.

---

## LC-U22-09 — MailDispatcher (Expanded Delivery-Notice Ownership)

**Layer**: App / delivery integration seam  
**Kind**: Existing production seam with expanded U-22 ownership

**Purpose under U-22**:
- continue to deliver one bounded next-morning farmhand letter while consuming richer categorized inputs

**Responsibilities**:
1. Accept categorized overflow/unassigned-output information
2. Preserve concise player-facing wording
3. Preserve existing delivery/fallback behavior

**Important constraint**:
- `MailDispatcher` should render and deliver the categorized story, not own the categorization algorithm itself

---

## LC-U22-10 — Test-Side Mixed-Scope Support

**Layer**: `Dayswork.Tests` only  
**Kind**: Dedicated regression-support helpers

### `U22PropertyGenerators`

**Purpose**:
- generate mixed combinations of outdoor zones, selected animal buildings, greenhouse selection, enabled tasks, destination maps, and overflow causes

### `U22ExampleTests`

**Purpose**:
- pin concrete stories such as:
  - greenhouse work stays out of outdoor batches
  - outdoor-zone changes do not affect selected-building animals
  - unsupported no-scope contracts fail before work begins
  - categorized overflow letters remain one-letter-per-shift and readable

### `U22PropertyTests`

**Purpose**:
- express invariants with FsCheck:
  - deterministic scope normalization
  - deterministic batch-family shaping
  - task-owned routing independence from provenance
  - stable scope-aware categorization

These are explicit logical components because U-22’s NFR bar is driven by mixed-scope combinations more than by isolated one-shot function calls.

---

## Interaction Summary

```text
Shift start
  -> RuntimeScopeSupportGuard validates supported contract
  -> WorkScopeClassifier normalizes ScopeSelection
  -> RuntimeBatchPlanner shapes deterministic batch families
  -> ShiftOrchestrator executes live work

Buffered output
  -> TaskKind continues to drive DepositPlanner routing
  -> ScopedOutputProvenance preserves scope family/origin
  -> ScopeAwareOverflowCategorizer groups undelivered cases
  -> MailDispatcher renders one bounded next-morning letter
```

---

## Why no additional runtime infrastructure was introduced

The NFR design intentionally does **not** add:
- a compatibility execution branch for no-scope contracts
- a second mail subsystem
- a background planning worker
- a cache or queue layer for categorization

Reason:
- only one worker exists in this mod’s current design
- the supported runtime contract is intentionally strict
- the hardest risks are determinism, clarity, and safe failure, not distributed scale
- the existing runtime shell is sufficient if authority and categorization are pulled into narrower seams

That keeps U-22’s scope-alignment retrofit sharp, incremental, and testable.
