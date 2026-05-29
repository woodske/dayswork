# Dayswork
A Stardew Valley mod for hiring NPC farmhands

## Status

Pre-alpha — v1 development in progress. Not yet available for download.

## Build From Source

### Prerequisites

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- [Visual Studio 2026](https://visualstudio.microsoft.com/) (with the .NET desktop development workload)
- [Stardew Valley](https://www.stardewvalley.net/) 1.6.x installed locally
- [SMAPI](https://smapi.io/) 4.x installed

### Steps

1. Clone the repository
2. Open `Dayswork.sln` in Visual Studio 2026
3. Build the solution (`Ctrl+Shift+B`)

`Pathoschild.Stardew.ModBuildConfig` (included via NuGet) automatically detects your Stardew Valley install path and:
- Resolves the SMAPI / StardewValley assembly references
- Copies the compiled mod into `<StardewModsPath>/Dayswork/` after each build

No manual copy step is needed. Launch Stardew Valley via SMAPI after a successful build.

### Verify the mod loads

Open SMAPI's console — you should see:

```
[Dayswork] Dayswork loaded
```

## Releasing to Nexus Mods

`Pathoschild.Stardew.ModBuildConfig` produces the Nexus-ready release zip automatically.
Zip creation is currently disabled for normal builds via `<EnableModZip>false</EnableModZip>`
in `Dayswork/Dayswork.csproj`; enable it for releases (gating it to Release builds keeps
local dev builds fast):

```xml
<EnableModZip Condition="'$(Configuration)' == 'Release'">true</EnableModZip>
```

### What the zip contains

A single mod folder with only the runtime files — no source, no `bin`/`obj`, no `.csproj`:

```
Dayswork/
  Dayswork.dll
  Dayswork.Core.dll   (bundled automatically from the project reference)
  manifest.json
  i18n/default.json
```

This single-folder layout is what SMAPI's installer expects. Do not manually rezip — a
zip with loose files at the root, or a doubled `Dayswork/Dayswork/` nesting, is the most
common cause of install failures.

### Steps

1. Bump `Version` in `Dayswork/manifest.json`. SMAPI and Nexus compare against this value.
2. Enable the release zip (see above).
3. Build Release: `dotnet build Dayswork/Dayswork.csproj -c Release`
4. Grab the zip from `Dayswork/bin/Release/Dayswork <version>.zip` (e.g. `Dayswork 0.1.0.zip`).
5. On Nexus (Stardew Valley section): create or open the mod page → **Files** tab →
   **Add file** → upload the zip, set the version field to match the manifest, mark it as
   the Main file.

### Notes

- Keep the manifest version, the zip name, and the Nexus file version in lockstep — this
  is what makes the `Nexus:<id>` update key in `manifest.json` report updates correctly.
- The numeric mod ID in the update key comes from the Nexus page URL
  (`nexusmods.com/stardewvalley/mods/12345` → `Nexus:12345`).
- List the dependencies on the Nexus page description with links: **MailFrameworkMod**
  (required — SMAPI blocks loading without it) and **Generic Mod Config Menu** (optional).

## Solution structure

```
Dayswork.sln
├── Dayswork.Core/    Pure C# — no SMAPI / Stardew refs. All testable business logic lives here.
├── Dayswork/         SMAPI mod. References Core + SMAPI/Stardew via ModBuildConfig.
└── Dayswork.Tests/   xUnit + FsCheck. References only Dayswork.Core.   (added in U-02)
```

## License

[MIT](LICENSE)

## Author

Bindicle — [Nexus Mods](https://www.nexusmods.com/users/bindicle)
