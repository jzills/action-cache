using System.Runtime.ExceptionServices;
using ActionCache.Common.Diagnostics;
using ActionCache.Utilities;
using Microsoft.Extensions.Logging;

namespace ActionCache.Common.Caching;

/// <summary>
/// An <see cref="IActionCache"/> decorator that guards a backing cache against
/// transient backend failures. By default it fails open: a backend exception is
/// logged at <see cref="LogLevel.Warning"/> and the operation degrades to a cache
/// miss (reads) or a no-op (writes/eviction/refresh) so the request still succeeds
/// without caching. When constructed with <c>failClosed: true</c>, the original
/// exception is rethrown instead.
/// </summary>
/// <remarks>
/// The guard deliberately catches every exception from the inner cache — not only
/// backend I/O errors but also, on <see cref="RefreshAsync"/>, exceptions raised while
/// re-invoking the cached action — treating any failure as a non-critical, degradable
/// caching error. Cancellation is the one exception to that rule, and is handled three ways:
/// <list type="bullet">
/// <item><description>
/// The <b>caller's</b> token was cancelled: the <see cref="OperationCanceledException"/> is
/// <b>rethrown</b>, even under fail-open. The caller asked to stop; degrading to a cache miss
/// would let a request nobody is waiting on carry on doing work.
/// </description></item>
/// <item><description>
/// <c>ActionCacheResilienceOptions.OperationTimeout</c> elapsed while the caller's
/// token remained live: treated as a backend failure — degraded under fail-open, rethrown
/// under fail-closed. This is what bounds a backend that hangs rather than throws.
/// </description></item>
/// <item><description>Anything else: degraded or rethrown as before.</description></item>
/// </list>
/// </remarks>
public class ResilientActionCache : IActionCache
{
    private readonly IActionCache _inner;
    private readonly ILogger _logger;
    private readonly bool _failClosed;
    private readonly Namespace _namespace;
    private readonly TimeSpan? _operationTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientActionCache"/> class.
    /// </summary>
    /// <param name="inner">The backing cache whose operations are guarded.</param>
    /// <param name="logger">The logger used to record degraded operations.</param>
    /// <param name="failClosed">
    /// When <see langword="false"/> (default), backend exceptions are swallowed and the
    /// operation degrades gracefully. When <see langword="true"/>, exceptions are rethrown.
    /// </param>
    /// <param name="operationTimeout">
    /// The maximum time a single backend operation may take before it is abandoned, or
    /// <see langword="null"/> for no timeout.
    /// </param>
    public ResilientActionCache(
        IActionCache inner,
        ILogger logger,
        bool failClosed = false,
        TimeSpan? operationTimeout = null)
    {
        _inner = inner;
        _logger = logger;
        _failClosed = failClosed;
        _namespace = inner.GetNamespace();
        _operationTimeout = operationTimeout;
    }

    /// <inheritdoc/>
    public Namespace GetNamespace() => _namespace;

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetKeysAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeoutSource(cancellationToken);

        try
        {
            return await _inner.GetKeysAsync(timeout?.Token ?? cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Degrade(exception, nameof(GetKeysAsync));
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeoutSource(cancellationToken);

        try
        {
            var value = await _inner.GetAsync<TValue>(key, timeout?.Token ?? cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                if (value is not null)
                {
                    ActionCacheLog.CacheHit(_logger, key, (string)_namespace);
                }
                else
                {
                    ActionCacheLog.CacheMiss(_logger, key, (string)_namespace);
                }
            }

            return value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Degrade(exception, nameof(GetAsync));
            return default;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync<TValue>(string key, TValue? value, CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeoutSource(cancellationToken);

        try
        {
            await _inner.SetAsync(key, value, timeout?.Token ?? cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                ActionCacheLog.CacheSet(_logger, key, (string)_namespace);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Degrade(exception, nameof(SetAsync));
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeoutSource(cancellationToken);

        try
        {
            await _inner.RemoveAsync(key, timeout?.Token ?? cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                ActionCacheLog.CacheKeyRemoved(_logger, key, (string)_namespace);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Degrade(exception, nameof(RemoveAsync));
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeoutSource(cancellationToken);

        try
        {
            await _inner.RemoveAsync(timeout?.Token ?? cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                ActionCacheLog.CacheEvicted(_logger, (string)_namespace);
            }
        }
        catch (Exception exception)
        {
            Degrade(exception, nameof(RemoveAsync));
        }
    }

    /// <inheritdoc/>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeoutSource(cancellationToken);

        try
        {
            await _inner.RefreshAsync(timeout?.Token ?? cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                ActionCacheLog.CacheRefreshed(_logger, (string)_namespace);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Degrade(exception, nameof(RefreshAsync));
        }
    }

    /// <summary>
    /// Creates a token source that cancels when the configured operation timeout elapses,
    /// linked to the caller's token, or <see langword="null"/> when no timeout is configured.
    /// </summary>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>A linked token source, or <see langword="null"/>.</returns>
    private CancellationTokenSource? CreateTimeoutSource(CancellationToken cancellationToken)
    {
        if (_operationTimeout is null)
        {
            return null;
        }

        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(_operationTimeout.Value);
        return source;
    }

    private void Degrade(Exception exception, string operation)
    {
        if (_failClosed)
        {
            ActionCacheLog.OperationFailedClosed(_logger, exception, operation, (string)_namespace);
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        ActionCacheLog.OperationDegraded(_logger, exception, operation, (string)_namespace);
    }
}
