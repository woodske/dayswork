using Dayswork.Core.Domain;
using Microsoft.Xna.Framework;
using StardewValley;

namespace Dayswork.Worker;

// U-10 thin implementation: warps the NPC to the destination each call.
// Real walking via PathFindController is introduced in U-13 alongside the tool-swap animator.
internal sealed class PathFindControllerAdapter
{
    public bool HasArrived { get; private set; } = true;
    public bool NavigationFailed { get; private set; }

    public void StartNavigation(TileCoord destination, GameLocation location, NPC npc)
    {
        NavigationFailed = false;
        HasArrived = false;

        // Check that the destination tile is passable before warping.
        if (!location.isTilePassable(new xTile.Dimensions.Location(destination.X, destination.Y),
                                     Game1.viewport))
        {
            NavigationFailed = true;
            HasArrived = false;
            return;
        }

        Game1.warpCharacter(npc, location, new Vector2(destination.X, destination.Y));
        HasArrived = true;
    }

    public void Clear()
    {
        HasArrived = true;
        NavigationFailed = false;
    }
}
