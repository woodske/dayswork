# U-04 — Business Rules

---

## Geometry Invariants (INV-GEO)

These are hard constraints on Zone/TileCoord values. Callers must enforce them before constructing a Zone; ZoneGeometry assumes inputs are valid and does not re-validate.

| ID | Rule | Enforcement |
|---|---|---|
| INV-GEO-01 | `zone.TopLeft.X ≤ zone.BottomRight.X` AND `zone.TopLeft.Y ≤ zone.BottomRight.Y`. Minimum valid zone is 1×1 (TopLeft == BottomRight). | Validated by caller (UI layer in U-11; ContractStore in U-06). ZoneGeometry may assert in debug builds. |
| INV-GEO-02 | `EnumerateTiles(zone)` returns exactly `(zone.BottomRight.X - zone.TopLeft.X + 1) × (zone.BottomRight.Y - zone.TopLeft.Y + 1)` tiles. | Verified by unit test in U-04. |
| INV-GEO-03 | `EnumerateTiles(zone)` always returns tiles in row-major order (Y outer, X inner: (TopLeft.X, TopLeft.Y), (TopLeft.X+1, TopLeft.Y), …, (BottomRight.X, BottomRight.Y)). Deterministic — same zone always yields same sequence. | Verified by unit test in U-04. |
| INV-GEO-04 | `Contains(zone, tile)` is true iff `tile.X ∈ [zone.TopLeft.X, zone.BottomRight.X]` AND `tile.Y ∈ [zone.TopLeft.Y, zone.BottomRight.Y]`. | Verified by unit test in U-04. |
| INV-GEO-05 | `Intersects(a, b)` is true iff the rectangles share at least one tile. Pure bounds math: `a.TopLeft.X ≤ b.BottomRight.X AND b.TopLeft.X ≤ a.BottomRight.X AND a.TopLeft.Y ≤ b.BottomRight.Y AND b.TopLeft.Y ≤ a.BottomRight.Y`. | Verified by unit test in U-04. |

---

## Rectangle Union Invariants (PBT-03 targets)

These three properties are verified by FsCheck property-based tests in `Dayswork.Tests/Geometry/`.

| ID | Property | Expression |
|---|---|---|
| PBT-03-GEO-A | **Commutativity** — tile set is the same regardless of zone list order. | `EnumerateUniqueTiles([A, B], p).ToHashSet() == EnumerateUniqueTiles([B, A], p).ToHashSet()` |
| PBT-03-GEO-B | **Idempotency** — adding a duplicate zone does not change the tile set. | `EnumerateUniqueTiles([A, A], p).Count == EnumerateUniqueTiles([A], p).Count` |
| PBT-03-GEO-C | **Area conservation (upper bound)** — union never exceeds sum of individual areas. | `EnumerateUniqueTiles([A, B], p).Count ≤ CountReachableTiles(A, p) + CountReachableTiles(B, p)` |

The passability oracle `p` is set to "always passable" (`_ => true`) for these PBT tests, so passability is not a confounding variable in geometry proofs.

---

## Zone JSON Round-Trip Rules (PBT-02 target)

| ID | Rule |
|---|---|
| PBT-02-GEO-01 | `Deserialize<Zone>(Serialize(zone)) == zone` for all valid `Zone` inputs. Test uses ≥ 1000 FsCheck-generated `Zone` values via `ZoneGen`. |
| PBT-02-GEO-02 | `LocationName` survives round-trip including strings with Unicode characters, spaces, and apostrophes (e.g., `"Marnie's Ranch"`). |
| PBT-02-GEO-03 | `TileCoord` coordinates with negative values (valid on some Stardew maps) survive round-trip. |

---

## DestinationKey Routing Rules

| ID | Rule | Source |
|---|---|---|
| DEST-01 | Every output-producing task in a contract has exactly one `DestinationKey` assigned. | FR-HIRE-06 |
| DEST-02 | Non-output tasks (WaterCrops, FeedAnimals, PetAnimals) never appear as destination assignment keys. | spec §Tasks |
| DEST-03 | If no chest is assigned for an output task, the destination is `MailDestination.Instance`. The worker still performs the task; all output is mailed the following morning, no penalty. | FR-HIRE-10, FR-OUT-04 |
| DEST-04 | If the player assigns the shipping bin, the destination is `ShippingBinDestination.Instance`. No overflow is possible (vanilla shipping bin is unbounded). | FR-OUT-06 |
| DEST-05 | If the player assigns a specific chest, the destination is `new ChestDestination(ChestRef(locationName, tile))`. | FR-HIRE-08 |
| DEST-06 | Two `ChestDestination` values are equal iff both `LocationName` and `Tile` fields of their inner `ChestRef` are equal. Structural equality via C# record semantics. | — |
| DEST-07 | Multiple tasks may share the same `ChestDestination`. `ItemBuffer` groups items by `DestinationKey` — items for two tasks that share a chest end up in the same bucket. | FR-HIRE-09 |

---

## TileCoord Validity

TileCoord places no constraints on X or Y values — both may be negative (some Stardew map regions use negative tile indices). Callers are responsible for domain-appropriate range validation if required.

---

## LocationName Rules

`LocationName` is a runtime string matching Stardew Valley's internal location unique name (e.g., `"Farm"`, `"Greenhouse"`, `"Barn"`, `"Coop"`, etc.). `Dayswork.Core` does not validate or enumerate location names — that is the SMAPI layer's responsibility.

Validation rule for `Zone.LocationName` and `ChestRef.LocationName`:
- Must be non-null and non-empty (enforced by caller before construction)
- No whitespace trimming — stored as-is
- Case-sensitive (Stardew location names are case-sensitive in the SMAPI API)
