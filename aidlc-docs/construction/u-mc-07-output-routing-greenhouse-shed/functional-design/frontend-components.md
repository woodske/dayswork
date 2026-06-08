# Frontend Components - U-MC-07 Output Routing + Greenhouse/Shed

**Unit**: U-MC-07 - Output Routing + Greenhouse/Shed  
**Stage**: CONSTRUCTION - Functional Design  
**Status**: Review required

U-MC-07 adds the no-season greenhouse/shed authoring path to the existing Manage Crops UI. The intent is to extend the current crop-group editor rather than create a new page.

## Component: `ManageCropsMenu`

### Existing Role

Lists crop groups, their seasonal summaries, zone counts, and edit/delete actions.

### U-MC-07 Extension

- Show each group's location label:
  - Farm
  - Greenhouse
  - Grandpa's Shed Greenhouse when SVE route support is available
- For season-agnostic groups, show one year-round crop summary instead of four seasonal summaries.
- Keep the Done/Optional hub status behavior unchanged: at least one materialized zone means Done.

## Component: `CropGroupEditorMenu`

### Existing Role

Renders Spring/Summer/Fall/Winter crop rows, fertilizer picker, replant checkbox, output chest picker, and draw button.

### U-MC-07 Extension

- Add a location selector at the top of the editor.
- Available values:
  - Farm, always available.
  - Greenhouse, when the vanilla greenhouse location is available.
  - Grandpa's Shed Greenhouse, when the active expansion profile exposes the route and location descriptor.
- Farm mode keeps the existing seasonal table.
- Greenhouse/shed mode replaces the seasonal table with one continuous crop row:
  - crop picker;
  - fertilizer picker;
  - replant checkbox;
  - output chest picker;
  - draw zones button.
- Changing a group's location clears its zones because zone coordinates are location-local.
- Existing seasonal crop data should not be silently converted to season-agnostic data without the user explicitly changing the location.

## Component: `CropListPickerMenu`

### Existing Role

Reusable list picker for crops and fertilizers.

### U-MC-07 Extension

- For season-agnostic crop groups, the crop picker uses `CropCatalogProvider.GetCatalog(null, greenhouse: true)`.
- The supply tag labels remain unchanged.
- Fertilizer picker behavior is unchanged.

## Component: `ZoneDrawMenu` / `ZoneDrawOverlay`

### Existing Role

Draws rectangular crop zones over the farm display location.

### U-MC-07 Extension

- Accept a target `GameLocation` and target location name.
- Swap the displayed location to that target without warping the player, matching the current farm draw pattern.
- Persist completed zones with the target location name instead of hardcoded `Farm`.
- Protect zones only within the active target location; same coordinates in another location are unrelated.
- Preserve the green active draw and red protected-zone colors from U-MC-04.
- Keep building selection disabled for managed-crop draw sessions.

## Component: Output Chest Picker

### Existing Role

Allows a crop group to choose an optional `ChestRef` output destination.

### U-MC-07 Extension

- Keep the automatic output fallback option.
- Show farm, building interior, greenhouse, and supported expansion-deposit chests returned by `ChestResolver`.
- Exclude built-in office input/output chests from explicit per-zone choices.
- Allow a zone in one managed-crop location to choose a chest in another reachable deposit location, relying on existing deposit routing.

## User Interaction Flows

### Farm Seasonal Group

1. Player adds or edits a crop group.
2. Location remains Farm.
3. Player configures seasonal crop rows.
4. Player optionally picks an output chest.
5. Player draws one or more farm zones.
6. The group projects to seasonal `CropZoneAssignment`s with `Zone.LocationName = "Farm"`.

### Greenhouse/Shed Year-Round Group

1. Player adds or edits a crop group.
2. Player selects Greenhouse or Grandpa's Shed Greenhouse.
3. UI shows the year-round crop row.
4. Player picks one crop, optional fertilizer, replant setting, and optional output chest.
5. Player draws one or more zones inside the chosen live location.
6. The group projects to `CropAssignmentMode.SeasonAgnostic` assignments whose zones carry the selected location name.

## Validation Rules

- Draw button is enabled only when the active group has a configured crop for its current mode.
- Crop groups with no zones do not materialize into `CropPlan`.
- Protected zones are location-scoped.
- If an SVE route or location is not available, the shed greenhouse option is hidden.
- All new labels are i18n-backed.

## Testable Properties

| Component | Property category | Property |
|---|---|---|
| `CropGroupDraft` projection | Round-trip | Season-agnostic group projection and hydration preserves location/mode/choice/chest/zones. |
| Location selector | Invariant | Changing location clears zones and keeps stale coordinates out of the new location. |
| Protected-zone filtering | Invariant | Protected zones include only other groups in the same location. |
| Crop catalog selection | Invariant | Season-agnostic picker uses the greenhouse catalog and does not apply a season filter. |
| Chest picker filtering | Invariant | Built-in office chests are excluded from explicit choices, while automatic fallback remains available. |

## Extension Compliance

| Extension | Status | Component impact |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops; no network/auth/PII surface. |
| Property-Based Testing | Compliant | PBT-01 identifies draft projection, filtering, and location-scoping properties; live rendering remains example/playtest-covered. |

