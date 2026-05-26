# Build Instructions — Dayswork Fixed-Price Redesign

## Prerequisites

- .NET 6 SDK
- Stardew Valley 1.6 with a writable `Mods/` folder
- Mail Framework Mod installed in the game `Mods/` folder
- Generic Mod Config Menu installed only if you want to verify the redesign settings UI

## Main Build Commands

### Compile only

Use this when the game or SMAPI is still open:

```bash
dotnet build Dayswork.sln /p:EnableModDeploy=false
```

Expected result:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Compile and deploy to the live mod folder

Use this when Stardew Valley and SMAPI are closed:

```bash
dotnet build Dayswork.sln
```

This publishes the current mod build to:

```text
X:\Steam\steamapps\common\Stardew Valley\Mods\Dayswork\
```

## Redesign Build Notes

- The saved `config.json` surface is redesign-only after U-24. Old hourly/deposit tuning fields are no longer part of the supported player-facing config shape.
- GMCM now exposes only redesign-era pricing, worker stamina, and worker behavior settings.
- Internal compatibility fields such as legacy hourly/deposit bridge values are still derived at runtime for transitional persistence paths, but they are not player-tunable.

## Artifact Checks

After a successful build, verify these files exist:

| Artifact | Location |
|---|---|
| `Dayswork.dll` | `Dayswork/bin/Debug/net6.0/` |
| `Dayswork.Core.dll` | `Dayswork.Core/bin/Debug/net6.0/` |
| `manifest.json` | `Dayswork/bin/Debug/net6.0/` and deployed mod folder |
| `i18n/default.json` | `Dayswork/bin/Debug/net6.0/i18n/` and deployed mod folder |

## Troubleshooting

### Deployed DLL is locked

Cause:
Stardew Valley, SMAPI, or another process still has the mod loaded.

Fix:
Close the game and SMAPI completely, then rerun `dotnet build Dayswork.sln`.

### Mod loads but redesign settings are missing

Cause:
Generic Mod Config Menu is not installed, failed to load, or the deployed mod folder is stale.

Fix:
Install GMCM, rebuild, and confirm the live `Mods/Dayswork/` folder timestamp changed.

### Build succeeds locally but the game still shows old behavior

Cause:
The compile-only build updated `bin/Debug` but did not deploy the new DLLs to the live mod folder.

Fix:
Run the full deploy build with the game closed, then relaunch Stardew Valley.
