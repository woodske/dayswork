namespace Dayswork.Orchestration;

/// <summary>Small reference-identity map used where two equal-looking game objects must still have
/// distinct owners. Tree attribution is deliberately keyed by the live tree instance, never tile
/// or vanilla farmer identity.</summary>
internal sealed class ReferenceOwnershipIndex<TKey, TValue>
    where TKey : class
{
    private readonly Dictionary<TKey, TValue> _entries = new(ReferenceEqualityComparer.Instance);

    public int Count => _entries.Count;

    public bool TryAdd(TKey key, TValue value) => _entries.TryAdd(key, value);

    public bool TryGetValue(TKey key, out TValue value) => _entries.TryGetValue(key, out value!);

    public bool Remove(TKey key) => _entries.Remove(key);

    public IReadOnlyList<KeyValuePair<TKey, TValue>> Snapshot(Func<TValue, bool> predicate) =>
        _entries.Where(pair => predicate(pair.Value)).ToList();

    public int RemoveWhere(Func<TValue, bool> predicate)
    {
        var keys = _entries
            .Where(pair => predicate(pair.Value))
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in keys)
            _entries.Remove(key);

        return keys.Count;
    }
}
