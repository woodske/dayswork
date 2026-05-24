# U-18 — Contract Terms Foundation: Code Summary

## Outcome

U-18 lands the new pure contract-terms foundation without rewiring the current playable hire/runtime flow yet.

The generated code now provides:
- typed raw selection and normalized scope modeling for outdoor zones, animal buildings, and greenhouse work
- pure fixed-price calculation and deterministic pricing breakdown snapshots
- pure worker energy-profile generation with a full action-cost table
- a richer immutable config snapshot plus per-key fallback resolution
- a runtime config bridge that can publish the new snapshot shape while preserving the historical U-05 fields for compatibility

This keeps the current historical UI and recurring scheduler intact for now, while giving U-19, U-20, U-23, and U-24 the new foundation they need.

---

## Created Files

### Core domain and energy model
- `Dayswork.Core/Domain/TaskKindSets.cs`
- `Dayswork.Core/Domain/AnimalBuildingTier.cs`
- `Dayswork.Core/Domain/AnimalBuildingSelection.cs`
- `Dayswork.Core/Domain/GreenhouseSelection.cs`
- `Dayswork.Core/Domain/ContractScopeSelection.cs`
- `Dayswork.Core/Domain/WorkScopeSet.cs`
- `Dayswork.Core/Domain/OutdoorWorkScope.cs`
- `Dayswork.Core/Domain/AnimalBuildingScope.cs`
- `Dayswork.Core/Domain/GreenhouseWorkScope.cs`
- `Dayswork.Core/Domain/OutdoorBandSize.cs`
- `Dayswork.Core/Domain/OutdoorServiceBand.cs`
- `Dayswork.Core/Domain/OutdoorPriceKey.cs`
- `Dayswork.Core/Domain/AnimalBuildingPriceKey.cs`
- `Dayswork.Core/Domain/GreenhousePriceKey.cs`
- `Dayswork.Core/Domain/ContractPriceTotals.cs`
- `Dayswork.Core/Domain/PricingFamily.cs`
- `Dayswork.Core/Domain/PricingLineItem.cs`
- `Dayswork.Core/Domain/PricingSnapshot.cs`
- `Dayswork.Core/Domain/ContractValidationCode.cs`
- `Dayswork.Core/Domain/ContractValidationIssue.cs`
- `Dayswork.Core/Domain/ContractPreview.cs`
- `Dayswork.Core/Domain/ContractTermsSnapshot.cs`
- `Dayswork.Core/Energy/WorkActionKind.cs`
- `Dayswork.Core/Energy/WorkerEnergyProfile.cs`
- `Dayswork.Core/Energy/IWorkerEnergyProfileBuilder.cs`
- `Dayswork.Core/Energy/WorkerEnergyProfileBuilder.cs`

### Core config and pricing pipeline
- `Dayswork.Core/Config/ResolvedIntValue.cs`
- `Dayswork.Core/Config/ConfigValueResolver.cs`
- `Dayswork.Core/Pricing/IWorkScopeClassifier.cs`
- `Dayswork.Core/Pricing/WorkScopeClassifier.cs`
- `Dayswork.Core/Pricing/IOutdoorServiceBandClassifier.cs`
- `Dayswork.Core/Pricing/OutdoorServiceBandClassifier.cs`
- `Dayswork.Core/Pricing/IContractPriceCalculator.cs`
- `Dayswork.Core/Pricing/ContractPriceCalculator.cs`
- `Dayswork.Core/Pricing/IPriceBreakdownBuilder.cs`
- `Dayswork.Core/Pricing/PriceBreakdownBuilder.cs`
- `Dayswork.Core/Pricing/IContractTermsBuilder.cs`
- `Dayswork.Core/Pricing/ContractTermsBuilder.cs`

### Integration bridge
- `Dayswork/Integration/ContractTermsConfigKeyCodec.cs`

### Tests
- `Dayswork.Tests/Generators/U18ContractTermsGen.cs`
- `Dayswork.Tests/Pricing/U18BuilderFactory.cs`
- `Dayswork.Tests/Config/ConfigValueResolverTests.cs`
- `Dayswork.Tests/Pricing/WorkScopeClassifierTests.cs`
- `Dayswork.Tests/Pricing/ContractTermsBuilderTests.cs`
- `Dayswork.Tests/Pricing/ContractTermsPropertyTests.cs`

---

## Modified Files

### Core model and config
- `Dayswork.Core/Domain/Contract.cs`
- `Dayswork.Core/Config/IConfigSnapshot.cs`
- `Dayswork.Core/Config/ConfigSnapshot.cs`
- `Dayswork.Core/Config/ConfigSnapshotFactory.cs`
- `Dayswork.Core/Config/ConfigDefaults.cs`

### Integration bridge
- `Dayswork/Integration/ModConfig.cs`
- `Dayswork/Integration/ModConfigManager.cs`
- `Dayswork/Integration/RuntimeConfigSnapshotMapper.cs`
- `Dayswork/ModEntry.cs`

### Shared generators and existing config tests
- `Dayswork.Tests/Generators/PricingGen.cs`
- `Dayswork.Tests/Generators/ConfigSnapshotGen.cs`
- `Dayswork.Tests/Persistence/Generators/ContractGen.cs`
- `Dayswork.Tests/Config/ConfigDefaultsTests.cs`
- `Dayswork.Tests/Config/ConfigSnapshotFactoryTests.cs`
- `Dayswork.Tests/Config/ConfigSnapshotGenSmokeTests.cs`
- `Dayswork.Tests/Config/RuntimeConfigSnapshotMapperTests.cs`

---

## Key Design Notes

### Legacy compatibility bridge

U-18 intentionally does **not** switch the historical consumers to the new pricing model yet.

The bridge strategy is:
- keep `RateCalculator`, `DepositCalculator`, `RefundCalculator`, `HoursEstimator`, and `DepositHoursPolicy` compiled
- keep the old U-05 config fields in `ConfigSnapshot` and `ModConfig`
- add the richer U-18 tables beside them
- extend `Contract` additively with optional `ScopeSelection` and `TermsSnapshot`

That means:
- U-20 can switch the hire preview/summary flow later
- U-23 can switch recurring billing/day-start logic later
- U-19 can change persistence separately without this unit taking serializer risk

### Outdoor normalization strategy

`WorkScopeClassifier` currently normalizes overlapping outdoor rectangles into deterministic non-overlapping single-tile `Zone` entries. That is sufficient for U-18 because the pricing foundation only needs a stable union footprint and tile count. Later runtime units still use raw scope selection and can choose different execution-oriented normalization if needed.

### Config fallback/logging boundary

`ConfigValueResolver` stays pure and only reports whether a default was used. Integration-side config normalization now logs compact maintainer-facing fallback codes through `ModConfigManager`, which satisfies the repository’s no-hardcoded-user-facing-strings lint rule while still surfacing fallback events.

---

## Test Surface Added

### Example coverage
- overlap normalization does not double-charge outdoor scope
- irrelevant scope families are omitted when their task family is disabled
- two deluxe coops aggregate into one animal pricing line with quantity `2`
- greenhouse water and harvest create separate package lines
- invalid drafts with zero chargeable scope-task pairs return structured invalid previews
- `ConfigValueResolver` falls back per key for missing/invalid data

### Property coverage
- preview determinism for identical selection/task/config inputs
- pricing subtotal and grand-total reconciliation
- overlap-equivalent outdoor selections price identically
- preview validity iff at least one chargeable scope-task pair exists
- valid terms snapshots preserve the full action-cost table

---

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false`
  - Result: success, `0` errors, `0` warnings
- `dotnet test Dayswork.sln`
  - Result: `231` passed, `1` expected skip, `0` failed

---

## Deliberate Deferrals

- **U-19**: persistence DTO/schema changes for `ContractTermsSnapshot` and legacy contract dropping
- **U-20**: hire-flow preview and summary switchover to `ContractTermsBuilder`
- **U-23**: recurring billing/day-start switchover to rebuilt fixed terms
- **U-24**: GMCM exposure and validation for the new pricing/energy config surface
