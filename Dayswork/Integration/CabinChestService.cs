using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;

namespace Dayswork.Integration;

internal sealed class CabinChestService
{
    internal const string InputChestNameKey = "building.office.input_chest.name";
    internal const string OutputChestNameKey = "building.office.output_chest.name";

    public void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) => EnsureOfficeChests(Game1.getFarm());

    public void OnDayStarted(object? sender, DayStartedEventArgs e) => EnsureOfficeChests(Game1.getFarm());

    internal void EnsureOfficeChests(Farm? farm)
    {
        if (farm is null)
            return;

        foreach (var building in farm.buildings)
        {
            if (!IsHiringBuilding(building))
                continue;

            EnsureInputChest(building);
            ApplyDisplayNameFormats(building);
        }
    }

    internal Chest? EnsureInputChest(Building office)
    {
        if (!IsHiringBuilding(office))
            return null;

        var chest = office.GetBuildingChest(HiringBuilding.InputChestId);
        if (chest is not null)
            return chest;

        // Stardew normally creates BuildingData.Chests for new buildings. This path covers existing
        // offices from saves created before the input chest was declared.
        chest = new Chest(true)
        {
            Name = HiringBuilding.InputChestId,
        };
        office.buildingChests.Add(chest);
        return chest;
    }

    internal void ApplyDisplayNameFormats(Building office)
    {
        if (!IsHiringBuilding(office))
            return;

        ApplyDisplayNameFormat(office.GetBuildingChest(HiringBuilding.InputChestId), InputChestNameKey);
        ApplyDisplayNameFormat(office.GetBuildingChest(HiringBuilding.OutputChestId), OutputChestNameKey);
    }

    internal Chest? TryGetInputChest(Building office) =>
        IsHiringBuilding(office) ? office.GetBuildingChest(HiringBuilding.InputChestId) : null;

    internal Chest? TryGetOutputChest(Building office) =>
        IsHiringBuilding(office) ? office.GetBuildingChest(HiringBuilding.OutputChestId) : null;

    internal bool IsBuiltInInputChestTile(Farm? farm, int x, int y) =>
        HiringBuilding.TryGetInputChestTile(farm) is { } tile && tile.X == x && tile.Y == y;

    internal bool IsBuiltInOutputChestTile(Farm? farm, int x, int y) =>
        HiringBuilding.TryGetOutputChestTile(farm) is { } tile && tile.X == x && tile.Y == y;

    internal static bool IsInputDisplayTile(int localX, int localY) =>
        HiringBuilding.IsInputChestDisplayTile(localX, localY);

    internal static bool IsOutputDisplayTile(int localX, int localY) =>
        HiringBuilding.IsOutputChestDisplayTile(localX, localY);

    internal static Point ToAbsoluteTile(Building building, Point displayTile) =>
        new(building.tileX.Value + displayTile.X, building.tileY.Value + displayTile.Y);

    private static bool IsHiringBuilding(Building building) =>
        string.Equals(building.buildingType.Value, HiringBuilding.BuildingType, StringComparison.Ordinal);

    internal static string ToLocalizedTextToken(string key) => $"[LOCALIZED_TEXT {key}]";

    private static void ApplyDisplayNameFormat(Chest? chest, string key)
    {
        if (chest is null)
            return;

        chest.displayNameFormat = ToLocalizedTextToken(key);
    }
}
