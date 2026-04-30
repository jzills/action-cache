using ActionCache.Common.Concurrency;

namespace Unit.Common;

[TestFixture]
public class SemaphoreSlimLockerTests
{
    private SemaphoreSlimLocker _locker;

    [SetUp]
    public void SetUp() =>
        _locker = new SemaphoreSlimLocker(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500));

    [Test]
    public async Task TryAcquireLockAsync_AcquiresLock()
    {
        var cacheLock = await _locker.TryAcquireLockAsync("res");
        cacheLock.IsAcquired.Should().BeTrue();
        cacheLock.Resource.Should().Be("res");
        await _locker.ReleaseLockAsync(cacheLock);
    }

    [Test]
    public async Task WaitForLockAsync_AcquiresLock()
    {
        var cacheLock = await _locker.WaitForLockAsync("res");
        cacheLock.IsAcquired.Should().BeTrue();
        await _locker.ReleaseLockAsync(cacheLock);
    }

    [Test]
    public async Task ReleaseLockAsync_WhenAcquired_ReleasesSemaphore()
    {
        var first = await _locker.TryAcquireLockAsync("res");
        await _locker.ReleaseLockAsync(first);

        var second = await _locker.TryAcquireLockAsync("res");
        second.IsAcquired.Should().BeTrue();
        await _locker.ReleaseLockAsync(second);
    }

    [Test]
    public async Task ReleaseLockAsync_WhenNotAcquired_IsNoOp()
    {
        var cacheLock = await _locker.TryAcquireLockAsync("res");
        cacheLock.IsAcquired = false;
        await _locker.Invoking(l => l.ReleaseLockAsync(cacheLock)).Should().NotThrowAsync();
    }

    [Test]
    public async Task TryAcquireLockAsync_WhenAlreadyHeld_TimesOutAndReturnsNotAcquired()
    {
        var shortTimeout = new SemaphoreSlimLocker(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(50));
        var first = await shortTimeout.TryAcquireLockAsync("res");
        first.IsAcquired.Should().BeTrue();

        var second = await shortTimeout.TryAcquireLockAsync("res");
        second.IsAcquired.Should().BeFalse();

        await shortTimeout.ReleaseLockAsync(first);
    }

    [Test]
    public async Task WaitForLockThenAsync_Action_ExecutesWhenLockAcquired()
    {
        var executed = false;
        await _locker.WaitForLockThenAsync("res", () => { executed = true; });
        executed.Should().BeTrue();
    }

    [Test]
    public async Task WaitForLockThenAsync_AsyncFunc_ReturnsResult()
    {
        var result = await _locker.WaitForLockThenAsync("res", () => Task.FromResult("hello"));
        result.Should().Be("hello");
    }

    [Test]
    public async Task WaitForLockThenAsync_WhenLockNotAcquired_DoesNotExecuteAction()
    {
        var shortTimeout = new SemaphoreSlimLocker(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(50));
        var held = await shortTimeout.TryAcquireLockAsync("blocked");
        held.IsAcquired.Should().BeTrue();

        var executed = false;
        await shortTimeout.WaitForLockThenAsync("blocked", () => { executed = true; });
        executed.Should().BeFalse();

        await shortTimeout.ReleaseLockAsync(held);
    }
}
