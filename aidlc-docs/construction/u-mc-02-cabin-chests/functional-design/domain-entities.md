# U-MC-02 Domain Entities

**Unit**: U-MC-02 - Cabin Chests (Input + Backfill)
**Stage**: CONSTRUCTION - Functional Design
**Status**: Review required

## Entity Overview

U-MC-02 is mostly a Mod-side integration unit. It does not introduce new pure Core domain entities. It defines stable Mod-side identities and service responsibilities around built-in farmhand office chests.

## Farmhand Office Chest Role

| Field | Meaning |
|---|---|
| `Id` | Stable built-in chest ID used by Stardew building data. |
| `Role` | Business role: input supply source or output deposit destination. |
| `DisplayTile` | Tile on the farmhand office footprint where the chest appears and can be clicked. |
| `SelectableDestinationBehavior` | Whether `ChestResolver` includes the chest in destination lists. |
| `ProgrammaticNameKey` | i18n key for the fixed built-in chest label. |

## Chest Instances

| Role | Id | Display tile | Selectability | Business meaning |
|---|---|---|---|---|
| Input | `Bindicle.Dayswork_Input` | `(1, 2)` | Excluded | Managed-crop seed/fertilizer supply source. |
| Output | `Bindicle.Dayswork_Output` | `(3, 2)` | Included | Default/fallback task-output deposit chest and explicit selectable output destination. |

## CabinChestService

| Method | Responsibility |
|---|---|
| `EnsureOfficeChests(Farm farm)` | Finds farmhand office buildings and ensures the input chest exists. |
| `EnsureInputChest(Building office)` | Idempotently creates/backfills the input chest if missing. |
| `ApplyProgrammaticNames(Building office)` | Applies fixed i18n-backed names to input and output chests. |
| `TryGetInputChest(Building office)` | Resolves the built-in input chest by ID. |
| `TryGetOutputChest(Building office)` | Resolves the built-in output chest by ID. |
| `IsInputChestTile(Farm farm, int x, int y)` | Identifies the input chest farm tile for `ChestResolver` exclusion. |
| `IsOutputChestTile(Farm farm, int x, int y)` | Identifies the output chest farm tile for discovery and interaction. |

## HiringBuilding Additions

| Member | Design |
|---|---|
| `InputChestId` | New constant: `Bindicle.Dayswork_Input`. |
| `InputChestDisplayTile` | New static tile: `(1, 2)`. |
| `OutputChestId` | Existing constant retained: `Bindicle.Dayswork_Output`. |
| `OutputChestDisplayTile` | Existing tile retained: `(3, 2)`. |
| `BuildData()` | Declares both input and output `BuildingChest` entries. |
| `TryGetInputChest(Farm farm)` | Returns the built-in input chest when the office exists. |
| `TryGetOutputChest(Farm farm)` | Existing output lookup remains, possibly delegated through `CabinChestService`. |

## ChestResolver Behavior

| Chest type | Discovery behavior |
|---|---|
| Input built-in office chest | Excluded from selectable destination lists. |
| Output built-in office chest | Included as a selectable destination. |
| Ordinary farm chest | Included using existing display-name behavior. |
| Building-interior chest | Included using existing behavior. |
| Expansion-location chest | Included using existing expansion-compat behavior. |

## State and Persistence

U-MC-02 does not add save DTO fields. The built-in chests are represented by Stardew building chest state, and the input-chest backfill is an idempotent live-world ensure operation rather than a Dayswork save-schema migration.

## Relationships

| Entity/service | Relationship |
|---|---|
| `HiringBuilding` | Declares the built-in chest identities and display tiles. |
| `CabinChestService` | Uses `HiringBuilding` IDs to backfill/name/resolve office chests. |
| `HiringBuildingInteraction` | Uses display tiles to open the correct built-in chest. |
| `ChestResolver` | Uses chest-role helpers to exclude input and include output. |
| U-MC-05 runtime | Will read crop supplies from the input chest. |
| U-MC-07 runtime | Will route task output to assigned destinations or output fallback. |

## Testable Properties

| Entity/service | Property category | Property |
|---|---|---|
| `CabinChestService` | Idempotence | `EnsureOfficeChests` repeated over the same office does not create duplicate input chests. |
| `CabinChestService` | Idempotence | Reapplying names converges to fixed i18n-backed input/output labels. |
| `ChestResolver` | Invariant | Input chest is never returned as a selectable destination. |
| `ChestResolver` | Invariant | Output chest is returned as selectable when present. |
| `HiringBuilding` | Invariant | Input and output display tiles are distinct and inside the office footprint. |

## PBT Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-01 | Compliant | Entity-level idempotence and invariant properties are identified. |
| PBT-02 | N/A | No inverse mapping is introduced. |
| PBT-03 | Compliant for design | Chest identity/discovery invariants are listed. |
| PBT-04 | Compliant for design | Ensure and naming idempotence are listed. |
| PBT-05 | N/A | No oracle/reference implementation is applicable. |
| PBT-06 | N/A | Live game objects are not suitable for generated command-sequence testing in this unit. |
| PBT-07 | N/A | No property generators are required by this Functional Design. |
| PBT-08 | N/A | No PBT execution behavior is introduced here. |
| PBT-09 | Compliant | Existing FsCheck selection remains valid. |
| PBT-10 | Compliant | Example-test coverage is specified for business-critical live behavior. |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | Functional Design identifies testable properties and documents why Q5=B leads to example-focused code-generation tests for this live-integration unit. |
