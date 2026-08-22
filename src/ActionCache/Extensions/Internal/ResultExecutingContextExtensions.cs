using ActionCache.Common.Enums;
using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ActionCache.Common.Extensions;

/// <summary>
/// Provides extension methods for <see cref="ResultExecutingContext"/> to facilitate adding cache status headers.
/// </summary>
internal static class ResultExecutingContextExtensions
{
    /// <summary>
    /// Adds a cache status header to the HTTP response in the specified <see cref="ResultExecutingContext"/>.
    /// </summary>
    /// <param name="context">The <see cref="ResultExecutingContext"/> for the current HTTP response.</param>
    /// <param name="status">The <see cref="CacheStatus"/> value to set in the response headers.</param>
    internal static void AddCacheStatus(
        this ResultExecutingContext context, 
        CacheStatus status
    ) => context.HttpContext.Response.Headers.AddCacheStatus(status);
}