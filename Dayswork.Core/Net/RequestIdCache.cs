namespace Dayswork.Core.Net;

/// <summary>
/// A bounded set of recently handled request ids, so a retried request is answered again but acted
/// on once. This is what keeps a one-time contract's price from being charged twice when a client
/// re-sends after a timeout that the host had in fact already processed.
/// <para>
/// Insertion-ordered with a hard cap: the oldest id is evicted once the cap is reached. A day's
/// menu traffic is a handful of requests per player, so the cap only ever matters as a leak guard.
/// </para>
/// </summary>
public sealed class RequestIdCache
{
    private readonly int _capacity;
    private readonly HashSet<string> _seen;
    private readonly Queue<string> _order;
    private readonly Dictionary<string, object> _responses;

    public RequestIdCache(int capacity = 64)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "The cache must hold at least one request id.");

        _capacity = capacity;
        _seen = new HashSet<string>(StringComparer.Ordinal);
        _order = new Queue<string>(capacity);
        _responses = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public int Count => _seen.Count;

    /// <summary>
    /// Records a request id and returns true when it is new (the caller should do the work). A
    /// repeat returns false, and <paramref name="previousResponse"/> carries what was answered the
    /// first time so the caller can re-send it rather than inventing a new answer.
    /// </summary>
    public bool TryBegin(string requestId, out object? previousResponse)
    {
        previousResponse = null;
        if (string.IsNullOrEmpty(requestId))
            return true;   // an unidentified request cannot be deduped; treat every one as new

        if (_seen.Contains(requestId))
        {
            _responses.TryGetValue(requestId, out previousResponse);
            return false;
        }

        _seen.Add(requestId);
        _order.Enqueue(requestId);
        Trim();
        return true;
    }

    /// <summary>Remembers the answer sent for a request id, so a retry replays it.</summary>
    public void Remember(string requestId, object response)
    {
        if (!string.IsNullOrEmpty(requestId) && _seen.Contains(requestId))
            _responses[requestId] = response;
    }

    public void Clear()
    {
        _seen.Clear();
        _order.Clear();
        _responses.Clear();
    }

    private void Trim()
    {
        while (_order.Count > _capacity)
        {
            var evicted = _order.Dequeue();
            _seen.Remove(evicted);
            _responses.Remove(evicted);
        }
    }
}
