using Dayswork.Guards;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;

namespace Dayswork.Integration;

/// <summary>
/// Handles action-clicks on the hiring building. Clicking anywhere on the building footprint opens
/// the hire/manage flow, except the two porch chests which open their own UI. Single-player only.
/// </summary>
internal sealed class HiringBuildingInteraction
{
    private readonly IModHelper _helper;

    public HiringBuildingInteraction(IModHelper helper) => _helper = helper;

    public void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsPlayerFree || Game1.activeClickableMenu is not null)
            return;
        // Also accept MouseLeft: on Android gamepadControls=true causes IsActionButton()
        // to reject touch taps (which fire as SButton.MouseLeft).
        if (!e.Button.IsActionButton() && e.Button != SButton.MouseLeft)
            return;
        if (MultiplayerGuard.IsMultiplayer())
            return;
        if (Game1.currentLocation is not Farm farm)
            return;

        // Use Tile (actual tap position) not GrabTile: on Android, GrabTile snaps to the
        // nearest reachable tile, which is outside the blocked building footprint.
        var grab = e.Cursor.Tile;
        var gx = (int)grab.X;
        var gy = (int)grab.Y;

        // Only when the action-clicked tile is within our building's footprint and the player is
        // standing next to the building (the board sits high on the wall, so we check proximity to
        // the whole footprint rather than to the clicked tile).
        var building = FindHiringBuilding(farm);
        if (building is null || !FootprintContains(building, gx, gy))
            return;
        if (!PlayerNextToFootprint(building, Game1.player.TilePoint))
            return;

        // Input chest on the porch.
        if (IsInputChestDisplayTile(building, gx, gy) &&
            building.GetBuildingChest(HiringBuilding.InputChestId) is { } inputChest)
        {
            _helper.Input.Suppress(e.Button);
            inputChest.ShowMenu();
            return;
        }

        // Output chest on the porch.
        if (IsOutputChestDisplayTile(building, gx, gy) &&
            building.GetBuildingChest(HiringBuilding.OutputChestId) is { } chest)
        {
            _helper.Input.Suppress(e.Button);
            chest.ShowMenu();
            return;
        }

        // Any non-chest tile on the building opens the hire/manage flow.
        _helper.Input.Suppress(e.Button);
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

    private static bool IsInputChestDisplayTile(Building building, int x, int y) =>
        x == building.tileX.Value + HiringBuilding.InputChestDisplayTile.X
        && y == building.tileY.Value + HiringBuilding.InputChestDisplayTile.Y;

    /// <summary>True when the player stands within one tile of the building's footprint (any side).</summary>
    private static bool PlayerNextToFootprint(Building building, Point player)
    {
        int nx = Math.Clamp(player.X, building.tileX.Value, building.tileX.Value + building.tilesWide.Value - 1);
        int ny = Math.Clamp(player.Y, building.tileY.Value, building.tileY.Value + building.tilesHigh.Value - 1);
        return Math.Max(Math.Abs(player.X - nx), Math.Abs(player.Y - ny)) <= 1;
    }
}
