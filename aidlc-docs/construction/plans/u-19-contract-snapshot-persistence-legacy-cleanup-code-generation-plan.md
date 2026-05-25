# U-19 — Contract Snapshot Persistence + Legacy Cleanup: Code Generation Plan

**Unit**: U-19 — Contract Snapshot Persistence + Legacy Cleanup
**Phase**: CONSTRUCTION — Code Generation (Part 1 — Planning)
**Builds on**: approved Functional Design, approved NFR Requirements, and approved NFR Design for the schema-v2 persistence retrofit, typed scope serialization, `ReplaceTermsSnapshot(...)`, deterministic serializer ordering, and local/minimal compatibility projection.

> **This plan is the single source of truth for U-19 Code Generation.** Part 2 executes these steps in order, checking each box on completion. No code or test work should happen outside these steps.

---

## Stories & Traceability

| Story / Requirement | Coverage in U-19 |
|---|---|
| **S-05** | Persists the redesigned one-time and recurring contract shape so saved contracts keep authoritative scope and contract terms across save/load. |
| **S-12** | Introduces the persistence seams needed for future recurring edit/reprice flows, including saved-scope survival and narrow terms-snapshot replacement. |
| **S-19** | Keeps the persistence retrofit inside pure/testable Core seams with strong example and FsCheck coverage for round-trip, determinism, and malformed-load resilience. |

**Definition of Done** (from [unit-of-work.md](/C:/Users/kwood/Repos/dayswork/aidlc-docs/inception/application-design/unit-of-work.md)):
- Contracts round-trip with saved scope plus saved `ContractTermsSnapshot`.
- One-time contracts preserve the terms snapshot charged at confirmation.
- Recurring contracts persist enough information to rebuild tomorrow's terms from saved scope.
- Legacy pre-release hourly/deposit/refund contracts are silently dropped on load with no player-facing explanation.

---

## Project Context & Execution Boundaries

- **Workspace root**: `C:\Users\kwood\Repos\dayswork`
- **Solution shape**: existing multi-project mod solution with `Dayswork.Core`, `Dayswork`, and `Dayswork.Tests`
- **Brownfield retrofit rule**: all target files are modified in place or created fresh in the existing structure; never create duplicate `*_new.cs` or `*_modified.cs` files
- **Layer mapping for this unit**:
  - **Business Logic**: `Dayswork.Core/Persistence/` and `Dayswork.Core/Domain/` serializer/store persistence seams
  - **Integration bridge**: `Dayswork/Integration/ContractPersistenceAdapter.cs`
  - **Tests**: `Dayswork.Tests/Persistence/` and supporting generators/helpers
  - **API / Frontend / GMCM**: N/A in this unit

### Explicit retrofit constraints for U-19

1. **Do not switch runtime consumers to `TermsSnapshot` yet.**
   `HiringFlowCoordinator`, `SummaryMenu`, `RecurringContractScheduler`, `ShiftContext`, and related runtime/UI consumers still compile against legacy financial fields. U-19 persists the new authoritative fields while keeping those compatibility fields alive until later units rewire the consumers.

2. **Schema v2 is the only write path.**
   Current saves must write the explicit redesign shape. Schema v1 remains only as legacy-read support for silent drop behavior and regression tests.

3. **Keep the compatibility bridge local and intentionally minimal.**
   `Zones`, `DepositAmount`, and `HourlyRate` remain temporary compatibility projections on the in-memory `Contract`, but they are no longer the authoritative saved pricing source.

4. **No migration framework or async persistence pipeline is introduced here.**
   Save/load remains synchronous, lightweight, and owned by the existing serializer/store/adapter seams.

5. **Current-schema malformed entries must fail in isolation.**
   A bad v2 contract should be skipped with a maintainer-facing warning while valid siblings in the same payload still load.

---

# PART 1 — PLANNING (this document)

Steps 1–18 below. Approval of this plan authorizes Part 2 (execution).

---

# PART 2 — GENERATION STEPS

## Phase A — Store seam and schema-v2 DTO surface

- [x] **Step 1 — Add the explicit schema-v2 DTO model in `Dayswork.Core/Persistence/Dto/`.** Create the current-schema envelope and nested DTOs needed to persist authoritative redesign data: `DaysworkSaveDataV2`, `ContractDtoV2`, typed scope DTOs, pricing snapshot DTOs, worker-energy DTOs, and any nested helper DTOs needed for deterministic JSON structure. Keep DTOs structural only. *S-05, S-12, S-19*

- [x] **Step 2 — Extend `IContractStore` with narrow terms replacement support.** Add `ReplaceTermsSnapshot(...)` to the interface in `Dayswork.Core/Persistence/IContractStore.cs` with a signature that updates only the saved `ContractTermsSnapshot` for an existing contract. Do not broaden this seam into a general partial-update API. *S-12, S-19*

- [x] **Step 3 — Implement `ReplaceTermsSnapshot(...)` in `Dayswork.Core/Persistence/ContractStore.cs`.** Preserve the store's immutable record-update style, keep all non-terms fields unchanged, and retain the existing duplicate/existence guard style already used by the store. *S-12, S-19*

## Phase B — Serializer retrofit to current schema

- [x] **Step 4 — Rewrite `Dayswork.Core/Persistence/SaveDataSerializer.cs` to write schema v2.** Replace the v1-only write path with explicit schema-v2 mapping so persisted contracts now include authoritative `ScopeSelection` and `TermsSnapshot`. Writing must remain synchronous and deterministic. *S-05, S-19*

- [x] **Step 5 — Add explicit version-gated deserialize branching in `SaveDataSerializer`.** Keep null/empty and invalid-JSON guards, reject future schemas with warning, silently drop schema v1 as legacy pre-release data with maintainer-facing warning only, and route schema v2 through the current mapping path. *S-05, S-19*

- [x] **Step 6 — Implement authoritative v2 domain mapping plus local compatibility projection.** On schema-v2 load, hydrate authoritative `ScopeSelection` and `TermsSnapshot`, then project the temporary compatibility fields (`Zones`, `DepositAmount`, `HourlyRate`) locally so downstream runtime code keeps compiling until later retrofit units switch over. *S-05, S-12, S-19*

- [x] **Step 7 — Keep legacy DTO support only where still necessary.** Retain or isolate the v1 DTO types only for legacy detection/tests during the bridge period, and remove any remaining v1 assumptions from the current write path. *S-05, S-19*

- [x] **Step 8 — Make persisted ordering canonical inside the serializer seam.** Ensure emitted contracts, tasks, scope members, and pricing/energy collections are serialized in explicit deterministic order rather than incidental dictionary/list order. Keep canonical ordering ownership concentrated in the serializer mapping layer. *S-19*

## Phase C — SMAPI persistence adapter bridge

- [x] **Step 9 — Update `Dayswork/Integration/ContractPersistenceAdapter.cs` to stop depending on `DaysworkSaveDataV1`.** Move the adapter to a schema-agnostic raw payload handoff so the serializer owns version branching, mixed-payload behavior, and silent v1 drop logic. The adapter should write schema v2 only. *S-05, S-19*

## Phase D — Regression coverage for schema-v2 persistence

- [x] **Step 10 — Add dedicated U-19 persistence generators/helpers in `Dayswork.Tests`.** Create or extend generators/helpers so tests can build valid current-schema contracts with authoritative scope and terms snapshots, plus malformed payload variants for resilience checks. *S-19*

- [x] **Step 11 — Rewrite `Dayswork.Tests/Persistence/SaveDataSerializerTests.cs` around schema v2.** Cover v2 round-trip with scope and terms, silent schema-v1 drop, future-schema rejection, mixed valid/malformed v2 survival, compatibility projection of temporary legacy fields, and at least one deterministic-structure example. *S-05, S-12, S-19*

- [x] **Step 12 — Add FsCheck coverage for the new persistence invariants.** Add property tests for schema-v2 round-trip identity, deterministic repeated serialization, mixed-payload valid-sibling survival, and any focused `ContractTermsSnapshot` replacement invariant that is best expressed property-first. Follow the existing FsCheck seed/shrunk-input logging conventions in the repo. *S-19*

- [x] **Step 13 — Extend `Dayswork.Tests/Persistence/ContractStoreTests.cs` for `ReplaceTermsSnapshot(...)`.** Verify the new seam updates only the terms snapshot, preserves contract identity and all unrelated fields, and fails cleanly for unknown ids. *S-12, S-19*

- [x] **Step 14 — Update shared persistence generators and equality helpers only as needed for the retrofit.** Refresh any older v1-only contract comparison/generation helpers so the persistence suite asserts the richer saved contract shape without breaking unrelated tests that still rely on transitional compatibility fields. *S-05, S-19*

## Phase E — Verification, documentation, and workflow state

- [x] **Step 15 — Build the solution with deploy disabled.** Run `dotnet build Dayswork.sln /p:EnableModDeploy=false` and fix any persistence retrofit breakage before moving on. *Workflow requirement*

- [x] **Step 16 — Run the full automated test suite.** Run `dotnet test Dayswork.sln` and ensure the existing suite plus the new U-19 persistence coverage pass cleanly. *Workflow requirement*

- [x] **Step 17 — Write the U-19 code summary artifact.** Create `aidlc-docs/construction/u-19-contract-snapshot-persistence-legacy-cleanup/code/code-summary.md` summarizing modified vs created files, the schema-v2 save shape, the local compatibility bridge, test additions, and deliberate deferrals to later units. *Workflow requirement*

- [x] **Step 18 — Update workflow tracking and present the standardized completion gate.** Mark all completed plan steps `[x]`, update `aidlc-docs/aidlc-state.md`, append the completion audit entry, and present the required 2-option Code Generation completion message. *Workflow requirement*

---

## Risk Notes

- **Highest risk**: mixed-payload resilience while changing the authoritative save shape. The mitigation is explicit per-contract isolation plus serializer-owned version branching.
- **Second highest risk**: the temporary compatibility projection. `Zones`, `DepositAmount`, and `HourlyRate` must stay correct enough for transitional consumers without becoming the authoritative model again.
- **Testing load is intentionally high here.** U-19 is the main persistence seam for the redesign, so round-trip and determinism coverage belong here rather than being deferred.

## Artifact Output

- **Application code**:
  - `Dayswork.Core/Persistence/`
  - `Dayswork.Core/Persistence/Dto/`
  - `Dayswork.Core/Domain/` if minor contract-shape adjustments are needed
  - `Dayswork/Integration/ContractPersistenceAdapter.cs`
- **Tests**:
  - `Dayswork.Tests/Persistence/`
  - `Dayswork.Tests/Persistence/Generators/`
  - any shared persistence/config helpers updated in place
- **Documentation**:
  - `aidlc-docs/construction/u-19-contract-snapshot-persistence-legacy-cleanup/code/code-summary.md`
