using ActionCache.Common.Caching;
using ActionCache.Utilities;

namespace Unit.Common.Caching;

[TestFixture]
public class ActionCacheDescriptorProviderNullTests
{
    private ActionCacheDescriptorProviderNull _sut;

    [SetUp]
    public void SetUp()
    {
        _sut = new ActionCacheDescriptorProviderNull();
    }

    [Test]
    public void GetControllerActionMethodInfo_Always_ReturnsEmptyDescriptor()
    {
        var result = _sut.GetControllerActionMethodInfo(new Namespace("TestNs"));

        result.Should().NotBeNull();
        result.MethodInfos.Should().BeEmpty();
    }

    [Test]
    public void CreateKey_Always_ReturnsEmptyString()
    {
        var result = _sut.CreateKey("area", "controller", "action");

        result.Should().BeEmpty();
    }

    [Test]
    public void CreateKey_WithNullArguments_ReturnsEmptyString()
    {
        var result = _sut.CreateKey(null, null, null);

        result.Should().BeEmpty();
    }
}
