namespace System;

internal static class BuildalyzerArrayExtensions
{
    /// <inheritdoc cref="Array.Exists{T}(T[], Predicate{T})"/>>
    [Pure]
    public static bool Exists<T>(this T[] array, Predicate<T> match)
        => Array.Exists(array, match);

    /// <inheritdoc cref="Array.TrueForAll{T}(T[], Predicate{T})"/>>
    [Pure]
    public static bool TrueForAll<T>(this T[] array, Predicate<T> match)
        => Array.TrueForAll(array, match);
}
