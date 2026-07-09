using ActionCache.Common.Caching;
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
}
