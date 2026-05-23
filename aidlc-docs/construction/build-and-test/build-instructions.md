# Build Instructions — Dayswork SMAPI Mod

## Prerequisites

- **Build Tool**: .NET 6 SDK (`dotnet` CLI)
- **IDE (optional)**: Visual Studio 2022 or Rider
- **Game**: Stardew Valley installed via Steam (tested against SDV 1.6)
- **Mods folder**: Stardew Valley `Mods/` directory must be writable for auto-deploy
- **Required mod dependencies** (must be installed in the game's `Mods/` folder):
  - Mail Framework Mod `>= 1.20.0` (UniqueID: `DIGUS.MailFrameworkMod`)
- **Optional mod dependencies**:
  - Generic Mod Config Menu `>= 1.14.0` (UniqueID: `spacechase0.GenericModConfigMenu`)
- **NuGet packages**: Restored automatically by MSBuild on first build

## Build Steps

### 1. Restore NuGet Packages (first time only)

```bash
dotnet restore Dayswork.sln
```

### 2. Build — Compile Only (no game deploy)

Use this when the game is running or you only want to verify compilation:

```bash
dotnet build Dayswork.sln /p:EnableModDeploy=false
```

Expected output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### 3. Build — Compile and Deploy

Use this when the game is closed. Automatically copies the mod to:
`X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork\`

```bash
dotnet build Dayswork.sln
```

Expected output:
```
[mod build package] Handling build with options ... EnableModDeploy: true ...
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### 4. Verify Build Artifacts

After a successful build, confirm these files exist:

| File | Location |
|------|----------|
| `Dayswork.dll` | `Dayswork/bin/Debug/net6.0/` |
| `Dayswork.Core.dll` | `Dayswork.Core/bin/Debug/net6.0/` |
| `manifest.json` | `Mods/Dayswork/manifest.json` |
| `Dayswork.dll` (deployed) | `Mods/Dayswork/Dayswork.dll` |

## Project Structure

| Project | Purpose |
|---------|---------|
| `Dayswork/` | Main SMAPI mod entry point, UI, integration layer |
| `Dayswork.Core/` | Business logic — domain, shifts, capabilities, workers |
| `Dayswork.Tests/` | Unit + lint tests |

## Troubleshooting

### `DLL locked` error during deploy
- **Cause**: Stardew Valley or SMAPI is running and has locked the mod DLL.
- **Solution**: Close the game, then rebuild with the full deploy command.

### `Could not load file or assembly` at game startup
- **Cause**: A required dependency mod is missing from the `Mods/` folder.
- **Solution**: Verify Mail Framework Mod is installed. Check SMAPI console for the specific missing assembly.

### NuGet restore fails
- **Cause**: No internet access or private feed not configured.
- **Solution**: `dotnet restore Dayswork.sln --no-cache` or restore from a network-accessible machine and commit the packages.
