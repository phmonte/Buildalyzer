namespace System;

internal static class BuildalyzerStringExtensions
{
    /// <summary>
    /// Returns true if the <paramref name="value"/> string has the same value, ignoring casing.
    /// </summary>
    [Pure]
    public static bool IsMatch(this string? self, string? value) => string.Equals(self, value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if the string starts with <paramref name="value"/>, ignoring casing.
    /// </summary>
    [Pure]
    public static bool IsMatchStart(this string self, string value)
        => self.StartsWith(value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if the string ends with <paramref name="value"/>, ignoring casing.
    /// </summary>
    [Pure]
    public static bool IsMatchEnd(this string self, string value)
        => self.EndsWith(value, StringComparison.OrdinalIgnoreCase);
}
