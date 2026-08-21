using ActionCache.Common.Caching;
using ActionCache.Common.Keys;
using Unit.TestUtilities;
using ActionCache.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Unit.Common.Caching;

[TestFixture]
public class ActionCacheRefreshProviderTests
{
    private Mock<IActionCacheDescriptorProvider> _descriptorProviderMock;
    private ActionCacheRefreshProvider _sut;

    [SetUp]
    public void SetUp()
    {
        _descriptorProviderMock = new Mock<IActionCacheDescriptorProvider>();
        _sut = new ActionCacheRefreshProvider(_descriptorProviderMock.Object, NullLogger<ActionCacheRefreshProvider>.Instance);
    }

    [Test]
    public void GetRefreshResults_WhenNoMethodInfos_ReturnsEmptyDictionary()
    {
        _descriptorProviderMock
            .Setup(provider => provider.GetControllerActionMethodInfo(It.IsAny<Namespace>()))
            .Returns(new ActionCacheDescriptor());

        var result = _sut.GetRefreshResults(new Namespace("Test"), []);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetRefreshResults_WhenKeysIsEmpty_ReturnsEmptyDictionary()
    {
        var descriptor = new ActionCacheDescriptor();
        _descriptorProviderMock
            .Setup(provider => provider.GetControllerActionMethodInfo(It.IsAny<Namespace>()))
            .Returns(descriptor);

        var result = _sut.GetRefreshResults(new Namespace("Test"), []);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetRefreshResults_WhenDescriptorHasNoMethodInfos_ReturnsEmptyDictionary()
    {
        _descriptorProviderMock
            .Setup(provider => provider.GetControllerActionMethodInfo(It.IsAny<Namespace>()))
            .Returns(new ActionCacheDescriptor());

        var result = _sut.GetRefreshResults(new Namespace("Test"), ["some-key"]);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetRefreshResults_WhenAKeyCarriesVaryByValues_SkipsItAndWarns()
    {
        // Refresh re-invokes actions by reflection with no HttpContext, so it cannot
        // reproduce a per-user response and would warm every variant with one identical
        // value. Skipping is the only correct option until refresh runs the real pipeline.
        var logger = new CapturingLogger<ActionCacheRefreshProvider>();
        var sut = new ActionCacheRefreshProvider(_descriptorProviderMock.Object, logger);

        // The skip is checked inside the loop, which only runs when the descriptor has at
        // least one action to consider.
        var descriptor = new ActionCacheDescriptor();
        descriptor.Add("some-key", GetType().GetMethod(nameof(GetRefreshResults_WhenAKeyCarriesVaryByValues_SkipsItAndWarns))!, this);
        _descriptorProviderMock
            .Setup(provider => provider.GetControllerActionMethodInfo(It.IsAny<Namespace>()))
            .Returns(descriptor);

        var variedKey = new ActionCacheKeyBuilder()
            .WithRouteValues(new Microsoft.AspNetCore.Routing.RouteValueDictionary
            {
                { "controller", "Users" },
                { "action", "Get" }
            })
            .WithVaryByValues(new SortedDictionary<string, string?> { ["user"] = "user-1" })
            .Build();

        var result = sut.GetRefreshResults(new Namespace("Test"), [variedKey]);

        result.Should().NotContainKey(variedKey);
        logger.Entries.Should().Contain(entry =>
            entry.EventId.Id == 7001 && entry.Message.Contains("varies by request context"));
    }
}
