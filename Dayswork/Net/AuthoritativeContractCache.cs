using Dayswork.Core.Net;
using StardewModdingAPI.Utilities;

namespace Dayswork.Net;

/// <summary>
/// A remote client's view of what the host says an office holds (R7). The host stamps every
/// commit/action answer with the office's authoritative state; this folds it into the client's own
/// read cache before the menu's callback redraws, so an accepted Pause shows Resume immediately
/// rather than staying Active until the office is reopened.
/// <para>
/// It also remembers whether that office's shift is live on the host. A client's
/// <c>ShiftFleet</c> is permanently empty — the engine runs on the host alone — so asking it
/// "is a shift running" always answers no, and Cancel would offer itself on a worker that is out.
/// </para>
/// <para>Per screen, like <see cref="MenuSnapshotCache"/>: in split-screen two players can each
/// have an office open. A split-screen guest never uses this — it shares the host's process and
/// therefore the host's own store and fleet.</para>
/// </summary>
internal static class AuthoritativeContractCache
{
    private static readonly PerScreen<AuthoritativeStateTracker> Tracker = new(() => new AuthoritativeStateTracker());
    private static readonly PerScreen<Dictionary<Guid, bool>> ShiftRunningByOffice = new(() => new Dictionary<Guid, bool>());

    /// <summary>
    /// Whether this state is newer than the last one applied for its office, and records it if so.
    /// Ordering is by the host's sequence, never by revision: revisions restart at zero for a new
    /// contract, so an out-of-order answer could otherwise look current.
    /// </summary>
    public static bool TryAccept(AuthoritativeContractState? state) => Tracker.Value.TryAccept(state);

    /// <summary>Records the live-shift flag an accepted state carried.</summary>
    public static void RememberShiftRunning(Guid officeId, bool running) =>
        ShiftRunningByOffice.Value[officeId] = running;

    /// <summary>
    /// Whether the host last said a shift was live for this office. Null when the client has never
    /// been told — the menu then falls back to allowing the action and letting the host decide,
    /// which is what it did before this cache existed.
    /// </summary>
    public static bool? ShiftRunning(Guid officeId) =>
        ShiftRunningByOffice.Value.TryGetValue(officeId, out var running) ? running : null;

    public static void ClearCurrentScreen()
    {
        Tracker.Value.Clear();
        ShiftRunningByOffice.Value.Clear();
    }
}
