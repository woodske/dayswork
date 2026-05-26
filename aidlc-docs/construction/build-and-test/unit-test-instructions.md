# Unit Test Instructions — Dayswork Fixed-Price Redesign

## Main Test Command

Run the full automated suite with:

```bash
dotnet test Dayswork.sln
```

Current verified baseline at U-24 closeout:

```text
Passed!  - Failed: 0, Passed: 286, Skipped: 1, Total: 287
```

## Focus Areas

### Config and normalization

- redesign-only `ModConfig` shape
- deterministic `RuntimeConfigSnapshotMapper` publication
- `ConfigValueResolver` narrow fallback behavior
- fixed-price thresholds, package tables, and worker action-cost snapshots

### Pricing and contract terms

- fixed-price scope breakdown generation
- greenhouse, outdoor, and animal-building pricing splits
- worker energy profile snapshots
- recurring rebuild determinism

### Runtime and output pipeline

- task-owned destination routing
- overflow categorization and next-morning mail behavior
- tool snapshot and skip rules
- worker energy ledger, stop reasons, and work-unit boundaries
- stuck detection and wrap-up guarantees

### Final cleanup regressions

- redesign GMCM publication
- multiplayer bulletin-board refusal behavior
- hit-reaction trigger logic
- i18n lint boundary

## Useful Filters

Run a focused slice with:

```bash
dotnet test Dayswork.sln --filter "FullyQualifiedName~Config"
dotnet test Dayswork.sln --filter "FullyQualifiedName~U24"
dotnet test Dayswork.sln --filter "FullyQualifiedName~Overflow"
```

## Failure Handling

1. Reproduce the failure with a focused `--filter`.
2. Decide whether the break is in production behavior, test expectations, or stale redesign documentation.
3. Fix the issue.
4. Rerun `dotnet build Dayswork.sln /p:EnableModDeploy=false`.
5. Rerun `dotnet test Dayswork.sln`.

## Lint Gate

`Dayswork.Tests/Lint/HardcodedUserFacingStringLintTests.cs` remains part of the standard suite.

If it fails:

1. Move the player-visible string into `Dayswork/i18n/default.json`.
2. Replace the hardcoded literal with `I18nHelper.Get(...)`.
3. Rerun the suite.
