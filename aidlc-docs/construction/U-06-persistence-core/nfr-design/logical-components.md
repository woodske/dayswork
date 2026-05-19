# U-06 Persistence Core — Logical Components

**Unit**: U-06 — Persistence Core

---

## Component map

```
Dayswork.Core/Persistence/
├── ContractStore               ← in-memory domain store (C-12)
├── SaveDataSerializer          ← stateless serialization service (C-13)
└── Dto/
    ├── DaysworkSaveDataV1      ← versioned top-level envelope
    ├── ContractDtoV1           ← per-contract serialization DTO
    ├── ZoneDtoV1               ← zone serialization DTO
    ├── DestinationDtoV1        ← destination serialization DTO (flat + type-tag)
    └── GameDateDtoV1           ← game date serialization DTO

Dayswork.Tests/Persistence/
├── ContractStoreTests          ← unit tests for store operations + status DAG
├── SaveDataSerializerTests     ← PBT-02 round-trip + NFR-SAFE-03 edge cases
└── Generators/
    └── ContractGen             ← FsCheck Arbitrary<Contract> (PBT-07)
```

---

## ContractStore (C-12)

**Kind**: In-memory domain store  
**Pattern**: Immutable Record + `with` (Pattern 4); Atomic Hydration (Pattern 6)

**Internal structure**:
```
_contracts : Dictionary<ContractId, Contract>
```

**Responsibilities**:
- CRUD: `Add`, `Get`, `Update`
- Status transitions: `Pause`, `Resume`, `Cancel`
- Bulk read: `List`, `ListActiveForDate` (stubbed — throws `NotImplementedException`)
- Save-load lifecycle: `Hydrate`

**Key design constraints**:
- Never mutates a `Contract` in place — always replaces via `with` expression
- `Hydrate` is the only method that clears the store; all other writes are additive or replacement-of-one-entry
- Status transition guard clauses run before the `with` expression; invalid transitions throw before any state is changed

**Caller boundary** (U-09 wires these):
```
ContractPersistenceAdapter → ContractStore.Hydrate()   (on SaveLoaded)
ContractPersistenceAdapter → ContractStore (exposes IContractStore)
HiringFlowCoordinator      → IContractStore.Add()
RecurringContractScheduler → IContractStore.ListActiveForDate()  ← stubbed
```

---

## SaveDataSerializer (C-13)

**Kind**: Stateless serialization service  
**Pattern**: Exception Barrier (Pattern 1), Null-Safe Empty Result (Pattern 2), Versioned Envelope (Pattern 3), Explicit DTO Mapping (Pattern 5)

**Internal structure**:
```
_serializerSettings : JsonSerializerSettings   (static readonly)
    Formatting         = Indented
    NullValueHandling  = Ignore
```

**Responsibilities**:
- `Serialize(contracts, modVersion)` → JSON string: maps domain → DTOs → JSON
- `Deserialize(json?)` → contract list: JSON → DTOs → domain, with all three null-safe guards and per-record exception barrier

**Private methods** (implementation detail):
```
MapDomainToDto(Contract)     → ContractDtoV1
MapDtoToDomain(ContractDtoV1) → Contract         ← wrapped in per-record try/catch
MapDestination(DestinationDtoV1) → DestinationKey  ← switch on Type; unknown arm throws JsonException
MapZone(ZoneDtoV1)           → Zone
MapZone(Zone)                → ZoneDtoV1
MapDate(GameDateDtoV1)       → GameDate
MapDate(GameDate)            → GameDateDtoV1
```

**Caller boundary**:
```
ContractPersistenceAdapter → ISaveDataSerializer.Serialize()    (on Saving event)
ContractPersistenceAdapter → ISaveDataSerializer.Deserialize()  (on SaveLoaded event)
```

**No SMAPI dependency**: `ISaveDataSerializer` takes and returns plain strings. The adapter in U-09 (which references SMAPI) calls `Helper.Data.WriteSaveData(key, json)` and `Helper.Data.ReadSaveData<string>(key)`. `SaveDataSerializer` never touches SMAPI — it only transforms strings.

---

## DTO types (Dayswork.Core/Persistence/Dto/)

**Kind**: Plain data containers for JSON serialization  
**Design constraint**: No domain logic, no constructors beyond the default, only auto-properties. Newtonsoft.Json uses the default constructor + property setters for deserialization.

| DTO | Domain equivalent | Key fields |
|---|---|---|
| `DaysworkSaveDataV1` | Store snapshot | `SchemaVersion`, `ModVersion`, `Contracts` |
| `ContractDtoV1` | `Contract` | `Id`, `EnabledTasks`, `Zones`, `TaskDestinations`, `Schedule`, `Status`, `HireDate`, `DepositAmount`, `HourlyRate` |
| `ZoneDtoV1` | `Zone` | `LocationName`, `TopLeftX/Y`, `BottomRightX/Y` |
| `DestinationDtoV1` | `DestinationKey` | `Type` (discriminator), `LocationName?`, `X?`, `Y?` |
| `GameDateDtoV1` | `GameDate` | `Day`, `Season` (string), `Year` |

**No inheritance in DTO layer**: `DestinationDtoV1` is a single flat class with a `Type` discriminator string — no abstract base, no subclasses. The domain-side polymorphism (`ChestDestination`, `ShippingBinDestination`, `MailDestination`) is resolved in `MapDestination()`, not in the DTO itself.

---

## ContractGen (Dayswork.Tests/Persistence/Generators/ContractGen.cs)

**Kind**: FsCheck Arbitrary generator (PBT-07 obligation)  
**Pattern**: Composed generator from prior units' generators

**Responsibilities**:
- Provides `Arbitrary<Contract>` for PBT-02 round-trip property in `SaveDataSerializerTests`
- Available to downstream units (U-09, U-12) that write PBTs involving `Contract` inputs

**Generator composition**:

```
ContractGen.Arbitrary() =
    Gen.zip(
        ContractId generator      ← new from U-06
        EnabledTasks generator    ← non-empty subset of TaskKind values
        Zones generator           ← ZoneGen (U-04), non-empty list
        TaskDestinations generator ← subset of output-producing TaskKinds → random DestinationKey
        Schedule generator        ← Gen.Elements(OneTime, Recurring)
        Status generator          ← Gen.Elements(Active, Paused, Cancelled)
        HireDate generator        ← day 1–28, random Season, year 1–10
        DepositAmount generator   ← Gen.Choose(0, 10_000)
        HourlyRate generator      ← Gen.Choose(50, 500)
    )
```

**Output-producing TaskKinds** (only these appear as `TaskDestinations` keys):
`HarvestCrops`, `CollectFruit`, `CollectAnimalProducts`, `CutTrees`, `ClearRocks`, `ClearWeeds`

**FsCheck shrinking**: FsCheck automatically shrinks generated `Contract` values toward the smallest failing case. The `sealed record` type with value equality allows FsCheck to compare shrunk inputs against the original failure — no custom shrinker needed.

---

## Data flow: save / load lifecycle

```
[SAVE EVENT]
SaveDataSerializer.Serialize(store.List(), modVersion)
    → DaysworkSaveDataV1 (envelope)
        → List<ContractDtoV1> (one per Contract)
            → each ContractDtoV1 has List<ZoneDtoV1>, Dict<string, DestinationDtoV1>
    → JsonConvert.SerializeObject(envelope, settings)
    → JSON string
ContractPersistenceAdapter → Helper.Data.WriteSaveData("Dayswork.Contracts", json)

[LOAD EVENT]
ContractPersistenceAdapter → Helper.Data.ReadSaveData<string>("Dayswork.Contracts")
    → null / json string
SaveDataSerializer.Deserialize(json?)
    → Guard 1: null/empty → return []
    → JsonConvert.DeserializeObject<DaysworkSaveDataV1>(json)
    → Guard 2: null envelope → return []
    → Guard 3: SchemaVersion > 1 → return []
    → for each ContractDtoV1:
        try { MapDtoToDomain(dto) } catch { log warn, skip }
    → IReadOnlyList<Contract>
ContractStore.Hydrate(contracts)
    → _contracts.Clear()
    → insert each (skip duplicates with warn)
```

---

## Interface boundary diagram

```
[SMAPI layer — Dayswork project]
    ContractPersistenceAdapter
        |── Helper.Data.WriteSaveData("Dayswork.Contracts", json)
        |── Helper.Data.ReadSaveData<string>("Dayswork.Contracts")
        |
        |── ISaveDataSerializer.Serialize(contracts, modVersion) → string
        |── ISaveDataSerializer.Deserialize(json?) → IReadOnlyList<Contract>
        |
        └── IContractStore.Hydrate(contracts)
            IContractStore.Add / Get / Update / Cancel / Pause / Resume / List

[Core layer — Dayswork.Core project]
    SaveDataSerializer   ← stateless; only dep: Newtonsoft.Json
    ContractStore        ← stateful; no deps beyond System collections
    DTO types            ← pure POCO classes
    Domain types         ← Contract, ContractId, GameDate, Season, ContractStatus, ContractSchedule
```

No arrow crosses the boundary carrying a SMAPI or StardewValley type. The adapter is the sole translation point between the two layers.
