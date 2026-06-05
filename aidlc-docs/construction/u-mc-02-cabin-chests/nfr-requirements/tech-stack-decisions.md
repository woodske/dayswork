# U-MC-02 Tech Stack Decisions

**Unit**: U-MC-02 - Cabin Chests (Input + Backfill)
**Stage**: CONSTRUCTION - NFR Requirements
**Status**: Complete

## Decisions

| Area | Decision | Rationale |
|---|---|---|
| Runtime language | C# / .NET 6 | Existing project stack. |
| Mod integration | Existing SMAPI and StardewValley APIs | Built-in chests and building data are live-game concepts. |
| New dependencies | None | U-MC-02 can be implemented with current project references. |
| Service shape | New Mod-side `CabinChestService` | Keeps backfill/name/role logic isolated from UI menus. |
| i18n | Existing `I18nHelper` and `i18n/default.json` | Fixed input/output chest names need translation keys. |
| Tests | xUnit examples | User selected Q5=B; live game API behavior is better pinned with examples/mocks. |
| PBT framework | Existing FsCheck.Xunit 2.16.5 | Already selected for applicable pure/property-heavy units; no new package needed. |

## Non-Decisions

| Topic | Reason |
|---|---|
| New save DTO fields | Built-in chest state lives in Stardew building/chest state; backfill is an idempotent live-world ensure. |
| Background jobs/queues | Single-player lifecycle operation; no async infrastructure needed. |
| Network/security packages | No network/auth/PII surface in U-MC-02. |

## Extension Compliance

| Extension | Status | Rationale |
|---|---|---|
| Security Baseline | N/A | Disabled for Manage Crops. |
| Property-Based Testing | Compliant | Existing FsCheck stack remains available; U-MC-02 code generation will focus on examples per Q5=B. |
