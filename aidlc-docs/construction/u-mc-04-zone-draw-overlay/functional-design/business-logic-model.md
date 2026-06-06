# U-MC-04 — Business Logic Model (as-built)

**Unit**: U-MC-04 — Zone Draw Overlay Extension

The crop-zone draw session reuses the general zone-draw machinery (`ZoneDrawMenu` +
`ZoneDrawOverlay`) configured as a distinct **managed-crop layer**. The logic below is the
behavior already shipping after the U-MC-03 review fix.

## Launch (HiringFlowCoordinator.BeginCropZoneDraw)

When the player presses **Draw** for a crop group, the coordinator opens a `ZoneDrawMenu`
configured as the crop layer:

- `allowBuildingSelection: false` — crops do not select buildings.
- `overlapTogglesSelection: true` — drawing over an own-group tile removes it (delete-and-redraw).
- `protectedZones: draft.CropPlan.ProtectedZones(groupId)` — all **other** groups' zones.
- `zoneFillColor: Color.LimeGreen * 0.5f` — active draw is green.
- `initialZones: group.Zones` — seed from the group's existing zones so re-entering preserves work.

On **Done**, `CropPlanDraft.SetGroupZones(groupId, zones)` replaces the group's zones and the
preview refreshes; the configured seasonal plan is applied to every drawn zone via the
existing `CropPlanDraft` → `CropZoneAssignment` projection.

## Rendering decision (ZoneDrawOverlay.OnRenderedWorld)

Per `Display.RenderedWorld` frame, the overlay draws (in O(zone count), not O(tile count)):

1. The tile grid (while in draw mode).
2. `CompletedZones` filled with `ZoneFillColor` → **green** for the active crop group.
3. `ProtectedZones` filled with `ProtectedZoneFillColor` → **red** for other groups (DEV-MC-01).
4. The in-progress drag rectangle as a light-green preview.

Buildings are not drawn for the crop layer (none are passed).

## Selection / overlap decision (ZoneDrawMenu.releaseLeftClick)

On release of a drag from `start` to `end`, the menu computes the normalized rectangle and:

1. **Protected-overlap guard** — if the rectangle overlaps **any** protected zone
   (`ZoneOverlapPolicy.OverlapsAny`), reject it: show the
   `ui.manage_crops.zone_overlap_protected` HUD error, play `cancel`, and add nothing. This
   makes existing assignments **unselectable** (FR-MC-06).
2. **Own-group toggle** (overlapTogglesSelection): if the rectangle overlaps one or more of
   the active group's `CompletedZones`, **remove** those zones (play `bigDeSelect`) — this is
   delete-and-redraw (FR-MC-07). Otherwise add the new `Zone` (play `coin`).

## Overlap predicate (ZoneOverlapPolicy)

Pure, deterministic rectangle intersection (shared edges count as overlap):

```
ZonesOverlap(zone, tl, br) =
    zone.TopLeft.X <= br.X && zone.BottomRight.X >= tl.X &&
    zone.TopLeft.Y <= br.Y && zone.BottomRight.Y >= tl.Y
OverlapsAny(zones, tl, br) = zones.Any(z => ZonesOverlap(z, tl, br))
```

## Protected-zone projection (CropPlanDraft.ProtectedZones)

`ProtectedZones(activeGroupId)` returns every zone from every group **except** the active one,
flattened — so the layer being edited is never protected against itself, but all sibling
groups are.
