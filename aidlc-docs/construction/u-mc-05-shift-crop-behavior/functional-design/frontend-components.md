# U-MC-05 Frontend Components

**Unit**: U-MC-05 — Shift Crop Behavior
**Stage**: CONSTRUCTION — Functional Design
**Status**: Review required

U-MC-05 is primarily runtime. Its only UI surface is the **two plan-level toggle
checkboxes** that U-MC-03 explicitly deferred to this unit (state log: "defer plan-level
toggles to U-MC-05"). They expose the already-persisted `CropPlan` flags so the player can
turn them off; both default ON.

## Component: Manage Crops page toggles (extends `ManageCropsMenu`)

| Control | Bound to | Default | Behavior |
|---|---|---|---|
| "Clear debris before tilling" checkbox | `CropPlanDraft.ClearDebrisBeforeTilling` → `CropPlan.ClearDebrisBeforeTilling` | ON | When off, the runtime skips debris-blocked managed tiles for till/plant (BR-MC5-18). |
| "Clear dead plants" checkbox | `CropPlanDraft.ClearDeadPlants` → `CropPlan.ClearDeadPlants` | ON | When off, dead-crop managed tiles are skipped, not re-tilled/replanted (BR-MC5-19). |

### Placement & interaction

- Rendered as two standard menu checkboxes near the top of the Manage Crops page (above or
  beside the crop-group list), matching the existing menu's checkbox idiom.
- Toggling updates `CropPlanDraft` immediately; the values persist with the contract on
  confirm and hydrate on edit (reuse the existing `CropPlanDraft` ↔ `CropPlan` mapping).
- Gamepad/keyboard parity follows the existing menu's control conventions.

### State & validation

- No validation: both are free booleans. They do not affect zone/draw state.
- The status chip logic ("Done" when ≥1 zone configured) is unchanged.

### i18n

- Labels are i18n-backed keys (e.g. `ui.manage_crops.clear_debris_toggle`,
  `ui.manage_crops.clear_dead_plants_toggle`) and pass the hardcoded-string lint gate
  (NFR-MC-07, BR-MC5-24).

## Out of scope (this unit)

- No new menus/pages. Purchase/store-preference UI is already authored (store preference
  lives on `CropPlan` from U-MC-01/03); the shopping runtime is U-MC-06.
- HUD notices (tool-skip, fertilizer-unavailable) are immediate `Game1.addHUDMessage`
  notifications, not menu components.
