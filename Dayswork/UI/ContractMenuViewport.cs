namespace Dayswork.UI;

internal static class ContractMenuViewport
{
    public static int GetVisibleCount(int availableHeight, int itemHeight, int gap = 0)
    {
        if (itemHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(itemHeight));

        return Math.Max(1, (availableHeight + gap) / (itemHeight + gap));
    }

    public static int GetVisibleVariableCount(
        IReadOnlyList<int> itemHeights,
        int startIndex,
        int availableHeight)
    {
        if (itemHeights.Count == 0)
            return 0;

        startIndex = Math.Clamp(startIndex, 0, itemHeights.Count - 1);

        var usedHeight = itemHeights[startIndex];
        var visibleCount = 1;

        for (var i = startIndex + 1; i < itemHeights.Count; i++)
        {
            if (usedHeight + itemHeights[i] > availableHeight)
                break;

            usedHeight += itemHeights[i];
            visibleCount++;
        }

        return visibleCount;
    }
}
