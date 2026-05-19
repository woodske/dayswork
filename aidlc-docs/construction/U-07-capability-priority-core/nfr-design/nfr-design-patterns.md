# U-07 Capability & Priority Core — NFR Design Patterns

**Unit**: U-07 — Capability & Priority Core
**NFRs addressed**: NFR-MAINT-03, PBT-03, PBT-07

---

## Pattern 1: Pure Function Isolation

**Addresses**: NFR-MAINT-03 — "Pure business-logic modules are separated from
SMAPI/game-engine integration so they can be unit-tested without launching Stardew."

### Problem

`CapabilityEvaluator` and `TaskPriorityOrderer` need tool upgrade levels and task
enums from the game, but cannot reference `Game1`, `StardewValley.*`, or
`StardewModdingAPI.*` — they must be testable without a running game instance.

### Solution: Extraction at the Mod boundary

All game state is extracted *before* entering Core. The Mod layer (`ToolLevelReader`,
U-10) reads the player's tool upgrade levels from `Game1.player` at 6am, casts the
integer values to `ToolLevel` enum values, and builds an immutable `ToolSnapshot`.
That snapshot is then passed into `ICapabilityEvaluator` as a plain record.

```
[SMAPI / Stardew Valley game engine]
         |
         | ToolLevelReader reads Game1.player tool UpgradeLevels at 6am
         | casts int → ToolLevel enum
         v
[ToolSnapshot record]                  <- Dayswork.Core/Domain/
         |
         | passed as parameter
         v
[ICapabilityEvaluator.CanChop(snap, target)]   <- Dayswork.Core/Capabilities/
[ICapabilityEvaluator.CanBreak(snap, target)]
         |
         | returns: bool
         v
[ShiftOrchestrator]                    <- Dayswork/ (Mod project)
         |
         | skips tile or queues task
         v
[SMAPI / NPC pathfinding]
```

`TaskPriorityOrderer` receives only `IEnumerable<TaskKind>` — a pure .NET enum
collection with no game-engine coupling.

### Enforcement mechanism

`Dayswork.Core.csproj` references only `Newtonsoft.Json`. Any accidental
`using StardewValley;` in `Dayswork.Core/Capabilities/` or `Dayswork.Core/Shifts/`
causes a **compile error** — automated enforcement, no code review needed.

---

## Pattern 2: Static Threshold Table (CapabilityMatrix)

**Addresses**: NFR-MAINT-03 (readable, explicit spec-mapping); supporting PBT-03
(table is the ground truth the exhaustive tests verify against).

### Problem

The spec's tool-inheritance table is a matrix of (tool level, object class) → bool.
This needs to be encoded in a way that:
- Directly maps to the spec table (easy to audit against the spec)
- Is uninstantiable (it's pure lookup data, not an object with lifecycle)
- Makes the FruitTree hard rule visible and impossible to accidentally bypass

### Solution: `static class` with threshold comparisons

`CapabilityMatrix` is a C# `static class` — it cannot be instantiated (`new` is a
compile error) and all members are `static`. The capability rules are encoded as
threshold comparisons directly matching the spec table:

```csharp
// CapabilityMatrix.CanChop — direct spec mapping
public static bool CanChop(ToolLevel axeLevel, AxeTarget target) => target switch
{
    AxeTarget.FruitTree  => false,                        // FR-SKIP-03: always skip, no threshold
    AxeTarget.LargeLog   => axeLevel >= ToolLevel.Gold,   // Gold+ only
    AxeTarget.LargeStump => axeLevel >= ToolLevel.Steel,  // Steel+ only
    _                    => true,                         // StandingTree, SmallStump: any level
};

// CapabilityMatrix.CanBreak — direct spec mapping
public static bool CanBreak(ToolLevel pickLevel, PickTarget target) => target switch
{
    PickTarget.Meteorite    => pickLevel >= ToolLevel.Gold,   // Gold+ only
    PickTarget.LargeBoulder => pickLevel >= ToolLevel.Steel,  // Steel+ only
    _                       => true,                          // SmallRock: any level
};
```

**FruitTree unconditional first**: the `FruitTree` branch is evaluated before any
level comparison. No future refactor can inadvertently introduce a level-threshold
path for fruit trees.

**C# note (switch expression)**: The `=> target switch { ... }` pattern is C# 8+
pattern matching. The `_` arm is the default/catch-all. The compiler warns if any
enum value is unhandled — adding a new `AxeTarget` value will produce a warning
at the switch site, making spec expansions visible.

---

## Pattern 3: FsCheck Generator for ToolSnapshot (PBT-07)

**Addresses**: PBT-07 — shared generators for types introduced in this unit,
available to downstream units.

### Problem

U-10 (`ShiftStateMachine`), U-13 (extended state machine + orchestrator), and any
future unit that receives a `ToolSnapshot` need to generate arbitrary snapshots for
property tests. Without a shared generator, each unit would re-implement the same
arbitrary.

### Solution: `ToolSnapshotGen` in the shared Generators namespace

```csharp
// Dayswork.Tests/Generators/ToolSnapshotGen.cs
public static class ToolSnapshotGen
{
    // Uniform distribution over all 5 ToolLevel values
    private static readonly Arbitrary<ToolLevel> ArbToolLevel =
        Arb.From(Gen.Elements(
            ToolLevel.Basic, ToolLevel.Copper, ToolLevel.Steel,
            ToolLevel.Gold, ToolLevel.Iridium));

    public static Arbitrary<ToolSnapshot> ArbToolSnapshot =>
        Arb.From(
            from axe  in ArbToolLevel.Generator
            from pick in ArbToolLevel.Generator
            from can  in ArbToolLevel.Generator
            select new ToolSnapshot(axe, pick, can));
}
```

**Distribution note**: `Gen.Elements` gives uniform distribution across the 5 levels.
FsCheck will also shrink toward `ToolLevel.Basic` (lowest int value) on failure — this
is the desired shrink direction (simpler/lower-level snapshots are easier to debug).

**Usage in downstream tests**:
```csharp
Prop.ForAll(ToolSnapshotGen.ArbToolSnapshot, snap => { /* test here */ })
```

---

## Summary: patterns × NFRs

| Pattern | NFR-MAINT-03 | PBT-03 | PBT-07 |
|---|---|---|---|
| Pure Function Isolation | Primary | Enabled by | Enabled by |
| Static Threshold Table | Supporting | Ground truth for exhaustive tests | — |
| ToolSnapshotGen | — | — | Primary |
