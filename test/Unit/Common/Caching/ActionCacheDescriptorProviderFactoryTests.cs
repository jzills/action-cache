using ActionCache.Common.Caching;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Unit.Common.Caching;

[TestFixture]
public class ActionCacheDescriptorProviderFactoryTests
{
    [Test]
    public void Create_WhenNoDescriptorProviderRegistered_ReturnsNullProvider()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var sut = new ActionCacheDescriptorProviderFactory(serviceProvider);

        var result = sut.Create();

        result.Should().BeOfType<ActionCacheDescriptorProviderNull>();
    }

    [Test]
    public void Create_WhenDescriptorProviderRegistered_ReturnsRealProvider()
    {
        var descriptorProviderMock = new Mock<IActionDescriptorCollectionProvider>();
        descriptorProviderMock.Setup(provider => provider.ActionDescriptors)
            .Returns(new ActionDescriptorCollection([], 0));

        var services = new ServiceCollection();
        services.AddSingleton(descriptorProviderMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var sut = new ActionCacheDescriptorProviderFactory(serviceProvider);

        var result = sut.Create();

        result.Should().BeOfType<ActionCacheDescriptorProvider>();
    }
}
