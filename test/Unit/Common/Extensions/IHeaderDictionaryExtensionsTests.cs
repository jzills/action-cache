using ActionCache.Common.Enums;
using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Http;

namespace Unit.Common.Extensions;

[TestFixture]
public class IHeaderDictionaryExtensionsTests
{
    [Test]
    public void AddCacheStatus_WhenHeaderNotPresent_AddsHeader()
    {
        var headers = new HeaderDictionary();

        headers.AddCacheStatus(CacheStatus.Hit);

        headers[CacheHeaders.CacheStatus].Should().ContainSingle().Which.Should().Be(nameof(CacheStatus.Hit));
    }

    [TestCase(CacheStatus.Add)]
    [TestCase(CacheStatus.Hit)]
    [TestCase(CacheStatus.Miss)]
    [TestCase(CacheStatus.Evict)]
    [TestCase(CacheStatus.Refresh)]
    [TestCase(CacheStatus.None)]
    public void AddCacheStatus_Always_SetsCorrectStatusName(CacheStatus status)
    {
        var headers = new HeaderDictionary();

        headers.AddCacheStatus(status);

        headers[CacheHeaders.CacheStatus].Should().ContainSingle().Which.Should().Be(Enum.GetName(status));
    }

    [Test]
    public void AddCacheStatus_WhenCalledTwice_DoesNotOverwriteExistingHeader()
    {
        var headers = new HeaderDictionary();

        headers.AddCacheStatus(CacheStatus.Hit);
        headers.AddCacheStatus(CacheStatus.Miss);

        headers[CacheHeaders.CacheStatus].Should().ContainSingle().Which.Should().Be(nameof(CacheStatus.Hit));
    }
}
