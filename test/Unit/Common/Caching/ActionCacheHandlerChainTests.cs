using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Utilities;

namespace Unit.Common.Caching;

[TestFixture]
public class ActionCacheHandlerChainTests
{
    private sealed class FakeCache : IActionCache
    {
        private readonly Dictionary<string, object?> _entries = [];

        public int GetCalls { get; private set; }
        public int SetCalls { get; private set; }

        public void Seed(string key, object? value) => _entries[key] = value;
        public bool Contains(string key) => _entries.ContainsKey(key);

        public Namespace GetNamespace() => new("Test");

        public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(_entries.TryGetValue(key, out var value) ? (TValue?)value : default);
        }

        public Task SetAsync<TValue>(string key, TValue? value, CancellationToken cancellationToken = default)
        {
            SetCalls++;
            _entries[key] = value;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetKeysAsync(CancellationToken cancellationToken = default) => Task.FromResult(_entries.Keys.AsEnumerable());
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) { _entries.Remove(key); return Task.CompletedTask; }
        public Task RemoveAsync(CancellationToken cancellationToken = default) { _entries.Clear(); return Task.CompletedTask; }
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static (ActionCacheHandler Handler, FakeCache L1, FakeCache L2) BuildChain()
    {
        var l1 = new FakeCache();
        var l2 = new FakeCache();
        var handler = new ActionCacheHandler(l1);
        handler.SetNext(l2);
        return (handler, l1, l2);
    }

    [Test]
    public async Task GetKeysAsync_UnionsKeysFromEveryLayer()
    {
        // Regression: the handler's "?? NextIfExists(...)" fallback was dead code, because
        // no backend returns null from GetKeysAsync. Layered setups only ever saw L1, so
        // namespace refresh and key eviction silently skipped L2.
        var (handler, l1, l2) = BuildChain();
        l1.Seed("OnlyInL1", "a");
        l2.Seed("OnlyInL2", "b");

        var keys = await handler.GetKeysAsync();

        keys.Should().BeEquivalentTo(["OnlyInL1", "OnlyInL2"]);
    }

    [Test]
    public async Task GetKeysAsync_WhenLayersShareAKey_ReturnsItOnce()
    {
        var (handler, l1, l2) = BuildChain();
        l1.Seed("Shared", "a");
        l2.Seed("Shared", "b");

        var keys = await handler.GetKeysAsync();

        keys.Should().BeEquivalentTo(["Shared"]);
    }

    [Test]
    public async Task GetAsync_WhenFoundInTheNextLayer_BackfillsTheFirst()
    {
        var (handler, l1, l2) = BuildChain();
        l2.Seed("Key", "value");

        var result = await handler.GetAsync<string>("Key");

        result.Should().Be("value");
        l1.Contains("Key").Should().BeTrue("an L2 hit must be promoted so later reads skip the round-trip");
    }

    [Test]
    public async Task GetAsync_AfterBackfill_IsServedByTheFirstLayer()
    {
        var (handler, _, l2) = BuildChain();
        l2.Seed("Key", "value");

        await handler.GetAsync<string>("Key");
        var callsAfterPromotion = l2.GetCalls;
        await handler.GetAsync<string>("Key");

        l2.GetCalls.Should().Be(callsAfterPromotion, "the promoted entry must be served by L1");
    }

    [Test]
    public async Task GetAsync_WhenFoundInTheFirstLayer_DoesNotConsultTheNext()
    {
        var (handler, l1, l2) = BuildChain();
        l1.Seed("Key", "value");

        await handler.GetAsync<string>("Key");

        l2.GetCalls.Should().Be(0);
    }

    [Test]
    public async Task GetAsync_WhenMissingEverywhere_WritesNothing()
    {
        var (handler, l1, l2) = BuildChain();

        var result = await handler.GetAsync<string>("Key");

        result.Should().BeNull();
        l1.SetCalls.Should().Be(0);
        l2.SetCalls.Should().Be(0);
    }
}
