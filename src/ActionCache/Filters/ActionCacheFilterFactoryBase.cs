using System.Diagnostics.CodeAnalysis;
using ActionCache.Common;
using ActionCache.Common.Enums;
using ActionCache.Common.Keys.VaryBy;
using ActionCache.Common.Filters;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace ActionCache.Filters;

/// <summary>
/// Provides a base factory for creating instances of action cache filters.
/// </summary>
public abstract class ActionCacheFilterFactoryBase : Attribute, IFilterFactory
{
    /// <summary>
    /// Gets or sets the namespace used to identify the related action caches.
    /// </summary>
    [StringSyntax("Route")] 
    public required string Namespace { get; set; }
    
    /// <summary>
    /// Indicates whether multiple instances of the filter attribute are reusable.
    /// </summary>
    public bool IsReusable => false;

    /// <inheritdoc/>
    public abstract IFilterMetadata CreateInstance(IServiceProvider serviceProvider);

    /// <summary>
    /// Resolves and creates an <see cref="IFilterMetadata"/> of the specified type with optional expiration settings.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve the abstract filter factory.</param>
    /// <param name="type">The type of filter to be created.</param>
    /// <param name="absoluteExpiration">The absolute expiration duration for a cache entry, or <see langword="null"/> for no absolute expiration.</param>
    /// <param name="slidingExpiration">The sliding expiration duration for a cache entry, or <see langword="null"/> for no sliding expiration.</param>
    /// <param name="singleFlight">Whether concurrent misses for one key are coalesced so the action runs once.</param>
    /// <param name="varyByOptions">Which request dimensions form part of the cache key.</param>
    /// <returns>An <see cref="IFilterMetadata"/> instance representing the resolved cache filter.</returns>
    protected IFilterMetadata CreateInstance(IServiceProvider serviceProvider,
        FilterType type,
        TimeSpan? absoluteExpiration = null, 
        TimeSpan? slidingExpiration = null,
        bool singleFlight = true,
        VaryByOptions? varyByOptions = null
    )
    {
        var noExpiration = TimeSpan.FromMilliseconds(ActionCacheEntryOptions.NoExpiration);
        
        if (absoluteExpiration == noExpiration)
        {
            absoluteExpiration = null;
        }

        if (slidingExpiration == noExpiration)
        {
            slidingExpiration = null;
        }

        return serviceProvider.GetRequiredService<IActionCacheFilterAbstractFactory<IFilterMetadata>>()
            .CreateInstance(Namespace, 
                absoluteExpiration, 
                slidingExpiration, 
                type,
                singleFlight,
                varyByOptions ?? new VaryByOptions()
            );
    }
}