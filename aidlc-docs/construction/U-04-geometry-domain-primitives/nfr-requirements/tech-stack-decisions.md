# U-04 — Tech Stack Decisions

## Summary

No new packages or tech stack choices are needed for U-04. All required libraries are already present in the solution from U-01/U-02.

---

## Serialization: Newtonsoft.Json

**Decision**: Use `Newtonsoft.Json` (already a transitive dep via SMAPI) for Zone/ChestRef JSON serialization.

**Why not System.Text.Json**: SMAPI and Stardew Valley ship Newtonsoft.Json as part of their distribution. Adding System.Text.Json would introduce a second serializer, increase mod size, and risk version conflicts in the SMAPI runtime. The entire project uses Newtonsoft.Json (decided in Application Design D1/component-dependency.md rule 1).

**Newtonsoft.Json behavior for `readonly record struct TileCoord`**:
Newtonsoft.Json 13.x serializes structs with public property-like members correctly. `readonly record struct TileCoord(int X, int Y)` generates public `X` and `Y` properties; no custom converter needed. Round-trip: `{"X": 5, "Y": 8}` → `TileCoord(5, 8)`.

**Newtonsoft.Json behavior for sealed records (Zone, ChestRef)**:
Standard serialization — nested objects. No `[JsonConstructor]` needed since the parameterized constructor is the only public constructor on records.

**Newtonsoft.Json behavior for DestinationKey hierarchy**:
`DestinationKey` itself is not persisted in U-04 (persistence is U-06). When U-06's `SaveDataSerializer` needs to persist destination assignments in the `Contract` DTO, it will add type-discriminator handling (e.g., a `"$type"` field or a custom converter). This is deferred to U-06 Functional Design.

---

## Testing: FsCheck.Xunit 2.16.5

**Decision**: Existing `FsCheck.Xunit 2.16.5` package from U-02 is used for all PBT tests in this unit. No version change.

**ZoneGen implementation notes**:
- Use `Gen.Choose(min, max)` for bounded integer generation
- Use `Gen.Frequency` or `Gen.Elements` for a small set of realistic `LocationName` strings (e.g., `"Farm"`, `"Greenhouse"`, `"Barn"`, `"Coop"` — keeps tests readable and avoids unrealistic location names)
- For the zone bounds invariant (TopLeft ≤ BottomRight): generate two TileCoords and sort them rather than adding a filter, to keep generation efficient

---

## No new packages

| Library | Status |
|---|---|
| `Newtonsoft.Json` | Already in solution (SMAPI transitive) |
| `FsCheck.Xunit 2.16.5` | Already installed in `Dayswork.Tests` |
| `xunit 2.6.2` | Already installed in `Dayswork.Tests` |

No `<PackageReference>` additions are needed in any `.csproj` file for this unit.
