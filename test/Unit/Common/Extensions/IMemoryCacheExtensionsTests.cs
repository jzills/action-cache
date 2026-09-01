using ActionCache.Memory.Extensions.Internal;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Memory;

using Unit.TestUtilities;

namespace Unit.Common.Extensions;

[TestFixture]
public class IMemoryCacheExtensionsTests
{
    private MemoryCache _cache = null!;
    private MemoryCacheEntryOptions _indexOptions = null!;

    [SetUp]
    public void SetUp()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _indexOptions = new MemoryCacheEntryOptions { Size = 1 };
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    [Test]
    public async Task SetKey_WhenOneKeyExpires_DoesNotTakeTheIndexWithIt()
    {
        // The index entry must not inherit a caller's expiration: one short-lived response
        // expiring cannot be allowed to drop the whole namespace's index, or a later
        // eviction would no longer know which keys to remove.
        Namespace @namespace = "Expiring";
        _cache.SetKey(@namespace, "Permanent", absoluteExpiration: null, _indexOptions);
        var ephemeralExpiry = DateTimeOffset.UtcNow.AddMilliseconds(30);
        _cache.SetKey(@namespace, "Ephemeral", absoluteExpiration: ephemeralExpiry, _indexOptions);

        await WallClock.WaitUntilPast(ephemeralExpiry);

        _cache.GetKeys(@namespace, _indexOptions).Keys.Should().BeEquivalentTo(["Permanent"]);
    }

    [Test]
    public async Task GetKeys_RemovesEntriesWhoseAbsoluteExpirationHasPassed()
    {
        Namespace @namespace = "Sweeping";
        _cache.SetKey(@namespace, "Fresh", absoluteExpiration: DateTimeOffset.UtcNow.AddMinutes(5), _indexOptions);
        var staleExpiry = DateTimeOffset.UtcNow.AddMilliseconds(20);
        _cache.SetKey(@namespace, "Stale", absoluteExpiration: staleExpiry, _indexOptions);

        await WallClock.WaitUntilPast(staleExpiry);

        var keys = _cache.GetKeys(@namespace, _indexOptions);
        keys.Keys.Should().BeEquivalentTo(["Fresh"]);
    }

    [Test]
    public void RemoveKey_RemovesOnlyTheNamedKey()
    {
        Namespace @namespace = "Removing";
        _cache.SetKey(@namespace, "Keep", absoluteExpiration: null, _indexOptions);
        _cache.SetKey(@namespace, "Drop", absoluteExpiration: null, _indexOptions);

        _cache.RemoveKey(@namespace, "Drop", _indexOptions);

        _cache.GetKeys(@namespace, _indexOptions).Keys.Should().BeEquivalentTo(["Keep"]);
    }
}
