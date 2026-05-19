# U-06 Persistence Core — Tech Stack Decisions

---

## JSON serialization library

**Decision**: Newtonsoft.Json  
**Version**: Transitive dependency via SMAPI; explicit `PackageReference` already declared in `Dayswork.Core.csproj` per U-01 unit definition.  
**Rationale**: SMAPI bundles Newtonsoft.Json and all SMAPI mod assemblies share this version. Adding an explicit reference in `Dayswork.Core.csproj` ensures the compiler resolves the correct version without conflict. No new NuGet package is needed — this was established at U-01.

---

## Enum serialization strategy

**Decision**: Explicit `ToString()` / `Enum.Parse<T>()` in DTO mapping code (Q1-B)  
**Rationale**: The DTO classes (`ContractDtoV1`, `GameDateDtoV1`, `DestinationDtoV1`) use plain `string` fields for all enum values. Newtonsoft.Json never encounters a C# enum type during serialization — it only sees strings. Conversion happens explicitly in `SaveDataSerializer`'s mapping methods:

```csharp
// Serialize direction:
dto.Status = contract.Status.ToString();          // "Active", "Paused", "Cancelled"
dto.Schedule = contract.Schedule.ToString();      // "OneTime", "Recurring"
dto.HireDate.Season = contract.HireDate.Season.ToString(); // "Spring", etc.

// Deserialize direction:
var status = Enum.Parse<ContractStatus>(dto.Status);
var schedule = Enum.Parse<ContractSchedule>(dto.Schedule);
var season = Enum.Parse<Season>(dto.HireDate.Season);
```

**Forward-compatibility**: If a future mod version adds a new `ContractStatus` value, old versions reading the save file will throw inside the per-contract try/catch block, skip that contract with a warning, and continue — consistent with NFR-SAFE-03 and Q9-A behavior.

**No `StringEnumConverter` needed**: `_serializerSettings` does not include `StringEnumConverter`. Simpler, explicit, and easier to trace in a debugger.

---

## `JsonSerializerSettings` configuration

```csharp
private static readonly JsonSerializerSettings _serializerSettings = new()
{
    Formatting = Formatting.Indented,
    NullValueHandling = NullValueHandling.Ignore,
};
```

| Setting | Value | Rationale |
|---|---|---|
| `Formatting` | `Indented` | Human-readable save files; easier to inspect when debugging |
| `NullValueHandling` | `Ignore` | Omits null fields — `LocationName`, `X`, `Y` are absent for `ShippingBin` and `Mail` destinations, keeping the JSON clean |

No `TypeNameHandling` — all polymorphism is handled by the explicit `"Type"` discriminator field in `DestinationDtoV1`, not by Newtonsoft's `$type` mechanism.

---

## `ContractStore` internal data structure

**Decision**: `Dictionary<ContractId, Contract>`  
**Rationale**: O(1) lookup by Id for `Get`, `Update`, `Cancel`, `Pause`, `Resume`. `List()` enumerates `Values` — O(n). Sufficient for the expected contract count (≤ 10 per save file).

---

## `ContractStore.Hydrate` guard behavior

**Decision**: Silent clear-and-replace; no defensive throw (Q2-A)  
**Rationale**: `Hydrate` has exactly one valid call site — `ContractPersistenceAdapter.OnSaveLoaded` (U-09). SMAPI fires `SaveLoaded` exactly once per session. A defensive guard would add runtime noise without catching a realistic bug in this codebase.

---

## `DestinationKey` custom converter placement

**Decision**: Implemented as a private nested class `DestinationDtoConverter : JsonConverter` inside `SaveDataSerializer`  
**Rationale**: The converter is an implementation detail of the serializer and has no value as a public type. Keeping it private and nested prevents accidental use elsewhere and keeps the `Dayswork.Core/Persistence/Dto/` namespace clean.

Wait — `DestinationDtoV1` is a plain flat class with no polymorphism (it has a `string Type` field plus nullable coords). Newtonsoft handles flat nullable fields natively with `NullValueHandling.Ignore`. **No custom `JsonConverter` is actually required for `DestinationDtoV1`.**

The type-tag → domain type dispatch happens in `SaveDataSerializer.MapDtoToDomain()` — a plain `switch` on `dto.Type`:

```csharp
DestinationKey MapDestination(DestinationDtoV1 dto) => dto.Type switch
{
    "Chest"       => new ChestDestination(new ChestRef(dto.LocationName!, new TileCoord(dto.X!.Value, dto.Y!.Value))),
    "ShippingBin" => ShippingBinDestination.Instance,
    "Mail"        => MailDestination.Instance,
    _             => throw new JsonException($"Unknown destination type: '{dto.Type}'"),
};
```

This throw is caught by the per-contract try/catch in `Deserialize`, which skips the malformed contract and logs a warning (NFR-SAFE-03 / Q9-A). **No `JsonConverter` class is needed** — the Q7-A decision is satisfied by the mapping switch, not a Newtonsoft converter.

---

## `ContractGen` generator design

**File**: `Dayswork.Tests/Persistence/Generators/ContractGen.cs`  
**Framework**: FsCheck.Xunit (already installed in U-02)

**Composition**:

| Field | Generator |
|---|---|
| `Id` | `Arb.Generate<Guid>().Select(g => new ContractId(g))` |
| `EnabledTasks` | `Gen.SubListOf(Enum.GetValues<TaskKind>()).Where(l => l.Count > 0).Select(l => (IReadOnlySet<TaskKind>)l.ToHashSet())` |
| `Zones` | `ZoneGen.Arbitrary().Generator.NonEmptyListOf().Select(l => (IReadOnlyList<Zone>)l)` — composes U-04 generator |
| `TaskDestinations` | Generate a subset of output-producing TaskKinds as keys; for each key, randomly pick `ChestDestination(random ChestRef)`, `ShippingBinDestination.Instance`, or `MailDestination.Instance` |
| `Schedule` | `Gen.Elements(ContractSchedule.OneTime, ContractSchedule.Recurring)` |
| `Status` | `Gen.Elements(ContractStatus.Active, ContractStatus.Paused, ContractStatus.Cancelled)` |
| `HireDate` | `Gen.zip3(Gen.Choose(1,28), Gen.Elements<Season>(), Gen.Choose(1,10)).Select(...)` |
| `DepositAmount` | `Gen.Choose(0, 10_000)` |
| `HourlyRate` | `Gen.Choose(50, 500)` |

**Output-producing TaskKinds** (keys allowed in `TaskDestinations`):
`HarvestCrops`, `CollectFruit`, `CollectAnimalProducts`, `CutTrees`, `ClearRocks`, `ClearWeeds`

**PBT-08 note**: Seed logging is inherited from the U-02 `[Property]` wiring. `ContractGen` needs no special seed-logging setup.
