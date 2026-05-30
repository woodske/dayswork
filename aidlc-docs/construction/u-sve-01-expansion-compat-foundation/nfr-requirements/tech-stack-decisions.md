# Tech Stack Decisions — U-SVE-01 Expansion-Compatibility Provider Foundation

**Decision (Q3=A): reuse the existing stack; no new dependencies.**

| Concern | Decision | Rationale |
|---|---|---|
| Language / runtime | C# / .NET 6 (existing) | Matches the Dayswork solution and SMAPI 4.x / SDV 1.6 target. |
| Pure compat logic | `Dayswork.Core/Compat/*` (no SMAPI/Stardew refs) | Preserves the project's Core-pure boundary; enables xUnit + FsCheck without the game (NFR-SVE-05). |
| Mod adapters | `Dayswork/Compat/*` (`ExpansionDetector`, `ExpansionCompatService`) | SMAPI-facing detection (`IModRegistry`) and live-object application live in the mod layer, mirroring existing adapters (e.g., `ToolLevelReader`, `ChestResolver`). |
| Detection API | SMAPI `IModRegistry.IsLoaded(id)` (existing idiom) | Already used in `ModEntry`/`GMCMRegistrar`; no new abstraction needed. |
| Testing | xUnit + FsCheck (existing), full PBT | Consistent with the project's test stack and the enabled PBT extension. |
| Dependency injection | Constructor injection from `ModEntry` (existing composition-root style) | Matches the established wiring; keeps the seam testable. |
| New libraries/frameworks | **None** | Nothing here warrants a new dependency; adding one would increase mod load surface and risk. |

## Constraints carried forward
- No SMAPI/Stardew types in `Dayswork.Core`.
- No new persisted data or `config.json` keys in this unit.
- SVE identifiers centralized in `SveExpansionProfile` (NFR-SVE-07).

## Extension Compliance

| Extension | Status | Compliance |
|---|---|---|
| Security Baseline | Disabled | N/A. |
| Property-Based Testing | Enabled, full | FsCheck is part of the reused stack; no tech-stack change needed to satisfy PBT obligations. |
