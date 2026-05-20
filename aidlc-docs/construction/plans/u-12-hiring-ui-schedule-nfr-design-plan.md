# NFR Design Plan — U-12 Hiring UI: Schedule + Edit/Pause/Cancel

## Depth
Minimal — all NFR patterns follow established conventions from U-09/U-11.
No clarifying questions needed.

## Stages

- [x] Step 1: Analyze NFR requirements for U-12
- [x] Step 2: Identify applicable design patterns
  - Pattern 1: Pre-Compute on Open (ScheduleMenu + ContractListMenu)
  - Pattern 2: State Transition Result (ContractStore.Pause/Resume/Cancel returns enum)
  - Pattern 3: Backward-Compatible Save Field (IsPaused with DefaultValueHandling.Populate)
  - Pattern 4: Cancel Guard (active-shift check via ShiftOrchestrator.ActiveContractId)
  - Pattern 5: PBT Test Design (Pause/Resume/Cancel state invariants)
  - Pattern 6: Constructor Injection (ContractStore injected, not newed in menus)
- [x] Step 3: Confirm no questions needed
- [x] Step 4: Generate nfr-design-patterns.md
- [x] Step 5: Generate logical-components.md
