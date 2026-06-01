# Code Generation Summary - u-t10-shed-greenhouse-routing

**Unit**: `u-t10-shed-greenhouse-routing`
**Stage**: Construction / Code Generation
**Status**: Complete, review required

## Implemented Changes

| Area | Files | Summary |
|---|---|---|
| Core route model | `Dayswork.Core/Compat/ExpansionRoutes.cs`, `IExpansionProfile.cs`, `VanillaExpansionProfile.cs` | Added immutable route ids, purposes, route requests, route definitions, hops, location descriptors, and typed failure values. Extended profiles with descriptor and route lookup APIs. Vanilla remains a Null-Object. |
| SVE profile data | `Dayswork.Core/Compat/SveExpansionProfile.cs` | Added centralized SVE constants, `Custom_GrandpasShedGreenhouse` as greenhouse work, `Custom_GrandpasShed` as deposit-only, and source-grounded routes for Grandpa's Farm, IF2R, and Frontier Farm. |
| Live validation | `Dayswork/Compat/ExpansionCompatService.cs` | Added descriptor discovery, draft-aware expansion chest visibility, live farm-signature route validation, live location lookup, tile bounds/passability checks, and typed failure formatting. |
| Cross-location navigation | `Dayswork/Orchestration/CrossLocationRouteNavigator.cs` | Added ordered-hop execution that walks to each approach tile, warps to each arrival tile, and reports navigation failure without owning skip/deposit policy. |
| UI and discovery | `Dayswork/Integration/ChestResolver.cs`, `Dayswork/UI/OutputDestinationsMenu.cs` | Added route-gated virtual shed greenhouse building outline discovery, draft-aware expansion chest discovery, and task filtering so expansion chests are offered only for greenhouse output tasks. |
| Runtime orchestration | `Dayswork/Orchestration/ShiftOrchestrator.cs` | Routed shed greenhouse work entry/exit and expansion chest deposit entry/exit through validated expansion routes. Route failures emit one warning and skip affected work or preserve deposit items through existing overflow handling. |
| Tests | `Dayswork.Tests/Compat/*`, `Dayswork.Tests/UI/Todo10ExpansionDestinationFilteringTests.cs` | Added route-definition examples, FsCheck route generators/properties, route failure payload checks, descriptor visibility checks, and updated vanilla no-op expectations. |

## Source-Grounded SVE Route Data

| Source | Verified data |
|---|---|
| `Stardew Valley Expanded/[CP] Stardew Valley Expanded/code/Locations/LocationsData.json` | `Custom_GrandpasShedGreenhouse` exists, has `DefaultArrivalTile` `(30,16)`, uses `Maps\Custom_GrandpasShedGreenhouse`, and is plantable. |
| `Grandpa's Farm/[CP] Grandpa's Farm/content.json` plus `GrandpasFarm_ShedFixed.tbin` | Patch area `(15,11)`; farm action tiles `(20,21)` and `(21,21)` warp to `Custom_GrandpasShed`; shed return tiles load Farm `(20,22)` and `(21,22)`. Dayswork uses `(20,22)` as the passable farm-side approach/arrival. |
| `Immersive Farm 2 Remastered/[CP] .../content.json` plus `FarmShedFixed.tbin` | Patch area `(138,23)`; farm action tiles `(144,33)` and `(145,33)` warp to `Custom_GrandpasShed`; shed return tiles load Farm `(144,34)` and `(145,34)`. Dayswork uses `(144,34)`. |
| `Frontier Farm/[CP] Frontier Farm/content.json` plus `FrontierFarm_ShedFixed.tmx` and `Warps_GrandpasShed_NF.tbin` | Patch area `(14,11)`; farm action tiles `(18,21)` and `(19,21)` warp to `Custom_GrandpasShed`; shed return tiles load Farm `(18,22)` and `(19,22)`. Dayswork uses `(18,22)`. |
| `GrandpasShed.tbin` | Shed action tile `(25,13)` warps to `Custom_GrandpasShedGreenhouse` arrival `(30,16)`. Dayswork uses shed approach `(25,14)`. |
| `GrandpasShedGreenhouse.tbin` | Greenhouse action tile `(30,15)` warps to `Custom_GrandpasShed` arrival `(25,14)`. Dayswork uses greenhouse approach `(30,16)`. |

## Verification

| Command | Result |
|---|---|
| `dotnet test Dayswork.sln /p:EnableModDeploy=false` | Passed: 397, skipped: 1, failed: 0 |
| `dotnet build Dayswork.sln /p:EnableModDeploy=false` | Succeeded with 0 warnings and 0 errors |

## Property-Based Testing Compliance

| Rule | Result |
|---|---|
| PBT-02 | N/A. TODO-10 introduced no reversible parse/format or serialize/deserialize transform. |
| PBT-03 | Compliant. Properties cover deterministic SVE route lookup, contiguous hop ordinals, contiguous source/target route shape, descriptor visibility policy, and route failure payload content. |
| PBT-07 | Compliant. `Todo10RouteGenerators` centralizes route request, route definition, descriptor, and failure generators. |
| PBT-08 | Compliant. FsCheck shrinking/replay behavior remains enabled; no replay suppression added. |
| PBT-09 | Compliant. Existing FsCheck.Xunit stack used directly. |

## Manual Playtest Checklist

| Scenario | Expected outcome |
|---|---|
| SVE with Grandpa's Farm, IF2R, and Frontier Farm after Grandpa's Shed repair | `Custom_GrandpasShedGreenhouse` appears as a selectable greenhouse work scope only when the route validates. |
| Shed greenhouse crop task | Worker walks Farm to shed, warps to shed, walks shed to greenhouse approach, warps into greenhouse, services reachable crop work, then returns through shed to Farm. |
| Standard greenhouse contract | Existing standard greenhouse behavior is unchanged and does not offer shed/main-shed chests. |
| Shed greenhouse selected with output destinations | Chests in `Custom_GrandpasShedGreenhouse` and `Custom_GrandpasShed` are offered only for greenhouse output tasks. |
| Route unavailable or blocked | One maintainer warning is logged; affected work is skipped or affected deposit items are mailed through existing overflow handling. |

## Deviations

No save-schema changes or new dependencies were introduced. The route definitions use the first verified left-side passable farm/shed approach tile for each paired SVE warp, rather than trying to trigger both map action tiles. This keeps routing deterministic while preserving the source-grounded route shape.
