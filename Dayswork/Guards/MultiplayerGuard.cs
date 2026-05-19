using StardewModdingAPI;

namespace Dayswork.Guards;

internal static class MultiplayerGuard
{
    internal static bool IsMultiplayer() => Context.IsMultiplayer;
}
