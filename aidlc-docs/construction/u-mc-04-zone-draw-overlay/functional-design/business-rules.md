# U-MC-04 — Business Rules (as-built)

**Unit**: U-MC-04 — Zone Draw Overlay Extension

| Rule | Statement | Source |
|---|---|---|
| BR-MC04-01 | Existing crop assignments (zones of all groups other than the one being edited) render in a single **red** fill and cannot be selected. | FR-MC-06, DEV-MC-01 |
| BR-MC04-02 | The active draw session renders in **green** (group's drawn zones + in-progress drag preview). | FR-MC-06, DEV-MC-01 |
| BR-MC04-03 | A drag rectangle that overlaps **any** protected (other-group) zone is rejected entirely — nothing is added, and a HUD error (`ui.manage_crops.zone_overlap_protected`) is shown. No partial selection. | FR-MC-06 |
| BR-MC04-04 | Within the active group, drawing over already-selected tile(s) is **delete-and-redraw**: the overlapping zone(s) are removed instead of stacking a new zone. There is no in-place reassignment. | FR-MC-07 |
| BR-MC04-05 | Two non-contiguous zones may belong to the same group and share its plan; each drawn zone is stored as its own independently-configured `Zone`. | FR-MC-08 |
| BR-MC04-06 | The crop draw layer does not select buildings (buildings are passed empty; `allowBuildingSelection: false`). | Crop layer scope |
| BR-MC04-07 | Re-entering the draw for a group seeds from that group's existing zones, so cancelling/returning preserves prior work; **Done** replaces the group's zones with the session result and applies the seasonal plan to each drawn zone. | FR-MC-07, S-28 DoD |
| BR-MC04-08 | Overlap is edge-inclusive: zones sharing only an edge tile are treated as overlapping. | `ZoneOverlapPolicy` |

## Edge cases

- **Empty protected set** (only one group, or first group): no zone is red; the player draws
  freely and only own-group delete-and-redraw applies.
- **Clear** removes all of the active group's drawn zones for that session.
- **Cancel / force-close**: the session restores world state (`currentLocation`, viewport,
  HUD) via `cleanupBeforeExit` and hands control back to the editor without committing.
