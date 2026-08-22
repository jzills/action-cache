using ActionCache.Common.Caching;
using ActionCache.Common.Concurrency;
using ActionCache.Common.Keys.VaryBy;
using ActionCache.Common.Responses;
using ActionCache.Common.Extensions.Internal;
using ActionCache.Common.Filters;
using ActionCache.Memory.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

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
        {
            resilienceOptions.FailClosed = options.FailClosed;
            resilienceOptions.OperationTimeout = options.OperationTimeout;
        });

        // Validated and registered before the backends run, because a backend that supplies a
        // distributed locker sizes its lease from these. A lease that cannot coalesce anything
        // fails at startup rather than degrading silently under load.
        options.SingleFlightOptions.Validate();
        services.TryAddSingleton(options.SingleFlightOptions);

        foreach (var registerBackend in options.BackendRegistrations)
        {
            registerBackend(services);
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
                serviceProvider.GetRequiredService<ActionCacheSingleFlightOptions>(),
                serviceProvider.GetRequiredService<ILogger<InProcessSingleFlight>>()));

        // Bodies are rendered with the application's own JSON options so a cached body is
        // byte-identical to what the action would have written. MVC's options win when the
        // app uses MVC; Minimal-API-only apps fall back to the Http.Json options.
        services.TryAddSingleton(serviceProvider =>
        {
            var mvcOptions = serviceProvider
                .GetService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()?.Value.JsonSerializerOptions;
            var httpOptions = serviceProvider
                .GetService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()?.Value.SerializerOptions;

            return new CachedResponseFactory(mvcOptions ?? httpOptions ?? JsonSerializerOptions.Default);
        });

        // Note what is no longer here: AddControllerInfo, which registered every controller
        // in the application's container as scoped purely so the old reflection-based
        // refresh could resolve one. Replay uses the endpoint's own RequestDelegate, so the
        // library no longer needs to modify the app's DI graph to refresh a cache entry.
        return services
            .AddScoped<ActionCacheVaryByResolver>()
            .AddSingleton<ResilientCacheDecorator>()
            .AddScoped<IActionCacheFilterAbstractFactory<IFilterMetadata>, ActionCacheFilterAbstractFactory>()
            .AddScoped<IActionCacheFilterAbstractFactory<IEndpointFilter>, ActionCacheEndpointFilterAbstractFactory>()
            .AddScoped<IActionCacheRefreshProvider, EndpointReplayRefreshProvider>();
    }

    /// <summary>
    /// Replaces the default in-process single-flight with one backed by a backend's
    /// distributed lock, so coalescing spans every instance of the application.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="options">The configured ActionCache options.</param>
    /// <returns>The IServiceCollection.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown at registration time when no configured backend supplied a distributed
    /// locker, so a misconfiguration fails at startup rather than under load.
    /// </exception>
    private static IServiceCollection AddDistributedSingleFlight(
        this IServiceCollection services,
        ActionCacheOptions options
    )
    {
        var lockerFactory = options.DistributedLockerFactory
            ?? throw new InvalidOperationException(
                "UseDistributedSingleFlight() requires a cache backend that provides a distributed lock, such as ActionCache.Redis or ActionCache.SqlServer.");

        services.Replace(ServiceDescriptor.Singleton<IActionCacheSingleFlight>(serviceProvider =>
            new DistributedSingleFlight(
                lockerFactory(serviceProvider),
                serviceProvider.GetRequiredService<ILogger<DistributedSingleFlight>>())));

        return services;
    }

    /// <summary>
    /// Registers a cache-key contributor, adding a custom dimension to every cache key —
    /// a tenant read from a subdomain, a feature-flag cohort, an API version.
    /// </summary>
    /// <typeparam name="TContributor">The contributor implementation.</typeparam>
    /// <param name="services">The IServiceCollection to add the contributor to.</param>
    /// <returns>The IServiceCollection.</returns>
    public static IServiceCollection AddActionCacheKeyContributor<TContributor>(
        this IServiceCollection services
    ) where TContributor : class, IActionCacheKeyContributor =>
        services.AddScoped<IActionCacheKeyContributor, TContributor>();
}
