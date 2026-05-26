# U-22 — NFR Requirements

**Unit**: U-22 — Scope-Driven Runtime Alignment

U-22 is a runtime-alignment retrofit unit. Its NFR surface is centered on **lightweight typed-scope planning**, **strict determinism for normalized scope, batch shaping, and scope-aware mail categorization**, **safe fail-fast handling for unsupported contracts without typed scope**, **clear but concise player-facing overflow explanations**, and **strong example + property-based regression coverage for the new provenance-aware seams**. NFR decisions applied: NFR-Q1=A, NFR-Q2=A, NFR-Q3=A, NFR-Q4=A, NFR-Q5=A. Functional-design decisions FD-Q1=A, FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=B, FD-Q8=X, and FD-Q9=A apply throughout.

---

## Performance

### PERF-U22-01 — Shift-start scope planning remains comfortably lightweight (NFR-Q1=A)
Typed-scope intake, scope normalization, greenhouse/animal batch shaping, and scope-aware overflow categorization must stay synchronous and cheap enough that they do not add visible hitching or noticeable runtime drag in normal play.

This unit is not permitted to depend on:
- expensive whole-farm rescans every tick
- background workers or async planning pipelines
- repeated reclassification of the same saved scope during one shift unless live state truly requires it

### PERF-U22-02 — Runtime alignment must reuse existing runtime scaffolding where possible
U-22 should achieve its scope-alignment goals by redirecting authority to the typed-scope model, not by introducing a second planner or parallel routing subsystem.

### PERF-U22-03 — Scope-aware mail shaping must stay bounded
Richer overflow/unassigned-output categorization may add more explanation, but letter shaping must remain operationally simple:
- one shift's mail remains one bounded aggregation problem
- categorization should operate on already-buffered output/provenance data
- no heavy retrospective world queries should be required after the shift ends

---

## Reliability & Correctness

### REL-U22-01 — Pure scope-alignment decisions are strictly deterministic (NFR-Q2=A)
Equivalent inputs to the pure scope-alignment seams must produce the same:
- normalized `WorkScopeSet`
- runtime batch family structure and ordering
- task-owned destination mapping outcomes
- scope-aware overflow categorization

across runs and machines.

### REL-U22-02 — Determinism must not depend on incidental ordering
Normalized scope, batch planning, and mail categorization must not vary because of:
- incidental dictionary ordering
- unstable collection ordering
- hidden mutable compatibility state
- differences in how older `Zones` happened to be stored

### REL-U22-03 — Animal and greenhouse behavior must stay predictably separated
Selected animal buildings and greenhouse scope must always resolve the same way for equivalent saved scope:
- outdoor-zone changes do not affect selected-building animal eligibility
- greenhouse work never leaks into outdoor batches
- outdoor targets never leak into greenhouse batches

### REL-U22-04 — Task-owned routing must remain stable under richer provenance
Adding scope provenance must not destabilize destination resolution. The same `TaskKind` and destination map must continue to yield the same delivery target regardless of whether the output came from outdoor, greenhouse, or animal-building work.

---

## Safety & Data Integrity

### SAFE-U22-01 — Unsupported no-scope contracts fail fast and safely (NFR-Q3=A)
If a contract somehow reaches runtime without authoritative `ScopeSelection`, U-22 must reject it before live work begins. No partial execution under guessed or compatibility-derived scope is acceptable in the supported path.

### SAFE-U22-02 — Fail-fast handling must preserve world and data safety
Rejecting an unsupported contract without typed scope must:
- avoid starting partial labor
- avoid routing output under guessed scope
- preserve maintainer-facing diagnostics for debugging
- leave the game state in a predictable non-working state

### SAFE-U22-03 — Richer mail categories must not weaken the no-item-loss invariant
Scope-aware overflow/unassigned-output letters are still governed by the same core safety rule: no collected item is lost. Richer explanation cannot come at the cost of delivery correctness.

### SAFE-U22-04 — No hidden billing or penalty semantics are reintroduced
Scope-aware overflow messaging must not imply or compute new penalties, refunds, or extra charges. This unit changes explanation and scope authority, not pricing semantics.

---

## Usability & Interaction Quality

### USAB-U22-01 — Scope-aware overflow letters must be clear and concise (NFR-Q4=A)
When overflow or unassigned output is mailed, the player should be able to quickly understand:
- why the letter arrived
- which scope family the issue came from
- that the items were still preserved safely

The text should not become noisy, bloated, or overly technical just because it is more specific.

### USAB-U22-02 — Scope-page wording must stay truthful to runtime behavior
The minimal wording updates approved in functional design must keep the UI aligned with live behavior:
- barns/coops clearly imply building-owned animal service anywhere on the farm
- greenhouse clearly implies a dedicated crop work area

### USAB-U22-03 — Added specificity should reduce confusion, not increase it
The point of richer scope-aware explanation is legibility. If multiple categories appear in one letter, the grouping and phrasing must still feel understandable to a player skimming their morning mail.

---

## Maintainability & Testability

### MAINT-U22-01 — Scope authority and normalization stay in pure or near-pure seams
Typed-scope authority, normalization, batch-family shaping, and provenance-aware categorization should remain in deterministic seams instead of being diffused across ad hoc runtime branches.

### MAINT-U22-02 — Strong example + property coverage is required (NFR-Q5=A)
Because U-22 tightens runtime authority and introduces scope provenance, it carries a strong regression bar. It requires:
- focused example-based tests for key mixed-scope scenarios
- meaningful FsCheck properties for the new invariants
- explicit regression coverage for unsupported-contract fail-fast behavior

### MAINT-U22-03 — Property coverage must target the new alignment invariants
At minimum, FsCheck-friendly coverage for U-22 should exercise:
- scope normalization determinism
- animal-zone independence
- greenhouse/outdoor separation
- task-owned routing invariants under varied provenance
- scope-aware mail categorization stability

### MAINT-U22-04 — No new runtime architecture is required for this unit
The quality bar should be met through clearer ownership and stronger tests, not by introducing:
- a background planning engine
- a new mail pipeline
- a second destination-routing model
- a separate compatibility runtime path

---

## Availability / Security / Infrastructure

### AVAIL-U22-01 — No availability-specific requirements
U-22 is an in-process single-player runtime seam. It has no external uptime, failover, or disaster-recovery surface.

### SEC-U22-01 — Security Baseline is N/A
Security Baseline is disabled project-wide. U-22 has no network, auth, or PII surface, so Security Baseline rules are N/A for this unit.

### INFRA-U22-01 — No infrastructure decisions introduced
U-22 requires no cloud, container, service, or deployment mapping beyond the existing `.NET 6` / SMAPI mod runtime.

---

## Property-Based Testing Obligations

### PBT-U22-01 — Scope normalization invariants
Equivalent `ContractScopeSelection` inputs should normalize into equivalent `WorkScopeSet` outputs independent of incidental ordering or compatibility-era `Zones`.

### PBT-U22-02 — Animal-zone independence invariants
For a fixed selected-building set, adding or removing outdoor zones must never change the eligible animal set.

### PBT-U22-03 — Greenhouse/outdoor separation invariants
Generated mixed-scope inputs should show that greenhouse targets never appear in outdoor batches and outdoor targets never appear in greenhouse batches.

### PBT-U22-04 — Task-owned routing invariants
For equivalent destination maps, delivery target choice must depend only on `TaskKind`, even when provenance varies across outdoor, greenhouse, and animal-building outputs.

### PBT-U22-05 — Scope-aware mail categorization invariants
Equivalent overflow/unassigned-output inputs should yield identical grouped scope-aware categories and preserve the one-letter-per-shift behavior.
