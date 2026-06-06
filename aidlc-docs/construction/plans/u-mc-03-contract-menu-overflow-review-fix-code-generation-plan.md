# Code Generation Plan - U-MC-03 Contract Menu Overflow Review Fix

**Unit**: U-MC-03 - Manage Crops Authoring UI review fix
**Stage**: CONSTRUCTION - Code Generation
**Status**: Complete

## Context

Playtest feedback reported that the Configure Contract hub can overlap the `Cancel` and `Hire`
footer buttons with the lower menu rows when the game is run in a small window. Inspection also
confirmed related overflow risk in the Work Scope summary page and the contract management list.

Scope is limited to contract-flow overflow containment:
- `HubMenu` refactor onto the layout shell + scroll panel
- `ZoneAndChestMenu` bounded summary scrolling
- `ContractListMenu` fixed-height scrolling body
- focused viewport math tests and regression verification

## Generation Steps

- [x] 1. Add a small internal viewport helper for contract-menu visible-row / viewport math.
- [x] 2. Refactor `Dayswork/UI/Layout/PageShell.cs` only as needed to support a cancel-labeled leading footer button for the hub menu.
- [x] 3. Refactor `Dayswork/UI/HubMenu.cs` onto `LayoutMenu` + `PageShell` + `ScrollPanel` with a pinned footer and non-overlapping scrollable body.
- [x] 4. Update `Dayswork/UI/ZoneAndChestMenu.cs` so the scope summary uses a bounded scrollable viewport above the footer.
- [x] 5. Update `Dayswork/UI/ContractListMenu.cs` to use a fixed-height body viewport with scrollable visible rows instead of content-sized menu growth.
- [x] 6. Add focused unit tests for extracted viewport math and any related scroll behavior.
- [x] 7. Run `dotnet build Dayswork.sln /p:EnableModDeploy=false` and `dotnet test Dayswork.sln /p:EnableModDeploy=false`.
- [x] 8. Update `aidlc-docs/audit.md`, `aidlc-docs/aidlc-state.md`, and the relevant code summary with the completed review fix details.

## Extension Compliance Intent

- **Security Baseline**: N/A (disabled in `aidlc-state.md`).
- **Property-Based Testing (full mode)**: apply where new pure viewport helpers expose stable
  invariants; otherwise use focused example tests for UI layout containment.
