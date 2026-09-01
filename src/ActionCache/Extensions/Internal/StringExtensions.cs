namespace ActionCache.Common.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="string"/> manipulation.
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    /// Splits a string containing comma-separated namespace values into a collection of trimmed, non-empty strings.
    /// </summary>
    /// <param name="values">The comma-separated string to split.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of strings, each representing a trimmed, non-empty segment of <paramref name="values"/>.</returns>
    internal static IEnumerable<string> SplitNamespace(this string values) =>
        values.Split(',', 
            StringSplitOptions.TrimEntries | 
            StringSplitOptions.RemoveEmptyEntries
        );
}