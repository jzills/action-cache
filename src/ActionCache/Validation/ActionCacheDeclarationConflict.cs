using ActionCache.Common.Enums;

namespace ActionCache.Common.Validation;

/// <summary>
/// Decides whether the cache attributes on a single endpoint can coexist.
/// </summary>
/// <remarks>
/// The rule is that an endpoint either caches or has cache side effects, never both, and that
/// no two side effects name the same namespace. Both halves exist because the alternative is
/// not a loud failure but a silent one:
///
/// <list type="bullet">
///   <item>Caching alongside eviction or refresh makes the side effect conditional on a cache
///     miss. The eviction and refresh filters run inside the cache filter, so once the cache
///     starts serving hits the endpoint stops executing and the side effect stops happening.
///     It behaves correctly against a cold cache and wrongly against a warm one.</item>
///   <item>Two side effects on one namespace contradict each other, and which one wins is
///     decided by the order the attributes happen to be declared in.</item>
/// </list>
///
/// This is a pure function over the declarations so every combination is testable without
/// building a pipeline; <c>ActionCacheEndpointValidator</c> applies it at startup.
/// </remarks>
public static class ActionCacheDeclarationConflict
{
    /// <summary>
    /// Returns a description of the first conflict among the declarations, or
    /// <see langword="null"/> when they can coexist.
    /// </summary>
    /// <param name="declarations">Every cache attribute found on one endpoint.</param>
    /// <returns>A sentence naming the conflict, or <see langword="null"/> when there is none.</returns>
    public static string? Detect(IReadOnlyList<ActionCacheDeclaration> declarations)
    {
        var caches = declarations.Where(declaration => declaration.Type == FilterType.Add).ToList();
        var sideEffects = declarations.Where(declaration => declaration.Type != FilterType.Add).ToList();

        if (caches.Count > 1)
        {
            return $"declares [ActionCache] more than once ({Describe(caches)}). A response can only " +
                    "be cached under one namespace.";
        }

        // Reported ahead of a namespace clash: an author who resolved only the clash would be
        // left with an endpoint that still silently stops evicting once the cache warms up.
        if (caches.Count == 1 && sideEffects.Count > 0)
        {
            return $"caches into \"{caches[0].Namespace}\" and also declares a cache side effect " +
                   $"({Describe(sideEffects)}). The side effect would only run when the cache misses, " +
                    "because a cached response never reaches the endpoint. Move the side effect to the " +
                    "endpoint that performs the write.";
        }

        var duplicate = sideEffects
            .GroupBy(declaration => declaration.Namespace, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            return $"declares more than one cache side effect for the namespace \"{duplicate.Key}\" " +
                   $"({Describe([.. duplicate])}). Which one takes effect would depend on the order " +
                    "they are declared in.";
        }

        return null;
    }

    /// <summary>
    /// Renders declarations as the attribute names an author would recognise.
    /// </summary>
    /// <param name="declarations">The declarations to describe.</param>
    /// <returns>A comma-separated list such as <c>[ActionCacheEviction("A")], [ActionCacheRefresh("A")]</c>.</returns>
    private static string Describe(IEnumerable<ActionCacheDeclaration> declarations) =>
        string.Join(", ", declarations.Select(declaration =>
        {
            var name = declaration.Type switch
            {
                FilterType.Add     => "ActionCache",
                FilterType.Evict   => "ActionCacheEviction",
                FilterType.Refresh => "ActionCacheRefresh",
                _                  => declaration.Type.ToString()
            };

            return $"[{name}(\"{declaration.Namespace}\")]";
        }));
}
