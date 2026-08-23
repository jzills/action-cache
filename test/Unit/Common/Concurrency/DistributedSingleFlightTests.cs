using ActionCache.Common.Concurrency;
using Microsoft.Extensions.Logging.Abstractions;

namespace Unit.Common.Concurrency;

[TestFixture]
public class DistributedSingleFlightTests
{
    /// <summary>
    /// Mimics a lock held by another node until the timeout elapses.
    /// </summary>
    private sealed class TimingOutLockerHandler : ICacheLockerHandler
    {
        public Task WaitForLockThenAsync(string resource, Action thenFunc) => throw Timeout(resource);
        public Task<TResult?> WaitForLockThenAsync<TResult>(string resource, Func<TResult> resultAccessor) => throw Timeout(resource);
        public Task WaitForLockThenAsync(string resource, Func<Task> thenFunc) => throw Timeout(resource);
        public Task<TResult?> WaitForLockThenAsync<TResult>(string resource, Func<Task<TResult>> resultAccessor) => throw Timeout(resource);

        public Task<CacheLockAttempt<TResult>> TryWaitForLockThenAsync<TResult>(
            string resource,
            Func<Task<TResult>> resultAccessor) =>
            Task.FromResult(new CacheLockAttempt<TResult>(LockAcquired: false, Result: default));

        private static InvalidOperationException Timeout(string resource) =>
            new($"Failed to acquire lock for resource '{resource}' within the configured timeout.");
    }

    /// <summary>
    /// Acquires immediately and runs the guarded work, like an uncontended distributed lock.
    /// </summary>
    private sealed class AcquiringLockerHandler : ICacheLockerHandler
    {
        public Task WaitForLockThenAsync(string resource, Action thenFunc)
        {
            thenFunc();
            return Task.CompletedTask;
        }

        public Task<TResult?> WaitForLockThenAsync<TResult>(string resource, Func<TResult> resultAccessor) =>
            Task.FromResult<TResult?>(resultAccessor());

        public Task WaitForLockThenAsync(string resource, Func<Task> thenFunc) => thenFunc();

        public async Task<TResult?> WaitForLockThenAsync<TResult>(string resource, Func<Task<TResult>> resultAccessor) =>
            await resultAccessor();

        public async Task<CacheLockAttempt<TResult>> TryWaitForLockThenAsync<TResult>(
            string resource,
            Func<Task<TResult>> resultAccessor) =>
            new(LockAcquired: true, Result: await resultAccessor());
    }

    private static DistributedSingleFlight Create(ICacheLockerHandler locker) =>
        new(locker, NullLogger<DistributedSingleFlight>.Instance);

    [Test]
    public async Task GetOrCreateAsync_WhenTheDistributedLockTimesOut_ExecutesUncoalescedRatherThanThrowing()
    {
        var singleFlight = Create(new TimingOutLockerHandler());

        var result = await singleFlight.GetOrCreateAsync<string>(
            "Namespace",
            "Key",
            cacheReader: () => Task.FromResult<string?>(null),
            valueFactory: () => Task.FromResult<string?>("Produced"));

        result.Value.Should().Be("Produced");
        result.WasCoalesced.Should().BeFalse();
    }

    [Test]
    public async Task GetOrCreateAsync_WhenTheLockIsAcquiredAndTheEntryExists_Coalesces()
    {
        var singleFlight = Create(new AcquiringLockerHandler());
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
    public async Task GetOrCreateAsync_WhenTheLockIsAcquiredAndNothingIsCached_RunsTheValueFactory()
    {
        var singleFlight = Create(new AcquiringLockerHandler());

        var result = await singleFlight.GetOrCreateAsync<string>(
            "Namespace",
            "Key",
            cacheReader: () => Task.FromResult<string?>(null),
            valueFactory: () => Task.FromResult<string?>("Produced"));

        result.WasCoalesced.Should().BeFalse();
        result.Value.Should().Be("Produced");
    }

    [Test]
    public async Task GetOrCreateAsync_WhenTheValueFactoryThrowsInvalidOperation_RunsItOnceAndPropagates()
    {
        // Regression: lock timeout used to be inferred from InvalidOperationException, which
        // the origin action raises just as readily as the locker. That logged a misleading
        // lock timeout and then ran the action a second time — in MVC, a second next() on a
        // context that has already been invoked.
        var singleFlight = Create(new AcquiringLockerHandler());
        var factoryRuns = 0;

        var act = async () => await singleFlight.GetOrCreateAsync<string>(
            "Namespace",
            "Key",
            cacheReader: () => Task.FromResult<string?>(null),
            valueFactory: () =>
            {
                Interlocked.Increment(ref factoryRuns);
                throw new InvalidOperationException("the action failed");
            });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("the action failed");
        factoryRuns.Should().Be(1, "a failure of the work is not a failure to lock, so it must not be retried");
    }

    [Test]
    public async Task GetOrCreateAsync_WhenTheLockTimesOutAndTheFactoryThrows_DoesNotSwallowTheFailure()
    {
        var singleFlight = Create(new TimingOutLockerHandler());

        var act = async () => await singleFlight.GetOrCreateAsync<string>(
            "Namespace",
            "Key",
            cacheReader: () => Task.FromResult<string?>(null),
            valueFactory: () => throw new InvalidOperationException("the action failed"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("the action failed");
    }
}
