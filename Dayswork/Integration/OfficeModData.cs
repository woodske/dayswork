using Dayswork.Core.Domain;
using Season = Dayswork.Core.Domain.Season;
using StardewValley;
using StardewValley.Buildings;

namespace Dayswork.Integration;

/// <summary>
/// The office's own <c>Building.modData</c> keys, and the only code that touches them.
///
/// Contracts live on their building rather than in host save data because that makes the contract
/// share the building's identity for free — demolishing the office removes the contract, moving it
/// keeps it, and no id-keyed bookkeeping has to be reconciled on <c>BuildingListChanged</c>.
/// <c>Building.modData</c> is a synced net field, so every connected player already has every
/// contract: the read-only status card and the cross-owner zone overlay need no message channel
/// (see the 2.0 plan's D1).
///
/// Writing is host-only by discipline — nothing in the netcode enforces it — so every write goes
/// through <see cref="OfficeContractPersistence"/> or the day-state helpers below.
/// </summary>
internal static class OfficeModData
{
    /// <summary>The office's one contract, as a schema-versioned envelope (see
    /// <c>SaveDataSerializer.SerializeOne</c>). Absent when the office has never been hired from.</summary>
    internal const string ContractKey = "Bindicle.Dayswork/Contract";

    /// <summary>Day stamp ("day/season/year") of the last shift this office finished normally.
    /// Drives the evening lit-windows overlay, which is why it is on the building and not in a
    /// static: with N offices the flag has to be per office, and other players have to see it.</summary>
    internal const string DoneOnKey = "Bindicle.Dayswork/DoneOn";

    internal static string? ReadContractJson(Building office) =>
        office.modData.TryGetValue(ContractKey, out var json) ? json : null;

    internal static void WriteContractJson(Building office, string? json)
    {
        if (json is null)
            office.modData.Remove(ContractKey);
        else
            office.modData[ContractKey] = json;
    }

    // ── "Worker finished here today" ────────────────────────────────────────
    // Stored as a date rather than a bool so it self-clears: a stamp from a previous day simply
    // isn't today's, which means no day-start sweep has to reach into every building to reset it
    // (and a save loaded on a later day is right without any migration).

    internal static void MarkDoneToday(Building office) =>
        office.modData[DoneOnKey] = StampFor(CurrentDate());

    internal static bool IsDoneToday(Building office) =>
        office.modData.TryGetValue(DoneOnKey, out var stamp)
        && string.Equals(stamp, StampFor(CurrentDate()), StringComparison.Ordinal);

    internal static string StampFor(GameDate date) => $"{date.Day}/{date.Season}/{date.Year}";

    internal static GameDate CurrentDate() =>
        new(Game1.dayOfMonth, Enum.Parse<Season>(Game1.currentSeason, ignoreCase: true), Game1.year);
}
