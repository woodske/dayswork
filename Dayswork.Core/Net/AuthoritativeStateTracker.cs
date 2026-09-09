namespace Dayswork.Core.Net;

/// <summary>
/// Remembers the newest <see cref="AuthoritativeContractState.Sequence"/> a client has applied for
/// each office, so an answer that overtakes another on the wire — or a request-id replay of an
/// older one — cannot put the client's read cache back to a state the host has already left.
/// <para>
/// Sequence is the only usable ordering: a contract's revision restarts at zero when an office is
/// hired for again, and an office holding no contract has no revision at all. Both cases would make
/// a revision comparison reject perfectly current state.
/// </para>
/// </summary>
public sealed class AuthoritativeStateTracker
{
    private readonly Dictionary<string, long> _appliedByOffice = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this state is newer than the last one applied for its office, and records it if so.
    /// A state with no office id, or one the host stamped with no sequence, is never applied — the
    /// host always stamps one, so its absence means the answer came from a build that did not.
    /// </summary>
    public bool TryAccept(AuthoritativeContractState? state)
    {
        if (state is null || state.OfficeId.Length == 0 || state.Sequence <= 0)
            return false;

        if (_appliedByOffice.TryGetValue(state.OfficeId, out var applied) && state.Sequence <= applied)
            return false;

        _appliedByOffice[state.OfficeId] = state.Sequence;
        return true;
    }

    /// <summary>Forgets every office — a new session's sequences start from zero again.</summary>
    public void Clear() => _appliedByOffice.Clear();
}
