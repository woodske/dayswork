# U-MC-04 — Domain Entities (as-built)

**Unit**: U-MC-04 — Zone Draw Overlay Extension

This unit introduces **no new persisted domain entities**. It reuses existing geometry and
authoring types and the existing draw-overlay seam. The entities below are the ones the
overlay behavior operates on.

## Reused entities

| Entity | Source | Role in U-MC-04 |
|---|---|---|
| `Zone` (`TopLeft`, `BottomRight`, `LocationName`) | `Dayswork.Core.Domain` | Rectangular tile region drawn or protected. |
| `TileCoord` | `Dayswork.Core.Domain` | Tile coordinate used for drag start/current and overlap math. |
| `BuildingOutline` | `Dayswork.Core.Domain` | Building footprint (not used by the crop layer — buildings are disabled for crop draws). |
| `CropGroupDraft` (`Id`, `Zones`, per-season choices, `OutputChest`) | `Dayswork/UI/CropPlanDraft.cs` | One authored crop group; its `Zones` are the active layer when editing it and become "protected" when editing a different group. |
| `CropPlanDraft` | `Dayswork/UI/CropPlanDraft.cs` | Owns all groups; exposes `ProtectedZones(activeGroupId)` and `SetGroupZones(...)`. |

## Draw-overlay data contract (`IZoneDrawSource`)

The overlay reads a read-only view per draw session. Fields relevant to U-MC-04:

| Member | Meaning for the crop layer |
|---|---|
| `CompletedZones` | The active group's drawn zones — rendered **green**. |
| `ProtectedZones` | Other groups' zones — rendered **red**, unselectable. |
| `ZoneFillColor` | Active layer color; the crop layer passes `Color.LimeGreen * 0.5f`. |
| `ProtectedZoneFillColor` | `Color.Red * 0.35f` (single red for all existing assignments — DEV-MC-01). |
| `DragStart` / `DragCurrent` | In-progress drag, rendered as a light-green preview. |
| `IsInZoneDrawMode` | Always true during a session; drives the tile-grid render. |

## Notes

- A drawn crop zone is **its own independently-configured unit** (FR-MC-08): non-contiguous
  zones in the same group share the group's plan but are stored as distinct `Zone`s.
- No schema change: drawn zones persist through the existing `CropZoneAssignment` projection
  established in U-MC-01/U-MC-03.
