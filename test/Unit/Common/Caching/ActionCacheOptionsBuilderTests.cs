using ActionCache.AzureCosmos;
using ActionCache.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Caching.StackExchangeRedis;

namespace Unit.Common.Caching;

[TestFixture]
public class ActionCacheOptionsBuilderTests
{
    private ActionCacheOptionsBuilder _sut;

    [SetUp]
    public void SetUp() => _sut = new ActionCacheOptionsBuilder();

    [Test]
    public void UseEntryOptions_Always_ConfiguresEntryOptionsAndReturnsBuilder()
    {
        var returned = _sut.UseEntryOptions(options => options.AbsoluteExpiration = TimeSpan.FromMinutes(5));

        var built = _sut.Build();
        returned.Should().BeSameAs(_sut);
        built.EntryOptions.AbsoluteExpiration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Test]
    public void UseMemoryCache_Always_SetsConfigureMemoryCacheOptionsAndReturnsBuilder()
    {
        var returned = _sut.UseMemoryCache(options => options.SizeLimit = 100);

        var built = _sut.Build();
        returned.Should().BeSameAs(_sut);
        built.ConfigureMemoryCacheOptions.Should().NotBeNull();
    }

    [Test]
    public void UseRedisCache_WithAction_SetsConfigureRedisCacheOptionsAndReturnsBuilder()
    {
        var returned = _sut.UseRedisCache(options => options.Configuration = "localhost");

        var built = _sut.Build();
        returned.Should().BeSameAs(_sut);
        built.ConfigureRedisCacheOptions.Should().NotBeNull();
    }

    [Test]
    public void UseRedisCache_WithConfigurationString_SetsConfigurationOnOptions()
    {
        _sut.UseRedisCache("localhost:6379");

        var built = _sut.Build();
        built.ConfigureRedisCacheOptions.Should().NotBeNull();

        var options = new RedisCacheOptions();
        built.ConfigureRedisCacheOptions!(options);
        options.Configuration.Should().Be("localhost:6379");
    }

    [Test]
    public void UseSqlServerCache_Always_SetsConfigureSqlServerCacheOptionsAndReturnsBuilder()
    {
        var returned = _sut.UseSqlServerCache(options => options.SchemaName = "dbo");

        var built = _sut.Build();
        returned.Should().BeSameAs(_sut);
        built.ConfigureSqlServerCacheOptions.Should().NotBeNull();
    }

    [Test]
    public void UseAzureCosmosCache_Always_SetsConfigureAzureCosmosCacheOptionsAndReturnsBuilder()
    {
        var returned = _sut.UseAzureCosmosCache(options => options.ConnectionString = "conn");

        var built = _sut.Build();
        returned.Should().BeSameAs(_sut);
        built.ConfigureAzureCosmosCacheOptions.Should().NotBeNull();
    }

    [Test]
    public void Build_Always_ReturnsConfiguredOptions()
    {
        _sut.UseEntryOptions(options => options.SlidingExpiration = TimeSpan.FromSeconds(30));

        var result = _sut.Build();

        result.Should().NotBeNull();
        result.EntryOptions.SlidingExpiration.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Test]
    public void UseMemoryCache_OptionsDelegateConfiguresCorrectly()
    {
        _sut.UseMemoryCache(options => options.SizeLimit = 50);

        var built = _sut.Build();
        var options = new MemoryCacheOptions();
        built.ConfigureMemoryCacheOptions!(options);
        options.SizeLimit.Should().Be(50);
    }
}
