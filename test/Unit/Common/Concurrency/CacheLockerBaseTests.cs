using ActionCache.Common.Concurrency;
using ActionCache.Common.Concurrency.Locks;

namespace Unit.Common.Concurrency;

[TestFixture]
public class CacheLockerBaseTests
{
    private sealed class AlwaysFailingLocker : CacheLockerBase<NullCacheLock>
    {
        public AlwaysFailingLocker() : base(TimeSpan.FromSeconds(10)) { }

        public override Task ReleaseLockAsync(NullCacheLock cacheLock) => Task.CompletedTask;

        public override Task<NullCacheLock> TryAcquireLockAsync(string resource)
        {
            var unacquired = new NullCacheLock(resource);
            unacquired.IsAcquired = false;
            return Task.FromResult(unacquired);
        }

        public override Task<NullCacheLock> WaitForLockAsync(string resource) =>
            TryAcquireLockAsync(resource);
    }

    private sealed class AcquiringLocker : CacheLockerBase<NullCacheLock>
    {
        public AcquiringLocker() : base(TimeSpan.FromSeconds(10)) { }

        public override Task ReleaseLockAsync(NullCacheLock cacheLock) => Task.CompletedTask;

        public override Task<NullCacheLock> TryAcquireLockAsync(string resource) =>
            Task.FromResult(new NullCacheLock(resource));

        public override Task<NullCacheLock> WaitForLockAsync(string resource) =>
            TryAcquireLockAsync(resource);
    }

    private AlwaysFailingLocker _locker = null!;

    [SetUp]
    public void SetUp() => _locker = new AlwaysFailingLocker();

    [Test]
    public async Task TryWaitForLockThenAsync_WhenLockNotAcquired_ReportsItWithoutThrowingOrRunningTheWork()
    {
        var ran = false;

        var attempt = await _locker.TryWaitForLockThenAsync<string>("resource", () =>
        {
            ran = true;
            return Task.FromResult("produced");
        });

        attempt.LockAcquired.Should().BeFalse();
        attempt.Result.Should().BeNull();
        ran.Should().BeFalse("the work must not run without the lock");
    }

    [Test]
    public async Task TryWaitForLockThenAsync_WhenTheWorkThrows_PropagatesInsteadOfReportingALockFailure()
    {
        // The distinction the type exists for: a caller must be able to tell "the lock was
        // busy" from "the work failed", because it retries the first and must not the second.
        var locker = new AcquiringLocker();

        var act = async () => await locker.TryWaitForLockThenAsync<string>(
            "resource", () => throw new InvalidOperationException("the work failed"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("the work failed");
    }

    [Test]
    public async Task WaitForLockThenAsync_Action_WhenLockNotAcquired_ThrowsInvalidOperationException()
    {
        Func<Task> act = () => _locker.WaitForLockThenAsync("resource", () => { });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*resource*");
    }

    [Test]
    public async Task WaitForLockThenAsync_AsyncAction_WhenLockNotAcquired_ThrowsInvalidOperationException()
    {
        Func<Task> act = () => _locker.WaitForLockThenAsync("resource", () => Task.CompletedTask);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*resource*");
    }

    [Test]
    public async Task WaitForLockThenAsync_AsyncFunc_WhenLockNotAcquired_ThrowsInvalidOperationException()
    {
        Func<Task> act = () => _locker.WaitForLockThenAsync("resource", () => Task.FromResult(42));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*resource*");
    }
}
