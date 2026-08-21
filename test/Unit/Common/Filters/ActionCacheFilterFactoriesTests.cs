using Unit.TestUtilities.Builders;
using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Common.Filters;
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
    private ServiceProvider _serviceProvider;

    [TearDown]
    public void TearDown() => _serviceProvider.Dispose();

    [SetUp]
    public void SetUp()
    {
        var cacheMock = new Mock<IActionCache>();
        cacheMock.Setup(cache => cache.GetNamespace()).Returns(new Namespace("Test"));

        var cacheFactoryMock = new Mock<IActionCacheFactory>();
        cacheFactoryMock
            .Setup(factory => factory.Create(It.IsAny<Namespace>()))
            .Returns(cacheMock.Object);
        cacheFactoryMock
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
            [cacheFactoryMock.Object], binderFactory, resilientDecorator, NullLoggerFactory.Instance, SingleFlightBuilder.Build());

        var endpointAbstractFactory = new ActionCacheEndpointFilterAbstractFactory(
            [cacheFactoryMock.Object], binderFactory, resilientDecorator, NullLoggerFactory.Instance, SingleFlightBuilder.Build());

        var services = new ServiceCollection();
        services.AddSingleton<IActionCacheFilterAbstractFactory<IFilterMetadata>>(mvcAbstractFactory);
        services.AddSingleton<IActionCacheFilterAbstractFactory<IEndpointFilter>>(endpointAbstractFactory);
        _serviceProvider = services.BuildServiceProvider() as ServiceProvider ?? throw new InvalidOperationException();
    }

    [Test]
    public void ActionCacheFilterFactory_CreateInstance_ReturnsAddFilter()
    {
        var factory = new ActionCacheFilterFactory { Namespace = "Test" };

        var result = factory.CreateInstance(_serviceProvider);

        result.Should().NotBeNull();
    }

    [Test]
    public void ActionCacheEvictionFilterFactory_CreateInstance_ReturnsEvictionFilter()
    {
        var factory = new ActionCacheEvictionFilterFactory { Namespace = "Test" };

        var result = factory.CreateInstance(_serviceProvider);

        result.Should().NotBeNull();
    }

    [Test]
    public void ActionCacheRefreshFilterFactory_CreateInstance_ReturnsRefreshFilter()
    {
        var factory = new ActionCacheRefreshFilterFactory { Namespace = "Test" };

        var result = factory.CreateInstance(_serviceProvider);

        result.Should().NotBeNull();
    }

    [Test]
    public void ActionCacheEndpointFilterFactory_CreateInstance_ReturnsAddFilter()
    {
        var factory = new ActionCacheEndpointFilterFactory { Namespace = "Test" };

        var result = factory.CreateInstance(_serviceProvider);

        result.Should().NotBeNull();
    }

    [Test]
    public void ActionCacheEndpointEvictionFilterFactory_CreateInstance_ReturnsEvictionFilter()
    {
        var factory = new ActionCacheEndpointEvictionFilterFactory { Namespace = "Test" };

        var result = factory.CreateInstance(_serviceProvider);

        result.Should().NotBeNull();
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

        result.Should().NotBeNull();
    }

    [Test]
    public void ActionCacheFilterFactory_IsReusable_ReturnsFalse()
    {
        var factory = new ActionCacheFilterFactory { Namespace = "Test" };

        factory.IsReusable.Should().BeFalse();
    }
}
