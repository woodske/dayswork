namespace Dayswork.Core.Inventory;

public static class CollectibleItemIdNormalizer
{
    public static bool TryNormalize(string? rawItemId, out string qualifiedItemId)
    {
        qualifiedItemId = "";

        if (string.IsNullOrWhiteSpace(rawItemId))
            return false;

        var trimmed = rawItemId.Trim();
        if (trimmed[0] == '(')
        {
            var closingParen = trimmed.IndexOf(')');
            if (closingParen <= 1 || closingParen == trimmed.Length - 1)
                return false;

            qualifiedItemId = trimmed;
            return true;
        }

        qualifiedItemId = $"(O){trimmed}";
        return true;
    }
}
