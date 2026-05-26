# U-24 — Config, Regression, and Documentation Cleanup: Domain Entities

**Unit**: U-24 — Config, Regression, and Documentation Cleanup  
**Stage**: CONSTRUCTION — Functional Design  
**Decisions applied**: FD-Q1=A, FD-Q2=C, FD-Q3=A, FD-Q4=A, FD-Q5=A, FD-Q6=A

This file defines the cleanup-oriented value shapes and projections that U-24 uses to make the redesign-era config surface, regression scope, and documentation refresh coherent.

See [business-logic-model.md](business-logic-model.md) for end-to-end flows and [business-rules.md](business-rules.md) for enforceable constraints.

---

## Existing types reused

| Type | Role in U-24 |
|---|---|
| `ModConfig` | Mutable persisted config source that becomes redesign-only after cleanup. |
| `ModConfigManager` | Save/reset/publish seam for config and GMCM interaction. |
| `IConfigSnapshot` / `ConfigSnapshot` | Immutable runtime config shape consumed by pricing, scheduling, and worker runtime. |
| `ConfigDefaults` | Default values for the redesign-era settings. |
| `RuntimeConfigSnapshotMapper` | Validation and normalization seam between saved config and immutable runtime snapshot. |
| `GMCMRegistrar` | Optional registration seam for the player-facing config UI. |
| `TaskKind`, `OutdoorBandSize`, `AnimalBuildingTier`, `WorkActionKind` | Existing domain keys used by redesign pricing and energy configuration. |
| `ContractTermsSnapshot`, `PricingSnapshot`, `WorkerEnergyProfile` | Existing redesign-era types that explain why the final config surface is built around package prices and action costs rather than hourly fields. |

---

## Cleanup-oriented projections

### `RedesignConfigSurface`

Conceptual projection of the complete player-facing redesign config model.

```text
RedesignConfigSurface
  OutdoorThresholds
  OutdoorServiceBandPrices
  AnimalBuildingPrices
  GreenhouseServicePrices
  WorkerDailyEnergyCapacity
  WorkActionCosts
  WorkerPacing
  WorkerRecovery
```

Interpretation:
- this is the authoritative player-facing tuning catalog after U-24
- it deliberately excludes hourly/deposit-era concepts

### `WorkerPacingSettings`

Conceptual grouping of the redesign-era pacing controls.

```text
WorkerPacingSettings
  WalkPixelsPerTick
  ActionAnimationMs
  EntranceHoldTicks
  HardCapTime
```

### `WorkerRecoverySettings`

Conceptual grouping of stuck-recovery timing controls.

```text
WorkerRecoverySettings
  StuckInitialWaitMinutes
  StuckPostTeleportWaitMinutes
```

### `ConfigShapeMode`

Explicit interpretation of the saved-config compatibility stance.

```text
ConfigShapeMode
  { RedesignOnlyCleanBreak }
```

U-24 adopts only one mode because the approved answer is explicit: the saved `config.json` shape becomes redesign-only after cleanup.

### `GMCMSectionProjection`

Projection used to describe the final player-facing config menu grouping.

```text
GMCMSectionProjection
  Key
  DisplayName
  Tooltip
  Fields
```

Expected top-level sections:
- `pricing`
- `worker_stamina`
- `worker_behavior`

### `GMCMFieldProjection`

Conceptual field descriptor for a redesign-era config control.

```text
GMCMFieldProjection
  Key
  ValueType
  Range
  ValidationRule
  I18nNameKey
  I18nTooltipKey
```

Interpretation:
- each field maps directly to redesign-era config semantics
- no dual-surface legacy aliases are expected after U-24

### `RegressionFocusArea`

Named target for the U-24 regression sweep.

```text
RegressionFocusArea
  Key
  StoryLinks
  AutomationLevel
  Notes
```

Approved focus areas:
- output routing / overflow
- tool snapshot / skip rules
- stuck recovery
- invulnerability
- multiplayer guard
- i18n lint

### `RegressionChecklistItem`

Reviewer-facing verification item for build/test docs.

```text
RegressionChecklistItem
  Key
  Area
  ExpectedBehavior
  AutomatedCoverageReference
  ManualCheckHint
```

This keeps the rewritten build/test docs concrete and scannable.

### `I18nEnforcementBoundary`

Conceptual description of what the lint gate must enforce.

```text
I18nEnforcementBoundary
  EnforcedSurfaces
  ApprovedExemptions
  LintOwner
```

Interpretation:
- enforced surfaces are player-visible text
- approved exemptions are technical/debug/internal literals
- the existing lint test remains the enforcement owner

### `DeviationNote`

Reviewer-facing summary item for accepted deviations or known caveats.

```text
DeviationNote
  Key
  Category
  Summary
  StillRelevantBecause
  VerificationImpact
```

This is the building block for the consolidated redesign-note output chosen in FD-Q6.

### `DocumentationRefreshPlan`

Conceptual plan for the final build/test doc rewrite.

```text
DocumentationRefreshPlan
  BuildInstructions
  UnitTestInstructions
  IntegrationTestInstructions
  PerformanceTestInstructions
  Summary
  RegressionChecklist
```

Interpretation:
- the existing artifact set stays the same
- the content becomes redesign-native

---

## Ownership boundaries locked by U-24

| Concern | Primary owner after U-24 |
|---|---|
| Player-facing config catalog | `ModConfig` + `GMCMRegistrar` + i18n keys |
| Runtime-safe validated config | `RuntimeConfigSnapshotMapper` + `ConfigSnapshot` |
| Final user-visible string boundary | `i18n/default.json` + hardcoded-string lint test |
| Targeted unchanged-behavior regression sweep | `Dayswork.Tests` |
| Redesign-native build/test instructions | `aidlc-docs/construction/build-and-test/` |
| Reviewer-facing deviations/caveats summary | U-24 documentation output |

The point of these projections is not to create new gameplay entities. It is to make the final cleanup pass precise enough that configuration, tests, and docs all match the redesign the code now implements.
