# U-MC-04 — Frontend Components (as-built)

**Unit**: U-MC-04 — Zone Draw Overlay Extension

No new UI component is introduced. U-MC-04 is a **configuration + rendering extension** of the
existing on-farm zone-draw session for the managed-crop layer.

## Component hierarchy

```
HiringFlowCoordinator
  └─ CropGroupEditorMenu  ("Draw" action)
        └─ ZoneDrawMenu (crop-layer config)      [IClickableMenu, IZoneDrawSource]
              └─ ZoneDrawOverlay                   [Display.RenderedWorld renderer]
```

## ZoneDrawMenu (crop-layer configuration)

| Concern | Crop-layer setting |
|---|---|
| Building selection | disabled (`allowBuildingSelection: false`, empty outline list) |
| Selection model | delete-and-redraw (`overlapTogglesSelection: true`) |
| Protected zones | other groups' zones (`CropPlanDraft.ProtectedZones(groupId)`) — red, unselectable |
| Active fill color | `Color.LimeGreen * 0.5f` |
| Protected fill color | `Color.Red * 0.35f` |
| Seed | the group's existing `Zones` |
| Buttons | Back (cancel), Clear, Done — reused from the general session |
| Input parity | mouse/keyboard + gamepad (right-stick pan, B = back) — reused |

## User interaction flow

1. Player opens a crop group editor, configures seasons/fertilizer/replant/output chest, presses **Draw**.
2. The farm view appears (no warp) with the tile grid; other groups' zones show red, this group's show green.
3. Player drags to add a green zone; dragging over an own green zone removes it; dragging onto a red zone is rejected with a HUD message.
4. **Done** commits the zones to the group and returns to the editor; **Back** returns without committing.

## State management

The menu is the `IZoneDrawSource`; the overlay is a stateless renderer reading it each frame.
Committed zones flow back through `onComplete → CropPlanDraft.SetGroupZones`, after which the
hub status chip / preview reflect the configured plan.

## Validation rules surfaced in UI

- Protected-overlap rejection → `ui.manage_crops.zone_overlap_protected` HUD error + `cancel` sound.
- Own-group removal → `bigDeSelect` sound; new zone add → `coin` sound.
