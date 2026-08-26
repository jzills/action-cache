using System.Diagnostics;
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
    /// The unresolved namespace template, used as the telemetry dimension.
    /// </summary>
    /// <remarks>
    /// <see cref="_namespace"/> converts to a string with its route template tokens already
    /// bound, so a templated namespace resolves per resource — <c>Account:42</c>. As a
    /// metric tag that mints a time series per id. The template is what identifies the
    /// cache, and it is bounded by the number of attributes in the application.
    /// </remarks>
    private readonly string _metricNamespace;

    /// <summary>The span and metric name for removing a single key.</summary>
    private const string RemoveKeyOperation = "RemoveKey";

    /// <summary>The span and metric name for evicting an entire namespace.</summary>
    private const string EvictNamespaceOperation = "EvictNamespace";

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
        _metricNamespace = _namespace.Value;
        _operationTimeout = operationTimeout;
    }

    /// <inheritdoc/>
    public Namespace GetNamespace() => _namespace;

    /// <inheritdoc/>
    public Task<IEnumerable<string>> GetKeysAsync(CancellationToken cancellationToken = default) =>
        GuardAsync(
            nameof(GetKeysAsync),
            token => _inner.GetKeysAsync(token),
            degraded: [],
            cancellationToken);

    /// <inheritdoc/>
    public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken = default) =>
        GuardAsync(
            nameof(GetAsync),
            token => _inner.GetAsync<TValue>(key, token),
            degraded: default,
            cancellationToken,
            onCompleted: (activity, value) =>
            {
                activity?.SetTag("actioncache.hit", value is not null);
                if (!_logger.IsEnabled(LogLevel.Debug))
                {
                    return;
                }

                if (value is not null)
                {
                    ActionCacheLog.CacheHit(_logger, key, (string)_namespace);
                }
                else
                {
                    ActionCacheLog.CacheMiss(_logger, key, (string)_namespace);
                }
            });

    /// <inheritdoc/>
    public Task SetAsync<TValue>(string key, TValue? value, CancellationToken cancellationToken = default) =>
        SetAsync(key, value, entryOptions: null, cancellationToken);

    /// <inheritdoc/>
    public Task SetAsync<TValue>(string key, TValue? value, ActionCacheEntryOptions? entryOptions, CancellationToken cancellationToken = default) =>
        GuardAsync(
            nameof(SetAsync),
            token => _inner.SetAsync(key, value, entryOptions, token),
            cancellationToken,
            onCompleted: _ =>
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    ActionCacheLog.CacheSet(_logger, key, (string)_namespace);
                }
            });

    /// <inheritdoc/>
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        GuardAsync(
            RemoveKeyOperation,
            token => _inner.RemoveAsync(key, token),
            cancellationToken,
            onCompleted: _ =>
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    ActionCacheLog.CacheKeyRemoved(_logger, key, (string)_namespace);
                }
            });

    /// <inheritdoc/>
    public Task RemoveAsync(CancellationToken cancellationToken = default) =>
        GuardAsync(
            EvictNamespaceOperation,
            token => _inner.RemoveAsync(token),
            cancellationToken,
            onCompleted: _ =>
            {
                // The actioncache.evictions counter is deliberately not recorded here. This
                // decorator wraps every backend individually, and ActionCacheHandler fans a
                // namespace eviction out to each layer, so a Memory + Redis + SQL chain
                // published three evictions for one request. The eviction filters record it
                // once per request instead — the same move the request counter already made.
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    ActionCacheLog.CacheEvicted(_logger, (string)_namespace);
                }
            });

    /// <inheritdoc/>
    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        GuardAsync(
            nameof(RefreshAsync),
            token => _inner.RefreshAsync(token),
            cancellationToken,
            onCompleted: _ =>
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    ActionCacheLog.CacheRefreshed(_logger, (string)_namespace);
                }
            });

    /// <summary>
    /// Runs one backend operation under the resilience, cancellation and telemetry policy
    /// every operation shares.
    /// </summary>
    /// <typeparam name="TResult">The operation's result type.</typeparam>
    /// <param name="operation">The operation name, used for spans, metrics and logs.</param>
    /// <param name="operate">Invokes the inner cache.</param>
    /// <param name="degraded">The value to return when a failure is degraded.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <param name="onCompleted">Runs after a successful operation, with its span.</param>
    /// <returns>The operation's result, or <paramref name="degraded"/>.</returns>
    /// <remarks>
    /// One body rather than six near-copies. The copies had already drifted: namespace
    /// eviction alone was missing the cancellation rethrow and started no span, the
    /// duration histogram was recorded on one operation's success path only, and one
    /// method carried a duplicated catch clause. Sharing the policy is what stops the next
    /// operation from drifting the same way.
    /// </remarks>
    private async Task<TResult> GuardAsync<TResult>(
        string operation,
        Func<CancellationToken, Task<TResult>> operate,
        TResult degraded,
        CancellationToken cancellationToken,
        Action<Activity?, TResult>? onCompleted = null)
    {
        using var timeout = CreateTimeoutSource(cancellationToken);
        using var activity = ActionCacheDiagnostics.StartOperation(operation, _metricNamespace);
        var stopwatch = ValueStopwatch.Start();
        var outcome = ActionCacheDiagnostics.Outcomes.Ok;

        try
        {
            var result = await operate(timeout?.Token ?? cancellationToken);
            onCompleted?.Invoke(activity, result);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = ActionCacheDiagnostics.Outcomes.Cancelled;
            throw;
        }
        catch (Exception exception)
        {
            outcome = ActionCacheDiagnostics.Outcomes.Error;
            Degrade(exception, operation, activity);
            return degraded;
        }
        finally
        {
            ActionCacheDiagnostics.RecordDuration(_metricNamespace, operation, outcome, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Runs one backend operation that produces no value.
    /// </summary>
    /// <param name="operation">The operation name, used for spans, metrics and logs.</param>
    /// <param name="operate">Invokes the inner cache.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <param name="onCompleted">Runs after a successful operation, with its span.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task GuardAsync(
        string operation,
        Func<CancellationToken, Task> operate,
        CancellationToken cancellationToken,
        Action<Activity?>? onCompleted = null) =>
        GuardAsync<object?>(
            operation,
            async token =>
            {
                await operate(token);
                return null;
            },
            degraded: null,
            cancellationToken,
            onCompleted is null ? null : (activity, _) => onCompleted(activity));

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

    /// <summary>
    /// Logs a failed operation and, under fail-closed, rethrows it.
    /// </summary>
    /// <param name="exception">The backend failure.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="activity">The operation's own span, or <see langword="null"/>.</param>
    /// <remarks>
    /// The status goes on the span this operation started, never on
    /// <see cref="Activity.Current"/>. When nothing subscribes to ActionCache's activity
    /// source — an app that traces only ASP.NET Core, which is the common case — the
    /// current activity is the incoming <b>request</b> span. Marking that one would report
    /// a request that returned 200 as failed, because a cache read degraded exactly as
    /// fail-open is designed to.
    /// </remarks>
    private void Degrade(Exception exception, string operation, Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

        if (_failClosed)
        {
            ActionCacheLog.OperationFailedClosed(_logger, exception, operation, (string)_namespace);
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        ActionCacheLog.OperationDegraded(_logger, exception, operation, (string)_namespace);
    }
}
