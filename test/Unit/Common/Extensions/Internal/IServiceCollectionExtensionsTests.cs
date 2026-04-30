using ActionCache.Common.Caching;
using ActionCache.Common.Extensions;
using ActionCache.Common.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Unit.Common.Extensions.Internal;

[TestFixture]
public class IServiceCollectionInternalExtensionsTests
{
    [Test]
    public void AddActionCacheCommon_RegistersDescriptorProviderFactory()
    {
        var services = new ServiceCollection();

        services.AddActionCacheCommon();

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(ActionCacheDescriptorProviderFactory));
    }

    [Test]
    public void AddActionCacheCommon_RegistersMvcFilterAbstractFactory()
    {
        var services = new ServiceCollection();

        services.AddActionCacheCommon();

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheFilterAbstractFactory<IFilterMetadata>));
    }

    [Test]
    public void AddActionCacheCommon_RegistersEndpointFilterAbstractFactory()
    {
        var services = new ServiceCollection();

        services.AddActionCacheCommon();

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheFilterAbstractFactory<IEndpointFilter>));
    }

    [Test]
    public void AddActionCacheCommon_RegistersRefreshProvider()
    {
        var services = new ServiceCollection();

        services.AddActionCacheCommon();

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheRefreshProvider));
    }

    [Test]
    public void AddActionCacheCommon_WithNoApplicationPartManager_DoesNotThrow()
    {
        var services = new ServiceCollection();

        Action act = () => services.AddActionCacheCommon();

        act.Should().NotThrow();
    }
}
