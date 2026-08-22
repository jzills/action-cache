using ActionCache.SqlServer.Concurrency;
using ActionCache.SqlServer.Extensions;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using ActionCache.Common;

using ActionCache.Common.Concurrency;

namespace ActionCache.Common.Extensions;

/// <summary>
/// Registers the SQL Server cache backend.
/// </summary>
/// <remarks>
/// Declared in <c>ActionCache.Common.Extensions</c> — the namespace callers already
/// import for <c>AddActionCache</c> — so existing
/// <c>options.UseSqlServerCache(...)</c> call sites compile unchanged once this package is
/// referenced.
/// </remarks>
public static class SqlServerActionCacheOptionsBuilderExtensions
{
    /// <summary>
    /// Enables the SQL Server cache.
    /// </summary>
    /// <param name="builder">The options builder.</param>
    /// <param name="configureOptions">Configures the underlying <see cref="SqlServerCacheOptions"/>.</param>
    /// <returns>The options builder, for chaining.</returns>
    public static ActionCacheOptionsBuilder UseSqlServerCache(
        this ActionCacheOptionsBuilder builder,
        Action<SqlServerCacheOptions> configureOptions
    ) => builder
            .AddBackend(services => services.AddActionCacheSqlServer(configureOptions))
            .AddDistributedLocker(serviceProvider =>
            {
                // No lease: sp_getapplock is session-scoped and held until its dedicated
                // connection closes, so it cannot expire mid-operation.
                var singleFlightOptions = serviceProvider
                    .GetRequiredService<ActionCacheSingleFlightOptions>();
                var connectionString = serviceProvider
                    .GetRequiredService<IOptions<SqlServerCacheOptions>>().Value.ConnectionString;

                return string.IsNullOrWhiteSpace(connectionString)
                    ? throw new InvalidOperationException(
                        "UseDistributedSingleFlight() requires SqlServerCacheOptions.ConnectionString to be set.")
                    : new SqlServerCacheLocker(connectionString, singleFlightOptions.WaitTimeout);
            });
}
