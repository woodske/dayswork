# U-MC-07 Logical Components

**Unit**: U-MC-07 - Output Routing + Greenhouse/Shed  
**Stage**: CONSTRUCTION - NFR Design  
**Status**: Review required

## New Components

| Component | Project | Responsibility | Pattern |
|---|---|---|---|
| `ManagedCropProvenanceKey` or equivalent helper | `Dayswork.Core/Crops` or `Dayswork.Core/Inventory` | Build stable assignment keys from group id, location, and zone bounds. | P3 |
| `ManagedCropDestinationMapBuilder` or equivalent helper | `Dayswork.Core/Crops` or `Dayswork.Core/Inventory` | Convert assignments with `OutputChest` into `OutputScopeProvenance` to `DestinationKey` map entries. | P1/P3 |
| `ManagedCropLocationClassifier` or equivalent runner seam | `Dayswork/Orchestration` | Classify active managed-crop locations as farm, vanilla greenhouse, or expansion greenhouse; decide season-agnostic flag and route need. | P5/P6 |

Exact class names can be finalized during Code Generation, but these responsibilities should stay small and independently testable.

## Extended Components

| Component | Change | Pattern |
|---|---|---|
| `OutputScopeFamily` | Add `ManagedCrop`. | P1 |
| `OutputScopeProvenance` | Add `ManagedCrop(string assignmentKey)` factory. | P1/P3 |
| `TileAction` | Carry optional output provenance for managed harvest actions. | P1 |
| `DepositPlanner` | Add provenance-aware `Plan(...)` overload; existing overload delegates with an empty provenance map. | P1/P2 |
| `ShiftPlanBuilder` | Emit one `ManagedCrops` batch for each distinct managed assignment location, not just `Farm`. | P4 |
| `ManagedCropFieldReader` | Read the active `GameLocation`; accept caller-provided season-agnostic classification; keep tile reads bounded to assigned zones. | P5 |
| `ShiftOrchestrator.ManagedCrops` | Resolve/enter active managed-crop location, set harvest provenance, re-enter non-farm locations after shopping, return to farm before global deposit/exit. | P1/P5/P6 |
| `ShiftOrchestrator.Deposit` / deposit integration path | Build and pass the managed-crop provenance destination map into `DepositPlanner`. | P1/P2/P8 |
| `CropGroupDraft` / `CropPlanDraft` | Add group location, seasonal vs season-agnostic mode, year-round slot projection/hydration, and location-change zone clearing. | P7 |
| `ManageCropsMenu` / `CropGroupEditorMenu` | Show location labels/selectors and switch between seasonal table and year-round row. | P7 |
| `CropListPickerMenu` / crop picker path | Use greenhouse catalog mode without season filter for season-agnostic groups. | P7/P9 |
| `ZoneDrawMenu` / `ZoneDrawOverlay` | Draw on the selected target location and protect zones only within that location. | P7/P9 |
| `ChestResolver` / output chest picker path | Exclude built-in office input and output chests from explicit choices while retaining automatic fallback. | P8 |

## Reused Components

| Component | Reuse |
|---|---|
| `CropShiftPlanner` | Continues to produce ordered per-tile actions from pure `FieldState`; season-agnostic viability bypass already belongs to pure planning. |
| `PlantingViabilityCalculator` | Reused through `CropShiftPlanner`; no new viability algorithm. |
| `CropSupplyPlanner`, `ShiftSupplyAggregator`, `PurchaseAffordabilityCalculator`, `StoreResolver`, `StoreHoursPolicy` | U-MC-06 shopping logic remains unchanged and feeds greenhouse/shed batches through existing supply semantics. |
| `ShopStockReader` and `ShopPurchaseService` | Existing live shop snapshot and exchange-atomic purchase service remain the only shop API/gold seams. |
| `BuildingWorkNavigator`, `CrossLocationRouteNavigator`, `ExpansionCompatService`, `SveExpansionProfile` | Existing route validation/navigation seams handle greenhouse and SVE shed movement. |
| `ChestDestination`, `AutomaticOutputDestination`, `ShippingBinDestination`, `ChestRef` | Existing destination concepts remain the public routing vocabulary. |
| `CropHudNotifier` | Reused for bounded notices; new text remains i18n-backed. |

## Component Responsibilities By NFR Category

| NFR category | Owning components |
|---|---|
| Resilience | `ShiftOrchestrator.ManagedCrops`, `ShiftOrchestrator.Deposit`, `DepositPlanner`, route validation through `ExpansionCompatService`, overflow/mail pipeline. |
| Scalability | `ShiftPlanBuilder`, `ManagedCropDestinationMapBuilder`, `ManagedCropFieldReader`, location-scoped zone helpers. |
| Performance | `DepositPlanner` dictionary lookup, assignment-count destination map, zone-bounded field reader, existing route descriptors. |
| Security | No active component; Security Baseline disabled and no security surface is introduced. |
| Maintainability | Pure Core routing/key helpers, thin Mod location/route adapters, existing menu components. |
| Testability | `Dayswork.Tests` example tests plus FsCheck properties over pure provenance, destination, batch, and draft projection seams. |

## Code Generation Notes

- Implement destination precedence first in pure Core tests, then wire runtime harvest provenance.
- Preserve current `DepositPlanner` behavior with regression examples before adding managed-crop map behavior.
- Keep the location classifier small; do not embed SVE-specific hardcoding outside existing expansion profile descriptors.
- Keep manual playtest coverage for visible greenhouse and SVE shed greenhouse behavior in the final Build and Test artifacts.
- Do not introduce new runtime dependencies, save schema changes, or infrastructure files.

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | Pure logical components carry the blocking property obligations; live adapters are scoped to example/manual coverage. |

