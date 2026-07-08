using ActionCache.AzureCosmos;

namespace Unit.AzureCosmos;

[TestFixture]
public class AsyncLazyTests
{
    [Test]
    public async Task Value_WhenAccessedFromManyThreadsConcurrently_RunsFactoryExactlyOnce()
    {
        var invocations = 0;
        var lazy = new AsyncLazy<int>(async () =>
        {
            Interlocked.Increment(ref invocations);
            await Task.Delay(50);
            return 42;
        });

        // Dispatch each access on its own thread-pool task so they genuinely
        // race on the lock, rather than being enumerated one-by-one on the test thread.
        var accesses = Enumerable.Range(0, 25)
            .Select(_ => Task.Run(() => lazy.Value))
            .ToArray();

        var results = await Task.WhenAll(accesses);

        invocations.Should().Be(1);
        results.Should().OnlyContain(value => value == 42);
    }

    [Test]
    public async Task Value_WhenAwaitedAgainAfterCompletion_DoesNotRerunFactory()
    {
        var invocations = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            Interlocked.Increment(ref invocations);
            return Task.FromResult(7);
        });

        (await lazy.Value).Should().Be(7);
        (await lazy.Value).Should().Be(7);

        invocations.Should().Be(1);
    }

    [Test]
    public async Task Value_WhenFirstFactoryTaskFaults_RetriesOnNextAccess()
    {
        var attempts = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            var attempt = Interlocked.Increment(ref attempts);
            return attempt == 1
                ? Task.FromException<int>(new InvalidOperationException("transient"))
                : Task.FromResult(99);
        });

        var firstAccess = async () => await lazy.Value;
        await firstAccess.Should().ThrowAsync<InvalidOperationException>();

        (await lazy.Value).Should().Be(99);
        attempts.Should().Be(2);
    }
}
