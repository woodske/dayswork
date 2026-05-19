# U-04 — NFR Requirements

---

## Applicable NFRs

### NFR-MAINT-01 — Core/Mod separation (hard constraint)

**Rule**: `Dayswork.Core` assembly must have **zero** SMAPI or StardewValley references.

**U-04 scope**: `TileCoord`, `Zone`, `ChestRef`, `DestinationKey`, `ZoneGeometry` all live in `Dayswork.Core`. None of them import from SMAPI, StardewValley, or any SMAPI-bound namespace. The passability oracle is `Func<TileCoord, bool>` — a pure .NET delegate.

**Compliance plan**: Verified at build time by the fact that `Dayswork.Core.csproj` has no `<Reference>` or `<PackageReference>` pointing to `Pathoschild.Stardew.ModBuildConfig`, `StardewModdingAPI`, or `StardewValley`. If any such reference appears, the build will fail with a missing-namespace error.

**Verification gate (Code Generation)**: After generating files, confirm that `dotnet build Dayswork.Core` succeeds with 0 errors, 0 warnings.

---

### PBT-02 — Round-trip: Zone JSON serialization

**Rule**: `deserialize(serialize(zone)) == zone` must hold for all valid `Zone` inputs, verified by a FsCheck property test.

**U-04 obligation**:
- Serializer: `Newtonsoft.Json.JsonConvert.SerializeObject` / `DeserializeObject<Zone>` (already in project via SMAPI transitive dep)
- Generator: `ZoneGen.Arbitrary()` (created in this unit; see PBT-07 below)
- Test file: `Dayswork.Tests/Geometry/ZoneGeometryTests.cs` (or separate `ZoneSerializationTests.cs`)
- Sample count: ≥ 1,000 inputs (FsCheck default is 100; override `MaxTest = 1000` per PBT-09 guidance)
- Must also verify: negative tile coords survive (some Stardew maps use them), Unicode in `LocationName`, `TopLeft == BottomRight` edge case (1×1 zone)

**Blocking**: Yes (PBT-02 is enforced in partial mode).

---

### PBT-03 — Invariants: ZoneGeometry rectangle union

**Rule**: `EnumerateUniqueTiles` must satisfy three invariants for all generated zone inputs.

**U-04 obligation** (three property tests, all using `isPassable = _ => true` to isolate geometry from passability):

| Property | Expression |
|---|---|
| **Commutativity** | `EnumerateUniqueTiles([A, B], p).ToHashSet() == EnumerateUniqueTiles([B, A], p).ToHashSet()` |
| **Idempotency** | `EnumerateUniqueTiles([A, A], p).Count == EnumerateUniqueTiles([A], p).Count` |
| **Area conservation** | `EnumerateUniqueTiles([A, B], p).Count <= CountReachableTiles(A, p) + CountReachableTiles(B, p)` |

Additional determinism invariant (unit test, not PBT):
- `EnumerateTiles(zone)` returns exactly `Width × Height` tiles in row-major order.
- `Contains(zone, tile)` is true iff tile is within inclusive bounds.
- `Intersects(a, b)` is true iff the rectangles share at least one tile.

**Blocking**: Yes (PBT-03 is enforced in partial mode).

---

### PBT-07 — Shared generators: ZoneGen

**Rule**: FsCheck generators for domain types that are used by multiple downstream units must be created as named `Arbitrary<T>` instances in the `Dayswork.Tests/Generators/` namespace.

**U-04 obligation**: Create `ZoneGen` in `Dayswork.Tests/Generators/ZoneGen.cs`:
- `ZoneGen.Zone()` → `Arbitrary<Zone>`: generates valid zones with non-trivial rectangles (width/height ≥ 1), random `LocationName`, bounded tile coords (e.g., X/Y ∈ [-5, 200] to be test-realistic)
- `ZoneGen.TileCoord()` → `Arbitrary<TileCoord>`: generates random tile coords including negatives
- `ZoneGen.ChestRef()` → `Arbitrary<ChestRef>`: generates random ChestRef values
- `ZoneGen.ZoneList()` → `Arbitrary<IReadOnlyList<Zone>>`: generates lists of 1–5 zones (used by multi-zone PBT tests)

**Downstream consumers**: U-05 (`HoursEstimator` tests), U-06 (`ContractGen` composes `ZoneGen`), U-07 (TaskPriorityOrderer tests use `TileCoord`).

**Blocking**: Yes (PBT-07 is enforced in partial mode).

---

### PBT-08 / PBT-09 — Seed logging and FsCheck version

**Status**: N/A — these are infrastructure concerns already satisfied by U-02 (`SeedLoggingDemoTests` and `FsCheck.Xunit 2.16.5` already installed). No new work required in U-04.

---

## NFRs not applicable to U-04

| NFR | Rationale |
|---|---|
| NFR-SAFE-01 (no items lost) | No item handling in this unit |
| NFR-SAFE-02 (integer rounding) | No monetary math; rounding applies in U-05 pricing |
| NFR-SAFE-03 (tolerate absent save data) | No persistence in this unit |
| NFR-UX-01 (gamepad) | No UI |
| NFR-UX-02 / NFR-MAINT-02 (i18n) | No user-visible strings |
| NFR-MAINT-04 (Harmony isolation) | No Harmony patches |
| Security Baseline | Disabled project-wide (Q28 = B) |
