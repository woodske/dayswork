# U-04 — Logical Components

All components in this unit are pure, in-process, synchronous. No queues, caches, or external services.

---

## ZoneGeometry (C-05)

**Role**: Stateless service — all methods are effectively static functions wrapped in an interface for testability and dependency injection.

**Internal data structures**:
- `HashSet<TileCoord>` — created per `EnumerateUniqueTiles` call, discarded after. No state held between calls.
- `List<TileCoord>` — accumulates results within each call, returned as `IReadOnlyList<TileCoord>`.

**Instantiation**: `new ZoneGeometry()` — no constructor parameters. Injected via `IZoneGeometry` interface wherever needed.

**Lifetime in mod**: Single instance, constructed in `ModEntry` composition root (U-10). Shared across all callers (`HoursEstimator`, `ShiftOrchestrator`).

---

## ZoneGen (test component)

**Role**: FsCheck `Arbitrary<T>` provider for Zone-related types. Lives in `Dayswork.Tests/Generators/ZoneGen.cs`.

**Generation strategy**:

| Type | Strategy |
|---|---|
| `TileCoord` | `Gen.Choose(-5, 200)` for both X and Y. Range chosen to be test-realistic (farm tiles are in 0–200 range; -5 covers maps with negative offsets). |
| `Zone` | Generate two `TileCoord` values; compute `topLeft = (Min(x1,x2), Min(y1,y2))`, `bottomRight = (Max(x1,x2), Max(y1,y2))`; always satisfies INV-GEO-01. Random `LocationName` drawn from a small set: `"Farm"`, `"Greenhouse"`, `"Barn"`, `"Coop"`. |
| `ChestRef` | Random `LocationName` from same small set; random `TileCoord`. |
| `IReadOnlyList<Zone>` | `Gen.ListOf` with size 1–5, using `Zone` generator above. Upper bound keeps PBT runs fast. |

**Why sorted-pair generation** (not rejection sampling): Rejection sampling for `TopLeft ≤ BottomRight` discards ~50% of generated pairs and makes FsCheck's shrinking less effective (shrunken values may be rejected too). Generating sorted pairs guarantees validity at construction time with no waste.

**Exported members** (all `static`):
```csharp
public static class ZoneGen
{
    public static Arbitrary<TileCoord> TileCoord();
    public static Arbitrary<Zone> Zone();
    public static Arbitrary<ChestRef> ChestRef();
    public static Arbitrary<IReadOnlyList<Zone>> ZoneList();
}
```

**Downstream registration** (PBT-07): FsCheck requires generators to be registered (via `Arb.Register<ZoneGen>()` in a test class or fixture) before they're used automatically. The base test class established in U-02 should expose a registration point; if not, individual test classes register `ZoneGen` in their constructors.

---

## DestinationKey hierarchy (value types — not a service)

**Role**: Closed type family for routing collected items. Behaves as a value type despite being a reference type (immutable, structurally equatable, suitable as dictionary key).

**No lifetime concerns**: All three subtypes (`ChestDestination`, `ShippingBinDestination`, `MailDestination`) are immutable value records. They are created at contract-draft time (U-09) and stored inside `Contract` records (U-06). No singleton lifecycle management needed beyond the convenience `Instance` fields on the no-property subtypes.

---

## No infrastructure components

This unit introduces no queues, caches, circuit breakers, connection pools, or external service adapters. All computation is synchronous, in-process, and bounded by farm tile counts (~3,500 tiles maximum).
