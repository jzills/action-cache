using ActionCache.Common.Caching;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Reflection;

namespace Unit.Common.Caching;

[TestFixture]
public class ActionCacheDescriptorProviderTests
{
    private Mock<IActionDescriptorCollectionProvider> _descriptorProviderMock;
    private ServiceProvider _serviceProvider;
    private ActionCacheDescriptorProvider _sut;

    [TearDown]
    public void TearDown() => _serviceProvider.Dispose();

    [SetUp]
    public void SetUp()
    {
        _descriptorProviderMock = new Mock<IActionDescriptorCollectionProvider>();
        _descriptorProviderMock.Setup(provider => provider.ActionDescriptors)
            .Returns(new ActionDescriptorCollection([], 0));

        _serviceProvider = new ServiceCollection().BuildServiceProvider() as ServiceProvider ?? throw new InvalidOperationException();

        _sut = new ActionCacheDescriptorProvider(_serviceProvider, _descriptorProviderMock.Object);
    }

    [Test]
    public void CreateKey_WithAreaControllerAndAction_ReturnsColonSeparatedKey()
    {
        var result = _sut.CreateKey("Area", "Controller", "Action");

        result.Should().Be("Area:Controller:Action");
    }

    [Test]
    public void CreateKey_WithoutArea_ReturnsControllerAndActionKey()
    {
        var result = _sut.CreateKey(null, "Controller", "Action");

        result.Should().Be("Controller:Action");
    }

    [Test]
    public void CreateKey_WithWhitespaceArea_OmitsAreaFromKey()
    {
        var result = _sut.CreateKey("   ", "Controller", "Action");

        result.Should().Be("Controller:Action");
    }

    [Test]
    public void CreateKey_WithNullController_ThrowsArgumentException()
    {
        Action act = () => _sut.CreateKey(null, null, "Action");

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void CreateKey_WithNullAction_ThrowsArgumentException()
    {
        Action act = () => _sut.CreateKey(null, "Controller", null);

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void CreateKey_WithWhitespaceController_ThrowsArgumentException()
    {
        Action act = () => _sut.CreateKey(null, "  ", "Action");

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void GetControllerActionMethodInfo_WhenNoDescriptors_ReturnsEmptyDescriptor()
    {
        var result = _sut.GetControllerActionMethodInfo("Test");

        result.MethodInfos.Should().BeEmpty();
        result.Controllers.Should().BeEmpty();
    }

    [Test]
    public void GetControllerActionMethodInfo_WhenMatchingDescriptorExists_ReturnsNonEmptyDescriptor()
    {
        var controllerType = typeof(CachedTestController);
        var methodInfo = controllerType.GetMethod("Get", BindingFlags.Public | BindingFlags.Instance)!;

        var descriptor = new ControllerActionDescriptor
        {
            MethodInfo = methodInfo,
            ControllerTypeInfo = controllerType.GetTypeInfo(),
            ControllerName = "CachedTest",
            ActionName = "Get",
            RouteValues = new Dictionary<string, string?>()
        };

        _descriptorProviderMock
            .Setup(provider => provider.ActionDescriptors)
            .Returns(new ActionDescriptorCollection([descriptor], 0));

        var serviceProvider = new ServiceCollection()
            .AddTransient(controllerType)
            .BuildServiceProvider();

        var sut = new ActionCacheDescriptorProvider(serviceProvider, _descriptorProviderMock.Object);

        var result = sut.GetControllerActionMethodInfo("DescriptorTestNs");

        result.MethodInfos.Should().NotBeEmpty();
    }
}

public class CachedTestController
{
    [ActionCache.Attributes.ActionCache(Namespace = "DescriptorTestNs")]
    public string Get() => "ok";
}
