using System.Reflection;
using ActionCache.Common.Caching;

namespace Unit.Common;

[TestFixture]
public class ActionCacheDescriptorTests
{
    private ActionCacheDescriptor _descriptor;

    [SetUp]
    public void SetUp() => _descriptor = new ActionCacheDescriptor();

    [Test]
    public void Add_WithValidKey_PopulatesBothDictionaries()
    {
        var method = typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!;
        _descriptor.Add("key", method, this);

        _descriptor.MethodInfos.Should().ContainKey("key");
        _descriptor.Controllers.Should().ContainKey("key");
    }

    [Test]
    public void Add_WithNullKey_ThrowsArgumentException()
    {
        var method = typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!;
        var act = () => _descriptor.Add(null!, method, this);
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Add_WithEmptyKey_ThrowsArgumentException()
    {
        var method = typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!;
        var act = () => _descriptor.Add(string.Empty, method, this);
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Add_WithWhitespaceKey_ThrowsArgumentException()
    {
        var method = typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!;
        var act = () => _descriptor.Add("   ", method, this);
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Add_DuplicateKey_ThrowsArgumentException()
    {
        var method = typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!;
        _descriptor.Add("key", method, this);
        var act = () => _descriptor.Add("key", method, this);
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void InitialState_HasEmptyDictionaries()
    {
        _descriptor.MethodInfos.Should().BeEmpty();
        _descriptor.Controllers.Should().BeEmpty();
    }
}
