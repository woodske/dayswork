# NFR Requirements Plan — U-11 Full Hiring UI: Zones & Chests

## Depth
Minimal — all NFRs fully determined from approved requirements.md and prior unit patterns.
No clarifying questions needed.

## Stages

- [x] Step 1: Analyze unit context (unit-of-work.md, requirements.md, U-09 NFR pattern)
- [x] Step 2: Identify applicable NFRs for U-11 scope
  - NFR-PERF-01: draw() frame budget (ZoneAndChestMenu + ZoneDrawOverlay)
  - NFR-PERF-03: Zone overlay rendering at full farm scale (~80×65 tiles)
  - NFR-UX-01: Gamepad navigation for ZoneAndChestMenu
  - NFR-UX-02: i18n routing for new zone/chest keys
  - NFR-UX-03: Zone draw mode overlays farm map in-place, returns to Screen 2 on completion
  - NFR-MAINT-03: ChestResolver injectable; Core separation rule
  - NFR-ONBOARD-01: JIT docs for Display.RenderedWorld, world→screen transform, overlay pattern
- [x] Step 3: Confirm no questions needed (all ambiguities resolved by requirements + prior units)
- [x] Step 4: Generate nfr-requirements.md
- [x] Step 5: Generate tech-stack-decisions.md
