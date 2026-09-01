using ActionCache.Common.Extensions.Internal;

namespace Unit.Common.Extensions;

[TestFixture]
public class IEnumerableExtensionsTests
{
    [Test]
    public void Some_WhenCollectionHasElements_ReturnsTrue()
    {
        IEnumerable<int> source = [1, 2, 3];

        source.Some().Should().BeTrue();
    }

    [Test]
    public void Some_WhenCollectionIsEmpty_ReturnsFalse()
    {
        IEnumerable<int> source = [];

        source.Some().Should().BeFalse();
    }

    [Test]
    public void Some_WhenCollectionIsNull_ReturnsFalse()
    {
        IEnumerable<int>? source = null;

        source.Some().Should().BeFalse();
    }

    [Test]
    public void Some_WhenCollectionHasSingleElement_ReturnsTrue()
    {
        IEnumerable<string> source = ["only"];

        source.Some().Should().BeTrue();
    }
}
