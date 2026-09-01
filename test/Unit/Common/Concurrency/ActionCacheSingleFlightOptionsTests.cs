using ActionCache.Common.Concurrency;
using ActionCache.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Unit.Common.Concurrency;

[TestFixture]
public class ActionCacheSingleFlightOptionsTests
{
    [Test]
    public void Defaults_LeaseOutlastsTheWait()
    {
        // The inversion this guards: single flight used to borrow the key-index lock's
        // settings, whose 5 s duration was shorter than its 10 s timeout. On Redis the
        // duration is the lock key's TTL, so any action taking 5–10 s lost its lock while
        // still running and a waiter executed it a second time.
        var options = new ActionCacheSingleFlightOptions();

        options.LeaseDuration.Should().BeGreaterThan(options.WaitTimeout);
    }

    [Test]
    public void Validate_WithTheDefaults_DoesNotThrow()
    {
        var act = () => new ActionCacheSingleFlightOptions().Validate();

        act.Should().NotThrow();
    }

    [Test]
    public void Validate_WhenTheLeaseIsShorterThanTheWait_Throws()
    {
        var options = new ActionCacheSingleFlightOptions
        {
            LeaseDuration = TimeSpan.FromSeconds(5),
            WaitTimeout = TimeSpan.FromSeconds(10)
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*must be longer than*");
    }

    [Test]
    public void Validate_WhenTheLeaseEqualsTheWait_Throws()
    {
        // Equal is still wrong: a caller that waits the full timeout finds the lease
        // expiring at exactly the moment it gives up.
        var options = new ActionCacheSingleFlightOptions
        {
            LeaseDuration = TimeSpan.FromSeconds(10),
            WaitTimeout = TimeSpan.FromSeconds(10)
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>();
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Validate_WhenTheLeaseIsNotPositive_Throws(int seconds)
    {
        var options = new ActionCacheSingleFlightOptions { LeaseDuration = TimeSpan.FromSeconds(seconds) };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Validate_WhenTheWaitIsNotPositive_Throws()
    {
        var options = new ActionCacheSingleFlightOptions { WaitTimeout = TimeSpan.Zero };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void AddActionCache_WithAnUnusableLease_FailsAtRegistrationRatherThanUnderLoad()
    {
        var services = new ServiceCollection();

        var act = () => services.AddActionCache(options => options
            .UseMemoryCache(memory => { })
            .UseSingleFlightOptions(singleFlight =>
            {
                singleFlight.LeaseDuration = TimeSpan.FromSeconds(1);
                singleFlight.WaitTimeout = TimeSpan.FromSeconds(30);
            }));

        act.Should().Throw<InvalidOperationException>().WithMessage("*must be longer than*");
    }

    [Test]
    public void AddActionCache_WithAConfiguredLease_MakesItAvailableToSingleFlight()
    {
        var services = new ServiceCollection();

        services.AddActionCache(options => options
            .UseMemoryCache(memory => { })
            .UseSingleFlightOptions(singleFlight => singleFlight.LeaseDuration = TimeSpan.FromMinutes(2)));

        var registered = services.BuildServiceProvider()
            .GetRequiredService<ActionCacheSingleFlightOptions>();

        registered.LeaseDuration.Should().Be(TimeSpan.FromMinutes(2));
    }
}
