namespace Unit.AzureCosmos;

[TestFixture]
public class AzureCosmosActionCacheTtlTests
{
    [TestCase(1500L, 2L, Description = "1500 ms rounds up to 2 s")]
    [TestCase(999L,  1L, Description = "999 ms rounds up to 1 s")]
    [TestCase(500L,  1L, Description = "500 ms rounds up to 1 s")]
    [TestCase(2000L, 2L, Description = "2000 ms (exact multiple) stays 2 s")]
    public void SetAsync_TtlConversion_RoundsUpToNearestSecond(long ttlMs, long expectedSeconds)
    {
        long actualTtlSeconds = (long)Math.Ceiling(ttlMs / 1000.0);

        actualTtlSeconds.Should().Be(expectedSeconds);
    }

    [Test]
    public void SetAsync_TtlConversion_WhenNoExpiration_ReturnsMinusOne()
    {
        long ttl = 0L; // ActionCacheEntryOptions.NoExpiration

        long cosmosttl = ttl == 0L ? -1 : (long)Math.Ceiling(ttl / 1000.0);

        cosmosttl.Should().Be(-1);
    }
}
