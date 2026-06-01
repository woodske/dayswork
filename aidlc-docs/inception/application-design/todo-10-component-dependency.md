# Component Dependency Addendum - TODO-10 SVE Grandpa's Shed Greenhouse

## Purpose
This addendum documents dependency relationships and communication patterns for TODO-10.

## Dependency Matrix

| Component | Depends on | Used by | Notes |
|---|---|---|---|
| `ExpansionRouteModel` | `TileCoord`, `FarmMapSignature` | `IExpansionProfile`, `SveExpansionProfile`, tests | Pure Core data only. |
| `ExpansionLocationDescriptor` | none beyond Core primitives | `SveExpansionProfile`, `ExpansionCompatService` | Pure work/destination metadata. |
| `IExpansionProfile` route extension | route model, location descriptors | `ExpansionProfileSelector`, `ExpansionCompatService` | Keeps expansion data centralized. |
| `SveExpansionProfile` route data | route model, descriptors | `ExpansionCompatService`, tests | Single SVE identifier/data home. |
| `ExpansionCompatService` | active `IExpansionProfile`, live `GameLocation` APIs, passability checks | `ShiftOrchestrator`, `ChestResolver`, UI discovery adapters | Mod-side bridge from pure data to live validation. |
| `CrossLocationRouteNavigator` | `WorkerMovementDriver`, `BuildingWorkNavigator` primitives | `ShiftOrchestrator` | Executes hops but does not own shift policy. |
| `ChestResolver` expansion discovery | `ExpansionCompatService` | hiring UI and deposit setup | Adds virtual expansion chest/building entries. |
| `LegacyScopeBootstrapper` classification | existing greenhouse classification, optional compat bridge | hiring UI | Keeps single greenhouse selection. |
| `ShiftOrchestrator` route policy | `ExpansionCompatService`, `CrossLocationRouteNavigator`, existing deposit/work services | runtime shift execution | Owns skip/continue and item safety decisions. |

## Communication Patterns

### Route Data Lookup
1. `ShiftOrchestrator` or `ChestResolver` identifies a target expansion location.
2. `ExpansionCompatService` computes the live farm signature.
3. `ExpansionCompatService` asks the active `IExpansionProfile` for a pure route definition.
4. The active profile returns route data or no route.
5. `ExpansionCompatService` validates the route against live locations and passability.
6. Consumers receive either a validated route or a failure reason.

### Work Route Execution
1. `ShiftOrchestrator` receives a validated work route.
2. `ShiftOrchestrator` starts `CrossLocationRouteNavigator`.
3. `CrossLocationRouteNavigator` walks/warps each hop using movement primitives.
4. `CrossLocationRouteNavigator` reports completion or failure.
5. `ShiftOrchestrator` queues greenhouse work or skips the batch.

### Deposit Route Execution
1. `ShiftOrchestrator` receives a deposit trip for an expansion chest.
2. `ShiftOrchestrator` validates and starts the expansion route.
3. `CrossLocationRouteNavigator` moves the worker into the destination location.
4. Existing chest stand-tile and deposit logic handles the actual item transfer.
5. On failure, `ShiftOrchestrator` routes items through existing undelivered/overflow handling.

### Discovery
1. `ChestResolver` gets normal farm/building data.
2. `ChestResolver` asks `ExpansionCompatService` for expansion discovery entries.
3. Virtual shed greenhouse outline becomes greenhouse-selectable.
4. Shed greenhouse/main shed chests become deposit entries.
5. No generic location-name scan is used.

## Data Ownership Rules
- SVE route IDs, SVE location names, and farm-signature route tables live in `SveExpansionProfile`.
- Live location validation lives in `ExpansionCompatService`.
- Route-hop execution lives in `CrossLocationRouteNavigator`.
- Shift outcomes live in `ShiftOrchestrator`.
- Chest availability and item transfer remain in existing deposit/chest components.

## Coupling Constraints
- Core cannot reference SMAPI or Stardew types.
- General orchestration code cannot hardcode SVE location strings.
- Route validation must not inspect SVE quest/event/mail flags as scheduling authority.
- The save schema and `ContractScopeSelection` remain unchanged.
- A future expansion should add profile data or a profile implementation rather than editing vanilla route code.

## Content Validation
- No Mermaid diagrams.
- No ASCII diagrams.
- Data flow is represented as numbered text lists and dependency tables.
