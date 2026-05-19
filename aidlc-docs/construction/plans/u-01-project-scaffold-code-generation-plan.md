# U-01 Project Scaffold — Code Generation Plan

**Unit**: U-01 Project Scaffold (see [unit-of-work.md](../../inception/application-design/unit-of-work.md))
**Stage**: CONSTRUCTION → U-01 → Code Generation (Part 1: Planning)
**Workspace root**: `C:\Users\kwood\Repos\dayswork`
**Project type**: Greenfield, multi-unit, single deployable artifact (.NET 6 / SMAPI 4.x)

---

## Unit context

### Stories assigned to U-01
- **None** delivered directly to the player. U-01 is foundational; it underpins **S-19** (pure-logic separable from SMAPI for testability) by establishing the three-project split that the rest of construction depends on.

### Component owned
- **M-01 ModEntry** — stub form only. `Entry()` logs `"Dayswork loaded"` and nothing else. Extended in every subsequent Mod-introducing unit.

### Dependencies on other units
- **None.** U-01 is the root of the construction DAG.

### Dependencies this unit unblocks
- **U-02 Test Infrastructure** (needs the solution file to add `Dayswork.Tests` to)
- **U-08 Bulletin Board + i18n + MP Guard** (needs the `Dayswork` project and `manifest.json` to extend)

### Skipped Construction stages for U-01 (logged decisions)
| Stage | Decision | Rationale |
|---|---|---|
| Functional Design | SKIP | No business logic; only project files and a 5-line log statement |
| NFR Requirements | SKIP | Architectural NFRs (NFR-MAINT-01..03 testability; NFR-MAINT-04 patch isolation) are enforced *by* the project file structure that is itself the deliverable of this unit. There is no separate NFR doc to produce. |
| NFR Design | SKIP | Cascades from NFR Requirements skip |
| Infrastructure Design | SKIP | Per execution plan, all units skip Infrastructure Design (Dayswork has no cloud/IaC layer; SMAPI is the platform) |

### Definition of Done (carry-over from [unit-of-work.md](../../inception/application-design/unit-of-work.md))
> `dotnet build` succeeds; dropping the compiled mod into `Stardew Valley/Mods/Dayswork/` and launching SMAPI shows "Dayswork loaded" in the SMAPI console.

---

## Code Generation Steps (Part 2 — executes after approval)

### Step 1 — Create solution file
- [ ] Create `Dayswork.sln` at workspace root
- [ ] Solution lists the three projects: `Dayswork.Core`, `Dayswork`, `Dayswork.Tests`
- [ ] Place `Dayswork.Tests` reference even though U-02 creates the actual csproj — leaving an unbuilt-but-referenced project is the simpler alternative to re-editing the .sln in U-02. (Alternative: U-02 adds the reference itself; both are valid. Default to "U-01 lists all three, U-02 fills in the missing csproj" for forward stability.)

### Step 2 — Create `Dayswork.Core` project
- [ ] Create directory `Dayswork.Core/`
- [ ] Create `Dayswork.Core/Dayswork.Core.csproj`:
  - `<TargetFramework>net6.0</TargetFramework>`
  - `<Nullable>enable</Nullable>` (catch null-ref bugs early per NFR-SAFE-04 spirit)
  - `<LangVersion>10.0</LangVersion>` (record types, file-scoped namespaces)
  - `<ImplicitUsings>enable</ImplicitUsings>`
  - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (enforces clean code from day one)
  - PackageReference to `Newtonsoft.Json` (explicit, so Core stays self-contained per [component-dependency.md](../../inception/application-design/component-dependency.md) rule 1)
  - **No** PackageReference or ProjectReference to SMAPI / StardewValley / Harmony — this is what makes the Core/Mod separation a compile-time guarantee

### Step 3 — Create `Dayswork` (the SMAPI mod) project
- [ ] Create directory `Dayswork/`
- [ ] Create `Dayswork/Dayswork.csproj`:
  - `<TargetFramework>net6.0</TargetFramework>`, `<Nullable>enable</Nullable>`, `<LangVersion>10.0</LangVersion>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
  - `<EnableHarmony>true</EnableHarmony>` (per components.md M-01)
  - `<EnableModDeploy>true</EnableModDeploy>` and `<EnableModZip>false</EnableModZip>` (auto-deploy to `<StardewModsPath>/Dayswork/` on build for fast iteration; defer release-zip until U-16)
  - PackageReference to `Pathoschild.Stardew.ModBuildConfig` (`Version="4.*"` to track current; this brings in SMAPI/Stardew/Harmony references via the standard Stardew dev pattern)
  - ProjectReference to `..\Dayswork.Core\Dayswork.Core.csproj`
  - Include `manifest.json` and `i18n/default.json` as `Content` with `CopyToOutputDirectory="Always"`

### Step 4 — Create `Dayswork/manifest.json`
- [ ] UniqueID: `Bindicle.Dayswork` (per user's modding handle from memory)
- [ ] Name: `Dayswork`
- [ ] Author: `Bindicle`
- [ ] Version: `0.1.0`
- [ ] Description: `Hire NPC farmhands from the Pelican Town bulletin board to work your farm while you adventure.`
- [ ] MinimumApiVersion: `4.0.0` (SMAPI 4.x per Q2)
- [ ] EntryDll: `Dayswork.dll`
- [ ] UpdateKeys: leave empty for now (filled when first published; deferred)
- [ ] Dependencies / OptionalDependencies: leave empty for now (MFM added in U-14; GMCM in U-16 per [unit-of-work.md](../../inception/application-design/unit-of-work.md))

### Step 5 — Create `Dayswork/ModEntry.cs` stub
- [ ] File contents: a single sealed class `ModEntry : Mod` in namespace `Dayswork` with an `Entry(IModHelper helper)` override that calls `this.Monitor.Log("Dayswork loaded", LogLevel.Info);` and nothing else
- [ ] Use file-scoped namespace syntax (`namespace Dayswork;`) — convention for the project per `<LangVersion>10.0</LangVersion>`

### Step 6 — Create `Dayswork/i18n/default.json`
- [ ] Empty JSON object `{}` (placeholder; U-08 populates first real entries)
- [ ] Ensures SMAPI's i18n helper has *something* to load and doesn't log a warning at startup

### Step 7 — Create `.gitignore`
- [ ] Standard .NET ignores: `bin/`, `obj/`, `*.user`, `*.suo`, `.vs/`, `*.lock.json`, `out/`, `[Bb]in/`, `[Oo]bj/`
- [ ] IDE: `.idea/`, `.vscode/` (kept locally where individual prefs differ)
- [ ] OS: `.DS_Store`, `Thumbs.db`
- [ ] Do NOT ignore `.csproj`, `.sln`, `manifest.json`, `i18n/`, source files

### Step 8 — Create `LICENSE`
- [ ] MIT license per Q7 decision
- [ ] Copyright 2026 Kevin Woods (Bindicle)
- [ ] Standard MIT text

### Step 9 — Update `README.md`
- [ ] Preserve existing title `# Dayswork` and tagline `A Stardew Valley mod for hiring NPC farmhands`
- [ ] Append: **Status** section (currently "Pre-alpha — v1 development in progress")
- [ ] Append: **Build From Source** section with Visual Studio 2026 instructions (per Q3 decision)
- [ ] Append: **License** section (MIT)
- [ ] Append: **Author** section (Bindicle)

### Step 10 — Create code summary doc
- [ ] Create `aidlc-docs/construction/U-01-project-scaffold/code/u-01-code-summary.md`
- [ ] Lists every file created in U-01 with its path and one-line purpose
- [ ] Records the verification steps that satisfy the Definition of Done

### Step 11 — Update aidlc-state.md
- [ ] Mark U-01 Construction loop complete (pending user approval gate)
- [ ] Current Stage advances to U-02 Test Infrastructure
- [ ] Record the per-unit-stage decisions table for U-01

### Step 12 — Update audit.md
- [ ] Append entry recording Part 2 execution complete

---

## Files this plan will produce

| File | Type | Purpose |
|---|---|---|
| `Dayswork.sln` | created | Solution file referencing all 3 projects |
| `Dayswork.Core/Dayswork.Core.csproj` | created | Pure-logic .NET 6 class library (no SMAPI refs) |
| `Dayswork/Dayswork.csproj` | created | SMAPI mod project, references Core + ModBuildConfig |
| `Dayswork/ModEntry.cs` | created | Stub entry that logs "Dayswork loaded" |
| `Dayswork/manifest.json` | created | SMAPI mod manifest |
| `Dayswork/i18n/default.json` | created | Empty i18n placeholder |
| `.gitignore` | created | .NET / IDE / OS ignores |
| `LICENSE` | created | MIT |
| `README.md` | modified | Extends existing title with Status/Build/License/Author sections |
| `aidlc-docs/construction/U-01-project-scaffold/code/u-01-code-summary.md` | created | Markdown summary of U-01 artifacts |
| `aidlc-docs/aidlc-state.md` | modified | Marks U-01 complete; advances Current Stage to U-02 |
| `aidlc-docs/audit.md` | modified | Appends Part 2 execution log entry |

**Total**: 10 application-code/config files created, 1 modified (README.md); 3 documentation files modified/created.

---

## Verification approach

Verification is **manual** for U-01 — there is no test code yet (test project itself is the deliverable of U-02). The Definition-of-Done check is:

1. From the workspace root, run `dotnet build Dayswork.sln`. Expected: build succeeds with 0 warnings, 0 errors. (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` makes any warning a build failure.)
2. Confirm the auto-deploy step placed the mod at `<StardewModsPath>/Dayswork/` containing `Dayswork.dll`, `Dayswork.Core.dll`, `manifest.json`, and `i18n/default.json`.
3. Launch Stardew Valley via SMAPI. Expected: SMAPI console shows `[Dayswork] Dayswork loaded` at info-level during startup; no errors; no warnings about i18n.

Verification will be **noted** in the Part 2 completion message but **performed by the user** (this assistant cannot launch Stardew). If `dotnet build` fails during Part 2 due to environment issues (missing SDK, missing Stardew install for ModBuildConfig's path detection, etc.), Part 2 will report the failure and propose fixes rather than mark this stage complete.

---

## Open questions for the user

None at planning time. All decisions were locked during Inception:
- Tech stack: .NET 6 + SMAPI 4.x (Q2)
- IDE: Visual Studio 2026 (Q3 + revision)
- Test framework: xUnit (Q4) — but `Dayswork.Tests` is U-02's deliverable, not U-01's
- License: MIT (Q7)
- UniqueID prefix: Bindicle (Q8)
- Composition root pattern: D2 (hand-wired, no DI container)
- Solution layout: D1 (3-project split — Core / Mod / Tests)
- Manifest declares MFM dependency: deferred to U-14 (V9 decision)
- GMCM optional dependency: deferred to U-16

If any of these need revisiting before generation, the user should flag now.
