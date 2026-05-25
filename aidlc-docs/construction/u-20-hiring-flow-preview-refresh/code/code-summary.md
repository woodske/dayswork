# U-20 — Hiring Flow Preview Refresh: Code Summary

## Overview

U-20 replaced the visible hire/edit pricing flow with a redesign-era typed-scope preview pipeline while deliberately keeping runtime billing and execution compatibility bridges alive for later retrofit units.

The key result is:
- Screen 1 now previews fixed-price service contributions instead of hourly rate
- Screen 2 now treats work scope as outdoor zones, animal buildings, and greenhouse
- output destinations now live on their own dedicated page between work scope and schedule
- the scroll-heavy screens now use Stardew's shop-style scrollbar textures/pattern instead of a custom-drawn hint
- the final review screen now reviews fixed price, worker stamina, payment timing, and invalid-preview reasons
- edit flow now hydrates typed scope and reopens at review first

## Modified files

- `Dayswork/ModEntry.cs`
  - wires the new `ContractTermsBuilder` dependency graph into `HiringFlowCoordinator`
- `Dayswork/UI/ContractDraft.cs`
  - replaced the old `Zones`-centric draft with typed scope, hydration mode, and preview/review carriers
- `Dayswork/UI/HiringFlowCoordinator.cs`
  - removed whole-farm fallback, added synchronous preview refresh, review-first edit hydration, and confirm-time typed-scope + terms persistence
- `Dayswork/UI/ScheduleMenu.cs`
  - changed schedule selection to drive coordinator refresh instead of just mutating raw state
- `Dayswork/UI/SummaryMenu.cs`
  - replaced estimated hours / rate / deposit / refund copy with fixed price, typed scope, worker stamina, validation-driven confirm gating, and wrapped long review lines so task lists and payment-timing copy stay inside the panel; follow-up review fixes corrected both line preservation and newline splitting from `Game1.parseText(...)`, then moved the review body into a fixed-height scrollable region, and later switched to Stardew's shop-style scrollbar visuals while deduplicating repeated validation messages by validation code
- `Dayswork/UI/TaskSelectionMenu.cs`
  - replaced hourly-rate preview with coordinator-provided service contribution rows and explicit “needs scope” states; follow-up review fixes first made the task list scrollable with hover text, then restored the compact row shape, moved the included-price/scope state entirely into the tooltip, and replaced the text hint with Stardew's shop-style scrollbar visuals
- `Dayswork/UI/ZoneAndChestMenu.cs`
  - now serves as a focused work-scope page only, with the work-scope action buttons lowered and kept side-by-side for breathing room under the title
- `Dayswork/UI/ZoneDrawMenu.cs`
  - now restores and returns typed outdoor/building selections instead of reconstructing them from generic draft zones
- `Dayswork/i18n/default.json`
  - added the redesign-era hire/edit copy and removed U-20 screen dependence on hour/deposit/refund wording; later adjusted page titles to split work scope from output destinations

## Created files

- `Dayswork/UI/MenuScrollBar.cs`
  - shared scrollbar drawing and drag/click behavior using Stardew's shop-style scrollbar textures/pattern across the long-content menus
- `Dayswork/UI/OutputDestinationsMenu.cs`
  - dedicated output-destination page extracted from the congested old scope/output screen, with chest picker behavior and scrollable destination rows
- `Dayswork/UI/TaskPresentation.cs`
  - central task ordering and i18n-key mapping for deterministic screen output
- `Dayswork/UI/LegacyScopeBootstrapper.cs`
  - narrow compatibility helper for edit hydration and confirm-time compatibility-zone projection
- `Dayswork/UI/HiringFlowViewModelBuilder.cs`
  - pure view-model builder for service rows, scope summary, payment timing, and review state
- `Dayswork.Tests/UI/HiringFlowViewModelBuilderTests.cs`
  - focused example coverage for missing-scope behavior, greenhouse validity, recurring-edit timing, and legacy bootstrap helpers
- `Dayswork.Tests/UI/HiringFlowViewModelPropertyTests.cs`
  - FsCheck coverage for deterministic equivalent-draft output and schedule/destination non-pricing invariants

## Behavior notes

- `ContractDraft` is now typed-scope-first:
  - `OutdoorZones`
  - `AnimalBuildings`
  - `Greenhouse`
  - `HydrationMode`
  - `PreviewState`
- `HiringFlowCoordinator` now owns the only preview refresh path:
  - task and scope changes rebuild `ContractPreview`
  - schedule changes only rebuild review/payment-timing view models
  - destination changes stay orthogonal to pricing preview
- edit hydration prefers saved `ScopeSelection`, then falls back once to compatibility bootstrap from legacy `Zones`
- confirmation now persists:
  - `ScopeSelection`
  - `TermsSnapshot`
  - derived compatibility `Zones`
- the late review-polish pass now keeps all three long-content screens inside fixed-height content windows:
  - Screen 1 uses a shop-style scrollbar and tooltip-only state text while preserving compact task rows
  - Screen 2 is now work-scope only, with more breathing room under the title
  - the new output-destinations page owns chest/mail/bin routing separately from work scope
  - the review screen uses the same shop-style scrollbar for long bodies and collapses repeated validation codes into one message each

## Compatibility bridge retained intentionally

U-20 keeps old compatibility financial fields alive on confirmed `Contract` instances:
- `DepositAmount`
- `HourlyRate`

Those are still produced from the historical compatibility calculator so `U-15` runtime and recurring lifecycle consumers keep working until `U-21` and `U-23` replace them.

This means U-20 intentionally updates the **visible** pricing model before the **runtime/day-start billing** model is fully refreshed. That remaining semantic alignment is deferred by design:
- `U-21` worker energy + shift runtime refresh
- `U-22` scope-driven runtime alignment
- `U-23` recurring billing + calendar refresh

## Verification

- `dotnet build Dayswork.sln /p:EnableModDeploy=false`
  - passed with `0` errors and `0` warnings
- `dotnet test Dayswork.sln`
  - passed with `246` tests passing and `1` expected skip
