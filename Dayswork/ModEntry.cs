using StardewModdingAPI;

namespace Dayswork;

public sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        this.Monitor.Log("Dayswork loaded", LogLevel.Info);
    }
}
