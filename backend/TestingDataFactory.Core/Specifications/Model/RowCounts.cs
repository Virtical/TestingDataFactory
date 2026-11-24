using System.Collections;

namespace TestingDataFactory.Core.Specifications.Model;

/// <summary>Неизменяемый снимок количества строк по именам таблиц.</summary>
public sealed class RowCounts : IReadOnlyDictionary<string, int>
{
    private readonly Dictionary<string, int> counts;

    public RowCounts(IReadOnlyDictionary<string, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        this.counts = new Dictionary<string, int>(counts, StringComparer.Ordinal);
        foreach (var (table, count) in this.counts)
        {
            if (!IsValidCount(count))
                throw new ArgumentOutOfRangeException(nameof(counts), count, $"Число строк для таблицы '{table}' должно быть неотрицательным.");
        }
    }

    public int this[string key] => counts[key];
    public IEnumerable<string> Keys => counts.Keys;
    public IEnumerable<int> Values => counts.Values;
    public int Count => counts.Count;

    public bool ContainsKey(string key) => counts.ContainsKey(key);
    public bool TryGetValue(string key, out int value) => counts.TryGetValue(key, out value);
    public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => counts.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal static bool IsValidCount(int count) => count >= 0;
}
