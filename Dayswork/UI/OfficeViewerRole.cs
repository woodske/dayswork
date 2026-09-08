using Dayswork.Integration;
using StardewValley;
using StardewValley.Buildings;

namespace Dayswork.UI;

/// <summary>
/// What the player standing at an office may do with it (2.0 plan D4). Anyone may walk up to any
/// office and read its card; only the owner edits it. The host additionally gets the two admin
/// actions that an absent owner's farm needs — stopping a worker, and taking over an office whose
/// owner no longer exists.
/// </summary>
internal enum OfficeViewerRole
{
    /// <summary>The office is theirs: hire, edit, pause, resume, cancel, buy upgrades.</summary>
    Owner,

    /// <summary>The host looking at someone else's office: read-only, plus Pause and Cancel.</summary>
    HostAdmin,

    /// <summary>The host looking at an office whose owner is not a player on this save: as
    /// HostAdmin, plus Claim.</summary>
    HostOrphanAdmin,

    /// <summary>Anyone else: read-only.</summary>
    Viewer,
}

internal static class OfficeViewerRoles
{
    /// <summary>The role of the player at THIS screen for this office. Per screen by construction:
    /// it reads <c>Game1.player</c>, which in split-screen is that screen's farmer.</summary>
    internal static OfficeViewerRole Resolve(Building? office)
    {
        if (office is null)
            return OfficeViewerRole.Viewer;

        var ownerId = Net.ContractRequestHandler.ResolveOwner(office);
        if (Game1.player.UniqueMultiplayerID == ownerId)
            return OfficeViewerRole.Owner;

        if (!Guards.Authority.IsHost)
            return OfficeViewerRole.Viewer;

        return Sponsor.Resolve(ownerId) is null
            ? OfficeViewerRole.HostOrphanAdmin
            : OfficeViewerRole.HostAdmin;
    }

    internal static bool CanEdit(this OfficeViewerRole role) => role == OfficeViewerRole.Owner;

    internal static bool CanPauseOrCancel(this OfficeViewerRole role) =>
        role is OfficeViewerRole.Owner or OfficeViewerRole.HostAdmin or OfficeViewerRole.HostOrphanAdmin;

    internal static bool CanResume(this OfficeViewerRole role) => role == OfficeViewerRole.Owner;

    internal static bool CanClaim(this OfficeViewerRole role) => role == OfficeViewerRole.HostOrphanAdmin;

    /// <summary>The owner's display name for the card, or a placeholder when their save slot is
    /// gone.</summary>
    internal static string OwnerName(long ownerId) =>
        Sponsor.Resolve(ownerId)?.Name is { Length: > 0 } name
            ? name
            : I18nHelper.Get("ui.contract.owner_unknown");
}
