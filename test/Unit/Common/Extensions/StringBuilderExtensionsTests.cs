using System.Text;
using ActionCache.Common.Extensions.Internal;

namespace Unit.Common.Extensions;

[TestFixture]
public class StringBuilderExtensionsTests
{
    [Test]
    public void AppendJoinNonNull_WhenAllValuesNonNull_JoinsAll()
    {
        var builder = new StringBuilder();

        builder.AppendJoinNonNull(':', "a", "b", "c");

        builder.ToString().Should().Be("a:b:c");
    }

    [Test]
    public void AppendJoinNonNull_WhenSomeNull_SkipsNullValues()
    {
        var builder = new StringBuilder();

        builder.AppendJoinNonNull(':', "a", null, "c");

        builder.ToString().Should().Be("a:c");
    }

    [Test]
    public void AppendJoinNonNull_WhenSomeWhitespace_SkipsWhitespaceValues()
    {
        var builder = new StringBuilder();

        builder.AppendJoinNonNull(':', "a", "  ", "c");

        builder.ToString().Should().Be("a:c");
    }

    [Test]
    public void AppendJoinNonNull_WhenAllNull_AppendsNothing()
    {
        var builder = new StringBuilder();

        builder.AppendJoinNonNull(':', null, null, null);

        builder.ToString().Should().BeEmpty();
    }

    [Test]
    public void AppendJoinNonNull_WhenSingleValue_AppendsWithoutSeparator()
    {
        var builder = new StringBuilder();

        builder.AppendJoinNonNull(':', "only");

        builder.ToString().Should().Be("only");
    }

    [Test]
    public void AppendJoinNonNull_Always_ReturnsSameBuilderInstance()
    {
        var builder = new StringBuilder();

        var returned = builder.AppendJoinNonNull(':', "a", "b");

        returned.Should().BeSameAs(builder);
    }
}
