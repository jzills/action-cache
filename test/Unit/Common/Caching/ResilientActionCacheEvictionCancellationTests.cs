using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Utilities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Unit.Common.Caching;

/// <summary>
/// Namespace eviction observes the same cancellation contract as every other operation.
/// </summary>
/// <remarks>
/// It was the one method without the rethrow: a caller that gave up mid-eviction had its
/// <see cref="OperationCanceledException"/> swallowed and logged as a degraded backend
/// failure, which is the opposite of what the decorator documents.
/// </remarks>
[TestFixture]
public class ResilientActionCacheEvictionCancellationTests
{
    private sealed class HangingCache : IActionCache
    {
        public Namespace GetNamespace() => new("Test");

        public Task<IEnumerable<string>> GetKeysAsync(CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => Enumerable.Empty<string>(), cancellationToken);

        public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => default(TValue), cancellationToken);

        public Task SetAsync<TValue>(string key, TValue? value, CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.Infinite, cancellationToken);

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.Infinite, cancellationToken);

        public Task RemoveAsync(CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.Infinite, cancellationToken);

        public Task RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private static ResilientActionCache Create(TimeSpan? timeout = null) =>
        new(new HangingCache(), NullLogger.Instance, failClosed: false, timeout);

    [Test]
    public async Task RemoveAsync_ForTheWholeNamespace_WhenTheCallersTokenIsCancelled_RethrowsEvenWhenFailingOpen()
    {
        var cache = Create();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = async () => await cache.RemoveAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task RemoveAsync_ForOneKey_WhenTheCallersTokenIsCancelled_RethrowsEvenWhenFailingOpen()
    {
        var cache = Create();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = async () => await cache.RemoveAsync("Key", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task RefreshAsync_WhenTheCallersTokenIsCancelled_RethrowsEvenWhenFailingOpen()
    {
        var cache = Create();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = async () => await cache.RefreshAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task RemoveAsync_ForTheWholeNamespace_WhenTheOperationTimeoutElapses_DegradesToANoOp()
    {
        // A timeout is a backend failure, not a caller giving up, so fail-open swallows it.
        var cache = Create(TimeSpan.FromMilliseconds(50));

        var act = async () => await cache.RemoveAsync();

        await act.Should().NotThrowAsync();
    }
}
