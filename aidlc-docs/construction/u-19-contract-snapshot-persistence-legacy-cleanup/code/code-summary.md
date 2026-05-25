# U-19 — Contract Snapshot Persistence + Legacy Cleanup: Code Summary

## Outcome

U-19 retrofits the persistence layer to the redesign-era contract model without forcing the rest of the runtime to switch over yet.

The generated code now provides:
- a schema-v2 save envelope for authoritative typed scope and `ContractTermsSnapshot`
- a narrow `ReplaceTermsSnapshot(...)` store seam for recurring repricing persistence
- serializer-owned version gating, deterministic ordering, and per-contract malformed-entry isolation
- a schema-agnostic SMAPI save bridge that no longer knows about `DaysworkSaveDataV1`
- dedicated v2 persistence example/property coverage in `Dayswork.Tests`

This lets later units rewire hiring and recurring runtime consumers at their own pace while persistence already speaks the new contract shape.

---

## Created Files

### Schema-v2 DTO layer
- `Dayswork.Core/Persistence/Dto/AnimalBuildingSelectionDto.cs`
- `Dayswork.Core/Persistence/Dto/ContractDtoV2.cs`
- `Dayswork.Core/Persistence/Dto/ContractScopeSelectionDto.cs`
- `Dayswork.Core/Persistence/Dto/ContractTermsSnapshotDto.cs`
- `Dayswork.Core/Persistence/Dto/DaysworkSaveDataV2.cs`
- `Dayswork.Core/Persistence/Dto/GreenhouseSelectionDto.cs`
- `Dayswork.Core/Persistence/Dto/LegacyFinancialBridgeDto.cs`
- `Dayswork.Core/Persistence/Dto/PricingLineItemDto.cs`
- `Dayswork.Core/Persistence/Dto/PricingSnapshotDto.cs`
- `Dayswork.Core/Persistence/Dto/WorkerEnergyProfileDto.cs`

### Persistence test support
- `Dayswork.Tests/Persistence/ContractStructuralComparer.cs`
- `Dayswork.Tests/Persistence/Generators/U19PersistenceGen.cs`
- `Dayswork.Tests/Persistence/SaveDataSerializerPropertyTests.cs`

---

## Modified Files

### Core persistence seams
- `Dayswork.Core/Persistence/IContractStore.cs`
- `Dayswork.Core/Persistence/ContractStore.cs`
- `Dayswork.Core/Persistence/SaveDataSerializer.cs`

### Integration bridge
- `Dayswork/Integration/ContractPersistenceAdapter.cs`

### Persistence regression suite
- `Dayswork.Tests/Persistence/ContractStoreTests.cs`
- `Dayswork.Tests/Persistence/ContractStoreStateTests.cs`
- `Dayswork.Tests/Persistence/SaveDataSerializerTests.cs`

---

## Key Design Notes

### Schema v2 is now the only write path

`SaveDataSerializer.Serialize(...)` always emits `DaysworkSaveDataV2`.

The current saved contract shape now persists:
- common contract metadata
- authoritative `ScopeSelection`
- authoritative `TermsSnapshot`
- temporary `LegacyFinancialBridge` (`DepositAmount`, `HourlyRate`)

Schema v1 remains only as a legacy-read branch so the serializer can detect and drop old pre-release hourly save data.

### Version branching and malformed isolation moved fully into the serializer

`SaveDataSerializer.Deserialize(...)` now owns:
- invalid/null payload handling
- explicit schema-v1 drop
- future-schema rejection
- per-contract try/catch isolation for schema-v2 entries
- local compatibility projection back into bridge-domain `Contract` fields

`ContractPersistenceAdapter` now hands raw `JToken` payloads to the serializer instead of prebinding to `DaysworkSaveDataV1`.

### Transitional bootstrap for pre-U20 in-memory contracts

The current hire flow still creates contracts without `ScopeSelection` or `TermsSnapshot`.

To avoid breaking the playable flow mid-retrofit, the serializer has a temporary bootstrap path on **save**:
- derive a best-effort typed scope from legacy `Zones`
- synthesize a fallback terms snapshot when one is still missing

That bootstrap is deliberately local to persistence and exists only until later units start creating fully redesign-native contracts at the source.

### Narrow recurring mutation seam

`ContractStore` now supports:
- existing whole-contract `Update(...)`
- narrow `ReplaceTermsSnapshot(...)`

The new seam updates only `TermsSnapshot` and preserves all other contract data, which gives U-23 a clean persistence hook for recurring repricing.

---

## Test Surface Added

### Example coverage
- schema-v1 envelopes drop with a maintainer-facing warning
- future-schema envelopes reject cleanly
- malformed schema-v2 entries are skipped without poisoning valid siblings
- current-schema contracts round-trip with compatibility projections intact
- serialized v2 payloads include authoritative scope, terms, and bridge fields
- `ReplaceTermsSnapshot(...)` updates only the terms snapshot and throws for unknown ids

### Property coverage
- valid current-schema contracts round-trip structurally through v2 serialization/deserialization
- repeated serialization is deterministic
- contract ordering in the input list does not change serialized output
- malformed v2 siblings do not poison a valid contract
- `ReplaceTermsSnapshot(...)` preserves all non-terms fields

---

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false`
  - Result: success, `0` errors, `0` warnings
- `dotnet test Dayswork.sln`
  - Result: `238` passed, `1` expected skip, `0` failed

---

## Deliberate Deferrals

- **U-20**: hire-flow preview/confirmation must start creating contracts with authoritative `ScopeSelection` and `ContractTermsSnapshot` directly
- **U-23**: recurring scheduler/runtime still consume compatibility `DepositAmount` / `HourlyRate` and will switch later
- **Later cleanup**: the temporary save-time bootstrap for missing scope/terms can be removed once no caller creates legacy-shaped in-memory contracts anymore
