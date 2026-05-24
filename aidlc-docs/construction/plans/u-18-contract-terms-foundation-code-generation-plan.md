# U-18 — Contract Terms Foundation: Code Generation Plan

**Unit**: U-18 — Contract Terms Foundation
**Phase**: CONSTRUCTION — Code Generation (Part 1 — Planning)
**Builds on**: approved Functional Design (FD-Q1=A through FD-Q10=A), approved NFR Requirements, and approved NFR Design (including `ConfigValueResolver`, pure recompute previews, canonical ordering in `PriceBreakdownBuilder`, and dedicated U-18 FsCheck support).

> **This plan is the single source of truth for U-18 Code Generation.** Part 2 executes these steps in order, checking each box on completion. No code or test work should happen outside these steps.

---

## Stories & Traceability

| Story / Requirement | Coverage in U-18 |
|---|---|
| **S-02** | Introduces the pure fixed-price calculation and preview terms model that later UI work will render live. |
| **S-03** | Introduces typed scope selection and classification for outdoor zones, animal buildings, and greenhouse work. |
| **S-06** | Introduces `ContractPreview`, `PricingSnapshot`, and `WorkerEnergyProfile` so later summary/confirm screens can show final price and stamina clearly. |
| **S-13** | Introduces the new config snapshot shape for contract prices, band thresholds, daily worker energy, and per-action energy costs. |
| **S-14** | Locks the pure rule that pricing depends on selected scope, enabled tasks, and config only, not weather/festival/morning actionable work. |
| **S-19** | Keeps the new pricing/energy foundation inside `Dayswork.Core` with strong example and property-based coverage. |

**Definition of Done** (from [unit-of-work.md](/C:/Users/kwood/Repos/dayswork/aidlc-docs/inception/application-design/unit-of-work.md)):
- Pure Core code classifies selected outdoor zones, animal buildings, and greenhouse work into typed scopes.
- Fixed contract price and price breakdown are deterministic for the same scope/task/config input.
- Worker energy profile is produced alongside the pricing snapshot.
- FsCheck coverage exists for determinism, reconciliation, overlap handling, invalid-preview behavior, and full energy-table snapshot invariants.

---

## Project Context & Execution Boundaries

- **Workspace root**: `C:\Users\kwood\Repos\dayswork`
- **Solution shape**: existing multi-project mod solution with `Dayswork.Core`, `Dayswork`, and `Dayswork.Tests`
- **Brownfield retrofit rule**: all target files are modified in place or created fresh in the existing structure; never create duplicate `*_new.cs` or `*_modified.cs` files
- **Layer mapping for this unit**:
  - **Business Logic**: `Dayswork.Core` typed scopes, pricing builders, energy-profile model, config-resolution helper
  - **Integration bridge**: `Dayswork/Integration/ModConfig.cs` and `RuntimeConfigSnapshotMapper.cs` only, so the runtime config can publish the new Core snapshot shape
  - **API / Repository / Frontend**: N/A in this unit

### Explicit retrofit constraints for U-18

1. **Do not retarget the historical hire UI or recurring scheduler yet.**
   `TaskSelectionMenu`, `SummaryMenu`, `HiringFlowCoordinator`, and `RecurringContractScheduler` still rely on the historical U-05 pricing path. U-18 introduces the new foundation beside that path; U-20 and U-23 switch the consumers over.

2. **Do not change persistence DTOs in this unit.**
   U-19 owns serializer/schema changes. U-18 may extend the in-memory `Contract` shape additively if needed for future builder signatures, but save DTO/version work waits for U-19.

3. **Legacy U-05 pricing code remains temporarily compiled for compatibility.**
   `RateCalculator`, `DepositCalculator`, `RefundCalculator`, `HoursEstimator`, and `DepositHoursPolicy` stay in place until the later retrofit units stop consuming them. U-18 removes them from the new architecture, not necessarily from the repo in the same step.

4. **No SMAPI/Stardew references are allowed in the new U-18 production seams.**
   The only non-Core work in this unit is publishing the richer config snapshot from the existing integration layer.

---

# PART 1 — PLANNING (this document)

Steps 1–20 below. Approval of this plan authorizes Part 2 (execution).

---

# PART 2 — GENERATION STEPS

## Phase A — Core domain, pricing, and energy model (`Dayswork.Core`)

- [x] **Step 1 — Add the raw selection and typed scope model in `Dayswork.Core/Domain/`.** Create the pure selection/scope files that U-18 owns: `AnimalBuildingTier.cs`, `AnimalBuildingSelection.cs`, `GreenhouseSelection.cs`, `ContractScopeSelection.cs`, `WorkScopeSet.cs`, `OutdoorWorkScope.cs`, `AnimalBuildingScope.cs`, and `GreenhouseWorkScope.cs`. Reuse existing `Zone` and `TaskKind` directly. *S-03, S-19*

- [x] **Step 2 — Add the structural pricing snapshot types in `Dayswork.Core/Domain/`.** Create `OutdoorBandSize.cs`, `OutdoorServiceBand.cs`, `OutdoorPriceKey.cs`, `AnimalBuildingPriceKey.cs`, `GreenhousePriceKey.cs`, `ContractPriceTotals.cs`, `PricingFamily.cs`, `PricingLineItem.cs`, `PricingSnapshot.cs`, `ContractValidationCode.cs`, `ContractValidationIssue.cs`, `ContractPreview.cs`, and `ContractTermsSnapshot.cs`. Keep them structural only: no localized strings, no weather, no hourly/deposit/refund fields. *S-02, S-06, S-14, S-19*

- [x] **Step 3 — Add the worker-energy model in the new `Dayswork.Core/Energy/` folder.** Create `WorkActionKind.cs`, `WorkerEnergyProfile.cs`, `IWorkerEnergyProfileBuilder.cs`, and `WorkerEnergyProfileBuilder.cs`. U-18 only owns the daily capacity plus full action-cost table profile; runtime spend ledger work waits for U-21. *S-06, S-13, S-19*

- [x] **Step 4 — Extend `Dayswork.Core/Domain/Contract.cs` additively for the retrofit bridge.** Preserve the historical `DepositAmount` and `HourlyRate` fields for compatibility, and add optional trailing U-18 fields for raw scope selection and terms snapshot so `IContractTermsBuilder.RebuildTerms(Contract, ...)` has a future home without forcing persistence changes yet. Keep existing call sites compiling through additive defaults only. *S-06, S-19; bridge to U-19/U-23*

- [x] **Step 5 — Extend the immutable config shape in `Dayswork.Core/Config/`.** Update `IConfigSnapshot.cs`, `ConfigSnapshot.cs`, `ConfigDefaults.cs`, and `ConfigSnapshotFactory.cs` to carry the U-18 data tables: outdoor band thresholds, outdoor service-band prices, animal-building prices by tier, greenhouse package prices, daily worker energy capacity, and per-action energy costs. Retain the historical U-05 pricing fields for temporary compatibility with U-20/U-23 consumers. *S-13, S-14, S-19*

- [x] **Step 6 — Add `ConfigValueResolver` in `Dayswork.Core/Config/`.** Implement the shared pure helper seam that resolves keyed U-18 values from `ConfigSnapshot`, falls back per-key to `ConfigDefaults`, and exposes whether a default was used. It must not log directly or localize messages. *S-13, S-19*

## Phase B — Pure builder pipeline (`Dayswork.Core/Pricing/`)

- [x] **Step 7 — Add `IWorkScopeClassifier` and `WorkScopeClassifier`.** Use existing geometry helpers to union overlapping outdoor rectangles, materialize only scope families relevant to the enabled tasks, and treat animal buildings and greenhouse as distinct typed scopes. Keep the classifier pure and deterministic. *S-03, S-14, S-19*

- [x] **Step 8 — Add `IOutdoorServiceBandClassifier` and `OutdoorServiceBandClassifier`.** Classify only enabled outdoor services against shared small/medium/large thresholds using the unioned outdoor footprint, with no discovered workload or weather input. *S-02, S-03, S-14, S-19*

- [x] **Step 9 — Add `IContractPriceCalculator` and `ContractPriceCalculator`.** Compute raw fixed totals for outdoor band pricing, per-building animal pricing, and greenhouse package pricing using `ConfigValueResolver`. Return family subtotals plus total price only. *S-02, S-06, S-13, S-14, S-19*

- [x] **Step 10 — Add `IPriceBreakdownBuilder` and `PriceBreakdownBuilder`.** Build deterministic `PricingSnapshot` output, aggregate by normalized pricing key, and make `PriceBreakdownBuilder` the sole owner of canonical family/service/key ordering. *S-02, S-06, S-19*

- [x] **Step 11 — Add `IContractTermsBuilder` and `ContractTermsBuilder`.** Implement `BuildPreview(...)`, `BuildTerms(...)`, and `RebuildTerms(...)` as the pure facade over classification, banding, fixed pricing, and energy profile building. Invalid preview must be structured data, not exceptions. `BuildTerms` must fail fast for zero chargeable scope-task pairs. `RebuildTerms` may reject legacy contracts that do not yet carry the new raw-scope data. *S-02, S-06, S-14, S-19*

## Phase C — Runtime config publishing bridge (`Dayswork/Integration/`)

- [x] **Step 12 — Extend `Dayswork/Integration/ModConfig.cs` for the richer U-18 config surface.** Add the new persisted config properties needed to build the U-18 snapshot shape, but leave GMCM exposure work for U-24. Preserve the historical fields temporarily so existing U-05 consumers continue working until they are replaced. *S-13; bridge to U-24*

- [x] **Step 13 — Update `Dayswork/Integration/RuntimeConfigSnapshotMapper.cs` and `ModConfigManager.cs` only as needed to publish the new snapshot.** Normalize the new U-18 pricing/energy config fields, build the richer `ConfigSnapshot`, and keep existing legacy pricing fields intact for transitional compatibility. No `TaskSelectionMenu`, `SummaryMenu`, `HiringFlowCoordinator`, `RecurringContractScheduler`, or `ModEntry` consumer rewiring happens in this unit. *S-13, S-14*

## Phase D — Test generators and U-18 regression coverage (`Dayswork.Tests`)

- [x] **Step 14 — Update the shared config and contract generators for the additive model.** Modify `Dayswork.Tests/Generators/ConfigSnapshotGen.cs`, `Dayswork.Tests/Generators/PricingGen.cs`, and `Dayswork.Tests/Persistence/Generators/ContractGen.cs` so existing historical tests still compile while the richer U-18 snapshot and optional contract fields are present. *S-19*

- [x] **Step 15 — Add focused U-18 example tests.** Create example-based tests covering overlap normalization, greenhouse per-service package lines, repeated animal-building tier aggregation, invalid preview with zero chargeable pairs, and `ConfigValueResolver` per-key fallback behavior. Use new dedicated U-18 test files under `Dayswork.Tests/Pricing/` and `Dayswork.Tests/Config/`. *S-02, S-03, S-06, S-13, S-14, S-19*

- [x] **Step 16 — Add dedicated U-18 property generators and property tests.** Create a unit-specific FsCheck helper in `Dayswork.Tests/Generators/` plus property tests in `Dayswork.Tests/Pricing/` for: deterministic repeated execution, totals/subtotals reconciliation, overlap-equivalence of outdoor pricing, invalid-preview iff zero chargeable scope-task pairs, and full action-cost table snapshot invariants. Follow the existing U-02 seed/shrunk-input logging pattern. *S-19*

- [x] **Step 17 — Refresh existing config tests to match the new snapshot contract.** Update `Dayswork.Tests/Config/ConfigDefaultsTests.cs`, `ConfigSnapshotFactoryTests.cs`, `ConfigSnapshotGenSmokeTests.cs`, and `RuntimeConfigSnapshotMapperTests.cs` so they validate the richer U-18 snapshot shape and the legacy compatibility bridge together. *S-13, S-19*

## Phase E — Verification, documentation, and workflow state

- [x] **Step 18 — Build and test the whole solution.** Run `dotnet build Dayswork.sln /p:EnableModDeploy=false` and `dotnet test Dayswork.sln`. Fix any breakage before proceeding. Expect the historical suite plus the new U-18 coverage to pass. *S-19*

- [x] **Step 19 — Write the code summary artifact.** Create `aidlc-docs/construction/u-18-contract-terms-foundation/code/code-summary.md` summarizing created vs modified files, the legacy-compatibility bridge, the U-18 test surface, and any deliberate deferrals to U-19/U-20/U-23/U-24. *Workflow requirement*

- [x] **Step 20 — Update workflow tracking and present the standardized completion message.** Mark all completed plan steps `[x]`, update `aidlc-docs/aidlc-state.md`, append the completion audit entry, and present the required 2-option Code Generation completion gate. *Workflow requirement*

---

## Risk Notes

- **Highest risk**: the config-snapshot bridge. U-18 is introducing the new fixed-price snapshot while the historical menus and recurring scheduler still exist. The mitigation is explicit: keep legacy U-05 pricing fields compiled until the later consumer-retrofit units land.
- **Second highest risk**: additive `Contract` bridging. The new builder surface wants scope + terms on the contract, but persistence does not change until U-19. The mitigation is additive optional fields only, with no DTO/schema edits in U-18.
- **Property-based testing load is intentionally heavy here.** U-18 is the main pure seam for the redesign, so this is where determinism and invariant coverage should be strongest.

## Artifact Output

- **Application code**:
  - `Dayswork.Core/Domain/`
  - `Dayswork.Core/Pricing/`
  - `Dayswork.Core/Energy/`
  - `Dayswork.Core/Config/`
  - `Dayswork/Integration/`
- **Tests**:
  - `Dayswork.Tests/Config/`
  - `Dayswork.Tests/Generators/`
  - `Dayswork.Tests/Pricing/`
  - `Dayswork.Tests/Persistence/Generators/`
- **Documentation**:
  - `aidlc-docs/construction/u-18-contract-terms-foundation/code/code-summary.md`
