# Unit Test Execution — Dayswork SMAPI Mod

## Overview

Unit tests live in `Dayswork.Tests/` and run entirely without Stardew Valley or SMAPI present.
Current baseline: **211 passed, 1 expected skip, 0 failures**.

The 1 skip is intentional: `PBT08_seed_and_shrunk_input_logged_on_failure` is a
seed-logging smoke test that only shows meaningful output on failure — it is marked
`[Fact(Skip = "...")]` by design.

## Run Unit Tests

### Execute All Tests

```bash
dotnet test Dayswork.sln
```

Expected output:
```
Passed!  - Failed: 0, Passed: 211, Skipped: 1, Total: 212, Duration: ~5s - Dayswork.Tests.dll (net6.0)
```

### Run a Specific Test Class

```bash
dotnet test Dayswork.sln --filter "FullyQualifiedName~Config"
```

### Run with Verbose Output

```bash
dotnet test Dayswork.sln --logger "console;verbosity=detailed"
```

## Test Coverage by Area

| Namespace | What It Covers |
|-----------|---------------|
| `Dayswork.Tests.Config` | `ModConfigManager` — defaults, mutation, reset, save/publish |
| `Dayswork.Tests.Config.Mapping` | `RuntimeConfigSnapshotMapper` — range clamping, default equivalence, invalid input |
| `Dayswork.Tests.Shifts` | `ShiftContext` — cost computation, refund logic, HHMM→minutes conversion |
| `Dayswork.Tests.Scheduling` | `HiringScheduler` guard chain — affordability, festival, rain surcharge |
| `Dayswork.Tests.Deposits` | `DepositHoursPolicy` — flat-hour policy vs raw estimation |
| `Dayswork.Tests.Mail` | `MailDispatcher` — settlement, overflow, festival/cannot-afford notice queuing |
| `Dayswork.Tests.Lint` | `HardcodedUserFacingStringLintTests` — scans Dayswork source for literals outside `I18nHelper` callsites |
| `Dayswork.Tests.Smoke` | Property-based testing seed-logging demo (1 intentional skip) |

## Fixing Failing Tests

If `dotnet test` reports failures:

1. Note the test name and failure message in the console output.
2. Run the specific failing test with verbose output:
   ```bash
   dotnet test Dayswork.sln --filter "FullyQualifiedName~<TestName>" --logger "console;verbosity=detailed"
   ```
3. Identify whether the failure is in production code or the test fixture.
4. Fix the issue, rebuild (`dotnet build Dayswork.sln /p:EnableModDeploy=false`), and rerun tests.
5. Do **not** commit with any non-skip failures.

## Lint Gate

The `Dayswork.Tests/Lint/HardcodedUserFacingStringLintTests` suite performs source-level
scanning for hardcoded user-visible English strings outside approved `I18nHelper` callsites.

If the lint test fails, locate the flagged file and key, add the string to the i18n JSON at
`Dayswork/i18n/default.json`, and replace the literal with `I18nHelper.Get("your.key")`.
