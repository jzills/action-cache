using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionCache.Common.Caching;

/// <summary>
/// Wraps cache instances in a <see cref="ResilientActionCache"/> so that backend
/// failures degrade gracefully. Shared by the MVC and Minimal API abstract filter
/// factories at the point backend caches enter the cache handler.
/// </summary>
public class ResilientCacheDecorator
{
    private readonly ILogger<ResilientActionCache> _logger;
    private readonly bool _failClosed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientCacheDecorator"/> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create the resilience logger.</param>
    /// <param name="resilienceOptions">The configured resilience options.</param>
    public ResilientCacheDecorator(
        ILoggerFactory loggerFactory,
        IOptions<ActionCacheResilienceOptions> resilienceOptions)
    {
        _logger = loggerFactory.CreateLogger<ResilientActionCache>();
        _failClosed = resilienceOptions.Value.FailClosed;
    }

    /// <summary>
    /// Wraps the specified cache in a <see cref="ResilientActionCache"/>.
    /// </summary>
    /// <param name="cache">The backing cache to guard.</param>
    /// <returns>A resilient cache decorating <paramref name="cache"/>.</returns>
    public IActionCache Decorate(IActionCache cache) =>
        new ResilientActionCache(cache, _logger, _failClosed);
}
