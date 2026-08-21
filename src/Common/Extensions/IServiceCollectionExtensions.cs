using ActionCache.AzureCosmos.Extensions;
using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Extensions.Internal;
using ActionCache.Common.Filters;
using ActionCache.Memory.Extensions;
using ActionCache.Redis.Extensions;
using ActionCache.SqlServer.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionCache.Common.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to support ActionCache.
/// </summary>
public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds ActionCache services to the specified IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configureOptions">A delegate to configure ActionCacheOptions.</param>
    /// <returns>The IServiceCollection.</returns>
    public static IServiceCollection AddActionCache(
        this IServiceCollection services,
        Action<ActionCacheOptionsBuilder> configureOptions
    )
    {
        var optionsBuilder = new ActionCacheOptionsBuilder();
        configureOptions.Invoke(optionsBuilder);

        var options = optionsBuilder.Build();
        services.Configure<ActionCacheEntryOptions>(configureOptions =>
        {
            configureOptions.SlidingExpiration = options.EntryOptions.SlidingExpiration;
            configureOptions.AbsoluteExpiration = options.EntryOptions.AbsoluteExpiration;
        });

        services.Configure<ActionCacheResilienceOptions>(resilienceOptions =>
            resilienceOptions.FailClosed = options.FailClosed);

        if (options.ConfigureMemoryCacheOptions is not null)
        {
            services.AddActionCacheMemory(options.ConfigureMemoryCacheOptions);
        }

        if (options.ConfigureRedisCacheOptions is not null)
        {
            services.AddActionCacheRedis(options.ConfigureRedisCacheOptions);
        }

        if (options.ConfigureSqlServerCacheOptions is not null)
        {
            services.AddActionCacheSqlServer(options.ConfigureSqlServerCacheOptions);
        }

        if (options.ConfigureAzureCosmosCacheOptions is not null)
        {
            services.AddActionCacheAzureCosmos(options.ConfigureAzureCosmosCacheOptions);
        }

        return services;
    }

    /// <summary>
    /// Adds common ActionCache-related services to the IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <returns>The IServiceCollection with common ActionCache services added.</returns>
    internal static IServiceCollection AddActionCacheCommon(
        this IServiceCollection services
    )
    {
        // Every backend's registration extension calls this, so single-flight is registered
        // with Try* semantics: one shared instance, however many backends are configured.
        services.TryAddSingleton<IActionCacheSingleFlight>(serviceProvider =>
            new InProcessSingleFlight(
                serviceProvider.GetRequiredService<IOptions<ActionCacheEntryOptions>>().Value,
                serviceProvider.GetRequiredService<ILogger<InProcessSingleFlight>>()));

        return services
            .AddControllerInfo()
            .AddSingleton<ActionCacheDescriptorProviderFactory>()
            .AddSingleton<ResilientCacheDecorator>()
            .AddScoped<IActionCacheFilterAbstractFactory<IFilterMetadata>, ActionCacheFilterAbstractFactory>()
            .AddScoped<IActionCacheFilterAbstractFactory<IEndpointFilter>, ActionCacheEndpointFilterAbstractFactory>()
            .AddScoped<IActionCacheRefreshProvider, ActionCacheRefreshProvider>()
            .AddScoped(serviceProvider => serviceProvider
                .GetRequiredService<ActionCacheDescriptorProviderFactory>()
                .Create());
    }
}