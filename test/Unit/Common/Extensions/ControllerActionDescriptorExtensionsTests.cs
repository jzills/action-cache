using ActionCache.Common.Extensions.Internal;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Reflection;

namespace Unit.Common.Extensions;

[TestFixture]
public class ControllerActionDescriptorExtensionsTests
{
    [Test]
    public void Deconstruct_WithoutArea_SetsNullAreaName()
    {
        var descriptor = new ControllerActionDescriptor
        {
            ControllerName = "Users",
            ActionName = "GetAll",
            ControllerTypeInfo = typeof(FakeController).GetTypeInfo(),
            RouteValues = new Dictionary<string, string?>()
        };

        var (areaName, controllerName, actionName, controllerTypeInfo) = descriptor;

        areaName.Should().BeNull();
        controllerName.Should().Be("Users");
        actionName.Should().Be("GetAll");
        controllerTypeInfo.Should().Be(typeof(FakeController).GetTypeInfo());
    }

    [Test]
    public void Deconstruct_WithArea_SetsAreaName()
    {
        var descriptor = new ControllerActionDescriptor
        {
            ControllerName = "Users",
            ActionName = "Get",
            ControllerTypeInfo = typeof(FakeController).GetTypeInfo(),
            RouteValues = new Dictionary<string, string?> { { "area", "Admin" } }
        };

        var (areaName, controllerName, actionName, controllerTypeInfo) = descriptor;

        areaName.Should().Be("Admin");
        controllerName.Should().Be("Users");
        actionName.Should().Be("Get");
    }
}

file class FakeController { }
