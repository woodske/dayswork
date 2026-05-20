using Dayswork.Core.Domain;
using Dayswork.UI;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace Dayswork.Integration;

// Integration bridge between Core ChestRef and live Stardew game state.
// Singleton — constructed once in ModEntry. All methods query game state fresh on each call.
internal sealed class ChestResolver
{
    private readonly IModHelper _helper;

    internal ChestResolver(IModHelper helper) => _helper = helper;

    // Returns all accessible chests on the farm and in buildings.
    // Called once when ZoneAndChestMenu opens — never per frame (NFR-PERF-01).
    internal List<ChestEntry> GetAllChests(GameLocation farm)
    {
        var result = new List<ChestEntry>();
        string farmGroup = I18nHelper.Get("ui.zone_chest.group_farm");

        // Open-farm chests
        foreach (var (tile, obj) in farm.Objects.Pairs)
        {
            if (obj is Chest chest)
            {
                var tileX = (int)tile.X;
                var tileY = (int)tile.Y;
                var chestRef = new ChestRef(farm.Name, new TileCoord(tileX, tileY));
                result.Add(new ChestEntry(chestRef, GetDisplayName(chest, farm, tileX, tileY), farmGroup));
            }
        }

        // Building-interior chests
        if (farm is Farm f)
        {
            foreach (var building in f.buildings)
            {
                var indoors = building.indoors.Value;
                if (indoors == null) continue;

                string buildingName = building.buildingType.Value;
                foreach (var (tile, obj) in indoors.Objects.Pairs)
                {
                    if (obj is Chest chest)
                    {
                        var tileX = (int)tile.X;
                        var tileY = (int)tile.Y;
                        var chestRef = new ChestRef(indoors.Name, new TileCoord(tileX, tileY));
                        result.Add(new ChestEntry(chestRef, GetDisplayName(chest, indoors, tileX, tileY), buildingName));
                    }
                }
            }
        }

        return result;
    }

    // Resolves a stored ChestRef to a live Chest. Returns null if the chest was moved or destroyed (FR-HIRE-08).
    internal Chest? ResolveChest(ChestRef chestRef)
    {
        var location = Game1.getLocationFromName(chestRef.LocationName);
        if (location == null) return null;

        var tile = new Vector2(chestRef.Tile.X, chestRef.Tile.Y);
        return location.Objects.TryGetValue(tile, out var obj) && obj is Chest chest ? chest : null;
    }

    // Generates i18n-aware display name per FR-HIRE-07.
    // Uses chest.Name if set by the player; otherwise falls back to "{building} — Chest at {x}, {y}".
    internal string GetDisplayName(Chest chest, GameLocation location, int tileX, int tileY)
    {
        if (!string.IsNullOrWhiteSpace(chest.Name) && chest.Name != "Chest")
            return chest.Name;

        return I18nHelper.Get("ui.zone_chest.chest_fallback_name",
            new { buildingName = location.Name, x = tileX, y = tileY });
    }

    // Returns building footprint outlines for ZoneDrawOverlay and building-select mode.
    internal List<BuildingOutline> GetBuildingOutlines(Farm farm)
    {
        var result = new List<BuildingOutline>();
        foreach (var building in farm.buildings)
        {
            // Some buildings (e.g. Greenhouse) are primary world locations whose interior is
            // not linked via building.indoors — fall back to a lookup by building type name.
            var indoors = building.indoors.Value
                          ?? Game1.getLocationFromName(building.buildingType.Value);
            if (indoors == null) continue;

            result.Add(new BuildingOutline(
                indoors.Name,
                new Rectangle(building.tileX.Value, building.tileY.Value,
                               building.tilesWide.Value, building.tilesHigh.Value),
                building.buildingType.Value));
        }
        return result;
    }
}
