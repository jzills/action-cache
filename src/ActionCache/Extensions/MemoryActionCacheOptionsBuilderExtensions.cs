using ActionCache.Memory.Extensions;
using Microsoft.Extensions.Caching.Memory;

using ActionCache.Common;

namespace ActionCache.Common.Extensions;

/// <summary>
/// Registers the in-memory cache backend, which ships in the core package.
/// </summary>
public static class MemoryActionCacheOptionsBuilderExtensions
{
    /// <summary>
    /// Enables the in-process memory cache.
    /// </summary>
    /// <param name="builder">The options builder.</param>
    /// <param name="configureOptions">Configures the underlying <see cref="MemoryCacheOptions"/>.</param>
    /// <returns>The options builder, for chaining.</returns>
    /// <remarks>
    /// Set <see cref="MemoryCacheOptions.SizeLimit"/> when caching per-user responses:
    /// vary-by multiplies entries by the number of active callers.
    /// </remarks>
    public static ActionCacheOptionsBuilder UseMemoryCache(
        this ActionCacheOptionsBuilder builder,
        Action<MemoryCacheOptions> configureOptions
    ) => builder.AddBackend(services => services.AddActionCacheMemory(configureOptions));
}
