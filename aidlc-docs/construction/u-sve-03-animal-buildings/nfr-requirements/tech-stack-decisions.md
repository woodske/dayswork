# Tech Stack Decisions — U-SVE-03 SVE Animal Buildings

**Answer**: Q3=A — reuse the existing stack; no new dependencies.

## Decision
Implement the unit entirely on the existing technology and component seams:
- **Pure decision logic** in `Dayswork.Core/Compat` — `AnimalBuildingCapacityPolicy` (already present; now consumed with the real `MaxOccupants` bound) and `SveExpansionProfile.MapPremiumBuildingTier` (populate the premium→Deluxe table).
- **Thin Mod adapter** in `Dayswork/Compat/ExpansionCompatService` — counts live `Trough` tiles + reads real `MaxOccupants`; delegates tier mapping to the active profile.
- **Consumers rewired** at the two existing hardcoded sites: `AnimalTaskHandler` (feed capacity) and the hiring enumeration `LegacyScopeBootstrapper` (tier), both behind the U-SVE-01 seams.
- **Tests**: xUnit examples + FsCheck properties in `Dayswork.Tests/Compat` (and animal-handler coverage), no SMAPI required for the pure logic.

## Rationale
- The capacity policy and tier-mapping seam already exist from U-SVE-01; this unit only **populates and wires** them. No structural or dependency change is warranted.
- Consistent with U-SVE-01/02 (pure-Core + thin-adapter, FsCheck full mode).
- No save-schema/enum change (App Design Q4=A): premium buildings reuse the existing `Deluxe*` tiers.

## Dependencies
- **None added.** Existing: .NET 6, SMAPI, xUnit, FsCheck.

## Extension config (unchanged)
- Security Baseline: disabled. Property-Based Testing: enabled, full mode.
