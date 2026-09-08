using StardewModdingAPI;

namespace Dayswork.Guards;

/// <summary>
/// Who this process is, for the host-authoritative model 2.0 Phase 4 introduces (plan D2).
/// Dayswork's engine — the day-start scheduler, every shift orchestrator, all modData and save-data
/// writes, money, XP, and world mutation — runs on the host alone. A client renders what the world
/// already syncs to it and sends requests for anything it wants changed.
/// <para>
/// This replaces <c>MultiplayerGuard</c>, whose two <c>IsMultiplayer()</c> early-returns made the
/// mod inert in co-op rather than authoritative.
/// </para>
/// </summary>
internal static class Authority
{
    /// <summary>
    /// True on the machine that owns the world. In split-screen this is true only for screen 0, so
    /// the engine still runs exactly once per process while screen 1 behaves as a client whose
    /// messages loop back locally.
    /// </summary>
    internal static bool IsHost => Context.IsMainPlayer;

    /// <summary>True for a player on another machine.</summary>
    internal static bool IsRemoteClient => !Context.IsMainPlayer && !Context.IsOnHostComputer;

    /// <summary>True for a local co-op guest sharing the host's process on a second screen.</summary>
    internal static bool IsSplitScreenGuest => Context.IsSplitScreen && !Context.IsMainPlayer;

    /// <summary>True for anyone who is not the host — remote or split-screen. Everything a client
    /// wants changed goes through a request.</summary>
    internal static bool IsClient => !IsHost;

    /// <summary>
    /// True when the host is running in this very process, so a request can be handed to the host
    /// handler by a direct call instead of a message. That is both simpler and more reliable than
    /// trusting a mod message to loop back between split-screen players, and it keeps the
    /// split-screen guest on exactly the same commit path as everyone else.
    /// <para>Note that a split-screen player on a <em>remote</em> machine is a plain remote client:
    /// SMAPI's <c>IsSplitScreen</c> is only ever true on the host's computer.</para>
    /// </summary>
    internal static bool HostIsInThisProcess => Context.IsOnHostComputer;
}
