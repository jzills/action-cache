using ActionCache.Common.Concurrency;

namespace Unit.Common;

[TestFixture]
public class NullCacheLockerTests
{
    private NullCacheLocker _locker;

    [SetUp]
    public void SetUp() => _locker = new NullCacheLocker();

    [Test]
    public async Task TryAcquireLockAsync_ReturnsAcquiredLock()
    {
        var cacheLock = await _locker.TryAcquireLockAsync("resource");
        cacheLock.Should().NotBeNull();
        cacheLock.Resource.Should().Be("resource");
    }

    [Test]
    public async Task WaitForLockAsync_ReturnsAcquiredLock()
    {
        var cacheLock = await _locker.WaitForLockAsync("resource");
        cacheLock.Should().NotBeNull();
    }

    [Test]
    public async Task ReleaseLockAsync_CompletesWithoutError()
    {
        var cacheLock = await _locker.TryAcquireLockAsync("resource");
        await _locker.Invoking(l => l.ReleaseLockAsync(cacheLock)).Should().NotThrowAsync();
    }

    [Test]
    public async Task WaitForLockThenAsync_Action_DoesNotExecute_BecauseLockNeverAcquired()
    {
        // NullCacheLock.IsAcquired defaults false, so the base WaitForLockThenAsync never runs the action
        var executed = false;
        await _locker.WaitForLockThenAsync("resource", () => { executed = true; });
        executed.Should().BeFalse();
    }

    [Test]
    public async Task WaitForLockThenAsync_Func_ReturnsDefault_BecauseLockNeverAcquired()
    {
        var result = await _locker.WaitForLockThenAsync("resource", () => Task.FromResult(42));
        result.Should().Be(default(int));
    }

    [Test]
    public async Task WaitForLockThenAsync_AsyncAction_DoesNotExecute_BecauseLockNeverAcquired()
    {
        var executed = false;
        await _locker.WaitForLockThenAsync("resource", () =>
        {
            executed = true;
            return Task.CompletedTask;
        });
        executed.Should().BeFalse();
    }

    [Test]
    public async Task WaitForLockThenAsync_SyncFunc_ReturnsDefault_BecauseLockNeverAcquired()
    {
        var result = await _locker.WaitForLockThenAsync("resource", () => 99);
        result.Should().Be(default(int));
    }

}
