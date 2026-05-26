namespace Dayswork.Core.Shifts;

public static class HitReactionPolicy
{
    public static bool ShouldTriggerEmote(
        bool isSwinging,
        bool wasSwinging,
        float distanceTiles,
        float hitRangeTiles)
    {
        if (!isSwinging || wasSwinging)
            return false;

        return distanceTiles <= hitRangeTiles;
    }
}
