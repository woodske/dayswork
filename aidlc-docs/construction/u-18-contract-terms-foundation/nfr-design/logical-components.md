# U-18 — Logical Components

**Unit**: U-18 — Contract Terms Foundation

NFR design decisions applied: NFR-DES-Q1=A, NFR-DES-Q2=A, NFR-DES-Q3=A, NFR-DES-Q4=A. NFR and Functional Design decisions for `U-18` apply throughout.

---

## Component Map

```text
Dayswork.Core
  ├── ContractTermsBuilder           [existing owned seam]
  │     ├── WorkScopeClassifier      [existing owned seam]
  │     ├── OutdoorServiceBandClassifier [existing owned seam]
  │     ├── ContractPriceCalculator  [existing owned seam]
  │     ├── PriceBreakdownBuilder    [existing owned seam, canonical ordering owner]
  │     ├── WorkerEnergyProfileBuilder [existing owned seam]
  │     └── ConfigValueResolver      [NEW logical helper seam]
  │
  └── ContractPreview / ContractTermsSnapshot / WorkerEnergyProfile

Dayswork.Tests
  ├── U18ExampleTests                [NEW test-side grouping]
  ├── U18PropertyGenerators          [NEW test-side helper]
  └── U18PropertyTests               [NEW test-side grouping]
```

No new runtime/plugin/infrastructure component is introduced. The only new logical seam on the production side is the dedicated config-resolution helper.

---

## LC-U18-01 — ConfigValueResolver

**Layer**: Core / `Dayswork.Core/Config/` or adjacent pure helper namespace  
**Kind**: New logical helper seam  
**Injection**: Constructor-injected into the U-18 pricing/energy builders through `ModEntry` composition or a Core factory, consistent with existing explicit wiring style.

**Purpose**:
- resolve keyed price/action values from `ConfigSnapshot`
- apply per-key fallback to `ConfigDefaults`
- surface fallback-used metadata

**Responsibilities**:
1. Accept a typed config key:
   - outdoor band price key
   - animal-building price key
   - greenhouse price key
   - work-action energy key
2. Probe the current `ConfigSnapshot`
3. If the key is present and valid:
   - return the configured value
   - mark `UsedDefault = false`
4. If the key is missing or stale:
   - read the corresponding default from `ConfigDefaults`
   - return that fallback value
   - mark `UsedDefault = true`

**Output shape**:
- either a simple `(value, usedDefault)` tuple
- or a tiny record like `ResolvedConfigValue<T>`

Exact type choice is a Code Generation detail. The design requirement is one shared pure seam, not duplicated fallback logic.

**Not responsible for**:
- logging to SMAPI directly
- localizing messages
- deciding preview validity

It only resolves values and exposes fallback metadata. Later layers may observe and log fallback usage.

---

## LC-U18-02 — ContractTermsBuilder (Extended Ownership)

**Layer**: Core / existing owned seam  
**Existing component** — no new cache behavior added

**NFR-design responsibilities**:
1. Orchestrate the full pure pipeline synchronously
2. Keep preview computation stateless and recompute-based
3. Treat invalid-preview outcomes as structured data, not exceptions
4. Consume `ConfigValueResolver` indirectly through downstream builders

**Important design constraint**:
- no internal memoization or preview cache
- no async API
- no retained "last draft / last preview" state

This keeps the builder:
- predictable
- directly testable
- free of invalidation complexity

---

## LC-U18-03 — PriceBreakdownBuilder (Canonical Ordering Owner)

**Layer**: Core / existing owned seam  
**Existing component** — gains explicit determinism ownership in the NFR design

**Responsibilities under this design**:
1. Aggregate raw contributions by normalized pricing key
2. Compute deterministic line totals and family subtotals
3. Apply explicit canonical ordering before emitting `PricingSnapshot.LineItems`

**Ordering ownership rule**:
- upstream components may emit stable data
- `PriceBreakdownBuilder` is solely authoritative for final line-item order

**Internal helper allowance**:
- it may use a small internal comparer/helper object
- but that helper remains part of `PriceBreakdownBuilder`'s implementation boundary, not a separate top-level reusable production component by default

This matches NFR-DES-Q3=A: one existing seam owns determinism rather than spreading that concern across multiple stages.

---

## LC-U18-04 — Production Pipeline Boundaries

The rest of the U-18 owned seams remain, but with clarified NFR-design expectations:

### WorkScopeClassifier
- stays pure
- does no config fallback
- does no ordering policy work beyond its own local structural output

### OutdoorServiceBandClassifier
- stays pure
- consumes resolved thresholds/config as needed
- does not own final snapshot order

### ContractPriceCalculator
- stays pure
- reads resolved values rather than probing raw config dictionaries directly
- does not own fallback policy

### WorkerEnergyProfileBuilder
- stays pure
- reads resolved action-cost values through the shared config-resolution seam
- does not prune the table for one-time contracts

This separation keeps non-functional concerns well placed:
- resilience for config lookup -> `ConfigValueResolver`
- determinism for emitted snapshot order -> `PriceBreakdownBuilder`
- sync recompute preview -> `ContractTermsBuilder`

---

## LC-U18-05 — Test-Side Support Components

**Layer**: `Dayswork.Tests` only  
**Kind**: Dedicated unit-specific test helpers, not production code

### `U18PropertyGenerators`

**Purpose**:
- domain-specific FsCheck generators for U-18 contract shapes

**Responsibilities**:
- generate overlapping outdoor rectangles
- generate equivalent zone sets that should normalize the same way
- generate repeated animal-building tiers
- generate partially matched scope/task combinations
- generate valid and invalid preview cases
- generate mixed outdoor/animal/greenhouse task sets

### `U18ExampleTests`

**Purpose**:
- focused readable examples for key contract-shape behaviors

Examples include:
- overlapping zones normalize and do not double-charge
- two deluxe coops aggregate into one animal line with quantity `2`
- greenhouse water/harvest create separate package lines
- zero chargeable pair yields invalid preview and no proposed terms

### `U18PropertyTests`

**Purpose**:
- express invariant-style requirements with FsCheck

Examples include:
- determinism across repeated executions
- reconciliation of totals and subtotals
- invalid-preview iff zero chargeable pair
- one-time full action-cost table snapshot

These test-side helpers are explicit design components because U-18's NFR bar requires more than ad hoc inline generators.

---

## LC-U18-06 — Logging Observation Boundary

Because `ConfigValueResolver` is pure and Core stays free of SMAPI:
- fallback detection happens in Core
- fallback logging happens later in the integration layer when the result is observed

This keeps:
- Core pure
- logging policy outside the computation seam
- fallback behavior testable without asserting on log sinks

This is an important boundary choice for NFR-Q3=A: "fallback and log" does not mean the pure helper should itself depend on a logger.

---

## Interaction Summary

```text
BuildPreview / BuildTerms / RebuildTerms
  -> WorkScopeClassifier
  -> OutdoorServiceBandClassifier
  -> ContractPriceCalculator
       uses resolved config values
  -> PriceBreakdownBuilder
       owns canonical ordering
  -> WorkerEnergyProfileBuilder
       uses resolved config values
  -> ContractPreview / ContractTermsSnapshot

Test layer
  -> U18PropertyGenerators
  -> U18ExampleTests
  -> U18PropertyTests
```

---

## Why no additional components were introduced

The NFR design intentionally does **not** add:
- a cache component
- an async preview worker
- a separate ordering service
- a startup-time config normalization pass

Reason:
- input sizes are small and bounded
- immediate synchronous preview is already required
- one extra focused resilience seam is enough
- ordering has a natural home in `PriceBreakdownBuilder`
- excessive helper splitting would make a small pure unit harder to follow, not easier
