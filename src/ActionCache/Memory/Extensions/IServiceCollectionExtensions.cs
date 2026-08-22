using ActionCache.Common;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ActionCache.Memory.Extensions;

/// <summary>
/// Provides extension methods for adding Action Cache memory implementation to IServiceCollection.
/// </summary>
internal static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds Action Cache memory services to the IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <returns>The IServiceCollection with added Action Cache memory services.</returns>
    internal static IServiceCollection AddActionCacheMemory(
        this IServiceCollection services
    ) => services
            .AddActionCacheMemoryInternal()
            .AddMemoryCache();

    /// <summary>
    /// Adds Action Cache memory services with configuration options to the IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="configureOptions">The configuration options for the memory cache.</param>
    /// <returns>The IServiceCollection with added Action Cache memory services.</returns>
    internal static IServiceCollection AddActionCacheMemory(
        this IServiceCollection services,
        Action<MemoryCacheOptions> configureOptions
    ) => services
            .AddActionCacheMemory()
            .AddMemoryCache(configureOptions);

    /// <summary>
    /// Adds internal Action Cache memory services to the IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <returns>The IServiceCollection with added internal Action Cache memory services.</returns>
    internal static IServiceCollection AddActionCacheMemoryInternal(
        this IServiceCollection services
    ) => services
            .AddActionCacheCommon()
            .AddExpirationTokenSources()
            .AddSingleton(serviceProvider =>
            {
                // Singleton by necessity: caches are created per request, so a locker
                // constructed inside MemoryActionCacheFactory.Create would guard nothing.
                var entryOptions = serviceProvider
                    .GetRequiredService<IOptions<ActionCacheEntryOptions>>().Value;

                return new SemaphoreSlimCacheLocker(entryOptions.LockTimeout);
            })
            .AddScoped<IActionCacheFactory, MemoryActionCacheFactory>();

    /// <summary>
    /// Adds expirationToken sources services to the IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <returns>The IServiceCollection with added expirationToken sources services.</returns>
    internal static IServiceCollection AddExpirationTokenSources(
        this IServiceCollection services
    ) => services.AddScoped<IExpirationTokenSources, ExpirationTokenSourcesValidated>(serviceProvider =>
            new ExpirationTokenSourcesValidated(
                new ExpirationTokenSources(
                    serviceProvider.GetRequiredService<IMemoryCache>())));
}