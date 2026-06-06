# U-MC-04 — Zone Draw Overlay Extension — Code Generation Plan

**Unit**: U-MC-04 — Zone Draw Overlay Extension
**Stage**: CONSTRUCTION — Code Generation

## Situation

U-MC-04's Definition of Done was implemented early, during the **U-MC-03 code-generation
review fix** (crop groups). All required behavior — red/unselectable existing zones, green
active draw, overlap prevention, delete-and-redraw, and applying the seasonal plan to each
drawn zone — already ships in:

- `Dayswork/UI/IZoneDrawSource.cs` (active + protected fill colors)
- `Dayswork/UI/ZoneDrawOverlay.cs` (green active fill, red protected fill, drag preview)
- `Dayswork/UI/ZoneDrawMenu.cs` (protected-overlap guard, delete-and-redraw toggle)
- `Dayswork/UI/ZoneOverlapPolicy.cs` (pure overlap predicate)
- `Dayswork/UI/CropPlanDraft.cs` (`ProtectedZones`, `SetGroupZones`)
- `Dayswork/UI/HiringFlowCoordinator.cs` (`BeginCropZoneDraw` crop-layer config)

Therefore this code-generation stage is a **verification + coverage-closing** pass, not new
feature work.

## Part 2 — Generation steps

- [x] Step 1 — Confirm the as-built overlay/menu/policy seam meets FR-MC-06/07/08 + DEV-MC-01 (no code change required).
- [x] Step 2 — Add dedicated U-MC-04 example coverage in `Dayswork.Tests/UI/ZoneOverlapPolicyTests.cs`:
  - empty protected set → free draw (no overlap);
  - overlap against **any** of several protected zones (multi-group) → rejected.
- [x] Step 3 — Verify build: `dotnet build Dayswork.sln /p:EnableModDeploy=false` (0/0).
- [x] Step 4 — Verify tests: `dotnet test Dayswork.sln /p:EnableModDeploy=false` (all green, +2).
- [x] Step 5 — Deploy build: `dotnet build Dayswork.sln` to the live Mods folder.
- [x] Step 6 — Write code-summary; update `aidlc-state.md` and `audit.md`.

## Extension compliance

- **Security Baseline**: N/A (disabled for Manage Crops).
- **Property-Based Testing (full mode)**: compliant — the unit's pure overlap/protection logic
  is FsCheck-/example-covered; this pass adds focused examples. No new property-applicable
  algorithm introduced.
