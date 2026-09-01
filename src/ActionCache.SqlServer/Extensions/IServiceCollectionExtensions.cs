using ActionCache.Common.Extensions;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.DependencyInjection;

namespace ActionCache.SqlServer.Extensions;

/// <summary>
/// Provides extension methods for adding Action Cache SqlServer implementation to IServiceCollection.
/// </summary>
internal static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds Action Cache SqlServer services with configuration options to the IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="configureOptions">The configuration options for the SqlServer cache.</param>
    /// <returns>The IServiceCollection with added Action Cache SqlServer services.</returns>
    internal static IServiceCollection AddActionCacheSqlServer(
        this IServiceCollection services,
        Action<SqlServerCacheOptions> configureOptions
    ) => services
            .AddActionCacheCommon()
            .AddScoped<IActionCacheFactory, SqlServerActionCacheFactory>()
            .AddDistributedSqlServerCache(configureOptions);
}