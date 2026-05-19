# Dayswork.Tests

Test project for Dayswork — pure-Core xUnit + FsCheck property-based tests.

## Project purpose

This project tests `Dayswork.Core` only. It **cannot** reference `Dayswork` (the SMAPI mod project).
This is enforced at compile time: the `ProjectReference` in `Dayswork.Tests.csproj` points only to
`Dayswork.Core.csproj`. Any accidental SMAPI coupling is caught as a build error before it reaches CI.

## Test framework

- **xUnit 2.6.2** — standard unit and integration tests
- **FsCheck.Xunit 2.16.5** — property-based testing (PBT-09 recommendation for C#/.NET)

## Where tests live

Test files mirror the `Dayswork.Core/` directory layout:

| Test directory | Tests Core directory |
|---|---|
| `Dayswork.Tests/Config/` | `Dayswork.Core/Config/` |
| `Dayswork.Tests/Pricing/` | `Dayswork.Core/Pricing/` |
| `Dayswork.Tests/Zones/` | `Dayswork.Core/Zones/` |
| `Dayswork.Tests/Workers/` | `Dayswork.Core/Workers/` |
| `Dayswork.Tests/Smoke/` | Framework smoke tests (this unit) |

## Generators (PBT-07)

All FsCheck generators live in `Dayswork.Tests/Generators/`. Foundation units add domain-specific
generators here as they are built:

| Generator | Added by | Generates |
|---|---|---|
| *(empty placeholder)* | U-02 | Establishes the namespace |
| `ConfigSnapshotGen` | U-03 | `ConfigSnapshot` arbitrary |
| `ZoneGen`, `TileCoordGen` | U-04 | `Zone`, `TileCoord` arbitraries |
| `ContractGen` | U-06 | `HireContract` arbitrary |

Reference generators in tests via:

```csharp
// Per-test annotation
[Property(Arbitrary = new[] { typeof(DaysworkGenerators) })]
public bool MyProperty(Zone z) { ... }

// Or register globally in test class constructor
Arb.Register<DaysworkGenerators>();
```

## Seed logging (PBT-08)

FsCheck.Xunit's `[Property]` attribute prints the seed and shrunk minimal failing input on failure
**automatically** — no custom plumbing required.

Example failure output:
```
Falsifiable, after 23 tests (1 shrink) (StdGen (123456789,987654321)):
Original: <some complex input>
Shrunk:   <minimal failing input>
```

To replay a known failure:

```csharp
[Property(Replay = "(123456789, 987654321)")]
public bool MyProperty(int x) { ... }
```

The `Smoke/SeedLoggingDemoTests.cs` file contains a disabled example that demonstrates this
behavior. Remove the `Skip` attribute and run `dotnet test` to see the seed + shrunk-input output.

## Running tests locally

```shell
dotnet test Dayswork.sln
```

Or to run only this project:

```shell
dotnet test Dayswork.Tests/Dayswork.Tests.csproj
```

## CI (PBT-09)

Build-and-Test wiring is deferred to U-16. The existing test output already includes seed values
(FsCheck.Xunit default behavior), so the only CI requirement is to capture stdout.
