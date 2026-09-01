using ActionCache.Common;
using ActionCache.Common.Caching;
using ActionCache.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Unit.Common.Caching;

[TestFixture]
public class ActionCacheResilienceOptionsTests
{
    [Test]
    public void ActionCacheResilienceOptions_DefaultsToFailOpen()
    {
        new ActionCacheResilienceOptions().FailClosed.Should().BeFalse();
    }

    [Test]
    public void FailClosed_WhenCalledWithNoArgument_SetsFailClosedTrue()
    {
        var builder = new TestableBuilder();

        builder.FailClosed();

        builder.BuildOptions().FailClosed.Should().BeTrue();
    }

    [Test]
    public void FailClosed_WhenCalledWithFalse_LeavesFailClosedFalse()
    {
        var builder = new TestableBuilder();

        builder.FailClosed(false);

        builder.BuildOptions().FailClosed.Should().BeFalse();
    }

    [Test]
    public void FailClosed_ReturnsBuilderForChaining()
    {
        var builder = new TestableBuilder();

        builder.FailClosed().Should().BeSameAs(builder);
    }

    [Test]
    public void AddActionCache_RegistersResilienceOptions_FromBuilder()
    {
        var services = new ServiceCollection();

        services.AddActionCache(options => options.FailClosed());

        var resolved = services.BuildServiceProvider()
            .GetRequiredService<IOptions<ActionCacheResilienceOptions>>();

        resolved.Value.FailClosed.Should().BeTrue();
    }

    // Exposes the protected Build() so the test can read the configured options.
    private sealed class TestableBuilder : ActionCacheOptionsBuilder
    {
        public ActionCacheOptions BuildOptions() => Build();
    }
}
