using Dayswork.Guards;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;

namespace Dayswork.Integration;

/// <summary>
/// Opens the hire/manage flow when the player action-clicks the hiring building (U-25 WS2).
/// Replaces the bulletin-board entry point. Single-player only (REL-U10-01).
/// </summary>
internal sealed class HiringBuildingInteraction
{
    private readonly IModHelper _helper;

    public HiringBuildingInteraction(IModHelper helper) => _helper = helper;

    public void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsPlayerFree || Game1.activeClickableMenu is not null)
            return;
        if (!e.Button.IsActionButton())
            return;
        if (MultiplayerGuard.IsMultiplayer())
            return;
        if (Game1.currentLocation is not Farm farm)
            return;

        var grab = e.Cursor.GrabTile;
        var gx = (int)grab.X;
        var gy = (int)grab.Y;

        // Only when the action-clicked tile is within our building's footprint and the player stands adjacent.
        var building = FindHiringBuilding(farm);
        if (building is null || !FootprintContains(building, gx, gy))
            return;

        var player = Game1.player.TilePoint;
        if (Math.Abs(player.X - gx) > 1 || Math.Abs(player.Y - gy) > 1)
            return;

        _helper.Input.Suppress(e.Button);

        if (IsOutputChestDisplayTile(building, gx, gy) &&
            building.GetBuildingChest(HiringBuilding.OutputChestId) is { } chest)
        {
            chest.ShowMenu();
            return;
        }

        ModEntry.Coordinator.OpenFromBuilding();
    }

    internal static Building? FindHiringBuilding(Farm farm)
    {
        foreach (var building in farm.buildings)
        {
            if (string.Equals(building.buildingType.Value, HiringBuilding.BuildingType, StringComparison.Ordinal))
                return building;
        }

        return null;
    }

    private static bool FootprintContains(Building building, int x, int y) =>
        x >= building.tileX.Value
        && x < building.tileX.Value + building.tilesWide.Value
        && y >= building.tileY.Value
        && y < building.tileY.Value + building.tilesHigh.Value;

    private static bool IsOutputChestDisplayTile(Building building, int x, int y) =>
        x == building.tileX.Value + HiringBuilding.OutputChestDisplayTile.X
        && y == building.tileY.Value + HiringBuilding.OutputChestDisplayTile.Y;
}
