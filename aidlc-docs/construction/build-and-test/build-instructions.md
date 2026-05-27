# Build Instructions - U-WR Worker Routing and Dynamic Task Selection

## Prerequisites

- **Build tool**: .NET SDK capable of building `net6.0` projects.
- **Solution**: `Dayswork.sln`.
- **Projects**:
  - `Dayswork.Core`
  - `Dayswork`
  - `Dayswork.Tests`
- **Game/mod dependencies**: Stardew Valley / SMAPI mod build references are resolved by the existing project configuration and NuGet restore.
- **Workspace**: `C:\Users\kwood\Repos\dayswork`.

## Build Steps

### 1. Restore Dependencies

```powershell
dotnet restore Dayswork.sln
```

Expected result: all projects restore successfully, or report that dependencies are already up to date.

### 2. Build Without Mod Deployment

```powershell
dotnet build Dayswork.sln /p:EnableModDeploy=false
```

Expected result:

- `Dayswork.Core` builds successfully.
- `Dayswork` builds successfully.
- `Dayswork.Tests` builds successfully.
- Build ends with `0 Warning(s)` and `0 Error(s)`.

Latest verified result for U-WR:

- Command: `dotnet build Dayswork.sln /p:EnableModDeploy=false`
- Status: passed
- Warnings: `0`
- Errors: `0`

### 3. Optional Local Mod Deploy Build

```powershell
dotnet build Dayswork.sln
```

This uses the project default deploy setting and may copy the mod to the configured Stardew Valley `Mods\Dayswork` folder.

Use this only when you want the local game install refreshed for play-testing.

## Build Artifacts

- `Dayswork.Core/bin/Debug/net6.0/Dayswork.Core.dll`
- `Dayswork/bin/Debug/net6.0/Dayswork.dll`
- `Dayswork.Tests/bin/Debug/net6.0/Dayswork.Tests.dll`

## Troubleshooting

### Dependency Restore Fails

- Confirm the .NET SDK is available with `dotnet --info`.
- Rerun `dotnet restore Dayswork.sln`.
- If NuGet/network access is unavailable, retry once connectivity or package cache access is restored.

### Mod Deploy Fails Because Game Files Are Locked

- Use `/p:EnableModDeploy=false` for compile-only validation.
- Close Stardew Valley and SMAPI before running a deploy build.

### Compilation Fails

- Review the first compiler error, not only the final summary.
- Fix the source or test file reported by the compiler.
- Rerun `dotnet build Dayswork.sln /p:EnableModDeploy=false`.
