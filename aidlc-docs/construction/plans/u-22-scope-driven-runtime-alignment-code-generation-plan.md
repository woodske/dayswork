# U-22 — Scope-Driven Runtime Alignment: Code Generation Plan

**Unit**: U-22 — Scope-Driven Runtime Alignment  
**Phase**: CONSTRUCTION — Code Generation (Part 1 — Planning)  
**Builds on**: approved Functional Design, approved NFR Requirements, and approved NFR Design for authoritative typed-scope runtime intake, building-owned animal execution, greenhouse batch separation, task-owned output routing with sidecar provenance, fail-fast unsupported-contract handling, and deterministic scope-aware overflow categorization on the existing mail path.

> **This plan is the single source of truth for U-22 Code Generation.** Part 2 executes these steps in order, checking each box on completion. No code or test work should happen outside these steps.

---

## Stories & Traceability

| Story / Requirement | Coverage in U-22 |
|---|---|
| **S-03** | Aligns live runtime consumption of selected outdoor zones, animal buildings, and greenhouse scope with the redesign-era typed scope model. |
| **S-04** | Preserves task-owned output destinations while ensuring typed-scope runtime output still resolves through the correct routing path. |
| **S-08** | Makes selected barns/coops authoritative for animal service everywhere on the farm and gives greenhouse work its own dedicated crop batch ahead of outdoor crop/clearing work. |
| **S-10** | Keeps deposit behavior correct after scope alignment by preserving task-owned routing and deterministic batch execution. |
| **S-11** | Upgrades overflow and unassigned-output handling to produce scope-aware explanations without weakening the one-letter, no-item-loss model. |

**Definition of Done** (from [unit-of-work.md](/C:/Users/kwood/Repos/dayswork/aidlc-docs/inception/application-design/unit-of-work.md)):
- Selected barns/coops are treated as building-owned animal scopes at runtime.
- Worker services those animals wherever they are on the farm.
- Greenhouse is treated as dedicated crop-work scope rather than generic building geometry.
- Deposit/mail routing still works correctly under the new typed scope model.

---

## Project Context & Execution Boundaries

- **Workspace root**: `C:\Users\kwood\Repos\dayswork`
- **Solution shape**: existing multi-project mod solution with `Dayswork.Core`, `Dayswork`, and `Dayswork.Tests`
- **Brownfield retrofit rule**: all target files are modified in place or created fresh in the existing structure; never create duplicate `*_new.cs` or `*_modified.cs` files
- **Layer mapping for this unit**:
  - **Pure Core scope/routing seams**: `Dayswork.Core/Domain/`, `Dayswork.Core/Inventory/`, `Dayswork.Core/Pricing/`, `Dayswork.Core/Shifts/`
  - **Runtime shell / SMAPI orchestration**: `Dayswork/Orchestration/`, `Dayswork/Integration/`
  - **Player-facing wording**: `Dayswork/UI/`, `Dayswork/i18n/default.json`
  - **Tests**: `Dayswork.Tests/` with a focused U-22 area plus existing routing/scope suites updated in place
  - **Documentation**: `aidlc-docs/construction/u-22-scope-driven-runtime-alignment/code/`
  - **API / Infra / Deployment artifacts**: N/A in this unit

### Explicit retrofit constraints for U-22

1. **U-22 is the typed-scope runtime alignment unit, not another pricing or billing pass.**  
   Do not revisit contract pricing, recurring billing, or worker-energy arithmetic here.

2. **No compatibility runtime fallback path should be built for missing typed scope.**  
   Supported runtime execution requires `Contract.ScopeSelection`; unsupported no-scope contracts must fail fast rather than guessing from `Zones`.

3. **`DepositPlanner` must remain task-owned.**  
   Destinations stay keyed by `TaskKind`; scope provenance is added only for explanation/categorization.

4. **Scope-aware mail must layer onto the existing mail pipeline.**  
   Do not invent a second overflow-delivery subsystem or multiple-letter strategy unless absolutely required by the approved design.

5. **UI impact must stay minimal.**  
   This unit may update wording/summary text, but it should not reopen the larger U-20 screen-structure redesign.

6. **Deterministic mixed-scope behavior is a core requirement.**  
   Outdoor, greenhouse, and selected-building animal logic must remain stable across equivalent inputs and must be covered by example/property tests.

---

# PART 1 — PLANNING (this document)

Steps 1–21 below. Approval of this plan authorizes Part 2 (execution).

---

# PART 2 — GENERATION STEPS

## Phase A — Core authority, provenance, and batch-planning foundations

- [x] **Step 1 — Make typed scope the authoritative runtime input at shift start.** Update the runtime-facing contract/startup seams so live execution reads `Contract.ScopeSelection` as the supported scope source, and add the narrow fail-fast support guard for contracts that somehow still lack typed scope. Target `Dayswork.Core/Domain/Contract.cs`, `Dayswork.Core/Shifts/ShiftContext.cs`, and the relevant startup seams. *S-03, S-08, S-10*

- [x] **Step 2 — Introduce or retrofit the lightweight provenance carriers needed for typed-scope output explanation.** Extend the buffered-output / overflow data model so output can retain `TaskKind` plus scope family/location provenance without changing the task-owned destination model. Target `Dayswork.Core/Inventory/BufferedItem.cs`, `Dayswork.Core/Inventory/DepositPlan.cs`, and any adjacent Core records. *S-04, S-10, S-11*

- [x] **Step 3 — Retrofit deterministic runtime batch shaping around typed scope.** Expand or refactor the batch-planning seam so the runtime builds the approved family order from normalized scope: animal-building work, greenhouse crop work, outdoor crop work, then outdoor clearing work. Target `Dayswork.Core/Shifts/ShiftPlanBuilder.cs`, `Dayswork.Core/Shifts/WorkBatch.cs`, and any helper seams needed to represent the richer batch families deterministically. *S-03, S-08, S-10*

- [x] **Step 4 — Add focused Core tests for the new authority/provenance/batch foundations.** Create or update pure tests covering no-scope rejection behavior, greenhouse/outdoor batch separation, and task-owned routing remaining unaffected by provenance. *S-03, S-04, S-08, S-10, S-11*

## Phase B — Live runtime alignment in the orchestrator and animal helper

- [x] **Step 5 — Refactor `ShiftOrchestrator` startup away from `contract.Zones`.** Replace the current zone-driven initial-batch path with a typed-scope path that normalizes `Contract.ScopeSelection`, builds deterministic runtime batches from that normalized scope, and stops logging/depending on compatibility-zone authority for supported contracts. *S-03, S-08, S-10*

- [x] **Step 6 — Make selected animal buildings the authoritative source for animal-service eligibility.** Update the live animal-work path so selected barns/coops define the eligible animal set regardless of outdoor zones, while preserving indoor/outdoor-on-farm servicing behavior. Target `Dayswork/Orchestration/AnimalTaskHandler.cs` and the relevant `ShiftOrchestrator` animal-work assembly logic. *S-03, S-08*

- [x] **Step 7 — Align greenhouse runtime work to its dedicated crop batch.** Ensure the greenhouse is treated as its own crop-work scope rather than as a generic interior/building-zone placeholder, and keep it separate from outdoor crop/clearing execution. Target the batch-planning and location-entry logic in `ShiftOrchestrator`. *S-03, S-08, S-10*

- [x] **Step 8 — Remove supported-path assumptions that still derive runtime scope from compatibility data.** Clean up the live runtime path so supported contracts do not silently merge, re-project, or otherwise re-infer scope from `Zones`, while preserving only the minimal compatibility shape needed outside U-22’s supported execution path. *S-03, S-08*

## Phase C — Preserve task-owned routing and add scope-aware overflow categorization

- [x] **Step 9 — Keep `DepositPlanner` task-owned while adapting it to richer buffered output.** Update the planner and its callers only as needed so destination resolution still depends solely on `TaskKind`, even when buffered output now carries scope provenance for explanation. Target `Dayswork.Core/Inventory/DepositPlanner.cs` and its direct tests. *S-04, S-10*

- [x] **Step 10 — Thread scope provenance through live output collection.** Update the runtime collection call sites so buffered output from outdoor, greenhouse, and selected-building animal work all record the correct scope provenance at the moment they enter the worker buffer. This should include the main object/crop/tree collection paths and the animal-product path in `ShiftOrchestrator`. *S-04, S-10, S-11*

- [x] **Step 11 — Add a deterministic scope-aware overflow categorization seam.** Introduce the helper logic that groups undeliverable output by cause plus scope provenance while preserving the one-letter-per-shift aggregation model. Target Core or near-pure seams in `Dayswork.Core/Inventory/` or another narrow location that keeps categorization out of direct string-building code. *S-11*

- [x] **Step 12 — Expand the mail contract to accept richer categorized inputs.** Update `Dayswork/Integration/IMailDispatcher.cs` and `Dayswork/Integration/MailDispatcher.cs` so the existing mail path can render concise scope-aware explanations without changing delivery semantics or introducing multiple-letter complexity by default. *S-11*

- [x] **Step 13 — Retrofit end-of-shift overflow flushing to use the new categorization path.** Update the `ShiftOrchestrator` overflow/mail helpers so the worker still sends one bounded next-morning letter, but the body now reflects categorized outdoor, greenhouse, and animal-building cases. *S-10, S-11*

## Phase D — Minimal UI wording alignment

- [x] **Step 14 — Update the scope page wording to match building-owned animal execution.** Apply the approved minimal wording/summary changes so the scope-selection page clearly indicates that selected barns/coops cover their assigned animals wherever those animals roam on the farm. Target `Dayswork/UI/ZoneAndChestMenu.cs` plus `Dayswork/i18n/default.json`. *S-03*

- [x] **Step 15 — Update greenhouse wording to match the dedicated crop-work model.** Ensure the scope-related UI copy clearly communicates that the greenhouse is its own crop-work area rather than a generic building selection, without changing page structure. Target `ZoneAndChestMenu.cs`, and any summary text only if required for consistency. *S-03*

## Phase E — Regression and property coverage

- [x] **Step 16 — Add focused U-22 example tests for mixed-scope runtime behavior.** Create a dedicated U-22 test area covering representative scenarios like greenhouse work staying out of outdoor batches, outdoor-zone edits not changing selected-building animal eligibility, and unsupported no-scope contracts rejecting before work begins. *S-03, S-08, S-10*

- [x] **Step 17 — Add or extend FsCheck generators for mixed-scope contracts and overflow cases.** Create the generators needed to vary outdoor zones, selected buildings, greenhouse selection, enabled tasks, destination maps, and overflow causes without relying on legacy runtime assumptions. *S-03, S-04, S-08, S-10, S-11*

- [x] **Step 18 — Add FsCheck properties for U-22’s new invariants.** Cover deterministic scope normalization, animal-zone independence, greenhouse/outdoor separation, task-owned routing independence from provenance, and scope-aware mail categorization stability. *S-03, S-04, S-08, S-10, S-11*

- [x] **Step 19 — Update any shared helpers/comparers only as needed.** Refresh structural comparers, shared generators, or existing scope/routing tests so the U-22 provenance-aware model integrates cleanly without weakening prior coverage. *Supporting*

## Phase F — Verification and documentation

- [x] **Step 20 — Run verification for the completed runtime-alignment retrofit.** Execute `dotnet build Dayswork.sln /p:EnableModDeploy=false` and `dotnet test Dayswork.sln`, then fix any U-22 regressions required to restore a green build/test state. *All U-22 stories*

- [x] **Step 21 — Write the U-22 code summary and close the unit plan.** Document the modified/created files, key runtime/mail/routing changes, and verification results in `aidlc-docs/construction/u-22-scope-driven-runtime-alignment/code/code-summary.md`, then mark the plan complete. *All U-22 stories*
