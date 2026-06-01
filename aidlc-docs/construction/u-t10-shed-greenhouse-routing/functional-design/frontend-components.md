# Frontend Components - u-t10-shed-greenhouse-routing

**Unit**: `u-t10-shed-greenhouse-routing`
**Stage**: Functional Design

## UI Scope

TODO-10 does not add a new menu, control type, screen, or save-facing UI concept. It extends existing hiring-flow data sources so the shed greenhouse appears as a normal greenhouse alternative when discovery availability succeeds.

## Existing Components Affected

| Component | Current role | TODO-10 behavior |
|---|---|---|
| `HiringFlowCoordinator` | Opens work-scope drawing and output-destination menus. | Passes the same draft through existing menus; no new step is introduced. |
| `ZoneDrawMenu` | Lets the player choose outdoor zones and supported building outlines. | Receives a virtual outline for `Custom_GrandpasShedGreenhouse` when available. |
| `LegacyScopeBootstrapper` | Converts selected outlines into `ContractScopeSelection`. | Classifies the virtual shed greenhouse as `GreenhouseSelection("Custom_GrandpasShedGreenhouse")`; main shed is not classified as work scope. |
| `ZoneAndChestMenu` | Summarizes selected outdoor, animal, and greenhouse scope. | Shows the selected shed greenhouse through the existing greenhouse summary line. |
| `OutputDestinationsMenu` | Lets the player choose output destinations per enabled output task. | Includes shed greenhouse and main shed chest options only when the draft selected the shed greenhouse. |
| `ChestResolver` | Supplies building outlines and chest entries. | Appends virtual shed greenhouse outline and gated expansion chest entries from the compat bridge. |

## State Model

| State | Owner | Rule |
|---|---|---|
| `ContractDraft.Greenhouse` | Existing draft | Holds `GreenhouseSelection("Custom_GrandpasShedGreenhouse")` when selected. |
| `ContractDraft.Destinations` | Existing draft | May contain chest destinations from shed greenhouse or main shed only for eligible shed greenhouse output. |
| `ScopeSummaryModel.Greenhouse` | Existing preview state | Displays the selected shed greenhouse location name unless a later UI polish changes the label. |
| Expansion chest list | `ChestResolver`/output menu | Filtered by current draft scope; not persisted separately. |

## User Interaction Flow

1. Player opens the hiring flow and reaches work-area selection.
2. `ChestResolver.GetBuildingOutlines(Game1.getFarm())` gathers normal building outlines and appends the virtual shed greenhouse outline if discovery availability succeeds.
3. `ZoneDrawMenu` displays that outline in the same selectable-building set as other supported greenhouse/building outlines.
4. Player selects either the standard greenhouse or the shed greenhouse; only one greenhouse selection is stored.
5. `ZoneAndChestMenu` summarizes the selected greenhouse.
6. Player reaches output destination selection.
7. If the selected greenhouse is `Custom_GrandpasShedGreenhouse`, the destination picker includes eligible chests from `Custom_GrandpasShedGreenhouse` and `Custom_GrandpasShed` along with existing destinations.
8. If the selected greenhouse is absent or standard, shed greenhouse/main shed chests are not offered as general-purpose destinations.

## Validation and Error Handling

| Scenario | UI behavior |
|---|---|
| SVE absent | No virtual shed greenhouse outline or expansion chest options. Existing UI is unchanged. |
| SVE present but shed route shape unavailable | No virtual shed greenhouse outline; no player-facing error. |
| Shed greenhouse selected, then route becomes unavailable by shift time | Contract remains saved; runtime skip/continue policy handles the failure and logs a maintainer-facing reason. |
| Expansion chest moved or removed after selection | Existing chest resolution and undelivered/overflow paths handle the failure. |
| Main shed chest exists | Offered only for shed-greenhouse output when shed greenhouse is selected; never creates a work-area selection. |

## Display and Copy Rules

- No new player-facing route-unavailable message is introduced.
- Existing greenhouse section text remains the display surface for the selected shed greenhouse.
- Existing destination picker rows display chest names or fallback location/tile names as today.
- If future polish adds a friendlier label, it must come from the expansion descriptor or i18n, not a hardcoded SVE string in general UI code.

## API and Integration Points

| API point | Functional expectation |
|---|---|
| `ChestResolver.GetBuildingOutlines(Farm farm)` | Appends virtual shed greenhouse outline after route-shape discovery availability succeeds. |
| `ChestResolver.GetAllChests(...)` or equivalent draft-aware overload | Supplies expansion chest entries only when the selected work scope is the shed greenhouse. If the existing method signature is too broad, Code Generation should add a narrow draft/scope-aware filter rather than making expansion chests global. |
| `LegacyScopeBootstrapper.TryApplySelectedBuildings(...)` | Stores the virtual shed greenhouse in the existing `GreenhouseSelection` field. |
| `OutputDestinationsMenu.OpenPicker(...)` | Presents only the chest entries supplied by the resolver/filter for the current draft. |

## Frontend Test Scenarios

| Scenario id | Scenario |
|---|---|
| UI-T10-01 | With SVE absent, building outline and destination lists are identical to the pre-TODO-10 behavior. |
| UI-T10-02 | With discovery availability true, the shed greenhouse can be selected as the single greenhouse scope. |
| UI-T10-03 | Selecting the standard greenhouse does not include shed greenhouse work or shed/main-shed chests. |
| UI-T10-04 | Selecting the shed greenhouse enables eligible shed greenhouse/main shed chests for shed-greenhouse output. |
| UI-T10-05 | Main shed never appears as a work-scope building. |
| UI-T10-06 | Editing an existing non-shed contract does not add shed greenhouse destinations automatically. |

## PBT and UI Boundary

The UI itself is not a primary PBT target. The pure filtering rules behind UI data are PBT-applicable:

| Property id | Category | Property |
|---|---|---|
| P-T10-UI-01 | Invariant | Expansion chest filtering never returns shed/main-shed chests unless selected greenhouse location is `Custom_GrandpasShedGreenhouse`. |
| P-T10-UI-02 | Invariant | A `DepositOnly` expansion descriptor never appears in the work-scope outline set. |
| P-T10-UI-03 | Invariant | The scope projection still contains at most one greenhouse selection. |

## Extension Compliance

| Extension | Status | Functional Design result |
|---|---|---|
| Security Baseline | Disabled | Skipped per TODO-10 configuration. No network, authentication, secrets, or PII UI behavior is introduced. |
| Property-Based Testing | Enabled - Partial | Compliant. UI-adjacent pure filtering invariants are documented for PBT-03 and require domain generators for PBT-07. PBT-02 is N/A. PBT-08/PBT-09 carry forward to test execution. |

## Content Validation

- Markdown tables and lists only.
- No Mermaid diagrams.
- No ASCII diagrams.
- No parser-sensitive embedded code blocks.
