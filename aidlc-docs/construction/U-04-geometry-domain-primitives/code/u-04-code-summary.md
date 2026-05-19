# U-04 Code Summary — Geometry & Domain Primitives

## Files created

### Production (Dayswork.Core)

| File | Type | Purpose |
|---|---|---|
| `Dayswork.Core/Domain/TileCoord.cs` | `readonly record struct` | Tile-grid coordinate; value type, zero heap allocation |
| `Dayswork.Core/Domain/Zone.cs` | `sealed record` | Axis-aligned tile rectangle with LocationName |
| `Dayswork.Core/Domain/ChestRef.cs` | `sealed record` | Chest identity by LocationName + TileCoord |
| `Dayswork.Core/Domain/DestinationKey.cs` | sealed record hierarchy | Item routing: ChestDestination(ChestRef), ShippingBinDestination, MailDestination |
| `Dayswork.Core/Geometry/IZoneGeometry.cs` | interface | 6-method contract for tile-rectangle operations |
| `Dayswork.Core/Geometry/ZoneGeometry.cs` | `sealed class` | Stateless implementation; HashSet deduplication in EnumerateUniqueTiles |

### Tests (Dayswork.Tests)

| File | Type | Purpose |
|---|---|---|
| `Dayswork.Tests/Generators/ZoneGen.cs` | FsCheck generators | PBT-07 shared arbitraries: TileCoord, Zone, ChestRef, ZoneList |
| `Dayswork.Tests/Geometry/ZoneGeometryTests.cs` | xUnit + FsCheck | 12 [Fact] + 4 [Property] tests |

## Test results

- **Build**: 0 errors, 0 warnings
- **Total tests**: 38 (37 passed, 1 skipped — expected PBT-08 demo)
- **New U-04 tests**: 16 (12 unit + 4 PBT)

## PBT compliance (partial mode)

| Rule | Status | Details |
|---|---|---|
| PBT-02 | Compliant | `Zone_JsonRoundTrip` — 1000 generated inputs, all pass |
| PBT-03 | Compliant | `Commutativity`, `Idempotency`, `AreaConservation` — 1000 inputs each |
| PBT-07 | Compliant | `ZoneGen.TileCoord()`, `Zone()`, `ChestRef()`, `ZoneList()` in `Generators/` |

## NFR-MAINT-01 verification

`dotnet build Dayswork.Core` succeeded with 0 errors. `Dayswork.Core.csproj` references only `Newtonsoft.Json` — no SMAPI or StardewValley references.

## Notable implementation detail

`EnumerateUniqueTiles` uses an inline `HashSet<TileCoord>` seen-set. Because `TileCoord` is a `readonly record struct`, `HashSet<TileCoord>` stores keys by value (no boxing). `seen.Add(tile)` returns `false` on duplicate — no `.Distinct()` call needed, supporting the PBT-03 idempotency proof directly.
