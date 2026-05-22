# U-03 Config Foundation — Domain Entities

**Unit**: U-03 Config Foundation
**Scope**: Type schema for configuration

---

## Entity: `TaskKind` (enum)

**File**: `Dayswork.Core/Domain/TaskKind.cs`

**Plan deviation**: [unit-of-work.md](../../../inception/application-design/unit-of-work.md) originally placed `TaskKind` in U-04 Geometry & Domain Primitives. Moved to U-03 per the locked Q1 decision (ConfigSnapshot needs typed task identifiers to express per-task rate increments). U-04 retains ownership of `Zone`, `TileCoord`, `ChestRef`, `DestinationKey`, and ZoneGeometry.

### Schema

```csharp
namespace Dayswork.Core.Domain;

public enum TaskKind
{
    WaterCrops,
    HarvestCrops,
    CollectFruit,
    FeedAnimals,
    PetAnimals,
    CollectAnimalProducts,
    CutTrees,
    ClearRocks,
    ClearWeeds,
    ClearGrass,
}
```

**Source**: FR-TASK-01 — "The v1 task set is: Water crops, Harvest crops, Collect fruit, Feed animals, Pet animals, Collect animal products, Cut trees, Clear rocks, Clear weeds, Clear grass."

**Future-extension rule**: any new `TaskKind` value must be paired with a matching `TaskIncrements` default in `ConfigDefaults.Build()`. INV-CFG-03 is enforced in the factory and will throw at construction if the new value lacks a rate entry.

---

## Entity: `IConfigSnapshot` (interface)

**File**: `Dayswork.Core/Config/IConfigSnapshot.cs`
**Purpose**: read-only contract that downstream pricing + worker components depend on.

### Schema

```csharp
namespace Dayswork.Core.Config;

using Dayswork.Core.Domain;

public interface IConfigSnapshot
{
    int BaseRate { get; }
    IReadOnlyDictionary<TaskKind, int> TaskIncrements { get; }
    double AverageSpeedConstant { get; }
    int HardCapTime { get; }
    int StuckInitialWaitMinutes { get; }
    int StuckPostTeleportWaitMinutes { get; }
}
```

### Why an interface, not just the record?

- **U-05 calculators** (`IRateCalculator`, `IDepositCalculator`, `IRefundCalculator`, `IHoursEstimator`) take `IConfigSnapshot` in their constructors — they don't need to know about the concrete record.
- **Tests construct minimal fakes**: a test that exercises only `BaseRate` and `TaskIncrements` can build a dictionary-backed `IConfigSnapshot` fake without instantiating the full record.
- **U-17 GMCM** may produce alternative `IConfigSnapshot` implementations during the GMCM "edit then save" flow (e.g., a backing-mutable wrapper). Consumers stay unchanged.

---

## Entity: `ConfigSnapshot` (record)

**File**: `Dayswork.Core/Config/ConfigSnapshot.cs`
**Purpose**: immutable, value-equality implementation of `IConfigSnapshot`.

### Schema

```csharp
namespace Dayswork.Core.Config;

using Dayswork.Core.Domain;

public sealed record ConfigSnapshot(
    int BaseRate,
    IReadOnlyDictionary<TaskKind, int> TaskIncrements,
    double AverageSpeedConstant,
    int HardCapTime,
    int StuckInitialWaitMinutes,
    int StuckPostTeleportWaitMinutes
) : IConfigSnapshot;
```

### Design notes

- C# `record` (positional) gives us value equality, deconstruction, and `with` expressions for free.
- Positional record chosen over init-only properties — 6 fields stays concise, and constructor positionality makes "this is the canonical field order" obvious.
- `sealed` because there is no inheritance use case (alternative implementations go through the interface).
- `TaskIncrements` typed as `IReadOnlyDictionary<TaskKind, int>` for compile-time immutability of the rate table. The factory wraps the construction-time `Dictionary<TaskKind, int>` in `new ReadOnlyDictionary<,>(dict)`.

---

## Entity: `ConfigDefaults` (static factory)

**File**: `Dayswork.Core/Config/ConfigDefaults.cs`
**Purpose**: produce a fresh `IConfigSnapshot` populated with the spec-defined defaults from [business-rules.md](business-rules.md).

### Schema

```csharp
namespace Dayswork.Core.Config;

using System.Collections.ObjectModel;
using Dayswork.Core.Domain;

public static class ConfigDefaults
{
    public static IConfigSnapshot Build()
    {
        var increments = new Dictionary<TaskKind, int>
        {
            [TaskKind.WaterCrops]            = 20,
            [TaskKind.HarvestCrops]          = 25,
            [TaskKind.CollectFruit]          = 15,
            [TaskKind.FeedAnimals]           = 20,
            [TaskKind.PetAnimals]            = 10,
            [TaskKind.CollectAnimalProducts] = 15,
            [TaskKind.CutTrees]              = 30,
            [TaskKind.ClearRocks]            = 20,
            [TaskKind.ClearWeeds]            = 20,
            [TaskKind.ClearGrass]            = 20,
        };

        // INV-CFG-03: TaskIncrements must cover every defined TaskKind value.
        foreach (TaskKind kind in Enum.GetValues<TaskKind>())
        {
            if (!increments.ContainsKey(kind))
            {
                throw new InvalidOperationException(
                    $"ConfigDefaults.Build is missing a TaskIncrement entry for {kind}.");
            }
        }

        return new ConfigSnapshot(
            BaseRate: 50,
            TaskIncrements: new ReadOnlyDictionary<TaskKind, int>(increments),
            AverageSpeedConstant: 5.0,
            HardCapTime: 2000,
            StuckInitialWaitMinutes: 10,
            StuckPostTeleportWaitMinutes: 10
        );
    }
}
```

### Why this pattern?

- **Single source of truth** for the spec's default values. Future migrations (e.g., spec changes a rate) edit exactly one method.
- **Self-guarding** — the foreach loop catches the case where a future `TaskKind` value is added without a corresponding rate default. Without this guard, the missing key would surface as a `KeyNotFoundException` at runtime in U-05's `RateCalculator`.
- **Returns the interface** (`IConfigSnapshot`, not `ConfigSnapshot`) so callers depend on the contract.

---

## Directory layout produced by U-03

```text
Dayswork.Core/
├── Config/
│   ├── IConfigSnapshot.cs    ← interface  (this unit)
│   ├── ConfigSnapshot.cs     ← record     (this unit)
│   └── ConfigDefaults.cs     ← factory    (this unit)
└── Domain/
    └── TaskKind.cs           ← enum       (this unit, moved from U-04 per Q1)
```

**U-04** (Geometry & Domain Primitives) will subsequently populate `Dayswork.Core/Domain/` with `Zone.cs`, `TileCoord.cs`, `ChestRef.cs`, `DestinationKey.cs`, and create `Dayswork.Core/Geometry/` for `ZoneGeometry.cs`.

---

## Public API surface produced by U-03

| Symbol | Kind | Visibility |
|---|---|---|
| `Dayswork.Core.Domain.TaskKind` | enum | `public` |
| `Dayswork.Core.Config.IConfigSnapshot` | interface | `public` |
| `Dayswork.Core.Config.ConfigSnapshot` | record | `public sealed` |
| `Dayswork.Core.Config.ConfigDefaults` | static class | `public static` |
| `Dayswork.Core.Config.ConfigDefaults.Build()` | method | `public static` |

All symbols become part of the `Dayswork.Core` assembly's public surface. Internal helpers (none in U-03) would use `internal`.
