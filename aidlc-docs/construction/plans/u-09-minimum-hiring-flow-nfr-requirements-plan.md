# U-09 NFR Requirements Plan — Minimum Hiring Flow

## Unit
U-09 — Minimum Hiring Flow (HiringFlowCoordinator, TaskSelectionMenu, SummaryMenu, ContractPersistenceAdapter)

## Stage
NFR Requirements (minimal depth — all NFRs derived from requirements.md; no user questions needed)

## Steps
- [x] 1. Analyze unit scope (M-03, M-04, M-07, M-15 + ModEntry extension + ContractDraft)
- [x] 2. Evaluate all NFR categories — identify applicable vs. N/A
- [x] 3. Generate `nfr-requirements.md` (applicable NFRs + N/A list with rationale)
- [x] 4. Generate `tech-stack-decisions.md` (IClickableMenu pattern + SMAPI data API decisions)
- [x] 5. PBT compliance assessment
- [x] 6. Present completion message and await approval

## NFR Category Assessment (pre-artifact)

| Category | Verdict | Key NFRs |
|---|---|---|
| Safety — save data | **APPLICABLE** | NFR-SAFE-03: WriteSaveData/ReadSaveData must be safe; null read → empty list |
| Safety — gold | **APPLICABLE** | NFR-SAFE-02: deposit deduction is integer; afford-check before deduct |
| Safety — items/worker | N/A | No worker or items in U-09 |
| Performance — frame budget | **APPLICABLE** | NFR-PERF-01: draw() runs every frame; no expensive calls inside |
| Performance — tile scanning | **APPLICABLE** | NFR-PERF-02: HoursEstimator called once at confirm-screen entry, result cached |
| Performance — zone overlay | N/A | U-11 concern |
| Usability — gamepad | **APPLICABLE** | NFR-UX-01: TaskSelectionMenu + SummaryMenu fully gamepad-navigable |
| Usability — i18n | **APPLICABLE** | NFR-UX-02: all menu labels via I18nHelper; no hardcoded display text |
| Usability — zone-in-board | N/A | U-11 concern |
| Maintainability — SMAPI separation | **APPLICABLE** | NFR-MAINT-03: Core interfaces called from Mod; no Game1/SMAPI refs in Core |
| Maintainability — Harmony | N/A | No new patches |
| Maintainability — code style | Implicit | dotnet format |
| Onboarding docs | **APPLICABLE** | NFR-ONBOARD-01: IClickableMenu anatomy + SMAPI data API JIT in Code Gen plan |
| PBT | Mostly N/A | SMAPI-integrated components not testable without Stardew |
| Security Baseline | Disabled | Project-wide (Q28) |
