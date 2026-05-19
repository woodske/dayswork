# U-04 — NFR Design Patterns

---

## Performance Pattern: HashSet deduplication in EnumerateUniqueTiles

**NFR addressed**: PBT-03 (idempotency invariant); general correctness for multi-zone tile scanning.

**Problem**: `EnumerateUniqueTiles(IReadOnlyList<Zone> zones, Func<TileCoord, bool> isPassable)` must return each reachable tile exactly once, regardless of how many zones overlap. Naïvely concatenating all zones' tile lists and calling `.Distinct()` works but calls `Distinct` over the full multi-zone list in O(n log n) with LINQ's default grouping.

**Chosen pattern**: Inline `HashSet<TileCoord>` seen-set during enumeration.

```csharp
// Pseudocode — actual code generated in Code Generation stage
var seen = new HashSet<TileCoord>();
foreach (var zone in zones)
{
    for (int y = zone.TopLeft.Y; y <= zone.BottomRight.Y; y++)
    for (int x = zone.TopLeft.X; x <= zone.BottomRight.X; x++)
    {
        var tile = new TileCoord(x, y);
        if (isPassable(tile) && seen.Add(tile))   // HashSet.Add returns false if already present
            result.Add(tile);
    }
}
```

**Why this is correct for PBT-03**:
- *Idempotency*: `seen.Add` on a duplicate returns `false` — the tile is never added twice, so `[A, A]` produces the same result as `[A]`.
- *Commutativity*: The *set* of returned tiles is the same regardless of zone order (HashSet equality is order-independent).
- *Area conservation*: `seen.Count` ≤ `|tiles(A)| + |tiles(B)|` because overlap tiles are counted at most once.

**TileCoord as struct**: Because `TileCoord` is a `readonly record struct`, `HashSet<TileCoord>` uses the compiler-synthesized `GetHashCode` (based on X and Y fields) without boxing. Struct keys in a `HashSet<T>` are stored by value — no heap allocation per lookup.

**Expected scale**: Largest Stardew farm (Wilderness Farm, ~3,500 reachable tiles). HashSet with 3,500 int-pair keys fits in ~100 KB resident memory. No concern.

---

## Pattern: Zone bounds normalization at construction time

**NFR addressed**: INV-GEO-01 (TopLeft ≤ BottomRight).

**Problem**: When the player drags a zone rectangle on-screen, they may drag in any direction (top-right to bottom-left, bottom-up, etc.). The raw drag endpoints can violate `TopLeft ≤ BottomRight`.

**Chosen pattern**: Normalize coordinates in the UI layer (U-11, `ZoneDrawOverlay`) before constructing a `Zone`. Core assumes inputs are valid — no normalization in `ZoneGeometry`.

```csharp
// In ZoneDrawOverlay (U-11, SMAPI side — not in Core)
var topLeft = new TileCoord(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y));
var bottomRight = new TileCoord(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));
var zone = new Zone(locationName, topLeft, bottomRight);
```

**Rationale**: Keeping normalization in the UI layer keeps `Zone` a dumb value record with no validation logic, and keeps `ZoneGeometry` methods free of defensive bounds-checking loops.

---

## Pattern: Sealed record hierarchy as discriminated union (DestinationKey)

**NFR addressed**: PBT-03 (enables exact `TakeAllFor` per destination), DEST-06 (structural equality per ChestRef).

**Problem**: C# lacks native discriminated unions. `DestinationKey` must be:
1. A closed type family (no new subtypes added outside this assembly)
2. Structurally equatable (for use as a `Dictionary<DestinationKey, ...>` key)
3. Exhaustively matchable via `switch` expressions

**Chosen pattern**: `abstract record DestinationKey` + `sealed` concrete records.

```csharp
public abstract record DestinationKey;
public sealed record ChestDestination(ChestRef Ref) : DestinationKey;
public sealed record ShippingBinDestination : DestinationKey
{
    public static readonly ShippingBinDestination Instance = new();
}
public sealed record MailDestination : DestinationKey
{
    public static readonly MailDestination Instance = new();
}
```

**Why records work as dictionary keys**:
- `abstract record` synthesizes `Equals` and `GetHashCode` based on runtime type + properties.
- `ChestDestination` equality delegates to `ChestRef` equality (which delegates to `LocationName` + `TileCoord` equality).
- Two `ShippingBinDestination` instances are always equal (no properties → `GetHashCode` is the same for all instances).
- C# `switch` can exhaustively match on the closed hierarchy: `case ChestDestination cd:`, `case ShippingBinDestination:`, `case MailDestination:`.

**Singleton instances**: `ShippingBinDestination.Instance` and `MailDestination.Instance` avoid per-call allocations for the most common destinations. Callers should prefer the singleton, but new instances are functionally identical.

---

## Patterns not needed

| Category | Rationale |
|---|---|
| Resilience / retry | Pure in-process computation; no I/O, no transient faults |
| Circuit breaker / bulkhead | Not applicable to local synchronous math |
| Caching layer | ZoneGeometry is stateless; callers may cache the result list if they choose |
| Rate limiting / throttling | Single-player game; no network |
| Security controls | Security Baseline disabled; no attack surface in pure math types |
