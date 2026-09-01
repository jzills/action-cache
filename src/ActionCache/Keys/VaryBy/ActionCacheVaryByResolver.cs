using System.Security.Claims;
using ActionCache.Common.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ActionCache.Common.Keys.VaryBy;

/// <summary>
/// Builds the vary-by portion of a cache key from the request: the authenticated user,
/// any headers, query values or claims named on the attribute, and every registered
/// <see cref="IActionCacheKeyContributor"/>.
/// </summary>
public class ActionCacheVaryByResolver
{
    /// <summary>
    /// The key under which the caller's identity is recorded.
    /// </summary>
    internal const string UserKey = "user";

    /// <summary>
    /// The value recorded for an unauthenticated caller under
    /// <see cref="VaryByUserMode.Always"/>.
    /// </summary>
    internal const string AnonymousUser = "anonymous";

    /// <summary>
    /// The value recorded for an authenticated caller carrying neither a name identifier
    /// claim nor a name.
    /// </summary>
    internal const string UnidentifiedUser = "authenticated";

    private static readonly char[] Separator = [','];

    /// <summary>
    /// A resolver with no contributors, used by the filters that never build cache keys —
    /// eviction and refresh — so they need not carry a dependency they would never call.
    /// </summary>
    internal static readonly ActionCacheVaryByResolver None =
        new([], Microsoft.Extensions.Logging.Abstractions.NullLogger<ActionCacheVaryByResolver>.Instance);

    private readonly IEnumerable<IActionCacheKeyContributor> _contributors;
    private readonly ILogger<ActionCacheVaryByResolver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCacheVaryByResolver"/> class.
    /// </summary>
    /// <param name="contributors">The registered key contributors, if any.</param>
    /// <param name="logger">The logger used to record what was contributed.</param>
    public ActionCacheVaryByResolver(
        IEnumerable<IActionCacheKeyContributor> contributors,
        ILogger<ActionCacheVaryByResolver> logger)
    {
        _contributors = contributors;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the vary-by values for the current request.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="options">The vary-by settings declared on the attribute.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>
    /// The values that must form part of the cache key, sorted so that the order in which
    /// they were contributed cannot change the key.
    /// </returns>
    public async ValueTask<SortedDictionary<string, string?>> ResolveAsync(
        HttpContext httpContext,
        VaryByOptions options,
        CancellationToken cancellationToken = default)
    {
        var values = new SortedDictionary<string, string?>(StringComparer.Ordinal);

        AddUser(httpContext, options.User, values);
        AddNamed(options.Headers, name => httpContext.Request.Headers[name].ToString(), "header", values);
        AddNamed(options.Query, name => httpContext.Request.Query[name].ToString(), "query", values);
        AddNamed(options.Claims, type => httpContext.User.FindFirst(type)?.Value, "claim", values);

        foreach (var contributor in _contributors)
        {
            await contributor.ContributeAsync(httpContext, values, cancellationToken);
        }

        if (values.Count > 0 && _logger.IsEnabled(LogLevel.Debug))
        {
            ActionCacheLog.VaryByResolved(_logger, values.Count);
        }

        return values;
    }

    private static void AddUser(
        HttpContext httpContext,
        VaryByUserMode mode,
        SortedDictionary<string, string?> values)
    {
        if (mode == VaryByUserMode.Never)
        {
            return;
        }

        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false;
        if (!isAuthenticated)
        {
            // Auto contributes nothing for anonymous callers: there is no identity to
            // separate, and adding a constant would only bloat the key.
            if (mode == VaryByUserMode.Always)
            {
                values[UserKey] = AnonymousUser;
            }

            return;
        }

        values[UserKey] =
            httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User.Identity?.Name
            ?? UnidentifiedUser;
    }

    private static void AddNamed(
        string? names,
        Func<string, string?> accessor,
        string prefix,
        SortedDictionary<string, string?> values)
    {
        if (string.IsNullOrWhiteSpace(names))
        {
            return;
        }

        foreach (var name in names.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var value = accessor(name);

            // A named-but-absent dimension is still recorded, as an empty value: "no
            // Accept-Language" and "Accept-Language: en" must not collide on one entry.
            values[$"{prefix}:{name}"] = string.IsNullOrEmpty(value) ? string.Empty : value;
        }
    }
}
