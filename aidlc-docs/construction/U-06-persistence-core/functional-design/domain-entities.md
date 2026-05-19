# U-06 Persistence Core — Domain Entities

All types live in `Dayswork.Core`. No SMAPI or StardewValley references anywhere in this unit.

---

## New types introduced by U-06

---

### `Season` (enum)

**File**: `Dayswork.Core/Domain/Season.cs`  
**Namespace**: `Dayswork.Core.Domain`

```csharp
namespace Dayswork.Core.Domain;

public enum Season
{
    Spring,
    Summer,
    Fall,
    Winter,
}
```

**Rationale**: Introduced here because `GameDate` (below) needs it, and `GameDate` is introduced here as a U-06 dependency. SMAPI has its own `Season` type but `Dayswork.Core` must not reference SMAPI — this enum is the Core-side equivalent. The SMAPI-side adapter (U-09) maps between them.

---

### `GameDate` (value record)

**File**: `Dayswork.Core/Domain/GameDate.cs`  
**Namespace**: `Dayswork.Core.Domain`

```csharp
namespace Dayswork.Core.Domain;

public readonly record struct GameDate(int Day, Season Season, int Year);
```

**Semantics**:
- `Day`: day-of-season, 1–28 (inclusive). Values outside this range are invalid.
- `Season`: one of the four `Season` enum values.
- `Year`: game year, 1-based. Year 1 is the player's first year on the farm.

**Value type rationale**: Used as a field on `Contract` (an immutable record) and embedded in JSON save data. Struct avoids heap allocation; record struct synthesizes structural equality, which is required for PBT-02 round-trip equality assertions.

**Total-days computation** (available for sorting/comparison if needed):

```
totalDays = (Year - 1) * 112 + (int)Season * 28 + (Day - 1)
```

This formula is not stored on `GameDate` — it can be computed inline by any caller that needs ordering. No method is added to the struct in U-06; extension methods can be added later if required.

---

### `ContractId` (identifier value type)

**File**: `Dayswork.Core/Domain/ContractId.cs`  
**Namespace**: `Dayswork.Core.Domain`

```csharp
namespace Dayswork.Core.Domain;

public readonly record struct ContractId(Guid Value)
{
    public static ContractId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
```

**Rationale**: Wrapping `Guid` in a named struct prevents passing a raw `Guid` where a `ContractId` is expected (accidental parameter-swap bugs). The factory method `New()` is the canonical way to mint a fresh Id in U-09's `HiringFlowCoordinator`.

**Serialization**: Stored as a lowercase GUID string in `ContractDtoV1.Id` (e.g., `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"`). Parsed back via `Guid.Parse()` in `SaveDataSerializer`.

---

### `ContractStatus` (enum)

**File**: `Dayswork.Core/Domain/ContractStatus.cs`  
**Namespace**: `Dayswork.Core.Domain`

```csharp
namespace Dayswork.Core.Domain;

public enum ContractStatus
{
    Active,
    Paused,
    Cancelled,
}
```

**Valid transitions**:

| From | To | Trigger |
|---|---|---|
| `Active` | `Paused` | `ContractStore.Pause(id)` |
| `Active` | `Cancelled` | `ContractStore.Cancel(id)` |
| `Paused` | `Active` | `ContractStore.Resume(id)` |
| `Paused` | `Cancelled` | `ContractStore.Cancel(id)` |
| `Cancelled` | *(none)* | Terminal state |

`Cancelled` is terminal. Any attempt to `Pause`, `Resume`, or `Cancel` a `Cancelled` contract throws `InvalidOperationException`.

**One-time vs recurring**: One-time contracts completed after their shift ends are NOT given a special terminal status in U-06 — they remain `Active` until explicitly cancelled. The `RecurringContractScheduler` (U-09/U-15) is responsible for cancelling completed one-time contracts.

---

### `ContractSchedule` (enum)

**File**: `Dayswork.Core/Domain/ContractSchedule.cs`  
**Namespace**: `Dayswork.Core.Domain`

```csharp
namespace Dayswork.Core.Domain;

public enum ContractSchedule
{
    OneTime,
    Recurring,
}
```

---

### `Contract` (domain record)

**File**: `Dayswork.Core/Domain/Contract.cs`  
**Namespace**: `Dayswork.Core.Domain`

```csharp
namespace Dayswork.Core.Domain;

public sealed record Contract(
    ContractId Id,
    IReadOnlySet<TaskKind> EnabledTasks,
    IReadOnlyList<Zone> Zones,
    IReadOnlyDictionary<TaskKind, DestinationKey> TaskDestinations,
    ContractSchedule Schedule,
    ContractStatus Status,
    GameDate HireDate,
    int DepositAmount,
    int HourlyRate
);
```

**Field-by-field notes**:

| Field | Type | Notes |
|---|---|---|
| `Id` | `ContractId` | Caller-generated via `ContractId.New()`. Unique within a save file. |
| `EnabledTasks` | `IReadOnlySet<TaskKind>` | The tasks the player toggled on in Screen 1. Non-empty (at least one task must be enabled at hire time — enforced by U-09 SummaryMenu). |
| `Zones` | `IReadOnlyList<Zone>` | Ordered list of zones drawn in Screen 2. Non-empty (at least one zone required). |
| `TaskDestinations` | `IReadOnlyDictionary<TaskKind, DestinationKey>` | Output routing for output-producing tasks only. Missing key = no assignment; items are mailed (FR-HIRE-10). `ClearGrass` is never a key — hay routing is silo-first/drop-on-ground and is never routed through this dictionary (FR-TASK-09). |
| `Schedule` | `ContractSchedule` | `OneTime` or `Recurring`. |
| `Status` | `ContractStatus` | `Active`, `Paused`, or `Cancelled`. Starts as `Active` at creation. |
| `HireDate` | `GameDate` | The in-game date the contract was created. Set once at hire time; never mutated. |
| `DepositAmount` | `int` | Gold deducted at hire (one-time) or each morning (recurring). Locked at hire time (FR-PAY-08). |
| `HourlyRate` | `int` | The rate in effect at hire time. Locked for refund computation (FR-PAY-05, FR-PAY-08). |

**Immutability and update semantics**: `Contract` is an immutable `sealed record`. `ContractStore` status changes produce new record instances via C#'s `with` expression:

```csharp
// Inside ContractStore.Pause():
_contracts[id] = existing with { Status = ContractStatus.Paused };
```

**Output-producing tasks** (the only `TaskKind` values that may appear as keys in `TaskDestinations`):

| TaskKind | Output |
|---|---|
| `HarvestCrops` | Crops |
| `CollectFruit` | Fruit |
| `CollectAnimalProducts` | Eggs, milk, wool, truffles |
| `CutTrees` | Wood, sap, seeds |
| `ClearRocks` | Stone, ore, geodes, gems |
| `ClearWeeds` | Fiber, mixed seeds |

**Non-output tasks** (never appear in `TaskDestinations`):

| TaskKind | Reason |
|---|---|
| `WaterCrops` | No item drops |
| `FeedAnimals` | Consumes hay; no drops |
| `PetAnimals` | No item drops |
| `ClearGrass` | Hay goes to silo or drops on ground; never chest/mail (FR-TASK-09) |

---

## Persistence DTO types

These types live in `Dayswork.Core/Persistence/Dto/` and are used exclusively by `SaveDataSerializer`. They are not part of the domain model — they exist solely to produce stable, versioned JSON.

---

### `GameDateDtoV1`

```csharp
public sealed class GameDateDtoV1
{
    public int Day { get; set; }
    public string Season { get; set; } = "";
    public int Year { get; set; }
}
```

`Season` stored as its enum name string (e.g., `"Spring"`).

---

### `DestinationDtoV1`

```csharp
public sealed class DestinationDtoV1
{
    public string Type { get; set; } = "";        // "Chest" | "ShippingBin" | "Mail"
    public string? LocationName { get; set; }     // Chest only
    public int? X { get; set; }                   // Chest only
    public int? Y { get; set; }                   // Chest only
}
```

---

### `ZoneDtoV1`

```csharp
public sealed class ZoneDtoV1
{
    public string LocationName { get; set; } = "";
    public int TopLeftX { get; set; }
    public int TopLeftY { get; set; }
    public int BottomRightX { get; set; }
    public int BottomRightY { get; set; }
}
```

---

### `ContractDtoV1`

```csharp
public sealed class ContractDtoV1
{
    public string Id { get; set; } = "";                                    // ContractId → Guid string
    public List<string> EnabledTasks { get; set; } = [];                   // TaskKind enum names
    public List<ZoneDtoV1> Zones { get; set; } = [];
    public Dictionary<string, DestinationDtoV1> TaskDestinations { get; set; } = []; // TaskKind name → destination
    public string Schedule { get; set; } = "";                              // "OneTime" | "Recurring"
    public string Status { get; set; } = "";                                // "Active" | "Paused" | "Cancelled"
    public GameDateDtoV1 HireDate { get; set; } = new();
    public int DepositAmount { get; set; }
    public int HourlyRate { get; set; }
}
```

---

### `DaysworkSaveDataV1`

```csharp
public sealed class DaysworkSaveDataV1
{
    public int SchemaVersion { get; set; } = 1;
    public string ModVersion { get; set; } = "";     // e.g., "0.1.0" from manifest.json
    public List<ContractDtoV1> Contracts { get; set; } = [];
}
```

`ModVersion` enables future migration code to detect which mod version wrote the data (Q6-C decision). U-06 writes it; future versions read it during schema migrations.

---

## Directory layout produced by U-06

```text
Dayswork.Core/
├── Domain/
│   ├── Season.cs            ← enum           (this unit)
│   ├── GameDate.cs          ← record struct  (this unit)
│   ├── ContractId.cs        ← record struct  (this unit)
│   ├── ContractStatus.cs    ← enum           (this unit)
│   ├── ContractSchedule.cs  ← enum           (this unit)
│   └── Contract.cs          ← sealed record  (this unit)
└── Persistence/
    ├── IContractStore.cs    ← interface      (this unit)
    ├── ContractStore.cs     ← implementation (this unit)
    ├── ISaveDataSerializer.cs ← interface    (this unit)
    ├── SaveDataSerializer.cs  ← implementation (this unit)
    └── Dto/
        ├── GameDateDtoV1.cs       (this unit)
        ├── DestinationDtoV1.cs    (this unit)
        ├── ZoneDtoV1.cs           (this unit)
        ├── ContractDtoV1.cs       (this unit)
        └── DaysworkSaveDataV1.cs  (this unit)

Dayswork.Tests/
└── Persistence/
    ├── Generators/
    │   └── ContractGen.cs           ← FsCheck arbitraries (PBT-07)
    ├── ContractStoreTests.cs
    └── SaveDataSerializerTests.cs
```
