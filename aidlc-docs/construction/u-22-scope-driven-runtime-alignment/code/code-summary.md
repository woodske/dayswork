# U-22 — Scope-Driven Runtime Alignment: Code Summary

## Outcome

U-22 completed the runtime cutover from compatibility `Zones` to authoritative typed scope:

- live shift startup now requires `Contract.ScopeSelection` and refuses to execute unsupported no-scope contracts
- runtime batch planning is now deterministic across selected animal buildings, outdoor animal follow-up, greenhouse crop work, outdoor crop work, and outdoor clearing work
- selected barns/coops now drive animal-service eligibility everywhere on the farm instead of piggybacking on outdoor zones
- greenhouse execution now stays separate from outdoor crop/clearing execution
- task-owned deposit routing still resolves only from `TaskKind`, but buffered output now preserves sidecar scope provenance through deposit and overflow handling
- settlement letters now explain overflow in scope-aware terms without changing the existing one-letter safety model

## Modified files

### Core runtime and routing seams

- `Dayswork.Core/Domain/TaskKindSets.cs`
- `Dayswork.Core/Inventory/BufferedItem.cs`
- `Dayswork.Core/Inventory/DepositPlan.cs`
- `Dayswork.Core/Inventory/DepositPlanner.cs`
- `Dayswork.Core/Inventory/IItemBuffer.cs`
- `Dayswork.Core/Inventory/ItemBuffer.cs`
- `Dayswork.Core/Shifts/ShiftContext.cs`
- `Dayswork.Core/Shifts/ShiftPlanBuilder.cs`
- `Dayswork.Core/Shifts/WorkBatch.cs`
- `Dayswork.Core/Shifts/WorkItem.cs`

### Runtime shell and UI wording

- `Dayswork/Integration/IMailDispatcher.cs`
- `Dayswork/Integration/MailDispatcher.cs`
- `Dayswork/ModEntry.cs`
- `Dayswork/Orchestration/AnimalTaskHandler.cs`
- `Dayswork/Orchestration/IndoorWorkScanner.cs`
- `Dayswork/Orchestration/ShiftOrchestrator.cs`
- `Dayswork/Orchestration/WorkAreaScanner.cs`
- `Dayswork/UI/ZoneAndChestMenu.cs`
- `Dayswork/i18n/default.json`

### Existing regression areas refreshed

- `Dayswork.Tests/Generators/DepositInputGen.cs`
- `Dayswork.Tests/Inventory/DepositPlannerTests.cs`
- `Dayswork.Tests/Shifts/ShiftPlanBuilderTests.cs`
- `Dayswork.Tests/U21/ShiftContextTests.cs`

## Created files

### New scope/provenance and categorization seams

- `Dayswork.Core/Domain/OutputScopeFamily.cs`
- `Dayswork.Core/Domain/OutputScopeProvenance.cs`
- `Dayswork.Core/Inventory/RoutedItemStack.cs`
- `Dayswork.Core/Inventory/OverflowCategory.cs`
- `Dayswork.Core/Inventory/OverflowCategorizer.cs`

### Dedicated U-22 regression coverage

- `Dayswork.Tests/U22/U22PropertyGenerators.cs`
- `Dayswork.Tests/U22/OverflowCategorizerTests.cs`
- `Dayswork.Tests/U22/OverflowCategorizerPropertyTests.cs`
- `Dayswork.Tests/U22/ScopeDrivenRuntimeAlignmentTests.cs`

## Implementation notes

- `ShiftOrchestrator` now normalizes runtime scope from `Contract.ScopeSelection`, classifies it through `WorkScopeClassifier`, and stores the resulting `WorkScopeSet` on `ShiftContext`.
- `ShiftPlanBuilder` no longer infers batch families from compatibility zones. It now shapes deterministic skeleton batches directly from typed scope and task families.
- Runtime animal work now splits into:
  - per-building interior animal batches for feed and indoor care/product collection
  - a farm-level outdoor-animal batch for roaming animals and outdoor animal-product pickup
- `WorkAreaScanner`, `IndoorWorkScanner`, `WorkItem`, `AnimalWorkItem`, and `ItemBuffer` now carry scope provenance so downstream overflow explanation remains accurate.
- `DepositPlanner` still resolves destinations purely from `TaskKind`, but preserves provenance in `RoutedItemStack` so chest-full, chest-missing, no-destination, and not-delivered cases can be categorized after routing.
- `OverflowCategorizer` owns the deterministic `(reason, scope family, scope name)` grouping so `MailDispatcher` only renders already-classified categories.

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false` passed with `0` errors and `0` warnings.
- `dotnet test Dayswork.sln` passed with `260` tests passing and `1` expected skip.

## Deliberate deferrals

- U-22 does not revisit recurring billing, deposit arithmetic, or compatibility pricing fields. Those remain outside this runtime-alignment unit.
- U-22 keeps compatibility `Zones` persistence for transitional consumers, but supported live execution no longer derives runtime work scope from that compatibility projection.
