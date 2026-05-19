# U-08 NFR Requirements Plan — Bulletin Board Hook

## Unit
U-08 — Bulletin Board Hook + i18n + Multiplayer Guard

## Stage
NFR Requirements (minimal depth — all NFRs derived from requirements.md; no user questions needed)

## Steps

- [x] 1. Analyze unit scope (M-02 BulletinBoardPatch, M-18 MultiplayerGuard, M-21 I18nHelper + ModEntry extension)
- [x] 2. Evaluate all NFR categories — identify applicable vs. N/A
- [x] 3. Generate `nfr-requirements.md` (applicable NFRs + N/A list with rationale)
- [x] 4. Generate `tech-stack-decisions.md` (SMAPI APIs + Harmony pattern decisions)
- [x] 5. PBT compliance assessment (all rules N/A for this unit)
- [x] 6. Present completion message and await approval

## NFR Category Assessment (pre-artifact)

| Category | Verdict | Key NFRs |
|---|---|---|
| Safety & data integrity | N/A | No items, no gold, no save data in this unit |
| Performance | N/A | No worker loop, no tile scanning, no overlay |
| Usability — i18n | **APPLICABLE** | NFR-UX-02: all strings via i18n/default.json |
| Usability — gamepad | N/A | Hiring UI is U-09 |
| Maintainability — Harmony isolation | **APPLICABLE** | NFR-MAINT-04: one patch per file in Dayswork.Patches |
| Maintainability — pure logic | N/A | No Core types in this unit |
| Maintainability — code style | Implicit | NFR-MAINT-05: dotnet format; no design decisions needed |
| Multiplayer guard | **APPLICABLE** | FR-MP-01 / NFR-COMPAT-03: no-op bulletin patch in MP |
| Onboarding docs | **APPLICABLE** | NFR-ONBOARD-01: Harmony anatomy + SMAPI i18n in Code Gen |
| Security Baseline | Disabled | Project-wide (Q28) |
| PBT — all rules | N/A | No domain logic, no serialization, no generators |
