using Dayswork.Core.Net;
using Dayswork.Guards;
using Dayswork.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Dayswork.Net;

/// <summary>
/// Whether Dayswork is currently standing down because a connected peer cannot safely share the
/// session (2.0 plan D8).
///
/// The danger is concrete: the worker is a custom <c>NPC</c> subclass living in a synced location
/// collection, so a peer whose game cannot resolve the type throws in the netcode reader rather
/// than degrading. The default policy is therefore to suspend — end every live shift, refuse to
/// spawn, and say so loudly — rather than to let a modless guest break their own session. Kicking
/// instead is opt-in (<c>ModConfig.KickIncompatiblePeers</c>), and is the right setting for a
/// public lobby because the host cannot always despawn in time (plan correction 8).
///
/// The host owns the state and broadcasts changes; a client mirrors what it is told so its UI can
/// explain itself.
/// </summary>
internal sealed class DaysworkSuspension
{
    // Host side: the peers we refuse to run alongside. Suspension is "this set is non-empty", so a
    // second offender joining and the first leaving cannot race into a spurious resume.
    private readonly Dictionary<long, (SuspensionReason Reason, string Name)> _incompatiblePeers = new();
    private readonly HashSet<long> _unverifiedPeers = new();

    public bool IsSuspended { get; private set; }

    public SuspensionReason Reason { get; private set; } = SuspensionReason.NoMod;

    /// <summary>The offending player's name, for the banner and the chat line.</summary>
    public string PlayerName { get; private set; } = "";

    /// <summary>Raised on the host when suspension begins, so live shifts can be stopped.</summary>
    public Action? SuspensionBegan { get; set; }

    /// <summary>Raised on the host when the last incompatible peer leaves.</summary>
    public Action? SuspensionEnded { get; set; }

    /// <summary>
    /// Host-side request/spawn gate. A supported peer awaiting its acknowledgment doesn't force
    /// live workers home, but no new mutation or shift starts until compatibility is proven.
    /// </summary>
    public bool IsExecutionBlocked => IsSuspended || _unverifiedPeers.Count > 0;

    // ── Host side ────────────────────────────────────────────────────────────

    public void MarkIncompatible(long playerId, SuspensionReason reason, string playerName)
    {
        _unverifiedPeers.Remove(playerId);
        var wasSuspended = IsSuspended;
        _incompatiblePeers[playerId] = (reason, playerName);
        Recompute();

        if (!wasSuspended && IsSuspended)
            SuspensionBegan?.Invoke();
    }

    public void MarkCompatible(long playerId)
    {
        if (!_incompatiblePeers.Remove(playerId))
            return;

        var wasSuspended = IsSuspended;
        Recompute();

        if (wasSuspended && !IsSuspended)
            SuspensionEnded?.Invoke();
    }

    public bool IsIncompatible(long playerId) => _incompatiblePeers.ContainsKey(playerId);

    public void MarkUnverified(long playerId) => _unverifiedPeers.Add(playerId);

    public void MarkVerified(long playerId) => _unverifiedPeers.Remove(playerId);

    public void ForgetPeer(long playerId)
    {
        _unverifiedPeers.Remove(playerId);
        MarkCompatible(playerId);
    }

    public void Reset()
    {
        _incompatiblePeers.Clear();
        _unverifiedPeers.Clear();
        IsSuspended = false;
        PlayerName = "";
        HostIsIncompatible = false;
        HostIsUnverified = false;
    }

    // ── Client side ──────────────────────────────────────────────────────────

    /// <summary>Set on a client whose host has no Dayswork, or a different protocol version. All
    /// Dayswork UI on that client shows one explanatory card instead of the hiring flow.</summary>
    public bool HostIsIncompatible { get; set; }

    /// <summary>A remote client has not yet received a protocol Hello from its host.</summary>
    public bool HostIsUnverified { get; set; }

    public bool CannotUseMenus => HostIsIncompatible || HostIsUnverified || IsSuspended;

    /// <summary>Mirrors a host broadcast so a client's banner matches the host's.</summary>
    public void ApplyRemoteState(bool suspended, SuspensionReason reason, string playerName)
    {
        IsSuspended = suspended;
        Reason = reason;
        PlayerName = playerName;
    }

    // ── Banner ───────────────────────────────────────────────────────────────

    /// <summary>
    /// A persistent line at the top of the screen while Dayswork is standing down. A HUD message
    /// would scroll away after a few seconds; the player needs to know for as long as it lasts,
    /// because their farmhands are not working.
    /// </summary>
    public void OnRendered(object? sender, RenderedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not null || Game1.eventUp)
            return;
        if (!IsSuspended && !HostIsIncompatible && !HostIsUnverified)
            return;

        var text = HostIsUnverified
            ? I18nHelper.Get("ui.net.waiting_for_host")
            : HostIsIncompatible
            ? I18nHelper.Get("ui.net.host_incompatible_banner")
            : I18nHelper.Get(
                Reason == SuspensionReason.NoMod ? "ui.net.suspended_no_mod" : "ui.net.suspended_version",
                new { player = PlayerName });

        var font = Game1.smallFont;
        var size = font.MeasureString(text);
        var box = new Rectangle(
            (int)((Game1.uiViewport.Width - size.X) / 2f) - 16,
            8,
            (int)size.X + 32,
            (int)size.Y + 16);

        e.SpriteBatch.Draw(Game1.staminaRect, box, Color.Black * 0.6f);
        Utility.drawTextWithShadow(
            e.SpriteBatch, text, font,
            new Vector2(box.X + 16, box.Y + 8),
            Color.OrangeRed);
    }

    private void Recompute()
    {
        IsSuspended = _incompatiblePeers.Count > 0;
        if (!IsSuspended)
        {
            PlayerName = "";
            return;
        }

        // Report the first offender: the message names one player so it stays readable, and the
        // suspension itself lasts until every one of them has gone.
        var first = _incompatiblePeers.Values.First();
        Reason = first.Reason;
        PlayerName = first.Name;
    }
}
