# Tech Stack Decisions — U-SVE-04 New Content + Grandpa's Shed

**Answer**: Q3=A — reuse the existing stack; no new dependencies.

## Decision
Implement entirely on existing technology and seams:
- **Pure decision logic** in `Dayswork.Core/Compat` — the animal-product category set, `SveExpansionProfile` content-override + work-location tables, `ContentDescriptor`/`WorkClassification`.
- **Thin Mod adapters** in `Dayswork` — `WorkAreaScanner` category detection, `ObjectTargetClassifier` override consultation, the building navigators / `BuildingLocationResolver` / chest resolution for Grandpa's Shed and unique-name keying.
- **Tests**: xUnit examples + FsCheck properties in `Dayswork.Tests/Compat` (and scanner/resolver tests); pure logic needs no SMAPI.

## Rationale
- The classification, work-location, and resolver seams already exist (U-SVE-01..03); this unit **populates and wires** them. No structural or dependency change is warranted.
- Consistent with U-SVE-01/02/03 (pure-Core + thin-adapter, FsCheck full mode).
- No save-schema change (Q5=A reuses the `LocationName` field for unique keying).

## Dependencies
- **None added.** Existing: .NET 6, SMAPI, xUnit, FsCheck.

## Extension config (unchanged)
- Security Baseline: disabled. Property-Based Testing: enabled, full mode.
