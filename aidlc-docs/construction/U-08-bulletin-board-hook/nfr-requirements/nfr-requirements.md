# NFR Requirements — U-08 Bulletin Board Hook

## Unit
U-08 — Bulletin Board Hook + i18n + Multiplayer Guard

**Depth**: Minimal — all NFRs are directly derived from approved requirements.md; no clarifying questions needed.

---

## Applicable NFRs

### NFR-MAINT-04 — Harmony Patch Isolation
**Requirement**: Harmony patches are isolated in a single namespace (`Dayswork.Patches`) for visibility and conflict diagnosis.

**How it applies to U-08**:
- `BulletinBoardPatch.cs` goes in `Dayswork/Patches/` — one file, one patch class
- The `[HarmonyPatch]` attribute targets the bulletin board's `receiveLeftClick` or equivalent menu-option rendering method
- No Harmony patch logic leaks into other namespaces
- This is the **first and only** Harmony patch in v1; establishes the pattern for future units

**Enforcement**: File location is the constraint; Code Generation plan step explicitly places the file in `Dayswork/Patches/`.

---

### NFR-UX-02 — i18n Routing
**Requirement**: All user-visible strings are routed through SMAPI's i18n system (`i18n/default.json`), so community translators can add languages without code changes.

**How it applies to U-08**:
- `I18nHelper` wraps `IModHelper.Translation.Get(string key)` — the single call site for all string lookups
- The bulletin board entry label (`"Hire a Farmhand"`) must be looked up via `I18nHelper`, never hardcoded
- The multiplayer refusal log message must also go through `I18nHelper` (it's user-visible in the SMAPI console)
- `i18n/default.json` is created/extended in this unit with the first two keys:
  - `bulletin.hire_a_farmhand`
  - `multiplayer.refused_log_message`

**Enforcement**: Any hardcoded string in `Dayswork/` that would appear to the player or in the SMAPI log is a violation of NFR-UX-02. The i18n lint test (U-16) will catch regressions.

---

### FR-MP-01 / NFR-COMPAT-03 — Multiplayer Guard
**Requirement**: The mod refuses to load (or no-ops the bulletin patch) in multiplayer sessions and logs a friendly SMAPI warning. v1 is single-player only.

**How it applies to U-08**:
- `MultiplayerGuard` checks `Context.IsMultiplayer` (the correct SMAPI API, not `Game1.IsMultiplayer`)
- The check runs in the bulletin board postfix — if multiplayer is detected, the patch returns early without adding the entry
- A friendly i18n'd log message is emitted via `IMonitor.Log(...)` at `LogLevel.Warn`
- The guard is stateless: re-evaluated each time the bulletin board opens (handles edge cases like hot-joining)

**Enforcement**: Manual play-test in a multiplayer session (or check for `Context.IsMultiplayer` in the postfix) verifies compliance.

---

### NFR-ONBOARD-01 — Just-In-Time Docs
**Requirement**: C# / SMAPI / Harmony concepts are explained just-in-time during Construction stages, embedded in Code Generation plans rather than front-loaded.

**How it applies to U-08**:
The Code Generation plan for U-08 must include brief explanations of:
1. **Harmony postfix anatomy**: what `[HarmonyPatch(typeof(Target), "MethodName")]` + `[HarmonyPostfix]` means, why postfix is the right choice for adding to a list vs. prefix for blocking
2. **SMAPI i18n API**: `IModHelper.Translation.Get(key)` returns a `Translation` struct; it coerces to `string` via implicit operator; it returns the key itself if missing (safe fallback)
3. **`Context.IsMultiplayer`**: why this is the right API vs. `Game1.IsMultiplayer`; when it's safe to read (after `GameLoop.GameLaunched` at earliest)

These are embedded in the Code Generation plan step comments, not as separate doc files.

---

## N/A NFRs

| NFR | Rationale |
|---|---|
| NFR-SAFE-01 | No items collected or moved in this unit |
| NFR-SAFE-02 | No gold transactions |
| NFR-SAFE-03 | No save data written |
| NFR-SAFE-04 | No item pickup by worker |
| NFR-PERF-01 | No per-frame update loop |
| NFR-PERF-02 | No tile scanning |
| NFR-PERF-03 | No zone overlay rendering |
| NFR-UX-01 | Full gamepad nav is a U-09 concern (hiring menus) |
| NFR-UX-03 | Zone overlay is U-11 |
| NFR-MAINT-01 | xUnit project established in U-02 |
| NFR-MAINT-02 | PBT — no domain logic, no FsCheck tests in this unit |
| NFR-MAINT-03 | No Core types introduced (all files are Mod-layer) |
| NFR-MAINT-05 | `dotnet format` applies always; no design decisions |
| NFR-COMPAT-01 | Compatibility docs — README concern, not code |
| NFR-COMPAT-02 | Farm-type support — runtime concern in U-10+ |
| NFR-COMPAT-04 | Required deps (Harmony, MFM, GMCM) — scaffolded in U-01/U-14/U-16 |
| Security Baseline | Disabled project-wide (Q28) |

---

## PBT Extension Compliance

| Rule | Status | Rationale |
|---|---|---|
| PBT-02 (round-trip) | N/A | No serialized types in this unit |
| PBT-03 (invariants) | N/A | No domain invariants to enforce |
| PBT-07 (generator quality) | N/A | No new FsCheck generators needed |
| PBT-08 (shrinking/seed logging) | N/A | No PBT tests in this unit |
| PBT-09 (framework = FsCheck) | Already decided | No new framework decision |
