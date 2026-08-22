using System.Text;

namespace ActionCache.Common.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="StringBuilder"/>.
/// </summary>
internal static class StringBuilderExtensions
{
    /// <summary>
    /// Appends a concatenation of the specified strings, separated by the provided separator character,
    /// to the current instance of <see cref="StringBuilder"/>.
    /// Only non-null and non-whitespace strings are concatenated.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="separator">The character to use as a separator.</param>
    /// <param name="values">An array of strings, which can be null.</param>
    /// <returns>The original <see cref="StringBuilder"/> with the appended text.</returns>
    internal static StringBuilder AppendJoinNonNull(this StringBuilder builder, char separator, params string?[] values)
    {
        var nonNullValues = values.Where(value => !string.IsNullOrWhiteSpace(value));
        return builder.AppendJoin(separator, nonNullValues ?? []);
    }
}