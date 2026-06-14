namespace Dayswork.Core.Crops;

/// <summary>
/// Pure, total store-hours policy. Decides whether a store is open at a
/// given time of day and day of month, and when it opens. Stores open at 9 AM; Pierre's
/// (SeedShop) is 9 AM–5 PM and closed on Wednesdays; JojaMart is 9 AM–11 PM daily.
/// Time of day uses Stardew's HHMM clock convention (e.g. 900 = 9:00 AM, 1700 = 5:00 PM).
/// </summary>
public static class StoreHoursPolicy
{
    public const int OpenTime = 900;
    public const int PierreCloseTime = 1700;
    public const int JojaCloseTime = 2300;

    /// <summary>Stardew day-of-week: day 1 is Monday, so Wednesday is day-of-month % 7 == 3.</summary>
    public static bool IsWednesday(int dayOfMonth) => dayOfMonth % 7 == 3;

    public static int? OpensAt(Store store, int dayOfMonth)
    {
        if (store == Store.Pierre && IsWednesday(dayOfMonth))
            return null; // closed all day

        return OpenTime;
    }

    public static bool IsOpen(Store store, int timeOfDay, int dayOfMonth)
    {
        var opensAt = OpensAt(store, dayOfMonth);
        if (opensAt is null)
            return false;

        var closeTime = store == Store.Pierre ? PierreCloseTime : JojaCloseTime;
        return timeOfDay >= opensAt.Value && timeOfDay < closeTime;
    }
}
