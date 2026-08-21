using ActionCache.AzureCosmos.Extensions;
using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.Redis.Concurrency;
using ActionCache.SqlServer.Concurrency;
using Microsoft.Extensions.Caching.SqlServer;
using StackExchange.Redis;
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

        if (options.UseDistributedSingleFlight)
        {
            services.AddDistributedSingleFlight(options);
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

    /// <summary>
    /// Replaces the default in-process single-flight with one backed by a backend's
    /// distributed lock, so coalescing spans every instance of the application.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="options">The configured ActionCache options.</param>
    /// <returns>The IServiceCollection.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown at registration time when neither Redis nor SQL Server is configured, so a
    /// misconfiguration fails at startup rather than under load.
    /// </exception>
    private static IServiceCollection AddDistributedSingleFlight(
        this IServiceCollection services,
        ActionCacheOptions options
    )
    {
        if (options.ConfigureRedisCacheOptions is null &&
            options.ConfigureSqlServerCacheOptions is null)
        {
            throw new InvalidOperationException(
                "UseDistributedSingleFlight() requires a Redis or SQL Server cache backend to be configured.");
        }

        // Redis is preferred when both are configured: its lock is a single atomic
        // SET NX PX rather than a dedicated SQL connection per acquisition.
        var useRedis = options.ConfigureRedisCacheOptions is not null;

        services.Replace(ServiceDescriptor.Singleton<IActionCacheSingleFlight>(serviceProvider =>
        {
            var entryOptions = serviceProvider
                .GetRequiredService<IOptions<ActionCacheEntryOptions>>().Value;

            ICacheLockerHandler locker = useRedis
                ? new RedisCacheLocker(
                    serviceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase(),
                    entryOptions.LockDuration,
                    entryOptions.LockTimeout)
                : new SqlServerCacheLocker(
                    ResolveSqlServerConnectionString(serviceProvider),
                    entryOptions.LockDuration,
                    entryOptions.LockTimeout);

            return new DistributedSingleFlight(
                locker,
                serviceProvider.GetRequiredService<ILogger<DistributedSingleFlight>>());
        }));

        return services;
    }

    /// <summary>
    /// Reads the SQL Server cache connection string configured for the distributed cache.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve the options.</param>
    /// <returns>The configured connection string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no connection string is configured.</exception>
    private static string ResolveSqlServerConnectionString(IServiceProvider serviceProvider)
    {
        var connectionString = serviceProvider
            .GetRequiredService<IOptions<SqlServerCacheOptions>>().Value.ConnectionString;

        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                "UseDistributedSingleFlight() requires SqlServerCacheOptions.ConnectionString to be set.")
            : connectionString;
    }
}
