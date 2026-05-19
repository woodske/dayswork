# U-01 Project Scaffold — Code Summary

**Unit**: U-01 Project Scaffold
**Construction loop**: Code Generation only (Functional Design, NFR Requirements, NFR Design, Infrastructure Design all SKIPPED)
**Date completed**: 2026-05-18

---

## Files created

| File | Purpose |
|---|---|
| `Dayswork.sln` | Visual Studio solution — lists Dayswork.Core and Dayswork projects (Dayswork.Tests added by U-02) |
| `Dayswork.Core/Dayswork.Core.csproj` | Pure-logic .NET 6 class library; only Newtonsoft.Json as outside dep; no SMAPI/Stardew refs (compile-time enforcement of Core/Mod separation) |
| `Dayswork/Dayswork.csproj` | SMAPI mod project; references Dayswork.Core + Pathoschild.Stardew.ModBuildConfig 4.1.1; EnableHarmony=true; EnableModDeploy=true |
| `Dayswork/ModEntry.cs` | Stub SMAPI entry point — logs "Dayswork loaded" at Info level and nothing else |
| `Dayswork/manifest.json` | SMAPI mod manifest: UniqueID=Bindicle.Dayswork, Version=0.1.0, MinimumApiVersion=4.0.0 |
| `Dayswork/i18n/default.json` | Empty i18n placeholder `{}` — populated by U-08 |
| `.gitignore` | .NET / IDE / OS ignores (bin/, obj/, .vs/, *.user, etc.) |
| `LICENSE` | MIT license, copyright 2026 Kevin Woods (Bindicle) |

## Files modified

| File | Change |
|---|---|
| `README.md` | Preserved existing title + tagline; appended Status, Build From Source, Solution structure, License, Author sections |

---

## Plan deviation note

The approved plan's Step 1 listed Dayswork.Tests in the .sln. This was changed during generation: `Dayswork.Tests.csproj` does not exist until U-02, and a missing-csproj entry in .sln causes `dotnet build Dayswork.sln` to fail — violating this unit's Definition of Done. U-02 will add the Tests project reference to the .sln as part of its own Code Generation.

---

## Key project properties

### Dayswork.Core
- `TargetFramework`: net6.0
- `Nullable`: enable
- `LangVersion`: 10.0 (record types, file-scoped namespaces)
- `TreatWarningsAsErrors`: true
- Only external dependency: `Newtonsoft.Json 13.0.3`

### Dayswork
- `TargetFramework`: net6.0
- `Nullable`: enable, `LangVersion`: 10.0, `TreatWarningsAsErrors`: true
- `EnableHarmony`: true (required for BulletinBoardPatch in U-08)
- `EnableModDeploy`: true (auto-copies to `<StardewModsPath>/Dayswork/` after build)
- `EnableModZip`: false (deferred to U-16 release candidate)
- External package: `Pathoschild.Stardew.ModBuildConfig 4.1.1`

---

## Definition of Done — verification steps

1. Run `dotnet build Dayswork.sln` from workspace root. Expected: **0 errors, 0 warnings** (TreatWarningsAsErrors=true makes any warning a build failure).
2. Confirm the auto-deploy placed the mod in `<StardewModsPath>/Dayswork/` containing: `Dayswork.dll`, `Dayswork.Core.dll`, `manifest.json`, `i18n/default.json`.
3. Launch Stardew Valley via SMAPI. Expected: SMAPI console shows `[Dayswork] Dayswork loaded` at Info level during startup, with no SMAPI warnings about missing fields or unsupported manifest properties.

**Important**: Step 3 is verified by the user — this assistant cannot launch Stardew Valley. If `dotnet build` fails due to environment issues (missing .NET 6 SDK, ModBuildConfig unable to locate Stardew install), check:
- Is .NET 6 SDK installed? Run `dotnet --version`.
- Did NuGet restore packages? ModBuildConfig's MSBuild targets file must be present in `~/.nuget/packages/pathoschild.stardew.modbulidconfig/4.1.1/`.
- Is `STARDEW_MODS_PATH` or equivalent set, or does ModBuildConfig auto-detect from the default Steam install path?

---

## What U-02 inherits from U-01

- `Dayswork.sln` — U-02 adds the `Dayswork.Tests` project reference to this file
- `Dayswork.Core/Dayswork.Core.csproj` — U-02's test csproj references this
- Project naming conventions, `<LangVersion>10.0</LangVersion>`, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — U-02 mirrors these in its own csproj
