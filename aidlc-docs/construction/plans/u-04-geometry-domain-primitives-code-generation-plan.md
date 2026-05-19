# U-04 Geometry & Domain Primitives — Code Generation Plan

**Unit**: U-04 Geometry & Domain Primitives
**Stage**: CONSTRUCTION → U-04 → Code Generation
**Workspace root**: `C:\Users\kwood\Repos\dayswork`

---

## Unit context

### Stories implemented
- **S-19** (partial) — PBT-02 Zone round-trip + PBT-03 ZoneGeometry invariants + PBT-07 ZoneGen shared generator

### Dependencies (must exist before this unit)
- U-01: `Dayswork.Core.csproj` (Core library)
- U-02: `Dayswork.Tests.csproj` (FsCheck.Xunit + xUnit framework)
- U-03: `Dayswork.Core/Domain/TaskKind.cs` (already exists — do NOT recreate)

### What this unit unblocks
- U-05: `Zone` + `TileCoord` used by `HoursEstimator`; `ZoneGen` used by its PBTs
- U-06: `Zone` + `ChestRef` + `DestinationKey` stored in `Contract` record; `ZoneGen` used by `ContractGen`
- U-07: `TileCoord` used in `(TaskKind, TileCoord)` work items

---

## Files to create

| # | File | Project |
|---|---|---|
| 1 | `Dayswork.Core/Domain/TileCoord.cs` | Dayswork.Core |
| 2 | `Dayswork.Core/Domain/Zone.cs` | Dayswork.Core |
| 3 | `Dayswork.Core/Domain/ChestRef.cs` | Dayswork.Core |
| 4 | `Dayswork.Core/Domain/DestinationKey.cs` | Dayswork.Core |
| 5 | `Dayswork.Core/Geometry/IZoneGeometry.cs` | Dayswork.Core |
| 6 | `Dayswork.Core/Geometry/ZoneGeometry.cs` | Dayswork.Core |
| 7 | `Dayswork.Tests/Generators/ZoneGen.cs` | Dayswork.Tests |
| 8 | `Dayswork.Tests/Geometry/ZoneGeometryTests.cs` | Dayswork.Tests |
| 9 | `aidlc-docs/construction/U-04-geometry-domain-primitives/code/u-04-code-summary.md` | docs |

---

## Generation steps

### Step 1 — TileCoord.cs
- [x] Create `Dayswork.Core/Domain/TileCoord.cs`
- `readonly record struct TileCoord(int X, int Y)` in namespace `Dayswork.Core.Domain`
- Value type: zero heap allocation per tile; compiler-synthesizes equality, GetHashCode, ToString
- No additional members needed

### Step 2 — Zone.cs
- [x] Create `Dayswork.Core/Domain/Zone.cs`
- `sealed record Zone(string LocationName, TileCoord TopLeft, TileCoord BottomRight)` in namespace `Dayswork.Core.Domain`
- LocationName identifies the Stardew game location (e.g., "Farm", "Greenhouse")
- TopLeft and BottomRight are inclusive bounds (INV-GEO-01: TopLeft ≤ BottomRight — enforced by caller)
- No members beyond positional properties; compiler-synthesized equality is correct

### Step 3 — ChestRef.cs
- [x] Create `Dayswork.Core/Domain/ChestRef.cs`
- `sealed record ChestRef(string LocationName, TileCoord Tile)` in namespace `Dayswork.Core.Domain`
- Identifies a chest by location + tile (per FR-HIRE-08)
- Compiler-synthesized equality is correct

### Step 4 — DestinationKey.cs
- [x] Create `Dayswork.Core/Domain/DestinationKey.cs`
- `abstract record DestinationKey` (base)
- `sealed record ChestDestination(ChestRef Ref) : DestinationKey` — wraps a specific ChestRef
- `sealed record ShippingBinDestination : DestinationKey` — static `Instance` singleton
- `sealed record MailDestination : DestinationKey` — static `Instance` singleton
- All in namespace `Dayswork.Core.Domain`, single file

### Step 5 — IZoneGeometry.cs
- [x] Create `Dayswork.Core/Geometry/IZoneGeometry.cs`
- Namespace: `Dayswork.Core.Geometry`
- Using: `Dayswork.Core.Domain`
- Six methods:
  - `IReadOnlyList<TileCoord> EnumerateTiles(Zone zone)` — all tiles, row-major, no passability filter
  - `IReadOnlyList<TileCoord> EnumerateReachableTiles(Zone zone, Func<TileCoord, bool> isPassable)`
  - `IReadOnlyList<TileCoord> EnumerateUniqueTiles(IReadOnlyList<Zone> zones, Func<TileCoord, bool> isPassable)` — multi-zone deduplicated
  - `int CountReachableTiles(Zone zone, Func<TileCoord, bool> isPassable)` — avoids list allocation
  - `bool Contains(Zone zone, TileCoord tile)` — pure bounds check
  - `bool Intersects(Zone a, Zone b)` — pure bounds check

### Step 6 — ZoneGeometry.cs
- [x] Create `Dayswork.Core/Geometry/ZoneGeometry.cs`
- `public sealed class ZoneGeometry : IZoneGeometry`
- Namespace: `Dayswork.Core.Geometry`
- No constructor parameters — stateless; lifecycle: single instance in ModEntry (U-10)
- `EnumerateTiles`: preallocate list with `Width * Height` capacity; double for-loop Y outer, X inner
- `EnumerateReachableTiles`: same loop pattern; filter by `isPassable(tile)`
- `EnumerateUniqueTiles`: HashSet<TileCoord> seen-set; inline deduplication per nfr-design-patterns.md; `seen.Add(tile)` returns false for duplicates
- `CountReachableTiles`: double for-loop, increment counter; avoids allocating a list
- `Contains`: pure bounds expression (4 comparisons)
- `Intersects`: pure interval overlap check on both axes

### Step 7 — ZoneGen.cs (shared FsCheck generators)
- [x] Create `Dayswork.Tests/Generators/ZoneGen.cs`
- Namespace: `Dayswork.Tests.Generators`
- Using: `Dayswork.Core.Domain`, `FsCheck`
- Static class with four `Arbitrary<T>` factory methods:
  - `TileCoord()`: Gen.Choose(-5, 200) for X and Y; maps to `new TileCoord(x, y)`
  - `Zone()`: generate two (x,y) pairs; sort to produce valid TopLeft/BottomRight; Gen.Elements for LocationName from `{"Farm","Greenhouse","Barn","Coop"}`
  - `ChestRef()`: random LocationName from same set + TileCoord generator
  - `ZoneList()`: Gen.Choose(1, 5) for count; Gen.ListOf(n, Zone().Generator); cast to `IReadOnlyList<Zone>`
- No rejection sampling — sorted-pair generation always produces valid Zone (INV-GEO-01 satisfied by construction)

### Step 8 — ZoneGeometryTests.cs
- [x] Create `Dayswork.Tests/Geometry/ZoneGeometryTests.cs`
- Namespace: `Dayswork.Tests.Geometry`
- Using: `Dayswork.Core.Domain`, `Dayswork.Core.Geometry`, `Dayswork.Tests.Generators`, `FsCheck`, `FsCheck.Xunit`, `Xunit`
- Single `ZoneGeometry _geo = new()` field
- **PBT-02** (1 property, MaxTest = 1000): `Zone_JsonRoundTrip` — serialize Zone to JSON, deserialize, assert equal; uses `ZoneGen.Zone()`
- **PBT-03** (3 properties, MaxTest = 1000 each):
  - `EnumerateUniqueTiles_Commutativity` — `[A,B]` set equals `[B,A]` set; `Prop.ForAll(ZoneGen.Zone(), ZoneGen.Zone(), ...)`
  - `EnumerateUniqueTiles_Idempotency` — `[A,A].Count == [A].Count`; `Prop.ForAll(ZoneGen.Zone(), ...)`
  - `EnumerateUniqueTiles_AreaConservation` — union ≤ sum of individuals; `Prop.ForAll(ZoneGen.Zone(), ZoneGen.Zone(), ...)`
- **Unit tests** (`[Fact]`):
  - `EnumerateTiles_ReturnsExactTileCount` — 3×3 zone has 9 tiles
  - `EnumerateTiles_RowMajorOrder` — 2×2 zone in expected sequence
  - `EnumerateTiles_SingleTileZone` — 1×1 zone has exactly one tile
  - `Contains_ReturnsTrueForCornerTiles` — all four corners return true
  - `Contains_ReturnsFalseForOutsideTile` — adjacent-but-outside tiles return false
  - `Intersects_ReturnsTrue_WhenOverlapping`
  - `Intersects_ReturnsFalse_WhenAdjacent` — touching edge but no shared tile
  - `Intersects_ReturnsTrue_WhenOneContainsOther`
  - `ChestDestination_EqualityByValue` — same ChestRef → equal DestinationKey
  - `ShippingBinDestination_InstanceEquality` — two new instances are equal
  - `MailDestination_DifferentFromShippingBin` — distinct subtypes are not equal

### Step 9 — Run build and tests
- [x] Run `dotnet build Dayswork.Core\Dayswork.Core.csproj`; expect 0 errors 0 warnings — PASSED
- [x] Run `dotnet build Dayswork.Tests\Dayswork.Tests.csproj`; expect 0 errors 0 warnings — PASSED
- [x] Run `dotnet test Dayswork.Tests\Dayswork.Tests.csproj --logger "console;verbosity=normal"`; expect all tests pass — 37 passed, 1 skipped (PBT-08 demo), 0 failed
- [x] Verify: `dotnet build Dayswork.Core` produces no SMAPI/StardewValley references in output (NFR-MAINT-01) — CONFIRMED
- [x] PBT compliance: PBT-02 round-trip passes ≥1000 inputs; PBT-03 three invariants pass ≥1000 inputs each — ALL PASSED

### Step 10 — Code summary doc
- [x] Create `aidlc-docs/construction/U-04-geometry-domain-primitives/code/u-04-code-summary.md`

### Step 11 — State and audit update
- [x] Update `aidlc-docs/aidlc-state.md` → U-04 complete, advance to U-05
- [x] Append to `aidlc-docs/audit.md`

---

## Story traceability

| Story | Delivered by | Completion status after U-04 |
|---|---|---|
| S-19 | Step 7 (ZoneGen PBT-07), Steps 8 (PBT-02 + PBT-03) | Partial — U-04 portion complete; remainder in U-05/U-06/U-10/U-16 |
