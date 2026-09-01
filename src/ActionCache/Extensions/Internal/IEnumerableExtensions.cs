namespace ActionCache.Common.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="IEnumerable{T}"/> to add utility functionality.
/// </summary>
internal static class IEnumerableExtensions
{
    /// <summary>
    /// Determines if the specified <see cref="IEnumerable{T}"/> contains any elements.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="source">The collection to evaluate.</param>
    /// <returns><c>true</c> if the collection contains one or more elements; otherwise, <c>false</c>.</returns>
    internal static bool Some<T>(this IEnumerable<T>? source) => source is not null && source.Any();
}