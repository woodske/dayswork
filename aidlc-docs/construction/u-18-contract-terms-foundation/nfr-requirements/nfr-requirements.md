# U-18 — NFR Requirements

**Unit**: U-18 — Contract Terms Foundation

U-18 is a pure Core pricing foundation unit. Its NFR surface is centered on **preview responsiveness**, **strict determinism**, **config resilience**, and **test rigor**, not on availability or infrastructure. NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A. Functional-design decisions FD-Q1=A through FD-Q10=A apply throughout.

---

## Performance

### PERF-U18-01 — Live preview remains immediate (NFR-Q1=A, NFR-PERF-03)
`ContractTermsBuilder.BuildPreview(...)` must normally complete fast enough for same-interaction refresh during task toggles and scope edits. The hiring UI should not need debounce, background computation, or deferred preview state for ordinary contract edits. This preserves the "live price update" behavior from `S-02` and keeps the UI orchestration simple.

### PERF-U18-02 — Pure preview work stays scope-bounded and allocation-light
Preview work is bounded by:
- the number of outdoor zones selected
- the number of selected animal buildings
- the number of enabled tasks
- the number of configured price/action keys

The pipeline must stay linear in those inputs and avoid repeated recomputation of equivalent intermediate results inside one preview build. No morning-world scans, pathing queries, or engine calls are permitted in U-18.

### PERF-U18-03 — No async or cross-thread preview machinery
Because `BuildPreview(...)` is expected to be immediate and the unit is pure, U-18 introduces no asynchronous execution, background workers, or thread-safety complexity. Any later UI throttling would be a caller concern, not a requirement of this unit.

---

## Reliability & Correctness

### REL-U18-01 — Pricing snapshots are strictly deterministic (NFR-Q2=A)
Equivalent input must produce identical `PricingSnapshot` structure across runs and machines:
- same line-item count
- same line-item ordering
- same quantities
- same subtotals and total
- same serialized structural content

This is stricter than "visual determinism only" because persistence, diffs, and property-based tests depend on structural stability.

### REL-U18-02 — Determinism must not depend on dictionary enumeration order
No user-visible or persisted ordering may depend on the incidental iteration order of hash-based collections. Canonical sorting rules defined in Functional Design must be applied explicitly before emitting `PricingSnapshot.LineItems`.

### REL-U18-03 — Missing/stale config keys fall back to defaults and log (NFR-Q3=A)
If a required keyed value is missing or stale in:
- `OutdoorBandPrices`
- `AnimalBuildingPrices`
- `GreenhousePackagePrices`
- `ActionEnergyCosts`

then U-18 falls back to `ConfigDefaults` for that specific key, keeps preview/terms building usable, and emits a maintainer-facing warning. A single missing key must not invalidate the player's contract preview or fail mod startup.

### REL-U18-04 — Fallback behavior is key-local, not table-global
A stale or incomplete config table does not force the entire table to defaults. Only the missing or invalid keys fall back. This preserves user customization wherever the config remains valid.

### REL-U18-05 — Invalid preview is a handled business outcome, not a fault
When a contract has zero chargeable scope-task pairs overall, U-18 returns an invalid preview with structured issues and no proposed terms. This is a normal business result, not an exception path and not a reliability failure.

---

## Safety & Data Integrity

### SAFE-U18-01 — No hidden pricing leakage (NFR-SAFE-02)
U-18 must never reintroduce hidden billing mechanics through its pure terms model. The emitted `ContractTermsSnapshot` contains only the explicit fixed pricing breakdown and worker-energy profile. There is no hourly estimate, no deposit amount, and no refund placeholder embedded in the output shape.

### SAFE-U18-02 — Price math reconciles exactly
For every valid terms build:
- each line total equals `UnitPrice × Quantity`
- each family subtotal equals the sum of its family lines
- grand total equals the sum of all line totals

This is both a correctness rule and a safety rule because the snapshot is later used for real gold deduction.

### SAFE-U18-03 — One-time terms are immutable snapshots
A confirmed one-time contract's `ContractTermsSnapshot` must remain self-contained and stable:
- exact pricing snapshot
- exact daily energy capacity
- exact full action-cost table

Later config changes must not mutate what that already-confirmed one-time contract meant.

### SAFE-U18-04 — Recurring rebuilds are pure and repeatable
Recurring terms must rebuild from saved raw scope, saved tasks, and current config only. No hidden ambient state may influence rebuilt terms. This is required to keep recurring pricing stable on low-work and rainy days and to make config changes apply predictably on the next eligible day.

---

## Maintainability & Testability

### MAINT-U18-01 — U-18 remains fully pure Core logic (NFR-MAINT-03)
All U-18 components and data types stay in `Dayswork.Core` with zero SMAPI/Stardew references. The unit owns one of the cleanest test seams in the redesign and must stay that way.

### MAINT-U18-02 — Strong example + property coverage is required (NFR-Q4=A)
U-18 carries stronger test rigor than the minimum extension floor because it defines the most important pure contract model in the redesign. It requires:
- focused example-based unit tests
- a meaningful FsCheck suite
- reproducible seed logging for new properties

This is not a runtime-heavy unit where most confidence comes from playtesting; it is the unit where pure invariants should do real work.

### MAINT-U18-03 — Property coverage must target the real contract invariants
At minimum, the FsCheck suite for U-18 must exercise:
- outdoor zone union not double-charging overlap
- strict deterministic snapshot ordering/content
- subtotal/total reconciliation
- invalid-preview behavior when no chargeable pair exists
- repeated animal-building aggregation behavior
- one-time full energy-profile snapshot invariants

### MAINT-U18-04 — Extension compliance is a hard gate here
Property-Based Testing extension rules are especially applicable to U-18:
- `PBT-02` round-trip / structural stability
- `PBT-03` invariant properties
- `PBT-07` generator quality
- `PBT-08` shrinking and reproducibility
- `PBT-09` FsCheck framework use

Because U-18 is pure and deterministic by design, failure to meet these is a real quality gap, not a documentation formality.

---

## Usability Support

### UX-U18-01 — Preview remains understandable because the pure model remains structured
Even though U-18 does not render UI, its outputs must support the user-facing clarity goals from `NFR-UX-04`:
- line items correspond to legible pricing concepts
- invalid previews return structured reason codes
- pricing families remain distinguishable
- energy preview has one stable daily-capacity shape

This is a data-contract usability requirement for later UI units.

### UX-U18-02 — U-18 emits structural keys, not localized strings
Preview validity and pricing line items stay structural in Core and are localized later by UI/i18n layers. This preserves separation of concerns while still supporting `S-20`.

---

## Availability / Security / Infrastructure

### AVAIL-U18-01 — No availability-specific requirements
U-18 is an in-process pure library seam inside a local SMAPI mod. It has no separate uptime, failover, or disaster-recovery surface.

### SEC-U18-01 — Security Baseline is N/A
Security Baseline is disabled project-wide (`NFR-SEC-01`). U-18 has no network, auth, or PII surface. All Security Baseline rules are N/A for this unit.

### INFRA-U18-01 — No infrastructure decisions introduced
U-18 requires no external service, storage technology, deployment unit, or runtime host beyond the existing `.NET 6` / SMAPI mod environment.

---

## Property-Based Testing Obligations

### PBT-U18-01 — Zone-union pricing invariants
For equivalent outdoor coverage, overlapping raw rectangles must price the same as their geometric union. Overlap must never increase price.

### PBT-U18-02 — Deterministic snapshot invariants
Equivalent input must produce identical `PricingSnapshot` structure and totals across repeated executions.

### PBT-U18-03 — Breakdown reconciliation invariants
For every valid terms build, line totals, subtotals, and grand total must reconcile exactly.

### PBT-U18-04 — Invalid-preview invariants
If and only if the contract has zero chargeable scope-task pairs overall:
- preview is invalid
- validation issues are present
- proposed terms are absent

### PBT-U18-05 — Aggregation invariants
Adding another identical animal-building scope should only affect the matching aggregated animal line quantity and totals, not unrelated pricing lines.

### PBT-U18-06 — Energy-snapshot invariants
One-time terms always contain the full known action-cost table and daily capacity, even when the selected tasks use only a subset of those work actions.
