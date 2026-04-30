using ActionCache.Common.Extensions.Internal;

namespace Unit.Common.Extensions;

[TestFixture]
public class StringExtensionsTests
{
    [Test]
    public void SplitNamespace_WhenCommaSeparated_ReturnsEachValue()
    {
        var result = "alpha,beta,gamma".SplitNamespace();

        result.Should().BeEquivalentTo(["alpha", "beta", "gamma"]);
    }

    [Test]
    public void SplitNamespace_WhenValuesHaveWhitespace_ReturnsTrimmedValues()
    {
        var result = " alpha , beta , gamma ".SplitNamespace();

        result.Should().BeEquivalentTo(["alpha", "beta", "gamma"]);
    }

    [Test]
    public void SplitNamespace_WhenEmptySegmentsPresent_SkipsEmptyEntries()
    {
        var result = "alpha,,gamma".SplitNamespace();

        result.Should().BeEquivalentTo(["alpha", "gamma"]);
    }

    [Test]
    public void SplitNamespace_WhenSingleValue_ReturnsSingleElement()
    {
        var result = "only".SplitNamespace();

        result.Should().ContainSingle().Which.Should().Be("only");
    }

    [Test]
    public void SplitNamespace_WhenOnlyCommas_ReturnsEmpty()
    {
        var result = ",,,".SplitNamespace();

        result.Should().BeEmpty();
    }
}
