using ActionCache.Common.Keys;
using Unit.TestUtilities.Builders;
using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Common.Enums;
using ActionCache.Common.Filters;
using ActionCache.Exceptions;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Unit.Common.EndpointFilters;

[TestFixture]
public class ActionCacheEndpointFilterAbstractFactoryTests
{
    private Mock<IActionCacheFactory> _cacheFactoryMock = null!;
    private Mock<IActionCache> _cacheMock;
    private TemplateBinderFactory _binderFactory = null!;
    private ActionCacheEndpointFilterAbstractFactory _sut;

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IActionCache>();
        _cacheMock.Setup(cache => cache.GetNamespace()).Returns(new Namespace("Test"));

        _cacheFactoryMock = new Mock<IActionCacheFactory>();
        _cacheFactoryMock
            .Setup(factory => factory.Create(It.IsAny<Namespace>()))
            .Returns(_cacheMock.Object);
        _cacheFactoryMock
            .Setup(factory => factory.Create(It.IsAny<Namespace>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>()))
            .Returns(_cacheMock.Object);

        _binderFactory = new ServiceCollection()
            .AddLogging()
            .AddRouting()
            .BuildServiceProvider()
            .GetRequiredService<TemplateBinderFactory>();

        var resilientDecorator = new ResilientCacheDecorator(
            NullLoggerFactory.Instance,
            Options.Create(new ActionCacheResilienceOptions()));

        _sut = new ActionCacheEndpointFilterAbstractFactory(
            [_cacheFactoryMock.Object], _binderFactory, resilientDecorator, NullLoggerFactory.Instance, SingleFlightBuilder.Build(), VaryByBuilder.Resolver(), ResponseFactoryBuilder.Build(), new ActionCacheKeyOptions());
    }

    [Test]
    public void CreateInstance_WithAddType_ReturnsNonNullFilter()
    {
        var result = _sut.CreateInstance((Namespace)"Test", FilterType.Add);

        result.Should().NotBeNull();
    }

    [Test]
    public void CreateInstance_WithEvictType_ReturnsNonNullFilter()
    {
        var result = _sut.CreateInstance((Namespace)"Test", FilterType.Evict);

        result.Should().NotBeNull();
    }

    [Test]
    public void CreateInstance_WithRefreshType_ThrowsFilterTypeNotSupportedException()
    {
        Action act = () => _sut.CreateInstance((Namespace)"Test", FilterType.Refresh);

        act.Should().Throw<FilterTypeNotSupportedException>();
    }

    [Test]
    public void CreateInstance_WithExpiration_UsesExpirationFactory()
    {
        var result = _sut.CreateInstance((Namespace)"Test", TimeSpan.FromSeconds(30), null, FilterType.Add);

        result.Should().NotBeNull();
        _cacheFactoryMock.Verify(
            factory => factory.Create(It.IsAny<Namespace>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>()),
            Times.AtLeastOnce);
    }

    [Test]
    public void CreateHandler_WithEmptyCaches_ThrowsInvalidCacheInstanceException()
    {
        Action act = () => _sut.CreateHandler([], FilterType.Add);

        act.Should().Throw<InvalidCacheInstanceException>();
    }

    [Test]
    public void CreateHandler_WithSingleCache_ReturnsFilter()
    {
        var result = _sut.CreateHandler([_cacheMock.Object], FilterType.Add);

        result.Should().NotBeNull();
    }

    [Test]
    public void CreateFilter_WithUnsupportedFilterType_ThrowsFilterTypeNotSupportedException()
    {
        var handler = new ActionCacheHandler(_cacheMock.Object);

        Action act = () => _sut.CreateFilter(handler, (FilterType)99, true, VaryByBuilder.Options());

        act.Should().Throw<FilterTypeNotSupportedException>();
    }

    [Test]
    public void GetCacheInstances_WithSingleNamespace_ReturnsSingleInstance()
    {
        var result = _sut.GetCacheInstances((Namespace)"Test");

        result.Should().HaveCount(1);
    }

    [Test]
    public void GetCacheInstances_WithCommaDelimitedNamespace_ReturnsInstancePerNamespace()
    {
        var result = _sut.GetCacheInstances((Namespace)"Ns1,Ns2");

        result.Should().HaveCount(2);
    }

    [Test]
    public void AddCacheInstances_WhenFactoryReturnsNull_ThrowsInvalidCacheInstanceException()
    {
        _cacheFactoryMock
            .Setup(factory => factory.Create(It.IsAny<Namespace>()))
            .Returns((IActionCache?)null);

        var cacheInstances = new List<IActionCache>();

        Action act = () => _sut.AddCacheInstances((Namespace)"Test", cacheInstances);

        act.Should().Throw<InvalidCacheInstanceException>();
    }

    [Test]
    public void AddCacheInstances_WhenFactoryReturnsCache_InvokesFactoryOnce()
    {
        var cacheInstances = new List<IActionCache>();

        _sut.AddCacheInstances((Namespace)"Test", cacheInstances);

        _cacheFactoryMock.Verify(factory => factory.Create(It.IsAny<Namespace>()), Times.Once);
    }
}
