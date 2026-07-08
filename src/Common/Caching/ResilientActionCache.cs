using System.Runtime.ExceptionServices;
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
/// caching error. This includes <see cref="OperationCanceledException"/>; no
/// <see cref="IActionCache"/> operation accepts a cancellation token, so ambient
/// cancellation is not a concern here.
/// </remarks>
public class ResilientActionCache : IActionCache
{
    private readonly IActionCache _inner;
    private readonly ILogger _logger;
    private readonly bool _failClosed;
    private readonly Namespace _namespace;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientActionCache"/> class.
    /// </summary>
    /// <param name="inner">The backing cache whose operations are guarded.</param>
    /// <param name="logger">The logger used to record degraded operations.</param>
    /// <param name="failClosed">
    /// When <see langword="false"/> (default), backend exceptions are swallowed and the
    /// operation degrades gracefully. When <see langword="true"/>, exceptions are rethrown.
    /// </param>
    public ResilientActionCache(IActionCache inner, ILogger logger, bool failClosed = false)
    {
        _inner = inner;
        _logger = logger;
        _failClosed = failClosed;
        _namespace = inner.GetNamespace();
    }

    /// <inheritdoc/>
    public Namespace GetNamespace() => _namespace;

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetKeysAsync()
    {
        try
        {
            return await _inner.GetKeysAsync();
        }
        catch (Exception exception)
        {
            Degrade(exception, nameof(GetKeysAsync));
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<TValue?> GetAsync<TValue>(string key)
    {
        try
        {
            return await _inner.GetAsync<TValue>(key);
        }
        catch (Exception exception)
        {
            Degrade(exception, nameof(GetAsync));
            return default;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync<TValue>(string key, TValue? value)
    {
        try
        {
            await _inner.SetAsync(key, value);
        }
        catch (Exception exception)
        {
            Degrade(exception, nameof(SetAsync));
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key)
    {
        try
        {
            await _inner.RemoveAsync(key);
        }
        catch (Exception exception)
        {
            Degrade(exception, nameof(RemoveAsync));
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync()
    {
        try
        {
            await _inner.RemoveAsync();
        }
        catch (Exception exception)
        {
            Degrade(exception, nameof(RemoveAsync));
        }
    }

    /// <inheritdoc/>
    public async Task RefreshAsync()
    {
        try
        {
            await _inner.RefreshAsync();
        }
        catch (Exception exception)
        {
            Degrade(exception, nameof(RefreshAsync));
        }
    }

    private void Degrade(Exception exception, string operation)
    {
        if (_failClosed)
        {
            _logger.LogError(
                exception,
                "ActionCache backend operation '{Operation}' failed for namespace '{Namespace}'; propagating (fail-closed).",
                operation,
                (string)_namespace);

            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        _logger.LogWarning(
            exception,
            "ActionCache backend operation '{Operation}' failed for namespace '{Namespace}'; degrading gracefully.",
            operation,
            (string)_namespace);
    }
}
