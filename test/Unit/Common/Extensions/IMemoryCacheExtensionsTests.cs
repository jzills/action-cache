using ActionCache.Memory.Extensions.Internal;
using ActionCache.Utilities;
using Microsoft.Extensions.Caching.Memory;

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
    public void SetKey_WhenOneKeyExpires_DoesNotTakeTheIndexWithIt()
    {
        // The index entry must not inherit a caller's expiration: one short-lived response
        // expiring cannot be allowed to drop the whole namespace's index, or a later
        // eviction would no longer know which keys to remove.
        Namespace @namespace = "Expiring";
        _cache.SetKey(@namespace, "Permanent", absoluteExpiration: null, _indexOptions);
        _cache.SetKey(@namespace, "Ephemeral", absoluteExpiration: DateTimeOffset.UtcNow.AddMilliseconds(30), _indexOptions);

        Thread.Sleep(120);

        _cache.GetKeys(@namespace, _indexOptions).Keys.Should().BeEquivalentTo(["Permanent"]);
    }

    [Test]
    public void GetKeys_RemovesEntriesWhoseAbsoluteExpirationHasPassed()
    {
        Namespace @namespace = "Sweeping";
        _cache.SetKey(@namespace, "Fresh", absoluteExpiration: DateTimeOffset.UtcNow.AddMinutes(5), _indexOptions);
        _cache.SetKey(@namespace, "Stale", absoluteExpiration: DateTimeOffset.UtcNow.AddMilliseconds(20), _indexOptions);

        Thread.Sleep(80);

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
