# U-06 Persistence Core — Code Generation Plan

**Unit**: U-06 — Persistence Core  
**Stage**: Code Generation  
**Status**: Part 2 — complete

---

## Unit context

**Stories implemented**: Foundation for S-05 (contracts survive save/load); S-19 (PBT-02 primary obligation — SaveDataSerializer round-trip)  
**Dependencies**: U-03 (`TaskKind`), U-04 (`Zone`, `TileCoord`, `ChestRef`, `DestinationKey`)  
**Consumers (wired in later units)**:
- U-09 `ContractPersistenceAdapter` — calls `Hydrate`, `Serialize`, `Deserialize`
- U-09 `HiringFlowCoordinator` — calls `IContractStore.Add`
- U-10 `ShiftOrchestrator` — calls `IContractStore.Get`
- U-12 `ContractListMenu` — calls `IContractStore.Pause`, `Resume`, `Cancel`
- U-15 `RecurringContractScheduler` — calls `IContractStore.ListActiveForDate` (stubbed here)

---

## Code locations

- **Domain types**: `C:\Users\kwood\Repos\dayswork\Dayswork.Core\Domain\` (6 new files)
- **Persistence interfaces + impls**: `C:\Users\kwood\Repos\dayswork\Dayswork.Core\Persistence\` (4 new files)
- **DTO types**: `C:\Users\kwood\Repos\dayswork\Dayswork.Core\Persistence\Dto\` (5 new files)
- **Tests**: `C:\Users\kwood\Repos\dayswork\Dayswork.Tests\Persistence\` (2 new test files)
- **Generator**: `C:\Users\kwood\Repos\dayswork\Dayswork.Tests\Persistence\Generators\ContractGen.cs` (1 new file)
- **Summary doc**: `C:\Users\kwood\Repos\dayswork\aidlc-docs\construction\U-06-persistence-core\code\u-06-code-summary.md`

---

## Steps

### Step 1 — `Season.cs`
- [x] Create `Dayswork.Core/Domain/Season.cs`
- Namespace: `Dayswork.Core.Domain`
- `public enum Season { Spring, Summer, Fall, Winter }`
- Core-side equivalent of SMAPI's `Season` — no SMAPI reference allowed in Core

### Step 2 — `GameDate.cs`
- [x] Create `Dayswork.Core/Domain/GameDate.cs`
- Namespace: `Dayswork.Core.Domain`
- `public readonly record struct GameDate(int Day, Season Season, int Year)`
- No validation — caller's responsibility; Day range [1,28] documented in business rules

### Step 3 — `ContractId.cs`
- [x] Create `Dayswork.Core/Domain/ContractId.cs`
- Namespace: `Dayswork.Core.Domain`
- `public readonly record struct ContractId(Guid Value)` with `static ContractId New() => new(Guid.NewGuid())` and `ToString()` override returning `Value.ToString()`

### Step 4 — `ContractStatus.cs`
- [x] Create `Dayswork.Core/Domain/ContractStatus.cs`
- Namespace: `Dayswork.Core.Domain`
- `public enum ContractStatus { Active, Paused, Cancelled }`

### Step 5 — `ContractSchedule.cs`
- [x] Create `Dayswork.Core/Domain/ContractSchedule.cs`
- Namespace: `Dayswork.Core.Domain`
- `public enum ContractSchedule { OneTime, Recurring }`

### Step 6 — `Contract.cs`
- [x] Create `Dayswork.Core/Domain/Contract.cs`
- Namespace: `Dayswork.Core.Domain`
- `public sealed record Contract(ContractId Id, IReadOnlySet<TaskKind> EnabledTasks, IReadOnlyList<Zone> Zones, IReadOnlyDictionary<TaskKind, DestinationKey> TaskDestinations, ContractSchedule Schedule, ContractStatus Status, GameDate HireDate, int DepositAmount, int HourlyRate)`
- Usings: `Dayswork.Core.Domain` only — no SMAPI, no Newtonsoft

### Step 7 — `IContractStore.cs`
- [x] Create `Dayswork.Core/Persistence/IContractStore.cs`
- Namespace: `Dayswork.Core.Persistence`
- Interface with all methods from Application Design + `Hydrate`:
  ```csharp
  ContractId Add(Contract contract);
  Contract Get(ContractId id);
  void Update(ContractId id, Contract updated);
  void Cancel(ContractId id);
  void Pause(ContractId id);
  void Resume(ContractId id);
  IReadOnlyList<Contract> List();
  IReadOnlyList<Contract> ListActiveForDate(int day, Season season, int year);
  void Hydrate(IReadOnlyList<Contract> contracts);
  ```

### Step 8 — `ISaveDataSerializer.cs`
- [x] Create `Dayswork.Core/Persistence/ISaveDataSerializer.cs`
- Namespace: `Dayswork.Core.Persistence`
- Interface:
  ```csharp
  string Serialize(IReadOnlyList<Contract> contracts, string modVersion);
  IReadOnlyList<Contract> Deserialize(string? json);
  ```
- Note: `Serialize` signature extends the Application Design definition with `modVersion` parameter (Q6-C decision — `DaysworkSaveDataV1` stores `ModVersion`)

### Step 9 — DTO types (5 files)
- [x] Create `Dayswork.Core/Persistence/Dto/GameDateDtoV1.cs`
  - `public sealed class GameDateDtoV1 { public int Day { get; set; } public string Season { get; set; } = ""; public int Year { get; set; } }`
- [x] Create `Dayswork.Core/Persistence/Dto/DestinationDtoV1.cs`
  - `public sealed class DestinationDtoV1 { public string Type { get; set; } = ""; public string? LocationName { get; set; } public int? X { get; set; } public int? Y { get; set; } }`
- [x] Create `Dayswork.Core/Persistence/Dto/ZoneDtoV1.cs`
  - `public sealed class ZoneDtoV1 { public string LocationName { get; set; } = ""; public int TopLeftX, TopLeftY, BottomRightX, BottomRightY { get; set; } }`
- [x] Create `Dayswork.Core/Persistence/Dto/ContractDtoV1.cs`
  - Fields: `string Id`, `List<string> EnabledTasks`, `List<ZoneDtoV1> Zones`, `Dictionary<string, DestinationDtoV1> TaskDestinations`, `string Schedule`, `string Status`, `GameDateDtoV1 HireDate`, `int DepositAmount`, `int HourlyRate`
  - All list/dict fields initialised to empty collections in property declarations
- [x] Create `Dayswork.Core/Persistence/Dto/DaysworkSaveDataV1.cs`
  - `public sealed class DaysworkSaveDataV1 { public int SchemaVersion { get; set; } = 1; public string ModVersion { get; set; } = ""; public List<ContractDtoV1> Contracts { get; set; } = []; }`
- Namespace for all: `Dayswork.Core.Persistence.Dto`

### Step 10 — `ContractStore.cs`
- [x] Create `Dayswork.Core/Persistence/ContractStore.cs`
- Namespace: `Dayswork.Core.Persistence`; implements `IContractStore`
- Internal state: `private readonly Dictionary<ContractId, Contract> _contracts = new()`
- `Add`: duplicate-Id check → throw `InvalidOperationException`; store + return Id
- `Get`: `TryGetValue` → return or throw `KeyNotFoundException`
- `Update`: existence check + Id-match check → `_contracts[id] = updated`
- `Cancel`: existence check → status != Cancelled guard → `with { Status = Cancelled }`
- `Pause`: existence check → Cancelled/Paused guards → `with { Status = Paused }`
- `Resume`: existence check → Cancelled/Active guards → `with { Status = Active }`
- `List`: `_contracts.Values.ToList().AsReadOnly()`
- `ListActiveForDate`: `throw new NotImplementedException("ListActiveForDate is implemented in U-09")`
- `Hydrate`: `_contracts.Clear()`; loop → duplicate-Id skip with warn; insert

### Step 11 — `SaveDataSerializer.cs`
- [x] Create `Dayswork.Core/Persistence/SaveDataSerializer.cs`
- Namespace: `Dayswork.Core.Persistence`; implements `ISaveDataSerializer`
- Private static `_serializerSettings`: `Formatting.Indented`, `NullValueHandling.Ignore`
- `Serialize`: maps `IReadOnlyList<Contract>` → `List<ContractDtoV1>` via `MapDomainToDto`; wraps in `DaysworkSaveDataV1 { SchemaVersion=1, ModVersion=modVersion, Contracts=dtos }`; returns `JsonConvert.SerializeObject`
- `Deserialize`: Guard 1 (null/empty → return []); `JsonConvert.DeserializeObject<DaysworkSaveDataV1>` in try/catch (bad JSON → log warn + return []); Guard 2 (null envelope → log warn + return []); Guard 3 (SchemaVersion > 1 → log warn + return []); per-contract try/catch loop calling `MapDtoToDomain`; return `results.AsReadOnly()`
- Private mapping methods: `MapDomainToDto(Contract)`, `MapDtoToDomain(ContractDtoV1)`, `MapDestination(DestinationDtoV1)`, `MapZone(Zone)`, `MapZone(ZoneDtoV1)`, `MapDate(GameDate)`, `MapDate(GameDateDtoV1)`
- `MapDestination` switch: `"Chest"` → `ChestDestination`, `"ShippingBin"` → singleton, `"Mail"` → singleton, `_` → `throw new JsonException`
- Enum strings: `status.ToString()` / `Enum.Parse<ContractStatus>(dto.Status)` — no `StringEnumConverter`
- **SMAPI Monitor**: `SaveDataSerializer` must NOT take `IMonitor` as a constructor parameter (Core has no SMAPI dependency). Logging is handled via a `ILogger` abstraction OR warnings are embedded as thrown exceptions caught by the caller. Decision: use a `Action<string> logWarning` delegate injected via constructor — allows tests to capture warnings without SMAPI; U-09 adapter passes `(msg) => Monitor.Log(msg, LogLevel.Warn)`.

### Step 12 — `ContractGen.cs` (FsCheck generator, PBT-07)
- [x] Create `Dayswork.Tests/Persistence/Generators/ContractGen.cs`
- Namespace: `Dayswork.Tests.Generators`; static class
- `Arbitrary<Contract> Contract()` — composes all field generators per logical-components.md spec
- Output-producing TaskKinds for `TaskDestinations` keys: `HarvestCrops`, `CollectFruit`, `CollectAnimalProducts`, `CutTrees`, `ClearRocks`, `ClearWeeds`
- Destination values: randomly one of `new ChestDestination(new ChestRef(randomLocation, new TileCoord(x, y)))`, `ShippingBinDestination.Instance`, `MailDestination.Instance`
- Registers as `Arb.Register<ContractGen>()` in the test assembly's `FsCheckConfig` (or via `[assembly: Properties]` attribute matching U-02 convention)

### Step 13 — `ContractStoreTests.cs`
- [x] Create `Dayswork.Tests/Persistence/ContractStoreTests.cs`
- Namespace: `Dayswork.Tests.Persistence`
- **[Fact] tests**:
  1. `Add_StoresContract_GetReturnsIt`
  2. `Add_DuplicateId_Throws`
  3. `Get_UnknownId_Throws`
  4. `Update_ReplacesContract`
  5. `Update_IdMismatch_Throws`
  6. `Cancel_Active_SetsStatusCancelled`
  7. `Cancel_Paused_SetsStatusCancelled`
  8. `Cancel_AlreadyCancelled_Throws`
  9. `Pause_Active_SetsStatusPaused`
  10. `Pause_AlreadyPaused_Throws`
  11. `Pause_Cancelled_Throws`
  12. `Resume_Paused_SetsStatusActive`
  13. `Resume_AlreadyActive_Throws`
  14. `Resume_Cancelled_Throws`
  15. `List_ReturnsAllContracts`
  16. `Hydrate_ReplacesExistingContracts_Atomically`
  17. `Hydrate_DuplicateId_SkipsSecondAndWarns`
  18. `ListActiveForDate_ThrowsNotImplementedException` (documents the stub)
- Helper: `MakeContract(ContractStatus status = Active)` — builds a minimal valid `Contract` with `ContractId.New()`

### Step 14 — `SaveDataSerializerTests.cs`
- [x] Create `Dayswork.Tests/Persistence/SaveDataSerializerTests.cs`
- Namespace: `Dayswork.Tests.Persistence`
- **[Fact] tests** (NFR-SAFE-03 edge cases):
  1. `Deserialize_Null_ReturnsEmpty`
  2. `Deserialize_EmptyString_ReturnsEmpty`
  3. `Deserialize_InvalidJson_ReturnsEmptyAndWarns`
  4. `Deserialize_NullEnvelope_ReturnsEmptyAndWarns`
  5. `Deserialize_FutureSchemaVersion_ReturnsEmptyAndWarns`
  6. `Deserialize_MalformedContract_SkipsItAndWarns`
  7. `Deserialize_MalformedContractAmongValid_ReturnsOnlyValidOnes`
  8. `Deserialize_UnknownDestinationType_SkipsContractAndWarns`
  9. `Serialize_ProducesValidJson_ContainingSchemaVersion1`
  10. `Serialize_ModVersionPresentInOutput`
- **[Property] test** (PBT-02 primary obligation):
  1. `RoundTrip_DeserializeSerialize_IsIdentity` — `Deserialize(Serialize(contracts, "0.1.0")) == contracts` using `ContractGen.Contract()`, MaxTest=1000, verifies structural equality

### Step 15 — Build verification
- [x] Run `dotnet build Dayswork.Core\Dayswork.Core.csproj` — expect 0 errors, 0 warnings
- [x] Run `dotnet build Dayswork.Tests\Dayswork.Tests.csproj` — expect 0 errors, 0 warnings
- [x] Verify `Dayswork.Core/Persistence/` contains no `using StardewValley` or `using StardewModdingAPI` imports

### Step 16 — Test execution
- [x] Run `dotnet test Dayswork.Tests\Dayswork.Tests.csproj`
- [x] Confirm all new U-06 tests pass: 18 `[Fact]` (ContractStore) + 10 `[Fact]` + 1 `[Property]` (Serializer) = 29 new tests
- [x] Confirm prior U-02/U-03/U-04/U-05 tests still pass (no regressions)
- [x] Confirm PBT-02 round-trip property runs with MaxTest=1000 inputs

### Step 17 — Code summary
- [x] Create `aidlc-docs/construction/U-06-persistence-core/code/u-06-code-summary.md`
- Record all files created, test counts, PBT-02 compliance, build results

---

## Story traceability

| Story | Delivered by | Step(s) |
|---|---|---|
| S-05 (contracts survive save/load) — foundation | `IContractStore`, `ISaveDataSerializer` interfaces + impls | Steps 7–11 |
| S-19 (PBT-02 — round-trip fidelity) | `SaveDataSerializerTests` `[Property]` + `ContractGen` | Steps 12, 14 |
