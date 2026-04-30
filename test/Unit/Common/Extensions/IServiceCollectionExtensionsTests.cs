using ActionCache;
using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.Common.Extensions;
using ActionCache.Common.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Unit.Common.Extensions;

[TestFixture]
public class IServiceCollectionExtensionsTests
{
    [Test]
    public void AddActionCache_WithMemoryCache_RegistersIActionCacheFactory()
    {
        var services = new ServiceCollection();

        services.AddActionCache(options => options.UseMemoryCache(opt => { }));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheFactory));
    }

    [Test]
    public void AddActionCache_WithMemoryCache_RegistersFilterAbstractFactory()
    {
        var services = new ServiceCollection();

        services.AddActionCache(options => options.UseMemoryCache(opt => { }));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheFilterAbstractFactory<IFilterMetadata>));
    }

    [Test]
    public void AddActionCache_WithMemoryCache_RegistersEndpointFilterAbstractFactory()
    {
        var services = new ServiceCollection();

        services.AddActionCache(options => options.UseMemoryCache(opt => { }));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheFilterAbstractFactory<IEndpointFilter>));
    }

    [Test]
    public void AddActionCache_WithMemoryCache_RegistersRefreshProvider()
    {
        var services = new ServiceCollection();

        services.AddActionCache(options => options.UseMemoryCache(opt => { }));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheRefreshProvider));
    }

    [Test]
    public void AddActionCache_WithEntryOptions_ConfiguresOptions()
    {
        var services = new ServiceCollection();

        services.AddActionCache(options =>
        {
            options.UseMemoryCache(opt => { });
            options.UseEntryOptions(entryOptions =>
            {
                entryOptions.AbsoluteExpiration = TimeSpan.FromMinutes(5);
                entryOptions.SlidingExpiration = TimeSpan.FromMinutes(1);
            });
        });

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IActionCacheFactory));
    }

    [Test]
    public void AddActionCache_NoBackend_ReturnsServices()
    {
        var services = new ServiceCollection();

        var result = services.AddActionCache(options => { });

        result.Should().BeSameAs(services);
    }
}
