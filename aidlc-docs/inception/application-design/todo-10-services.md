# Services Addendum - TODO-10 SVE Grandpa's Shed Greenhouse

## Purpose
This file describes the application-service orchestration pattern for TODO-10. It focuses on service responsibilities and interactions, not detailed route business rules.

## Service Overview

| Service | Role in TODO-10 |
|---|---|
| `ExpansionCompatService` | Converts active profile route/location data into live validated routes and virtual discovery entries. |
| `CrossLocationRouteNavigator` | Executes ordered route hops after validation. |
| `ShiftOrchestrator` | Owns shift state, batch skip policy, deposit failure policy, and integration with existing work/deposit flows. |
| `ChestResolver` | Adds expansion destination chests exposed by the compat service. |
| `LegacyScopeBootstrapper` | Classifies virtual shed greenhouse outline as a single greenhouse selection. |
| `BuildingWorkNavigator` | Remains the existing single-building entry/exit helper; supplies primitives reused by the route navigator where useful. |

## Composition Root

`ModEntry` should construct and wire services in this order:

1. Existing Core compat objects: `VanillaExpansionProfile`, `SveExpansionProfile`, `ExpansionProfileSelector`, and `AnimalBuildingCapacityPolicy`.
2. `ExpansionCompatService`, extended with route/location bridge behavior.
3. `BuildingWorkNavigator` and existing movement services.
4. New `CrossLocationRouteNavigator`, depending on movement and building-navigation primitives.
5. `ChestResolver`, extended or constructed with access to `ExpansionCompatService` for virtual expansion chest entries.
6. `ShiftOrchestrator`, receiving `ExpansionCompatService` and `CrossLocationRouteNavigator` through constructor injection.

If a static bridge is still needed for existing static UI helpers, it should delegate to the same singleton `ExpansionCompatService`; there should not be a second SVE data source.

## Work Entry Flow

1. `ShiftOrchestrator` starts a `BatchKind.Greenhouse` batch.
2. If the batch location is a normal greenhouse, existing `BuildingWorkNavigator` behavior is used.
3. If the batch location is an expansion greenhouse alternative, `ShiftOrchestrator` asks `ExpansionCompatService` for a validated work route.
4. If validation succeeds, `ShiftOrchestrator` starts `CrossLocationRouteNavigator`.
5. `CrossLocationRouteNavigator` walks and transitions through each route hop.
6. On completion, `ShiftOrchestrator` queues existing greenhouse crop work in the target location.
7. On validation or navigation failure, `ShiftOrchestrator` logs a maintainer-facing reason and skips the batch.

## Deposit Flow

1. `DepositPlanner` produces existing deposit trips.
2. `ShiftOrchestrator` starts a trip for a chest in `Custom_GrandpasShedGreenhouse` or `Custom_GrandpasShed`.
3. `ShiftOrchestrator` asks `ExpansionCompatService` for a validated deposit route.
4. If validation succeeds, `CrossLocationRouteNavigator` executes the route and existing chest stand-tile/deposit behavior takes over.
5. If validation or navigation fails, `ShiftOrchestrator` uses existing undelivered-item/overflow handling for that trip.
6. Existing shipping-bin and farm-chest deposit behavior remains unchanged.

## UI and Discovery Flow

1. `ChestResolver.GetBuildingOutlines(farm)` includes normal farm buildings as today.
2. It appends expansion virtual outlines supplied by `ExpansionCompatService`.
3. `LegacyScopeBootstrapper.TryClassify` sees `Custom_GrandpasShedGreenhouse` as greenhouse-like and keeps the single `GreenhouseSelection(LocationName)` model.
4. `ChestResolver.GetAllChests(farm)` includes farm chests and normal building chests as today.
5. It appends chests found in expansion deposit locations supplied by `ExpansionCompatService`, including shed greenhouse and main shed.
6. Main shed chest entries are deposit destinations only; they do not make the main shed a work-scope location.

## Failure Policy

| Failure | Owner | Outcome |
|---|---|---|
| Route definition absent | `ShiftOrchestrator` after `ExpansionCompatService` result | Skip shed greenhouse work batch or mark deposit trip undelivered. |
| Live location missing | `ExpansionCompatService` validation result | Return failure reason; no exception. |
| Approach/arrival tile unusable | `ExpansionCompatService` validation result | Return failure reason; no exception. |
| Navigation fails mid-route | `CrossLocationRouteNavigator` status, interpreted by `ShiftOrchestrator` | Skip work batch or mark deposit trip undelivered. |
| Chest missing/full/busy | Existing deposit code | Existing overflow reasons and mail behavior. |

## Vanilla Path
- With SVE absent, the profile returns no expansion routes or locations.
- With SVE present but no shed greenhouse selected, normal greenhouse and farm behavior remains unchanged.
- With route unavailable, behavior changes only for the explicitly selected shed greenhouse batch or matching deposit trip.
