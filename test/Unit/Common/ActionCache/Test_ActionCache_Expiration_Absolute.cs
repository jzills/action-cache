using ActionCache;
using Unit.TestUtilities.Builders;

namespace Unit.Common;

[TestFixture]
public class ActionCacheAbsoluteExpirationTests
{
    private IActionCache _cache = null!;
    private IActionCacheFactory _factory;

    [SetUp]
    public void SetUp() => _factory = MemoryActionCacheFactoryBuilder.Build();

    [TearDown]
    public async Task TearDown() => await _cache.RemoveAsync();

    [Test]
    public async Task GetAsync_WhenAbsoluteExpirationElapsed_ReturnsNull()
    {
        _cache = _factory.Create(nameof(GetAsync_WhenAbsoluteExpirationElapsed_ReturnsNull), TimeSpan.FromSeconds(2))!;

        await _cache.SetAsync("Key_Expiration_1", "Value_1");

        var resultBefore = await _cache.GetAsync<string?>("Key_Expiration_1");
        var keysBefore = await _cache.GetKeysAsync();

        resultBefore.Should().Be("Value_1");
        keysBefore.Should().HaveCount(1);

        await Task.Delay(TimeSpan.FromSeconds(4));

        var resultAfter = await _cache.GetAsync<string?>("Key_Expiration_1");
        var keysAfter = await _cache.GetKeysAsync();

        resultAfter.Should().BeNull();
        keysAfter.Should().BeEmpty();
    }

    [Test]
    public async Task GetKeysAsync_WhenAbsoluteExpirationElapsed_ReturnsEmpty()
    {
        _cache = _factory.Create(nameof(GetKeysAsync_WhenAbsoluteExpirationElapsed_ReturnsEmpty), TimeSpan.FromSeconds(2))!;

        await _cache.SetAsync("Key_Expiration_1", "Value_1");

        var resultBefore = await _cache.GetAsync<string?>("Key_Expiration_1");
        var keysBefore = await _cache.GetKeysAsync();

        resultBefore.Should().Be("Value_1");
        keysBefore.Should().HaveCount(1);

        await Task.Delay(TimeSpan.FromSeconds(4));

        var resultAfter = await _cache.GetAsync<string?>("Key_Expiration_1");
        var keysAfter = await _cache.GetKeysAsync();

        resultAfter.Should().BeNull();
        keysAfter.Should().BeEmpty();
    }
}
