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
