using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Common.Responses;
using Microsoft.Extensions.Logging;
using Unit.TestUtilities;
using Unit.TestUtilities.Builders;

namespace Unit.Common.Caching;

[TestFixture]
public class ActionCacheRefreshResilienceTests
{
    /// <summary>
    /// Throws for one recorded path and succeeds for every other, so a test can tell a pass
    /// that stopped at the first failure from one that carried on.
    /// </summary>
    private sealed class ThrowingRefreshProvider : IActionCacheRefreshProvider
    {
        private readonly string _throwOnPath;

        internal ThrowingRefreshProvider(string throwOnPath) => _throwOnPath = throwOnPath;

        internal List<string> Replayed { get; } = [];

        public Task<CachedResponse?> ReplayAsync(
            CachedRequest request,
            CancellationToken cancellationToken = default)
        {
            Replayed.Add(request.Path);

            if (request.Path == _throwOnPath)
            {
                throw new InvalidOperationException("the action threw during replay");
            }

            return Task.FromResult<CachedResponse?>(new CachedResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = $"\"refreshed {request.Path}\"",
                Request = request
            });
        }
    }

    private sealed class CancellingRefreshProvider : IActionCacheRefreshProvider
    {
        private readonly CancellationTokenSource _source;

        internal CancellingRefreshProvider(CancellationTokenSource source) => _source = source;

        internal int Calls { get; private set; }

        public Task<CachedResponse?> ReplayAsync(
            CachedRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            _source.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CachedResponse?>(null);
        }
    }

    private sealed class SingleLoggerFactory : ILoggerFactory
    {
        private readonly ILogger _logger;

        internal SingleLoggerFactory(ILogger logger) => _logger = logger;

        public ILogger CreateLogger(string categoryName) => _logger;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }
    }

    private static CachedResponse Entry(string path) => new()
    {
        StatusCode = 200,
        ContentType = "application/json",
        Body = $"\"stale {path}\"",
        Request = new CachedRequest { Method = "GET", Path = path }
    };

    [Test]
    public async Task RefreshAsync_WhenOneEntryThrows_StillRefreshesTheRest()
    {
        // Regression: a replay executes the endpoint, so an action that throws — for a
        // resource deleted since it was cached, say — propagated straight out of the loop
        // and every remaining key in the namespace silently went unrefreshed.
        var provider = new ThrowingRefreshProvider("/boom");
        var logger = new CapturingLogger();
        var cache = MemoryActionCacheFactoryBuilder
            .Build(provider, new SingleLoggerFactory(logger))
            .Create("Resilience")!;

        await cache.SetAsync("a", Entry("/first"));
        await cache.SetAsync("b", Entry("/boom"));
        await cache.SetAsync("c", Entry("/last"));

        await cache.RefreshAsync();

        provider.Replayed.Should().BeEquivalentTo(["/first", "/boom", "/last"],
            "a failure on one key must not stop the keys after it from being replayed");

        (await cache.GetAsync<CachedResponse>("a"))!.Body.Should().Be("\"refreshed /first\"");
        (await cache.GetAsync<CachedResponse>("c"))!.Body.Should().Be("\"refreshed /last\"");

        // The failed entry keeps its previous value rather than being emptied or corrupted.
        (await cache.GetAsync<CachedResponse>("b"))!.Body.Should().Be("\"stale /boom\"");
    }

    [Test]
    public async Task RefreshAsync_WhenOneEntryThrows_LogsTheFailureWithItsException()
    {
        var provider = new ThrowingRefreshProvider("/boom");
        var logger = new CapturingLogger();
        var cache = MemoryActionCacheFactoryBuilder
            .Build(provider, new SingleLoggerFactory(logger))
            .Create("Resilience")!;

        await cache.SetAsync("b", Entry("/boom"));

        await cache.RefreshAsync();

        // Swallowing a failure silently would be its own bug — the exception has to surface.
        var failure = logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error).Subject;
        failure.Exception.Should().BeOfType<InvalidOperationException>();
        failure.Message.Should().Contain("b").And.Contain("Resilience");
    }

    [Test]
    public async Task RefreshAsync_WhenTheCallerCancels_StopsInsteadOfContinuing()
    {
        // Cancellation is not a per-key failure: it must end the pass, matching how
        // ResilientActionCache rethrows the caller's cancellation even when failing open.
        using var source = new CancellationTokenSource();
        var provider = new CancellingRefreshProvider(source);
        var cache = MemoryActionCacheFactoryBuilder.Build(provider).Create("Resilience")!;

        await cache.SetAsync("a", Entry("/first"));
        await cache.SetAsync("b", Entry("/second"));
        await cache.SetAsync("c", Entry("/third"));

        var act = async () => await cache.RefreshAsync(source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        provider.Calls.Should().Be(1, "the loop must stop at the cancellation, not swallow it and continue");
    }
}
