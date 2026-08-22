using ActionCache.AzureCosmos;
using ActionCache.AzureCosmos.Extensions;

using ActionCache.Common;

namespace ActionCache.Common.Extensions;

/// <summary>
/// Registers the Azure Cosmos DB cache backend.
/// </summary>
/// <remarks>
/// Declared in <c>ActionCache.Common.Extensions</c> — the namespace callers already
/// import for <c>AddActionCache</c> — so existing
/// <c>options.UseAzureCosmosCache(...)</c> call sites compile unchanged once this package
/// is referenced. Cosmos provides no distributed lock, so
/// <c>UseDistributedSingleFlight()</c> needs Redis or SQL Server alongside it.
/// </remarks>
public static class AzureCosmosActionCacheOptionsBuilderExtensions
{
    /// <summary>
    /// Enables Azure Cosmos DB as a cache backend.
    /// </summary>
    /// <param name="builder">The options builder.</param>
    /// <param name="configureOptions">Configures the Cosmos cache options.</param>
    /// <returns>The options builder, for chaining.</returns>
    public static ActionCacheOptionsBuilder UseAzureCosmosCache(
        this ActionCacheOptionsBuilder builder,
        Action<AzureCosmosCacheOptions> configureOptions
    ) => builder.AddBackend(services => services.AddActionCacheAzureCosmos(configureOptions));
}
