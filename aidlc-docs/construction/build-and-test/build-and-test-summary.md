# Build and Test Summary — Dayswork Fixed-Price Redesign

## Build Status

| Item | Detail |
|---|---|
| Build tool | .NET 6 SDK / MSBuild |
| Compile command | `dotnet build Dayswork.sln /p:EnableModDeploy=false` |
| Test command | `dotnet test Dayswork.sln` |
| Build status | Success |
| Build marker | `build=U24-Step19` |
| Live deploy behavior | `dotnet test Dayswork.sln` currently builds with deploy enabled for the mod project before the test project runs |

## Verified Totals

| Metric | Result |
|---|---|
| Passed | 286 |
| Failed | 0 |
| Skipped | 1 expected skip |
| Total | 287 |

The expected skip remains `Dayswork.Tests.Smoke.SeedLoggingDemoTests.PBT08_seed_and_shrunk_input_logged_on_failure`.

## U-24 Coverage Added or Refreshed

- redesign-only `ModConfig` surface and deterministic runtime snapshot mapping
- GMCM pricing, stamina, and worker-behavior publication
- `ConfigValueResolver` narrow fallback behavior
- task-owned output routing and preserved scope provenance
- capability snapshot invariants
- stuck recovery reset behavior
- multiplayer bulletin-board interaction policy
- worker hit-reaction trigger policy
- player-visible i18n lint boundary

## Final Automated Regression Checklist

- [x] No hourly/deposit-era knobs remain in `ModConfig` or GMCM
- [x] GMCM shows only `Pricing`, `Worker Stamina`, and `Worker Behavior`
- [x] `RuntimeConfigSnapshotMapper` publishes deterministic redesign-era snapshots
- [x] Internal legacy hourly/deposit compatibility values are derived only behind non-player-facing seams
- [x] Output routing and overflow behavior still pass automated coverage
- [x] Tool snapshot and skip rules still pass automated coverage
- [x] Stuck recovery, hit-reaction logic, and multiplayer refusal still pass automated coverage
- [x] The hardcoded-string lint gate still passes
- [x] Build/test docs now describe fixed pricing and worker stamina instead of deposits/refunds

## Manual Playtest Checklist

- [ ] Bulletin board entry works in single-player and stays blocked in multiplayer
- [ ] One-time review/purchase flow shows fixed pricing and typed scope correctly
- [ ] Recurring day-start billing, festival skip, and notice behavior match the redesign
- [ ] Worker stamina, pacing, wrap-up, and scope-driven execution all behave correctly in-game
- [ ] Output destinations, overflow mail, and greenhouse/animal scope behavior all remain clear

## Units Delivered in This Redesign Pass

| Unit | Focus |
|---|---|
| U-18 | Contract terms foundation |
| U-19 | Contract snapshot persistence |
| U-20 | Hiring flow preview refresh |
| U-21 | Worker energy and shift runtime refresh |
| U-22 | Scope-driven runtime alignment |
| U-23 | Recurring billing and calendar refresh |
| U-24 | Config, regression, and documentation cleanup |
