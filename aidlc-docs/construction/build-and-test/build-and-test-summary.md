# Build and Test Summary — Dayswork SMAPI Mod

## Build Status

| Item | Detail |
|------|--------|
| **Build Tool** | .NET 6 SDK / MSBuild |
| **Build Command** | `dotnet build Dayswork.sln` |
| **Build Status** | **Success** — 0 errors, 0 warnings |
| **Build Marker** | `build=U17-Step18` |
| **Deployed To** | `X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork\` |

### Build Artifacts

| Artifact | Location |
|----------|----------|
| `Dayswork.dll` | `Mods/Dayswork/Dayswork.dll` |
| `Dayswork.Core.dll` | `Mods/Dayswork/Dayswork.Core.dll` |
| `manifest.json` | `Mods/Dayswork/manifest.json` |
| `i18n/default.json` | `Mods/Dayswork/i18n/default.json` |

---

## Test Execution Summary

### Unit Tests

| Metric | Result |
|--------|--------|
| **Command** | `dotnet test Dayswork.sln` |
| **Total** | 212 |
| **Passed** | **211** |
| **Failed** | 0 |
| **Skipped** | 1 (intentional — seed-logging smoke demo) |
| **Duration** | ~5 seconds |
| **Status** | **PASS** |

#### Coverage by Area

| Area | Tests |
|------|-------|
| Config mutation / reset / publish | `Dayswork.Tests.Config` |
| Config → snapshot mapping, range clamping | `Dayswork.Tests.Config.Mapping` |
| Shift cost & refund computation | `Dayswork.Tests.Shifts` |
| Hiring scheduler guard chain | `Dayswork.Tests.Scheduling` |
| Deposit hours policy | `Dayswork.Tests.Deposits` |
| Mail dispatcher (settlement, overflow, notices) | `Dayswork.Tests.Mail` |
| i18n lint gate (no hardcoded user-visible strings) | `Dayswork.Tests.Lint` |
| PBT seed-logging demo | `Dayswork.Tests.Smoke` (1 skip) |

### Integration Tests

| Metric | Result |
|--------|--------|
| **Method** | Manual in-game playtesting (SMAPI mod — no automated integration test runner) |
| **Scenarios** | 7 defined in `integration-test-instructions.md` |
| **Status** | See playtest checklist below |

#### Playtest Checklist

- [ ] Mod loads with no SMAPI errors (with and without GMCM installed)
- [ ] GMCM config screen shows all fields with correct labels and tooltips
- [ ] Config changes persist after closing/reopening the menu
- [ ] One-time contract: hire → work → deposit to chest → settlement mail next morning
- [ ] Recurring contract: deposit deducted each day; festival-skip mail arrives same morning
- [ ] Animal tasks: pet+collect per animal before moving to the next
- [ ] Big rocks: multiple hits required; correct stone yield from game data
- [ ] Greenhouse crops: each tile harvested once; produce goes to worker buffer, not player inventory

### Performance Tests

| Status | Detail |
|--------|--------|
| **N/A** | Single-player SMAPI mod — no server/load testing applicable |
| **Informal check** | SMAPI `[Dayswork][scan]` log lines confirm sub-tick scan times at normal farm sizes |

### Additional Tests

| Test Type | Status |
|-----------|--------|
| Contract tests | N/A — no microservice API contracts |
| Security tests | N/A — no network surface or user authentication |
| E2E tests | Covered by manual integration playtest scenarios |

---

## Units Delivered

| Unit | Description | Status |
|------|-------------|--------|
| U-13 | Worker AI — pathfinding, priority, capability, stuck detection | Complete |
| U-13B | Worker Actor + Task Visuals — NPC-backed worker, tool animations | Complete |
| U-14 | Output Pipeline — multi-destination deposit, overflow/tool-missing mail | Complete |
| U-15 | Recurring Lifecycle + Calendar Handlers — recurring contracts, festival/rain/sleep | Complete |
| U-16 | Animals & Buildings — building navigation, animal tasks, indoor deposit | Complete |
| U-17 | GMCM + i18n Polish — mutable config, GMCM registration, i18n lint gate | Complete |

---

## Overall Status

| Category | Status |
|----------|--------|
| **Build** | **Success** |
| **Unit Tests** | **Pass** (211/211, 1 expected skip) |
| **Integration / Playtest** | In progress — see checklist above |
| **Performance** | N/A |
| **Ready for Operations** | Yes, pending playtest sign-off |
