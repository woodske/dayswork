using Dayswork.Core.Inventory;
using StardewValley;

namespace Dayswork.Orchestration;

internal static class DebrisItemIdResolver
{
    public static bool TryResolveCollectibleItemId(string? rawItemId, out string qualifiedItemId)
    {
        if (!CollectibleItemIdNormalizer.TryNormalize(rawItemId, out var normalizedItemId))
        {
            qualifiedItemId = "";
            return false;
        }

        var metadata = ItemRegistry.ResolveMetadata(normalizedItemId);
        if (metadata is null || string.IsNullOrWhiteSpace(metadata.QualifiedItemId))
        {
            qualifiedItemId = "";
            return false;
        }

        qualifiedItemId = metadata.QualifiedItemId;
        return true;
    }
}
