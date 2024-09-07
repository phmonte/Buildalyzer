namespace System;

internal static class BuildalyzerStringExtensions
{
    public static bool IsMatch(this string? str, string? other)
        => string.Equals(str, other, StringComparison.OrdinalIgnoreCase);
}
