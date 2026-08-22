using ActionCache.Common.Concurrency;

namespace Unit.Common;

[TestFixture]
public class NullCacheLockerTests
{
    private NullCacheLocker _locker = null!;

    [SetUp]
    public void SetUp() => _locker = new NullCacheLocker();

    [Test]
    public async Task TryAcquireLockAsync_ReturnsAcquiredLock()
    {
        var cacheLock = await _locker.TryAcquireLockAsync("resource");
        cacheLock.Should().NotBeNull();
        cacheLock.Resource.Should().Be("resource");
        cacheLock.IsAcquired.Should().BeTrue();
    }

    [Test]
    public async Task WaitForLockAsync_ReturnsAcquiredLock()
    {
        var cacheLock = await _locker.WaitForLockAsync("resource");
        cacheLock.Should().NotBeNull();
        cacheLock.IsAcquired.Should().BeTrue();
    }

    [Test]
    public async Task ReleaseLockAsync_CompletesWithoutError()
    {
        var cacheLock = await _locker.TryAcquireLockAsync("resource");
        await _locker.Invoking(l => l.ReleaseLockAsync(cacheLock)).Should().NotThrowAsync();
    }

    [Test]
    public async Task WaitForLockThenAsync_Action_AlwaysExecutes()
    {
        var executed = false;
        await _locker.WaitForLockThenAsync("resource", () => { executed = true; });
        executed.Should().BeTrue();
    }

    [Test]
    public async Task WaitForLockThenAsync_AsyncFunc_ReturnsResult()
    {
        var result = await _locker.WaitForLockThenAsync("resource", () => Task.FromResult(42));
        result.Should().Be(42);
    }

    [Test]
    public async Task WaitForLockThenAsync_AsyncAction_AlwaysExecutes()
    {
        var executed = false;
        await _locker.WaitForLockThenAsync("resource", () =>
        {
            executed = true;
            return Task.CompletedTask;
        });
        executed.Should().BeTrue();
    }

    [Test]
    public async Task WaitForLockThenAsync_SyncFunc_ReturnsResult()
    {
        var result = await _locker.WaitForLockThenAsync("resource", () => 99);
        result.Should().Be(99);
    }
}
