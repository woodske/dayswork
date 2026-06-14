# Dayswork.Tests

xUnit + FsCheck test project for Dayswork.

## What it covers

The suite mostly targets the pure logic in `Dayswork.Core` (pricing, energy, the shift state
machine, deposit planning, persistence/migration, routing selection), plus the game-free parts of
the `Dayswork` mod assembly (UI layout toolkit, view-model builders, player-state snapshot
guards). Because the project references `Dayswork`, building it requires a local Stardew Valley
install (resolved by `Pathoschild.Stardew.ModBuildConfig`), the same as building the mod itself.

## Test framework

- **xUnit 2.6.2** — standard unit tests
- **FsCheck.Xunit 2.16.5** — property-based tests

## Layout

Test folders mirror the source layout by behavior (`Pricing/`, `Energy/`, `Inventory/`, `Shifts/`,
`Scheduling/`, `Routing/`, `Persistence/`, `Geometry/`, `Compat/`, `Config/`, `UI/`, …). Shared
FsCheck generators live in `Generators/` (anchored by `DaysworkGenerators`).

Reference generators in tests via:

```csharp
// Per-test annotation
[Property(Arbitrary = new[] { typeof(DaysworkGenerators) })]
public bool MyProperty(Zone z) { ... }

// Or register globally in test class constructor
Arb.Register<DaysworkGenerators>();
```

## Seed logging

FsCheck.Xunit's `[Property]` attribute prints the seed and shrunk minimal failing input on failure
**automatically** — no custom plumbing required.

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

`Smoke/SeedLoggingDemoTests.cs` contains a disabled example — remove its `Skip` attribute to see
the output.

## Running tests

```shell
dotnet test Dayswork.Tests/Dayswork.Tests.csproj
```
