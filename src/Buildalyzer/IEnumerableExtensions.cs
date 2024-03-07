#nullable enable

namespace Buildalyzer;

internal static class IEnumerableExtensions
{
    [Pure]
    internal static IEnumerable<DictionaryEntry> ToDictionaryEntries(this IEnumerable? enumerable)
        => enumerable?
            .Cast<object>()
            .Select(AsDictionaryEntry)
        ?? Array.Empty<DictionaryEntry>();

    private static DictionaryEntry AsDictionaryEntry(object? obj) => obj switch
    {
        DictionaryEntry entry => entry,
        KeyValuePair<string, object?> strObj => new DictionaryEntry(strObj.Key, strObj.Value),
        KeyValuePair<string, string> strStr => new DictionaryEntry(strStr.Key, strStr.Value),
        KeyValuePair<object, object?> objObj => new DictionaryEntry(objObj.Key, objObj.Value),
        _ => throw new InvalidOperationException($"Could not determine enumerable dictionary entry type for {obj?.GetType()}."),
    };
}
