using Dayswork.Core.Domain;
using StardewValley;
using StardewValley.Tools;

namespace Dayswork.Integration;

// Progression inheritance: the worker swings the SPONSOR's tools, so the farmer passed in is the
// contract owner (Sponsor.ResolveOrHost), not whoever is at the keyboard. For an owner who is not
// connected that is their farmhandData Farmer — their tools as of their last disconnect.
internal sealed class ToolLevelReader
{
    public ToolSnapshot ReadSnapshot(Farmer player) =>
        new(
            AxeLevel:         FindLevel<Axe>(player),
            PickaxeLevel:     FindLevel<Pickaxe>(player),
            WateringCanLevel: FindLevel<WateringCan>(player));

    // Returns Basic (0) when the tool is not in that farmer's inventory.
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
