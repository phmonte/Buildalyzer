using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Buildalyzer;

internal static class IEnumerableExtensions
{
    internal static IEnumerable<DictionaryEntry> ToDictionaryEntries(this IEnumerable enumerable) =>
        enumerable
            .Cast<object>()
            .Select(x =>
            {
                switch (x)
                {
                    case DictionaryEntry dictionaryEntry:
                        return dictionaryEntry;
                    case KeyValuePair<string, string> kvpStringString:
                        return new DictionaryEntry(kvpStringString.Key, kvpStringString.Value);
                    case KeyValuePair<string, object> kvpStringObject:
                        return new DictionaryEntry(kvpStringObject.Key, kvpStringObject.Value);
                    case KeyValuePair<object, object> kvpObjectObject:
                        return new DictionaryEntry(kvpObjectObject.Key, kvpObjectObject.Value);
                    default:
                        throw new InvalidOperationException("Could not determine enumerable dictionary entry type");
                }
            });
}
