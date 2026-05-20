# NFR Requirements Plan — U-12 Hiring UI: Schedule + Edit/Pause/Cancel

## Depth
Minimal — all NFRs fully determined from approved requirements.md and prior unit patterns.
No clarifying questions needed.

## Stages

- [x] Step 1: Analyze unit context (unit-of-work.md, requirements.md, U-09/U-11 NFR patterns)
- [x] Step 2: Identify applicable NFRs for U-12 scope
  - NFR-UX-01: Gamepad navigation for ScheduleMenu and ContractListMenu
  - NFR-UX-02: i18n routing for all new schedule/contract-management keys
  - NFR-PERF-01: draw() frame budget for both new menus
  - NFR-SAFE-03: ContractStore.Pause/Resume must persist state safely via SMAPI API
  - NFR-MAINT-03: ContractStore (Core) extended without SMAPI references; BulletinBoardPatch extension stays in Patches namespace
  - NFR-MAINT-04: BulletinBoardPatch extension must remain in Dayswork.Patches namespace
  - PBT-02: ContractStore round-trip with paused state (new field in ContractDtoV1)
  - PBT-03: Pause/Resume/Cancel state-transition invariants on ContractStore
- [x] Step 3: Confirm no questions needed (all ambiguities resolved by requirements + prior units)
- [x] Step 4: Generate nfr-requirements.md
- [x] Step 5: Generate tech-stack-decisions.md
