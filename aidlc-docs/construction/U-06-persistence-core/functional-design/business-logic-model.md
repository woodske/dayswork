# U-06 Persistence Core — Business Logic Model

---

## ContractStore (C-12)

### Internal state

```
_contracts : Dictionary<ContractId, Contract>
```

A single private `Dictionary` keyed by `ContractId`. All public methods operate on this dictionary. No other mutable state.

---

### `ContractId Add(Contract contract)`

**Purpose**: Register a new contract in the store.

**Preconditions**:
- `contract.Id` must not already exist in `_contracts`.

**Logic**:
1. If `_contracts.ContainsKey(contract.Id)` → throw `InvalidOperationException("Duplicate ContractId")`.
2. `_contracts[contract.Id] = contract`.
3. Return `contract.Id`.

**Postcondition**: `Get(contract.Id)` returns `contract`.

---

### `Contract Get(ContractId id)`

**Logic**:
1. If `_contracts.TryGetValue(id, out var contract)` → return `contract`.
2. Else throw `KeyNotFoundException($"No contract with id {id}")`.

---

### `void Update(ContractId id, Contract updated)`

**Purpose**: Replace the entire contract record (e.g., after the player edits zones or tasks in U-12's Edit flow).

**Preconditions**:
- `id` must exist in `_contracts`.
- `updated.Id` must equal `id` (prevents Id drift on the updated record).

**Logic**:
1. If `!_contracts.ContainsKey(id)` → throw `KeyNotFoundException`.
2. If `updated.Id != id` → throw `ArgumentException("Contract Id mismatch")`.
3. `_contracts[id] = updated`.

---

### `void Cancel(ContractId id)`

**Preconditions**: `id` exists.

**Logic**:
1. `var existing = _contracts[id]` (throws `KeyNotFoundException` if absent).
2. If `existing.Status == ContractStatus.Cancelled` → throw `InvalidOperationException("Already cancelled")`.
3. `_contracts[id] = existing with { Status = ContractStatus.Cancelled }`.

---

### `void Pause(ContractId id)`

**Preconditions**: `id` exists; status is `Active`.

**Logic**:
1. `var existing = _contracts[id]`.
2. If `existing.Status == ContractStatus.Cancelled` → throw `InvalidOperationException("Cannot pause a cancelled contract")`.
3. If `existing.Status == ContractStatus.Paused` → throw `InvalidOperationException("Already paused")`.
4. `_contracts[id] = existing with { Status = ContractStatus.Paused }`.

*Note*: Method shell is created here; it is first called by U-12's `ContractListMenu`.

---

### `void Resume(ContractId id)`

**Preconditions**: `id` exists; status is `Paused`.

**Logic**:
1. `var existing = _contracts[id]`.
2. If `existing.Status == ContractStatus.Cancelled` → throw `InvalidOperationException("Cannot resume a cancelled contract")`.
3. If `existing.Status == ContractStatus.Active` → throw `InvalidOperationException("Already active")`.
4. `_contracts[id] = existing with { Status = ContractStatus.Active }`.

*Note*: Method shell is created here; it is first called by U-12's `ContractListMenu`.

---

### `IReadOnlyList<Contract> List()`

**Logic**: Return `_contracts.Values.ToList().AsReadOnly()`.

Order is unspecified (dictionary enumeration order). Callers that require a specific order (e.g., bulletin board display in U-12) sort by `HireDate` themselves.

---

### `IReadOnlyList<Contract> ListActiveForDate(int day, Season season, int year)`

**Stubbed in U-06** — throws `NotImplementedException("ListActiveForDate is implemented in U-09")`.

The interface signature is locked here. Full implementation lands in U-09 when `RecurringContractScheduler` (the first caller) is wired.

---

### `void Hydrate(IReadOnlyList<Contract> contracts)`

**Purpose**: Atomically replace all stored contracts with the deserialized list from the save file.

**Called by**: `ContractPersistenceAdapter` (U-09) on the SMAPI `GameLoop.SaveLoaded` event.

**Logic**:
1. `_contracts.Clear()`.
2. For each `contract` in `contracts`:
   - If `_contracts.ContainsKey(contract.Id)` → log a warning and skip (duplicate Id in save data; should not happen but tolerated per Q9-A semantics).
   - Else `_contracts[contract.Id] = contract`.

**Atomicity guarantee**: The dictionary is cleared before any inserts. If the caller passes an empty list (e.g., save has no contracts), the store ends up empty — never partially populated.

---

## SaveDataSerializer (C-13)

### `string Serialize(IReadOnlyList<Contract> contracts, string modVersion)`

**Purpose**: Convert the current contract list to a JSON string for storage via `Helper.Data.WriteSaveData`.

**Note on signature**: The Application Design signature is `string Serialize(IReadOnlyList<Contract> contracts)`. The `modVersion` parameter is added here (Q6-C decision — `DaysworkSaveDataV1` stores `ModVersion`). The adapter (U-09) passes `Helper.Manifest.Version.ToString()`.

**Logic**:
1. Map each `Contract` → `ContractDtoV1` (see mapping rules below).
2. Construct `DaysworkSaveDataV1 { SchemaVersion = 1, ModVersion = modVersion, Contracts = dtos }`.
3. Return `JsonConvert.SerializeObject(envelope, _serializerSettings)`.

---

### `IReadOnlyList<Contract> Deserialize(string? json)`

**Purpose**: Parse JSON from `Helper.Data.ReadSaveData` back into domain `Contract` objects.

**Logic**:

```
if json is null or empty:
    return empty list  ← NFR-SAFE-03: missing segment = empty store

envelope = JsonConvert.DeserializeObject<DaysworkSaveDataV1>(json)

if envelope is null:
    log warning: "Dayswork save data could not be parsed — starting fresh"
    return empty list

if envelope.SchemaVersion > 1:
    log warning: "Save data schema version {x} is newer than this mod version supports (v1). Contracts not loaded."
    return empty list

results = new List<Contract>()
for each dto in envelope.Contracts:
    try:
        contract = MapDtoToDomain(dto)
        results.Add(contract)
    catch Exception ex:
        log warning: "Skipping contract {dto.Id}: {ex.Message}"   ← Q9-A: skip malformed, warn

return results.AsReadOnly()
```

---

### DTO ↔ Domain mapping rules

#### `Contract` → `ContractDtoV1`

| Contract field | ContractDtoV1 field | Mapping |
|---|---|---|
| `Id.Value` | `Id` | `Guid.ToString()` (lowercase) |
| `EnabledTasks` | `EnabledTasks` | `taskKind.ToString()` per element |
| `Zones` | `Zones` | Each `Zone` → `ZoneDtoV1` (see below) |
| `TaskDestinations` | `TaskDestinations` | `taskKind.ToString()` → `DestinationDtoV1` (see below) |
| `Schedule` | `Schedule` | `schedule.ToString()` (`"OneTime"` or `"Recurring"`) |
| `Status` | `Status` | `status.ToString()` (`"Active"`, `"Paused"`, `"Cancelled"`) |
| `HireDate` | `HireDate` | `GameDate` → `GameDateDtoV1` (see below) |
| `DepositAmount` | `DepositAmount` | Direct `int` |
| `HourlyRate` | `HourlyRate` | Direct `int` |

#### `Zone` → `ZoneDtoV1`

| Zone field | ZoneDtoV1 field |
|---|---|
| `LocationName` | `LocationName` |
| `TopLeft.X` | `TopLeftX` |
| `TopLeft.Y` | `TopLeftY` |
| `BottomRight.X` | `BottomRightX` |
| `BottomRight.Y` | `BottomRightY` |

#### `DestinationKey` → `DestinationDtoV1`

| DestinationKey subtype | `Type` | `LocationName` | `X` | `Y` |
|---|---|---|---|---|
| `ChestDestination(ref)` | `"Chest"` | `ref.LocationName` | `ref.Tile.X` | `ref.Tile.Y` |
| `ShippingBinDestination` | `"ShippingBin"` | `null` | `null` | `null` |
| `MailDestination` | `"Mail"` | `null` | `null` | `null` |

#### `DestinationDtoV1` → `DestinationKey`

| `Type` field | Result |
|---|---|
| `"Chest"` | `new ChestDestination(new ChestRef(LocationName!, new TileCoord(X!.Value, Y!.Value)))` |
| `"ShippingBin"` | `ShippingBinDestination.Instance` |
| `"Mail"` | `MailDestination.Instance` |
| *(anything else)* | throw `JsonException($"Unknown destination type: {dto.Type}")` ← caught by per-contract try/catch |

#### `GameDate` → `GameDateDtoV1`

| GameDate field | GameDateDtoV1 field | Mapping |
|---|---|---|
| `Day` | `Day` | Direct `int` |
| `Season` | `Season` | `season.ToString()` (`"Spring"`, etc.) |
| `Year` | `Year` | Direct `int` |

Reverse: `Enum.Parse<Season>(dto.Season)`.

---

## Serializer settings

`SaveDataSerializer` holds a private `JsonSerializerSettings _serializerSettings` configured with:
- `Formatting.Indented` — human-readable save files (easier to inspect/debug).
- `NullValueHandling.Ignore` — omits null fields (e.g., `LocationName`, `X`, `Y` for non-Chest destinations).
- No type-name handling — all polymorphism is handled by the explicit DTO mapping, not Newtonsoft's `$type` mechanism.

---

## FsCheck generator (`ContractGen`)

**File**: `Dayswork.Tests/Persistence/Generators/ContractGen.cs`

The generator composes from prior-unit generators:

```
ContractGen.Arbitrary() : Arbitrary<Contract>
    ContractId    : Arbitrary.Generate<Guid>().Select(g => new ContractId(g))
    EnabledTasks  : NonEmptySet of TaskKind values (from Gen.Elements + Gen.ListOf)
    Zones         : NonEmptyList of Zone (from ZoneGen — U-04 generator)
    TaskDestinations : Gen for Dictionary<TaskKind, DestinationKey>
                       keys: subset of output-producing TaskKinds only
                       values: one of ChestDestination(arbitrary ChestRef) | ShippingBinDestination | MailDestination
    Schedule      : Gen.Elements(ContractSchedule.OneTime, ContractSchedule.Recurring)
    Status        : Gen.Elements(ContractStatus.Active, ContractStatus.Paused, ContractStatus.Cancelled)
    HireDate      : Gen for GameDate (Day 1–28, random Season, Year 1–10)
    DepositAmount : Gen.Choose(0, 10000)
    HourlyRate    : Gen.Choose(50, 500)
```

This generator is used by:
- `SaveDataSerializerTests` PBT-02 round-trip property
- Future units' PBTs that take a `Contract` as input (U-09 persistence adapter tests, U-12 store state tests)
