using ActionCache.Common.Keys;
using Unit.TestUtilities.Builders;
using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Common.Filters;
using ActionCache.EndpointFilters;
using ActionCache.Filters;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Unit.Common.Filters;

[TestFixture]
public class ActionCacheFilterFactoriesTests
{
    private ServiceProvider _serviceProvider = null!;
    private Mock<IActionCacheFactory> _cacheFactoryMock = null!;

    [TearDown]
    public void TearDown() => _serviceProvider.Dispose();

    [SetUp]
    public void SetUp()
    {
        var cacheMock = new Mock<IActionCache>();
        cacheMock.Setup(cache => cache.GetNamespace()).Returns(new Namespace("Test"));

        _cacheFactoryMock = new Mock<IActionCacheFactory>();
        _cacheFactoryMock
            .Setup(factory => factory.Create(It.IsAny<Namespace>()))
            .Returns(cacheMock.Object);
        _cacheFactoryMock
            .Setup(factory => factory.Create(It.IsAny<Namespace>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>()))
            .Returns(cacheMock.Object);

        var binderFactory = new ServiceCollection()
            .AddLogging()
            .AddRouting()
            .BuildServiceProvider()
            .GetRequiredService<TemplateBinderFactory>();

        var resilientDecorator = new ResilientCacheDecorator(
            NullLoggerFactory.Instance,
            Options.Create(new ActionCacheResilienceOptions()));

        var mvcAbstractFactory = new ActionCacheFilterAbstractFactory(
            [_cacheFactoryMock.Object], binderFactory, resilientDecorator, NullLoggerFactory.Instance, SingleFlightBuilder.Build(), VaryByBuilder.Resolver(), ResponseFactoryBuilder.Build(), new ActionCacheKeyOptions());

        var endpointAbstractFactory = new ActionCacheEndpointFilterAbstractFactory(
            [_cacheFactoryMock.Object], binderFactory, resilientDecorator, NullLoggerFactory.Instance, SingleFlightBuilder.Build(), VaryByBuilder.Resolver(), ResponseFactoryBuilder.Build(), new ActionCacheKeyOptions());

        var services = new ServiceCollection();
        services.AddSingleton<IActionCacheFilterAbstractFactory<IFilterMetadata>>(mvcAbstractFactory);
        services.AddSingleton<IActionCacheFilterAbstractFactory<IEndpointFilter>>(endpointAbstractFactory);
        _serviceProvider = services.BuildServiceProvider() as ServiceProvider ?? throw new InvalidOperationException();
    }

    // Each of these named a specific filter and then asserted only non-null, so all five
    // passed identically whatever the factories returned — including all returning the same
    // filter. The type is the whole distinction between them.
    [Test]
    public void ActionCacheFilterFactory_CreateInstance_ReturnsAddFilter()
    {
        var factory = new ActionCacheFilterFactory { Namespace = "Test" };

        var result = factory.CreateInstance(_serviceProvider);

        result.Should().BeOfType<ActionCacheFilter>();
    }

    [Test]
    public void ActionCacheEvictionFilterFactory_CreateInstance_ReturnsEvictionFilter()
    {
        var factory = new ActionCacheEvictionFilterFactory { Namespace = "Test" };

        var result = factory.CreateInstance(_serviceProvider);

        result.Should().BeOfType<ActionCacheEvictionFilter>();
    }

    [Test]
    public void ActionCacheRefreshFilterFactory_CreateInstance_ReturnsRefreshFilter()
    {
        var factory = new ActionCacheRefreshFilterFactory { Namespace = "Test" };

        var result = factory.CreateInstance(_serviceProvider);

        result.Should().BeOfType<ActionCacheRefreshFilter>();
    }

    [Test]
    public void ActionCacheEndpointFilterFactory_CreateInstance_ReturnsAddFilter()
    {
        var factory = new ActionCacheEndpointFilterFactory { Namespace = "Test" };

        var result = factory.CreateInstance(_serviceProvider);

        result.Should().BeOfType<ActionCacheEndpointFilter>();
    }

    [Test]
    public void ActionCacheEndpointEvictionFilterFactory_CreateInstance_ReturnsEvictionFilter()
    {
        var factory = new ActionCacheEndpointEvictionFilterFactory { Namespace = "Test" };

        var result = factory.CreateInstance(_serviceProvider);

        result.Should().BeOfType<ActionCacheEndpointEvictionFilter>();
    }

    [Test]
    public void ActionCacheFilterFactory_WithAbsoluteExpiration_CreatesFilterWithExpiration()
    {
        var factory = new ActionCacheFilterFactory
        {
            Namespace = "Test",
            AbsoluteExpiration = 5000
        };

        var result = factory.CreateInstance(_serviceProvider);

        // The name promises the expiration reaches the cache. Non-null did not check that:
        // a factory that discarded AbsoluteExpiration entirely still passed.
        result.Should().BeOfType<ActionCacheFilter>();
        _cacheFactoryMock.Verify(
            cacheFactory => cacheFactory.Create(
                It.IsAny<Namespace>(), TimeSpan.FromMilliseconds(5000), null),
            Times.AtLeastOnce);
    }

    [Test]
    public void ActionCacheFilterFactory_IsReusable_ReturnsFalse()
    {
        var factory = new ActionCacheFilterFactory { Namespace = "Test" };

        factory.IsReusable.Should().BeFalse();
    }
}
