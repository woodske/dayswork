# U-06 Persistence Core — Code Summary

**Unit**: U-06 — Persistence Core  
**Status**: Complete  
**Build**: ✅ 0 errors, 0 warnings  
**Tests**: ✅ 30 new tests pass; 0 regressions

---

## Files created

### Dayswork.Core — Domain types (6 files)

| File | Type | Description |
|---|---|---|
| `Dayswork.Core/Domain/Season.cs` | `enum` | Core-side Season equivalent (no SMAPI ref) |
| `Dayswork.Core/Domain/GameDate.cs` | `readonly record struct` | (Day, Season, Year) |
| `Dayswork.Core/Domain/ContractId.cs` | `readonly record struct` | Guid wrapper with `New()` factory |
| `Dayswork.Core/Domain/ContractStatus.cs` | `enum` | Active, Paused, Cancelled |
| `Dayswork.Core/Domain/ContractSchedule.cs` | `enum` | OneTime, Recurring |
| `Dayswork.Core/Domain/Contract.cs` | `sealed record` | Full contract entity (9 properties) |

### Dayswork.Core — Persistence interfaces + implementations (4 files)

| File | Description |
|---|---|
| `Dayswork.Core/Persistence/IContractStore.cs` | CRUD + status-transition + Hydrate interface |
| `Dayswork.Core/Persistence/ISaveDataSerializer.cs` | Serialize/Deserialize interface |
| `Dayswork.Core/Persistence/ContractStore.cs` | In-memory implementation; `ListActiveForDate` stubbed (U-09) |
| `Dayswork.Core/Persistence/SaveDataSerializer.cs` | Newtonsoft.Json round-trip; 4-level guard + per-record exception barrier |

### Dayswork.Core — DTOs (5 files)

| File | Description |
|---|---|
| `Dayswork.Core/Persistence/Dto/DaysworkSaveDataV1.cs` | Top-level envelope (SchemaVersion, ModVersion, Contracts) |
| `Dayswork.Core/Persistence/Dto/ContractDtoV1.cs` | Contract DTO |
| `Dayswork.Core/Persistence/Dto/ZoneDtoV1.cs` | Zone DTO (LocationName, TopLeft/BottomRight coords) |
| `Dayswork.Core/Persistence/Dto/DestinationDtoV1.cs` | Flat DTO with `Type` discriminator + nullable coord fields |
| `Dayswork.Core/Persistence/Dto/GameDateDtoV1.cs` | Date DTO (Day, Season string, Year) |

### Dayswork.Tests (3 files)

| File | Tests | Description |
|---|---|---|
| `Dayswork.Tests/Persistence/ContractStoreTests.cs` | 19 `[Fact]` | All CRUD + status transitions + Hydrate + stub check |
| `Dayswork.Tests/Persistence/SaveDataSerializerTests.cs` | 10 `[Fact]` + 1 `[Property]` | NFR-SAFE-03 edge cases + PBT-02 round-trip |
| `Dayswork.Tests/Persistence/Generators/ContractGen.cs` | N/A | FsCheck `Arbitrary<Contract>` generator (PBT-07) |

---

## Test results

| Category | Count | Result |
|---|---|---|
| ContractStore [Fact] | 19 | ✅ All pass |
| SaveDataSerializer [Fact] | 10 | ✅ All pass |
| SaveDataSerializer [Property] PBT-02 (1000 inputs) | 1 | ✅ Passes |
| Prior suite (U-02–U-05) | 70 | ✅ No regressions |
| **Total new** | **30** | ✅ |

---

## NFR compliance

| NFR | Status | Evidence |
|---|---|---|
| NFR-SAFE-03 | ✅ Compliant | null/empty/invalid JSON/null envelope/future schema all return `[]` with warnings |
| NFR-MAINT-03 | ✅ Compliant | No `using StardewValley` or `using StardewModdingAPI` in `Dayswork.Core/Persistence/` |
| PBT-02 | ✅ Compliant | `RoundTrip_DeserializeSerialize_IsIdentity` runs 1000 FsCheck inputs; 0 failures |
| PBT-07 | ✅ Compliant | `ContractGen.Contract()` returns `Arbitrary<Contract>`; downstream units can compose |

---

## Key design decisions

- **`Action<string> logWarning` delegate**: SMAPI-free warning injection; tests capture to `List<string>`; U-09 will pass `msg => Monitor.Log(msg, LogLevel.Warn)`
- **Type-tag DTO pattern**: `DestinationDtoV1.Type` discriminator resolved by `switch` in `MapDestinationToDomain`; no custom `JsonConverter`
- **Explicit enum mapping**: `status.ToString()` / `Enum.Parse<ContractStatus>(dto.Status)` — no `StringEnumConverter`
- **Atomic Hydrate**: `_contracts.Clear()` before inserts; duplicate IDs skip with warning
- **`ListActiveForDate` stub**: Throws `NotImplementedException` with message pointing to U-09
- **C# 10 compatibility**: Used `new()` initializers and `Array.Empty<T>()` instead of `[]`; LINQ query syntax instead of `Gen.zip*`
