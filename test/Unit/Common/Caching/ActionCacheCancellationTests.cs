using ActionCache;
using Unit.TestUtilities.Builders;

namespace Unit.Common.Caching;

[TestFixture]
public class ActionCacheCancellationTests
{
    private IActionCacheFactory _factory = null!;

    [SetUp]
    public void SetUp() => _factory = MemoryActionCacheFactoryBuilder.Build();

    [Test]
    public async Task GetAsync_WhenTokenIsAlreadyCancelled_Throws()
    {
        var cache = _factory.Create(nameof(GetAsync_WhenTokenIsAlreadyCancelled_Throws))!;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await cache.GetAsync<string>("Key", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task SetAsync_WhenTokenIsAlreadyCancelled_Throws()
    {
        var cache = _factory.Create(nameof(SetAsync_WhenTokenIsAlreadyCancelled_Throws))!;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await cache.SetAsync("Key", "Value", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task RemoveAsync_WhenTokenIsAlreadyCancelled_Throws()
    {
        var cache = _factory.Create(nameof(RemoveAsync_WhenTokenIsAlreadyCancelled_Throws))!;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await cache.RemoveAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task GetAsync_WithDefaultToken_Succeeds()
    {
        var cache = _factory.Create(nameof(GetAsync_WithDefaultToken_Succeeds))!;

        await cache.SetAsync("Key", "Value");
        var result = await cache.GetAsync<string>("Key");

        result.Should().Be("Value");
    }
}
