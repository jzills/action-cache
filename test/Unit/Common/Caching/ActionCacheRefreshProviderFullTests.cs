using ActionCache.Common.Caching;
using ActionCache.Common.Keys;
using ActionCache.Utilities;
using Microsoft.AspNetCore.Routing;
using Moq;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Unit.Common.Caching;

[TestFixture]
public class ActionCacheRefreshProviderFullTests
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
    public void GetRefreshResults_WhenKeyMatchesDescriptorAndMethodReturnsValue_ReturnsResult()
    {
        var controller = new RefreshTestController();
        var methodInfo = typeof(RefreshTestController).GetMethod(nameof(RefreshTestController.GetValue))!;

        var descriptorKey = "TestController:GetValue";
        var descriptor = new ActionCacheDescriptor();
        descriptor.Add(descriptorKey, methodInfo, controller);

        _descriptorProviderMock
            .Setup(provider => provider.GetControllerActionMethodInfo(It.IsAny<Namespace>()))
            .Returns(descriptor);
        _descriptorProviderMock
            .Setup(provider => provider.CreateKey(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(descriptorKey);

        var encodedKey = new ActionCacheKeyBuilder()
            .WithRouteValues(new RouteValueDictionary
            {
                { "controller", "TestController" },
                { "action", "GetValue" }
            })
            .Build();

        var result = _sut.GetRefreshResults(new Namespace("Test"), [encodedKey]);

        result.Should().ContainKey(encodedKey);
        result[encodedKey].Should().Be("cached-value");
    }

    [Test]
    public void GetRefreshResults_WhenKeyDoesNotMatchDescriptor_ReturnsEmptyDictionary()
    {
        var controller = new RefreshTestController();
        var methodInfo = typeof(RefreshTestController).GetMethod(nameof(RefreshTestController.GetValue))!;

        var descriptor = new ActionCacheDescriptor();
        descriptor.Add("OtherController:OtherAction", methodInfo, controller);

        _descriptorProviderMock
            .Setup(provider => provider.GetControllerActionMethodInfo(It.IsAny<Namespace>()))
            .Returns(descriptor);
        _descriptorProviderMock
            .Setup(provider => provider.CreateKey(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("TestController:GetValue");

        var encodedKey = new ActionCacheKeyBuilder()
            .WithRouteValues(new RouteValueDictionary
            {
                { "controller", "TestController" },
                { "action", "GetValue" }
            })
            .Build();

        var result = _sut.GetRefreshResults(new Namespace("Test"), [encodedKey]);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetRefreshResults_WhenMethodReturnsNull_DoesNotAddToResults()
    {
        var controller = new RefreshTestController();
        var methodInfo = typeof(RefreshTestController).GetMethod(nameof(RefreshTestController.GetNull))!;

        var descriptorKey = "TestController:GetNull";
        var descriptor = new ActionCacheDescriptor();
        descriptor.Add(descriptorKey, methodInfo, controller);

        _descriptorProviderMock
            .Setup(provider => provider.GetControllerActionMethodInfo(It.IsAny<Namespace>()))
            .Returns(descriptor);
        _descriptorProviderMock
            .Setup(provider => provider.CreateKey(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(descriptorKey);

        var encodedKey = new ActionCacheKeyBuilder()
            .WithRouteValues(new RouteValueDictionary
            {
                { "controller", "TestController" },
                { "action", "GetNull" }
            })
            .Build();

        var result = _sut.GetRefreshResults(new Namespace("Test"), [encodedKey]);

        result.Should().BeEmpty();
    }
}

file class RefreshTestController
{
    public string GetValue() => "cached-value";
    public string? GetNull() => null;
}
