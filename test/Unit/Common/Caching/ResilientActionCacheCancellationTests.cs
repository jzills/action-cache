using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Utilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Unit.Common.Caching;

[TestFixture]
public class ResilientActionCacheCancellationTests
{
    /// <summary>
    /// Blocks until whatever token it is handed is cancelled — standing in for a backend
    /// that hangs rather than throwing.
    /// </summary>
    private sealed class HangingCache : IActionCache
    {
        public Namespace GetNamespace() => new("Test");

        public async Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return default;
        }

        public async Task<IEnumerable<string>> GetKeysAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return [];
        }

        public Task SetAsync<TValue>(string key, TValue? value, CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.Infinite, cancellationToken);

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.Infinite, cancellationToken);

        public Task RemoveAsync(CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.Infinite, cancellationToken);

        public Task RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private static ResilientActionCache Create(bool failClosed = false, TimeSpan? timeout = null) =>
        new(new HangingCache(), NullLogger.Instance, failClosed, timeout);

    [Test]
    public async Task GetAsync_WhenTheCallersTokenIsCancelled_RethrowsEvenWhenFailingOpen()
    {
        // The regression that matters: degrading here would let a request nobody is
        // waiting on carry on doing work.
        var cache = Create(failClosed: false);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = async () => await cache.GetAsync<string>("Key", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task SetAsync_WhenTheCallersTokenIsCancelled_RethrowsEvenWhenFailingOpen()
    {
        var cache = Create(failClosed: false);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = async () => await cache.SetAsync("Key", "Value", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task GetAsync_WhenTheOperationTimeoutElapses_DegradesToAMiss()
    {
        var cache = Create(failClosed: false, timeout: TimeSpan.FromMilliseconds(50));

        var result = await cache.GetAsync<string>("Key");

        result.Should().BeNull("a timeout is a backend failure, which fail-open degrades");
    }

    [Test]
    public async Task GetKeysAsync_WhenTheOperationTimeoutElapses_DegradesToEmpty()
    {
        var cache = Create(failClosed: false, timeout: TimeSpan.FromMilliseconds(50));

        var result = await cache.GetKeysAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetAsync_WhenTheOperationTimeoutElapsesAndFailingClosed_Throws()
    {
        var cache = Create(failClosed: true, timeout: TimeSpan.FromMilliseconds(50));

        var act = async () => await cache.GetAsync<string>("Key");

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task GetAsync_WithNoOperationTimeout_IsNotInterrupted()
    {
        var cache = Create(failClosed: false, timeout: null);
        using var cts = new CancellationTokenSource();

        var pending = cache.GetAsync<string>("Key", cts.Token);
        var finished = await Task.WhenAny(pending, Task.Delay(200));

        finished.Should().NotBeSameAs(pending, "with no timeout configured the call must still be running");
        await cts.CancelAsync();
    }
}
