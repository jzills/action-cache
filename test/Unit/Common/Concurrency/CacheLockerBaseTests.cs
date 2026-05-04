using ActionCache.Common.Concurrency;
using ActionCache.Common.Concurrency.Locks;

namespace Unit.Common.Concurrency;

[TestFixture]
public class CacheLockerBaseTests
{
    private sealed class AlwaysFailingLocker : CacheLockerBase<NullCacheLock>
    {
        public AlwaysFailingLocker() : base(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)) { }

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

    private AlwaysFailingLocker _locker = null!;

    [SetUp]
    public void SetUp() => _locker = new AlwaysFailingLocker();

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
