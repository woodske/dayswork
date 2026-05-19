# U-04 Geometry & Domain Primitives — Functional Design Plan

**Unit**: U-04 Geometry & Domain Primitives (see [unit-of-work.md](../../inception/application-design/unit-of-work.md))
**Stage**: CONSTRUCTION → U-04 → Functional Design
**Workspace root**: `C:\Users\kwood\Repos\dayswork`

---

## Unit context

### Stories assigned to U-04
- **S-19** (partial) — PBT-02 round-trip for Zone JSON serialization; PBT-03 invariants for ZoneGeometry rectangle union.
- Foundation for S-03 (zone drawing), S-04 (chest assignment — uses ChestRef), S-10 (ItemBuffer uses DestinationKey).

### Components owned
- **C-05 ZoneGeometry** (interface + implementation)
- **Value types**: `TileCoord`, `Zone`, `ChestRef`, `DestinationKey` (TaskKind already created in U-03)

### Code organization
- `Dayswork.Core/Domain/` — `TileCoord.cs`, `Zone.cs`, `ChestRef.cs`, `DestinationKey.cs`
- `Dayswork.Core/Geometry/` — `IZoneGeometry.cs`, `ZoneGeometry.cs`
- `Dayswork.Tests/Geometry/` — `ZoneGen.cs`, PBT round-trip + invariant tests

### Dependencies on other units
- **U-01 Project Scaffold** — `Dayswork.Core.csproj` exists
- **U-02 Test Infrastructure** — FsCheck/xUnit framework exists; `ZoneGen` is added to `Generators/`
- **U-03 Config Foundation** — `TaskKind` enum already created

### Dependencies this unit unblocks
- **U-05 Pricing Core** — `HoursEstimator` calls `ZoneGeometry.CountReachableTiles`; `Zone` used in its signature
- **U-06 Persistence Core** — `ContractGen` composes `ZoneGen`; `Zone` round-trips through save JSON
- **U-07 Capability & Priority Core** — `TaskPriorityOrderer` sorts `(TaskKind, TileCoord)` work items
- **U-09 Minimum Hiring Flow** — `ContractDraft` holds `IReadOnlyList<Zone>` and `ChestRef` assignments
- **U-10 Minimum Worker Shift** — `ItemBuffer` indexed by `DestinationKey`; `ShiftOrchestrator` emits `TileCoord`-based intents

### Per-unit stage decisions
| Stage | Decision | Rationale |
|---|---|---|
| Functional Design | **EXECUTE** | New domain primitives, geometric operations, and type hierarchy decisions |
| NFR Requirements | **EXECUTE** | NFR-SAFE-02 (integer math), PBT-02 (serialization round-trip), PBT-03 (geometry invariants) |
| NFR Design | **EXECUTE** | Cascades from NFR Requirements |
| Infrastructure Design | **SKIP** | Per execution plan, all units skip Infra |
| Code Generation | **EXECUTE** | Always |

---

## Functional Design steps

- [x] Analyze unit context (this plan)
- [x] Collect user answers to Q1–Q6 (all recommendations accepted)
- [x] Generate `aidlc-docs/construction/U-04-geometry-domain-primitives/functional-design/domain-entities.md`
- [x] Generate `aidlc-docs/construction/U-04-geometry-domain-primitives/functional-design/business-logic-model.md`
- [x] Generate `aidlc-docs/construction/U-04-geometry-domain-primitives/functional-design/business-rules.md`
- [x] Update `aidlc-docs/aidlc-state.md`
- [x] Update `aidlc-docs/audit.md`
- [ ] Present REVIEW REQUIRED gate

---

## Files this stage produces

| File | Type | Purpose |
|---|---|---|
| `aidlc-docs/construction/U-04-geometry-domain-primitives/functional-design/domain-entities.md` | created | TileCoord, Zone, ChestRef, DestinationKey, IZoneGeometry schemas |
| `aidlc-docs/construction/U-04-geometry-domain-primitives/functional-design/business-logic-model.md` | created | ZoneGeometry operations, tile-enumeration pipeline, passability oracle model |
| `aidlc-docs/construction/U-04-geometry-domain-primitives/functional-design/business-rules.md` | created | Geometry invariants (PBT-02, PBT-03); rectangle bounds rules; DestinationKey routing rules |
| `aidlc-docs/aidlc-state.md` | modified | Advance to Functional Design Awaiting Approval |
| `aidlc-docs/audit.md` | modified | Q&A + FD generation log |

---

## Open questions for the user

Six design decisions drive everything else in this unit. Each is a short multiple-choice question.

---

### Q1 — Zone rectangle definition

A `Zone` defines an axis-aligned rectangle of farm tiles. How should the rectangle be stored?

**A) Two corners** — `TileCoord TopLeft` + `TileCoord BottomRight` (both inclusive; rectangle contains all tiles where `x ∈ [TopLeft.X, BottomRight.X]` and `y ∈ [TopLeft.Y, BottomRight.Y]`)

**B) Origin + dimensions** — `TileCoord Origin` (top-left corner) + `int Width` + `int Height` (tile counts)

*Context*: The player draws zones by dragging two corners, which maps naturally to option A. Option B requires computing `BottomRight = (Origin.X + Width - 1, Origin.Y + Height - 1)` internally. Both represent the same shape — this is a storage convenience choice.

[Answer]:

---

### Q2 — Zone location scope

Does a `Zone` record carry its own `string LocationName` (e.g., `"Farm"`, `"Greenhouse"`) to identify which game map it belongs to, or is location context always managed by the caller?

**A) Zone includes LocationName** — `Zone` is self-contained and can be persisted/restored without external context. Matches the `ChestRef` pattern (which already carries a `LocationName`). ZoneGeometry methods take these Zones as-is; the caller supplies a passability oracle appropriate for that location.

**B) Zone is location-agnostic** — location is always inferred from context; simplifies the Zone record. Requires callers (e.g., `ContractStore`, `ShiftOrchestrator`) to track location separately alongside each zone.

*Context*: Zones can exist on the farm map OR inside buildings. For persistence round-trips and multi-location contract support, the Zone must be reconstructable without context.

[Answer]:

---

### Q3 — DestinationKey representation

`ItemBuffer` (C-10, introduced in U-10) is described as "indexed by destination key (Chest|ShippingBin|Mail)". At shift execution time, the orchestrator knows which `ChestRef` is assigned to each task. How should `DestinationKey` represent a destination?

**A) Plain `enum`** — three values: `Chest`, `ShippingBin`, `Mail`. All chest-destined items share one buffer bucket; `DepositPlanner` (U-14) reconstructs per-chest grouping from a task→ChestRef assignment map passed to it separately. `TakeAllFor(DestinationKey.Chest)` returns everything destined for any chest.

**B) Sealed record hierarchy** — `abstract record DestinationKey` with `record ChestDestination(ChestRef Ref)`, `record ShippingBinDestination`, `record MailDestination`. Each unique `ChestRef` is its own buffer bucket; `TakeAllFor(new ChestDestination(ref))` returns only items for one specific chest. `DepositPlanner` calls `TakeAllFor` once per unique `ChestRef`.

*Context*: Option B makes the buffer a complete grouping structure and keeps `DepositPlanner` simpler. Option A is simpler now but DepositPlanner must perform additional grouping. The type lives in `Dayswork.Core/Domain/` and is defined in U-04 even though `ItemBuffer` arrives in U-10.

[Answer]:

---

### Q4 — ZoneGeometry union semantics

PBT-03 requires testing "commutative, idempotent, area conservation" for rectangle union. What is the intended union design in `IZoneGeometry`?

**A) No explicit `Union` method** — multi-zone support comes from methods that accept `IReadOnlyList<Zone>` (e.g., `EnumerateUniqueTiles(IReadOnlyList<Zone> zones, ...)`). The PBT-03 invariants test the `EnumerateUniqueTiles` operation: the tile set is the same regardless of zone list order (commutative), adding a duplicate zone changes nothing (idempotent), and the unique-tile count ≤ sum of individual zone tile counts (area conservation as upper bound).

**B) Explicit `Union(Zone a, Zone b) → Zone` method** — returns the smallest axis-aligned bounding rectangle enclosing both zones (bounding-box union). Commutativity and idempotency are direct properties of this method. Note: this over-approximates the area (includes tiles between the two rectangles that weren't in either zone).

**C) Explicit `Union(Zone a, Zone b) → IReadOnlyList<Zone>` method** — returns a set of non-overlapping rectangles whose tile-union equals the exact tile-union of the two input zones. Exact area conservation holds. More complex to implement.

*Context*: Option A is the simplest and doesn't require a formal "union of two rectangles" type — multi-zone is naturally a list. The PBT invariants still have full coverage.

[Answer]:

---

### Q5 — TileCoord value type

Should `TileCoord` be a value type (struct) or a reference type (class)?

**A) `readonly record struct TileCoord(int X, int Y)`** — value type; zero heap allocation per tile coordinate. Zone scanning may iterate thousands of TileCoords; struct avoids GC pressure. Nullable form is `TileCoord?`. Equality and `GetHashCode` are synthesized by the compiler from (X, Y).

**B) `record TileCoord(int X, int Y)`** — reference type; heap-allocated. Simpler in some edge cases (can be `null` without `?`); no copy-semantics caution needed. Consistent with `Zone`, `ChestRef`, and other records in the domain.

*Context*: `Zone` and `ChestRef` are almost certainly reference-type records (they hold strings). `TileCoord` is the hot-path type (appears in every tile-scan loop). Struct is the typical choice for 2D coordinate types in game modding.

[Answer]:

---

### Q6 — ZoneGeometry passability oracle scope

The C-05 description says `ZoneGeometry` "takes a `Func<TileCoord, bool>` passability oracle". At what scope is the oracle provided?

**A) Per-method parameter** — methods that filter by passability accept the oracle as an explicit argument:
```csharp
IReadOnlyList<TileCoord> EnumerateReachableTiles(Zone zone, Func<TileCoord, bool> isPassable);
int CountReachableTiles(Zone zone, Func<TileCoord, bool> isPassable);
```
Each call can use a different oracle (e.g., different locations). Increases method arity but makes pure testing trivial.

**B) Constructor parameter** — `new ZoneGeometry(Func<TileCoord, bool> isPassable)` — oracle is injected once at construction. Instance is scoped to one location's passability map (one `ZoneGeometry` per game location). Methods don't carry the oracle in their signatures:
```csharp
IReadOnlyList<TileCoord> EnumerateReachableTiles(Zone zone);
int CountReachableTiles(Zone zone);
```

*Context*: Option A is more flexible and keeps the class stateless. Option B fits the "one shift = one location" usage pattern (in practice, the orchestrator creates a `ZoneGeometry` per location at shift start and reuses it).

[Answer]:
