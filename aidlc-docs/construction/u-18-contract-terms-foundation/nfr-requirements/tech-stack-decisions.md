# U-18 — Tech Stack Decisions

**Unit**: U-18 — Contract Terms Foundation

NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A. Functional-design decisions FD-Q1=A through FD-Q10=A apply.

---

## TS-U18-01 — Stay entirely in `Dayswork.Core`
U-18 introduces no new framework, package, adapter, or manifest dependency. All implementation stays in the existing pure Core project:
- `Pricing/`
- `Energy/`
- `Domain/`
- `Config/`

This preserves `NFR-MAINT-03` and keeps the unit directly testable without SMAPI or Stardew types.

## TS-U18-02 — Synchronous preview pipeline, no async machinery
`ContractTermsBuilder.BuildPreview(...)` remains a synchronous pure method. No task-based async API, no background worker, and no debounce/caching subsystem are introduced at this layer. The UI coordinator can call it directly on contract-edit events.

## TS-U18-03 — Deterministic ordering must be explicit, never incidental
Implementation must not depend on raw dictionary or set enumeration order for emitted pricing snapshots. Canonical ordering should be enforced with explicit sort logic before building `PricingSnapshot.LineItems`.

Practical implication:
- use structural keys and explicit comparer logic
- sort before emitting persisted or UI-facing line arrays
- do not rely on insertion order of mutable hash collections

## TS-U18-04 — Keyed config lookup uses default-backed resolution
Price/action lookup for:
- outdoor band keys
- animal-building keys
- greenhouse service keys
- work-action energy keys

should resolve through a default-backed lookup strategy. Missing or stale keys fall back to `ConfigDefaults` at key granularity and emit a warning through the existing logging surface later in the Mod layer.

This avoids:
- preview failure from partially stale config
- startup failure from incomplete tables
- hidden silent zero-pricing for missing keys

## TS-U18-05 — Strong test stack remains `xUnit` + `FsCheck`
No new testing framework is introduced. U-18 explicitly leans into the existing stack:
- example-based assertions in `xUnit`
- invariant/property coverage in `FsCheck`

This is the right fit because the unit is pure, deterministic, and rich in structural invariants.

## TS-U18-06 — Property generators must model real contract shapes
The FsCheck suite for U-18 should include generators for:
- overlapping outdoor rectangles
- empty and partially matched scope families
- mixed outdoor/animal/greenhouse task sets
- repeated building tiers
- valid vs invalid preview cases

Generator quality matters here because shallow generators would miss the contract-shape edge cases this redesign cares about.

## TS-U18-07 — One-time terms snapshot the full energy table by design
Implementation should persist the full action-cost map inside one-time `WorkerEnergyProfile`, not a pruned subset. This keeps one-time terms self-contained and immune to later config drift.

For recurring contracts, the same shape is rebuilt from current config rather than mutated in place.

## TS-U18-08 — Preview validity uses structural issue codes
The Core layer should emit structured validation codes/issues, not localized strings or UI-ready prose. Later menu layers can translate those codes through i18n. This keeps U-18 pure while still supporting clear invalid-preview UX.

## TS-U18-09 — No caching requirement at U-18
Because the chosen latency target is immediate synchronous preview, U-18 does not require a memoization or cache layer. If a later profiling pass finds repeated identical previews expensive, caching can be considered as an optimization, not as a foundational requirement of this unit.
