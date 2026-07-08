using ActionCache.Redis.Extensions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Unit.Redis;

[TestFixture]
public class RedisServiceCollectionExtensionsTests
{
    [Test]
    public void BuildConfigurationOptions_Always_DisablesAbortOnConnectFail()
    {
        var result = IServiceCollectionExtensions.BuildConfigurationOptions("localhost:6379");

        result.AbortOnConnectFail.Should().BeFalse();
    }

    [Test]
    public void BuildConfigurationOptions_PreservesConfiguredEndpoint()
    {
        var result = IServiceCollectionExtensions.BuildConfigurationOptions("localhost:6379");

        result.EndPoints.Should().NotBeEmpty();
    }

    [Test]
    public void AddActionCacheRedis_RegistersConnectionMultiplexerAsLazyFactory()
    {
        var services = new ServiceCollection();

        services.AddActionCacheRedis(options => options.Configuration = "localhost:6379");

        var descriptor = services.Single(service => service.ServiceType == typeof(IConnectionMultiplexer));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        descriptor.ImplementationInstance.Should().BeNull();
        descriptor.ImplementationFactory.Should().NotBeNull();
    }

    [Test]
    public void AddActionCacheRedis_WhenConfigurationMissing_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddActionCacheRedis(options => options.Configuration = " ");

        act.Should().Throw<ArgumentException>();
    }
}
