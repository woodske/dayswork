# U-22 — Scope-Driven Runtime Alignment: Business Rules

**Unit**: U-22 — Scope-Driven Runtime Alignment  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A (authoritative typed scope only), FD-Q2=A, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A, FD-Q7=B, FD-Q8=X (no older contracts to support), FD-Q9=A

Enforceable rules for consuming redesign-era typed scope at runtime while preserving output safety. See [business-logic-model.md](business-logic-model.md) for flows, [domain-entities.md](domain-entities.md) for data shapes, and [frontend-components.md](frontend-components.md) for the small UI wording updates this unit requires.

---

## Runtime authority and supported contract set

**BR-SCOPE-01 — `Contract.ScopeSelection` is the only supported runtime scope source.** U-22 runtime planning must not derive live execution scope from `Contract.Zones`. *(FD-Q1=A)*

**BR-SCOPE-02 — `Contract.Zones` becomes a compatibility artifact only.** It may remain present structurally, but it is not authoritative for live scope selection once U-22 lands. *(FD-Q1=A)*

**BR-SCOPE-03 — Contracts without authoritative typed scope are outside the supported U-22 runtime set.** Because the project is not live yet, U-22 does not need a fallback execution path for older contracts. *(FD-Q8=X)*

**BR-SCOPE-04 — Runtime scope is normalized before planning begins.** Outdoor zones, animal buildings, and greenhouse selection must be converted into the canonical `WorkScopeSet` before batch planning starts. *(FD-Q1=A, application design)*

---

## Outdoor work rules

**BR-OUTDOOR-01 — Outdoor zones govern only outdoor crop and clearing work.** *(FR-TASK-12, FD-Q3=A, FD-Q4=A)*

**BR-OUTDOOR-02 — Outdoor zones do not define or constrain animal-service eligibility.** *(FD-Q2=A, FD-Q3=A)*

**BR-OUTDOOR-03 — Greenhouse work is never merged into outdoor zone execution.** *(FD-Q4=A)*

---

## Animal-building scope rules

**BR-ANIMAL-01 — Selected barns/coops own animal-service eligibility.** An animal is eligible because its home building was selected, not because its current position happens to fall inside an outdoor zone. *(FR-TASK-12, S-08, FD-Q2=A)*

**BR-ANIMAL-02 — The worker services selected-building animals wherever they are.** Indoor position and outdoor roaming on the farm are both valid execution states for the same selected building. *(S-08, FD-Q2=A)*

**BR-ANIMAL-03 — Outdoor zones never restrict selected-building animals.** A selected coop chicken remains eligible even if it wanders outside every drawn outdoor zone. *(FD-Q3=A)*

**BR-ANIMAL-04 — Outdoor zones never expand animal-service eligibility.** Animals from unselected buildings do not become eligible just because they are standing inside a selected outdoor zone. *(FD-Q3=A)*

---

## Greenhouse scope rules

**BR-GREENHOUSE-01 — Greenhouse selection creates a dedicated crop-work scope.** It is not treated as generic building geometry. *(FR-PAY-05, S-03, FD-Q4=A)*

**BR-GREENHOUSE-02 — Only greenhouse-compatible crop services may execute in the greenhouse scope.** U-22 does not authorize generic outdoor clearing work inside the greenhouse. *(FD-Q4=A)*

**BR-GREENHOUSE-03 — Greenhouse work forms its own batch ahead of outdoor crop and clearing batches.** The approved runtime order is animal-building work first, greenhouse crop work second, outdoor crop work third, and outdoor clearing work fourth. *(FD-Q5=A)*

---

## Output-routing rules

**BR-ROUTE-01 — Output destinations remain task-owned.** There is still exactly one destination mapping per output-producing `TaskKind`. *(S-04, FD-Q6=A)*

**BR-ROUTE-02 — Scope family does not change destination lookup.** If `HarvestCrops` points to one chest, that same mapping applies to both outdoor harvest and greenhouse harvest. *(FD-Q6=A)*

**BR-ROUTE-03 — Deposit planning remains destination-driven.** `DepositPlanner` groups buffered items by destination key, not by scope family. *(S-10, FD-Q6=A)*

**BR-ROUTE-04 — Runtime must retain scope provenance even though routing stays task-owned.** Provenance is required for explanation, not for destination choice. *(FD-Q6=A, FD-Q7=B)*

---

## Overflow and unassigned-output mail rules

**BR-MAIL-01 — Overflow and unassigned-output mail stays next-morning, no-fee, and lossless.** Scope-aware categories do not change the existing safety guarantee. *(S-11, NFR-SAFE-01, FD-Q7=B)*

**BR-MAIL-02 — Overflow causes must now include scope context.** The system must distinguish outdoor, greenhouse, and animal-building cases when shaping the explanatory mail copy. *(FD-Q7=B)*

**BR-MAIL-03 — Scope-aware causes do not require multiple letters.** One farmhand letter per shift may still aggregate all undelivered output as long as its body preserves the richer scope-aware reasons. *(S-11, FD-Q7=B)*

**BR-MAIL-04 — Unassigned-output mail must be scope-aware too.** A missing destination for greenhouse `HarvestCrops` is not described as a generic undifferentiated missing chest case if the runtime can identify the greenhouse provenance. *(FD-Q7=B)*

**BR-MAIL-05 — Scope-aware mail must not imply a pricing penalty.** Explanatory richness does not reintroduce refunds, fees, or hidden billing logic. *(pricing redesign boundary)*

---

## UI-alignment rules

**BR-UI-01 — Scope-page wording must match the building-owned animal model.** The scope UI should make it clear that selected barns/coops cover their assigned animals wherever those animals are on the farm. *(FD-Q9=A)*

**BR-UI-02 — Scope-page wording must match the dedicated greenhouse model.** The scope UI should make it clear that the greenhouse is its own crop work area. *(FD-Q9=A)*

**BR-UI-03 — U-22 should prefer wording updates over structural menu redesign.** The approved scope-page adjustments are intentionally minimal. *(FD-Q9=A)*

---

## Property-based testing obligations

Property-Based Testing is enabled in partial mode. U-22 should keep the scope/runtime seams deterministic and practical for FsCheck coverage.

| Rule | Required property / invariant |
|---|---|
| PBT-03 invariant | Adding or removing outdoor zones never changes the eligible animal set for a fixed selected-building set. |
| PBT-03 invariant | Greenhouse targets never appear in outdoor batches, and outdoor targets never appear in the greenhouse batch. |
| PBT-03 invariant | Destination lookup depends only on `TaskKind`, while scope-aware mail categorization depends on provenance. |
| PBT-07 generator quality | Generators should cover mixed scope combinations: outdoor-only, greenhouse-only, animal-only, and combined contracts. |
| PBT-08 shrinking | Counterexamples should shrink to the smallest mixed-scope contract that demonstrates a routing or categorization mismatch. |
| PBT-09 framework | FsCheck remains the property-based testing framework for these pure seams. |

Security Baseline remains disabled project-wide, so its rules are N/A here.
