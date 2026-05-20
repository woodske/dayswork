using Dayswork.Core.Domain;
using StardewValley;
using StardewValley.Tools;

namespace Dayswork.Integration;

internal sealed class ToolLevelReader
{
    public ToolSnapshot ReadSnapshot(Farmer player) =>
        new(
            AxeLevel:         FindLevel<Axe>(player),
            PickaxeLevel:     FindLevel<Pickaxe>(player),
            WateringCanLevel: FindLevel<WateringCan>(player));

    // Returns Basic (0) when the tool is not in the player's inventory (FR-TOOL-03, FD-Q3: A).
    private static ToolLevel FindLevel<T>(Farmer player) where T : Tool
    {
        foreach (var item in player.Items)
        {
            if (item is T tool)
                return (ToolLevel)tool.UpgradeLevel;
        }
        return ToolLevel.Basic;
    }
}
