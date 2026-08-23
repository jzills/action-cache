using ActionCache.Common;
using ActionCache.Common.Concurrency;
using Microsoft.Extensions.Logging.Abstractions;

namespace Unit.Common.Concurrency;

[TestFixture]
public class InProcessSingleFlightTests
{
    private static InProcessSingleFlight Create(ActionCacheSingleFlightOptions? options = null) =>
        new(options ?? new ActionCacheSingleFlightOptions(), NullLogger<InProcessSingleFlight>.Instance);

    [Test]
    public async Task GetOrCreateAsync_WhenNothingIsCached_RunsTheValueFactory()
    {
        var singleFlight = Create();

        var result = await singleFlight.GetOrCreateAsync<string>(
            "Namespace",
            "Key",
            cacheReader: () => Task.FromResult<string?>(null),
            valueFactory: () => Task.FromResult<string?>("Produced"));

        result.WasCoalesced.Should().BeFalse();
        result.Value.Should().Be("Produced");
    }

    [Test]
    public async Task GetOrCreateAsync_WhenTheLeaderPopulatedTheCache_CoalescesWithoutRunningTheFactory()
    {
        var singleFlight = Create();
        var factoryRuns = 0;

        var result = await singleFlight.GetOrCreateAsync<string>(
            "Namespace",
            "Key",
            cacheReader: () => Task.FromResult<string?>("FromCache"),
            valueFactory: () =>
            {
                Interlocked.Increment(ref factoryRuns);
                return Task.FromResult<string?>("Produced");
            });

        result.WasCoalesced.Should().BeTrue();
        result.Value.Should().Be("FromCache");
        factoryRuns.Should().Be(0);
    }

    [Test]
    public async Task GetOrCreateAsync_UnderConcurrentMissesOnOneKey_RunsTheFactoryExactlyOnce()
    {
        var singleFlight = Create();
        var factoryRuns = 0;
        string? cached = null;

        // Mirrors the filter: the reader sees whatever the leader has stored so far.
        Task<string?> Read() => Task.FromResult(Volatile.Read(ref cached));

        async Task<string?> Produce()
        {
            Interlocked.Increment(ref factoryRuns);
            await Task.Delay(25);
            Volatile.Write(ref cached, "Produced");
            return "Produced";
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ =>
            singleFlight.GetOrCreateAsync("Namespace", "Key", Read, Produce)));

        factoryRuns.Should().Be(1);
        results.Count(result => result.WasCoalesced).Should().Be(39);
        results.Should().OnlyContain(result => result.Value == "Produced");
    }

    [Test]
    public async Task GetOrCreateAsync_WhenKeysDiffer_DoesNotSerialize()
    {
        var singleFlight = Create();
        var factoryRuns = 0;

        await Task.WhenAll(Enumerable.Range(0, 10).Select(index =>
            singleFlight.GetOrCreateAsync<string>(
                "Namespace",
                $"Key:{index}",
                cacheReader: () => Task.FromResult<string?>(null),
                valueFactory: () =>
                {
                    Interlocked.Increment(ref factoryRuns);
                    return Task.FromResult<string?>("Produced");
                })));

        factoryRuns.Should().Be(10);
    }

    [Test]
    public async Task GetOrCreateAsync_WhenTheSameKeyIsUsedInDifferentNamespaces_DoesNotCoalesceAcrossThem()
    {
        var singleFlight = Create();
        var factoryRuns = 0;

        await Task.WhenAll(
            singleFlight.GetOrCreateAsync<string>("First", "Key",
                () => Task.FromResult<string?>(null),
                () => { Interlocked.Increment(ref factoryRuns); return Task.FromResult<string?>("A"); }),
            singleFlight.GetOrCreateAsync<string>("Second", "Key",
                () => Task.FromResult<string?>(null),
                () => { Interlocked.Increment(ref factoryRuns); return Task.FromResult<string?>("B"); }));

        factoryRuns.Should().Be(2);
    }

    [Test]
    public async Task GetOrCreateAsync_WhenTheLockCannotBeAcquired_ExecutesUncoalescedRatherThanThrowing()
    {
        // A 1ms timeout against a factory that holds the lock far longer forces the
        // acquisition failure path.
        var options = new ActionCacheSingleFlightOptions { WaitTimeout = TimeSpan.FromMilliseconds(1) };
        var singleFlight = Create(options);
        var factoryRuns = 0;

        async Task<string?> Produce()
        {
            Interlocked.Increment(ref factoryRuns);
            await Task.Delay(200);
            return "Produced";
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            singleFlight.GetOrCreateAsync("Namespace", "Key",
                cacheReader: () => Task.FromResult<string?>(null),
                valueFactory: Produce)));

        // Every request still gets a value; nothing throws.
        results.Should().OnlyContain(result => result.Value == "Produced");
        factoryRuns.Should().BeGreaterThan(1);
    }
}
