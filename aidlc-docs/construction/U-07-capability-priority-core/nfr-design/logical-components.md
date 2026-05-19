# U-07 Capability & Priority Core — Logical Components

**Unit**: U-07 — Capability & Priority Core

---

## Production component map

```
Dayswork.Core/Domain/
  ToolLevel.cs          (enum: Basic=0..Iridium=4)
  ToolSnapshot.cs       (record: AxeLevel, PickaxeLevel, WateringCanLevel)

Dayswork.Core/Capabilities/
  AxeTarget.cs          (enum: StandingTree, FruitTree, SmallStump, LargeStump, LargeLog)
  PickTarget.cs         (enum: SmallRock, LargeBoulder, Meteorite)
  CapabilityMatrix.cs   (static class: CanChop, CanBreak — spec threshold table)
  ICapabilityEvaluator.cs
  CapabilityEvaluator.cs  (delegates CanChop/CanBreak to CapabilityMatrix)

Dayswork.Core/Shifts/
  ITaskPriorityOrderer.cs
  TaskPriorityOrderer.cs  (LINQ OrderBy over static _rank dictionary)
```

---

## Dependency flow (production)

```
ToolLevelReader (U-10, Mod layer)
  |  reads Game1.player tool UpgradeLevels, casts to ToolLevel
  v
ToolSnapshot  ------>  ICapabilityEvaluator
                              |
                              | delegates
                              v
                       CapabilityMatrix (static)
                              |
                              | returns bool
                              v
                       ShiftOrchestrator (U-10, Mod layer)
                              |
                              | skips tile or queues task

Contract.TaskKinds  -->  ITaskPriorityOrderer
                              |
                              | returns IReadOnlyList<TaskKind>
                              v
                       ShiftOrchestrator (U-10, Mod layer)
                              |
                              | processes tasks in priority order
```

---

## DI wiring (planned for U-10 ModEntry composition root)

```csharp
// ModEntry.cs — added in U-10
ICapabilityEvaluator capabilityEvaluator = new CapabilityEvaluator();
ITaskPriorityOrderer taskPriorityOrderer = new TaskPriorityOrderer();
// passed into ShiftOrchestrator constructor
```

Neither component has mutable state — a single instance per mod lifetime is sufficient.

---

## Test component map

```
Dayswork.Tests/Generators/
  ToolSnapshotGen.cs    (PBT-07: Arbitrary<ToolSnapshot> via Gen.Elements over ToolLevel)

Dayswork.Tests/Capabilities/
  CapabilityEvaluatorTests.cs
    - [Fact] exhaustive table: 5 AxeLevel x 5 AxeTarget = 25 CanChop cases
    - [Fact] exhaustive table: 5 PickLevel x 3 PickTarget = 15 CanBreak cases
    - [Fact] FR-SKIP-03: FruitTree always false for all 5 axe levels (explicit named test)
    Total: 41 [Fact] tests

Dayswork.Tests/Shifts/
  TaskPriorityOrdererTests.cs
    - [Fact] all-10-tasks case: output matches FR-WORK-03 order exactly
    - [Fact] single-task case: list of 1
    - [Fact] empty input: empty list
    - [Property] PBT-03 determinism: forall subset → Order(x).SequenceEqual(Order(x))
    - [Property] PBT-03 ordering: forall subset → consecutive elements satisfy rank[i] < rank[i+1]
    Total: 3 [Fact] + 2 [Property] tests
```

---

## Infrastructure components

None. U-07 introduces no queues, caches, circuit breakers, external services, or
cloud resources. All components are in-process pure .NET objects.
