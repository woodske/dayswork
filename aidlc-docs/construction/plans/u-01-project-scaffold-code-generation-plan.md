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
- [x] Create `Dayswork.sln` at workspace root
- [x] Solution lists the two projects with existing csproj files: `Dayswork.Core`, `Dayswork`
- [x] **Plan deviation**: Dayswork.Tests omitted from .sln (no csproj yet — would break `dotnet build`); U-02 adds the Tests reference

### Step 2 — Create `Dayswork.Core` project
- [x] Create directory `Dayswork.Core/`
- [x] Create `Dayswork.Core/Dayswork.Core.csproj` (net6.0, Nullable=enable, LangVersion=10.0, TreatWarningsAsErrors=true, Newtonsoft.Json 13.0.3, no SMAPI/Stardew refs)

### Step 3 — Create `Dayswork` (the SMAPI mod) project
- [x] Create directory `Dayswork/`
- [x] Create `Dayswork/Dayswork.csproj` (net6.0, EnableHarmony=true, EnableModDeploy=true, EnableModZip=false, ModBuildConfig 4.1.1, ProjectReference to Core, Content includes manifest.json and i18n/default.json)

### Step 4 — Create `Dayswork/manifest.json`
- [x] UniqueID: `Bindicle.Dayswork`, Version: 0.1.0, MinimumApiVersion: 4.0.0, EntryDll: Dayswork.dll, UpdateKeys: []

### Step 5 — Create `Dayswork/ModEntry.cs` stub
- [x] Sealed class `ModEntry : Mod`, file-scoped namespace, Entry() logs "Dayswork loaded" at Info level only

### Step 6 — Create `Dayswork/i18n/default.json`
- [x] Empty `{}` placeholder

### Step 7 — Create `.gitignore`
- [x] .NET / IDE / OS ignores (bin/, obj/, .vs/, *.user, .idea/, .vscode/, .DS_Store, etc.)

### Step 8 — Create `LICENSE`
- [x] MIT, copyright 2026 Kevin Woods (Bindicle)

### Step 9 — Update `README.md`
- [x] Preserved title + tagline; appended Status / Build From Source (VS 2026 instructions) / Solution structure / License / Author sections

### Step 10 — Create code summary doc
- [x] Created `aidlc-docs/construction/U-01-project-scaffold/code/u-01-code-summary.md` with file list, plan deviation note, verification steps, and what U-02 inherits

### Step 11 — Update aidlc-state.md
- [x] Marks U-01 complete; Current Stage advances to U-02

### Step 12 — Update audit.md
- [x] Part 2 execution log appended

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
