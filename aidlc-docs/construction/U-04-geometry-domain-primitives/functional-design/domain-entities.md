# U-04 — Domain Entities

All types live in `Dayswork.Core`. No SMAPI or StardewValley references anywhere in this unit.

---

## TileCoord

**Type**: `readonly record struct TileCoord(int X, int Y)`  
**Namespace**: `Dayswork.Core.Domain`  
**File**: `Dayswork.Core/Domain/TileCoord.cs`

A single tile position on a game-location grid. Stardew Valley maps are tile-based; each tile is a 64×64 pixel square.

| Member | Kind | Notes |
|---|---|---|
| `X` | `int` | Horizontal tile index (0 = left edge of map) |
| `Y` | `int` | Vertical tile index (0 = top edge of map) |

**Value type rationale**: Zone scans iterate potentially thousands of `TileCoord` values per shift. A struct eliminates per-instance heap allocation and GC pressure. The compiler synthesizes `Equals`, `GetHashCode`, and `ToString` from (X, Y).

**Nullable form**: `TileCoord?` is used when a tile position may be absent (e.g., a "no tile found" return).

---

## Zone

**Type**: `sealed record Zone(string LocationName, TileCoord TopLeft, TileCoord BottomRight)`  
**Namespace**: `Dayswork.Core.Domain`  
**File**: `Dayswork.Core/Domain/Zone.cs`

An axis-aligned rectangle of tiles on a named game location.

| Member | Kind | Notes |
|---|---|---|
| `LocationName` | `string` | Game location name; e.g., `"Farm"`, `"Greenhouse"`, `"Barn"`. Must be non-null and non-empty. |
| `TopLeft` | `TileCoord` | Upper-left corner of the rectangle (inclusive). |
| `BottomRight` | `TileCoord` | Lower-right corner of the rectangle (inclusive). |

**Bounds invariant** (INV-GEO-01): `TopLeft.X ≤ BottomRight.X` AND `TopLeft.Y ≤ BottomRight.Y`. A minimum zone is 1×1 (TopLeft == BottomRight). Construction code must validate this before creating a Zone; ZoneGeometry assumes inputs are valid.

**Derived properties** (not stored — computed on demand):
- Width: `BottomRight.X - TopLeft.X + 1`
- Height: `BottomRight.Y - TopLeft.Y + 1`
- Tile count: `Width * Height`

**Persistence**: Zone serializes to/from JSON via Newtonsoft.Json (already in SMAPI's transitive deps). `TileCoord` serializes as a nested object `{"X": 5, "Y": 8}` (standard Newtonsoft behavior for structs with public properties).

---

## ChestRef

**Type**: `sealed record ChestRef(string LocationName, TileCoord Tile)`  
**Namespace**: `Dayswork.Core.Domain`  
**File**: `Dayswork.Core/Domain/ChestRef.cs`

Identifies a specific chest by its game location and tile position. Identity is by location+tile — renaming the chest in-game does not change its `ChestRef`; moving it does (per FR-HIRE-08).

| Member | Kind | Notes |
|---|---|---|
| `LocationName` | `string` | Game location name where the chest sits. |
| `Tile` | `TileCoord` | Tile position of the chest within that location. |

**Equality**: Compiler-synthesized from (LocationName, Tile). Two `ChestRef` values are equal iff both `LocationName` and `Tile` match exactly.

**Use in DestinationKey**: Wrapped by `ChestDestination(ChestRef Ref)` to form a concrete buffer destination (see below).

---

## DestinationKey (sealed record hierarchy)

**Base type**: `abstract record DestinationKey`  
**Namespace**: `Dayswork.Core.Domain`  
**File**: `Dayswork.Core/Domain/DestinationKey.cs`

Identifies where collected items should be routed. Used as the index key in `ItemBuffer` (C-10, introduced in U-10). Three concrete subtypes:

### ChestDestination

```
sealed record ChestDestination(ChestRef Ref) : DestinationKey
```

Items destined for a specific player-placed chest. Each unique `ChestRef` produces a distinct `ChestDestination` instance. Equality is structural: `new ChestDestination(ref1) == new ChestDestination(ref2)` iff `ref1 == ref2`.

### ShippingBinDestination

```
sealed record ShippingBinDestination : DestinationKey
```

Items destined for the farm's shipping bin. No capacity limit (per FR-OUT-06 — vanilla shipping bin is unbounded). All instances are equal (no properties).

Recommended usage: `ShippingBinDestination.Instance` (static singleton defined on the class) to avoid allocating new instances per call.

### MailDestination

```
sealed record MailDestination : DestinationKey
```

Items that have no assigned chest (per FR-HIRE-10, FR-OUT-04). Mailed to the player the following morning, no penalty. All instances are equal.

Recommended usage: `MailDestination.Instance` (static singleton).

**Routing assignment**: At contract creation time, the `HiringFlowCoordinator` (M-03) assigns one `DestinationKey` per output-producing task. The assignment is stored in the `Contract` record (introduced in U-06). The `ShiftOrchestrator` (M-12) reads this assignment at runtime and passes the correct `DestinationKey` to `ItemBuffer.Add(item, destination)`.

---

## IZoneGeometry / ZoneGeometry

**Interface**: `IZoneGeometry`  
**Implementation**: `ZoneGeometry`  
**Namespace**: `Dayswork.Core.Geometry`  
**Files**: `Dayswork.Core/Geometry/IZoneGeometry.cs`, `Dayswork.Core/Geometry/ZoneGeometry.cs`

Pure, stateless geometric operations over `Zone` records. Zero game-state dependencies. SMAPI-side code injects a passability oracle (`Func<TileCoord, bool>`) on a per-method basis.

### Interface

```csharp
public interface IZoneGeometry
{
    // Pure rectangle enumeration — no passability filter.
    IReadOnlyList<TileCoord> EnumerateTiles(Zone zone);

    // Passability-filtered enumeration for a single zone.
    IReadOnlyList<TileCoord> EnumerateReachableTiles(Zone zone, Func<TileCoord, bool> isPassable);

    // Multi-zone deduplicated passability-filtered enumeration.
    // Primary method for building the shift task-queue tile set.
    IReadOnlyList<TileCoord> EnumerateUniqueTiles(IReadOnlyList<Zone> zones, Func<TileCoord, bool> isPassable);

    // Count of passable tiles — for HoursEstimator (avoids list allocation).
    int CountReachableTiles(Zone zone, Func<TileCoord, bool> isPassable);

    // Point-in-rectangle test (pure bounds math — ignores passability).
    bool Contains(Zone zone, TileCoord tile);

    // Rectangle intersection test.
    bool Intersects(Zone a, Zone b);
}
```

**Passability oracle**: The `Func<TileCoord, bool> isPassable` argument is provided by the SMAPI-side caller (typically wrapping `Game1.getFarm().isTileLocationOpen()` or equivalent for non-farm locations). `ZoneGeometry` itself makes no game-state calls.

**Tile enumeration order** (deterministic, per INV-GEO-03): Row-major order — Y increments from `TopLeft.Y` to `BottomRight.Y`; within each row, X increments from `TopLeft.X` to `BottomRight.X`. Example for zone (0,0)→(1,1): `(0,0), (1,0), (0,1), (1,1)`.

**EnumerateUniqueTiles deduplication**: Zones are enumerated in the order provided. Each tile is yielded at most once (first occurrence wins). A `HashSet<TileCoord>` is used internally for O(1) lookup.
