using ActionCache.Common.Concurrency;

namespace Unit.Common.Concurrency;

[TestFixture]
public class SemaphoreSlimCacheLockerTests
{
    private static SemaphoreSlimCacheLocker CreateLocker(TimeSpan? lockTimeout = null) =>
        new(TimeSpan.FromSeconds(5), lockTimeout ?? TimeSpan.FromSeconds(10));

    [Test]
    public async Task WaitForLockAsync_WhenResourceIsFree_AcquiresLock()
    {
        var locker = CreateLocker();

        var cacheLock = await locker.WaitForLockAsync("Resource");

        cacheLock.IsAcquired.Should().BeTrue();
    }

    [Test]
    public async Task TryAcquireLockAsync_WhenResourceIsHeld_DoesNotAcquire()
    {
        var locker = CreateLocker();
        var held = await locker.WaitForLockAsync("Resource");

        var contender = await locker.TryAcquireLockAsync("Resource");

        contender.IsAcquired.Should().BeFalse();
        await locker.ReleaseLockAsync(held);
    }

    [Test]
    public async Task TryAcquireLockAsync_WhenResourceIsReleased_AcquiresLock()
    {
        var locker = CreateLocker();
        var held = await locker.WaitForLockAsync("Resource");
        await locker.ReleaseLockAsync(held);

        var contender = await locker.TryAcquireLockAsync("Resource");

        contender.IsAcquired.Should().BeTrue();
    }

    [Test]
    public async Task TryAcquireLockAsync_WhenDifferentResources_DoesNotSerialize()
    {
        var locker = CreateLocker();
        var first = await locker.WaitForLockAsync("ResourceOne");

        var second = await locker.TryAcquireLockAsync("ResourceTwo");

        second.IsAcquired.Should().BeTrue();
        await locker.ReleaseLockAsync(first);
        await locker.ReleaseLockAsync(second);
    }

    [Test]
    public async Task WaitForLockAsync_WhenTimeoutElapses_ReturnsUnacquiredLockWithoutThrowing()
    {
        var locker = CreateLocker(lockTimeout: TimeSpan.FromMilliseconds(50));
        var held = await locker.WaitForLockAsync("Resource");

        var contender = await locker.WaitForLockAsync("Resource");

        contender.IsAcquired.Should().BeFalse();
        await locker.ReleaseLockAsync(held);
    }

    [Test]
    public async Task WaitForLockThenAsync_UnderConcurrency_SerializesCriticalSection()
    {
        var locker = CreateLocker();
        var concurrent = 0;
        var maxObserved = 0;

        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ =>
            locker.WaitForLockThenAsync("Resource", async () =>
            {
                var observed = Interlocked.Increment(ref concurrent);
                maxObserved = Math.Max(maxObserved, observed);
                await Task.Delay(1);
                Interlocked.Decrement(ref concurrent);
            })));

        maxObserved.Should().Be(1);
    }

    [Test]
    public async Task ReleaseLockAsync_WhenAllLocksReleased_DoesNotRetainResources()
    {
        var locker = CreateLocker();

        await Task.WhenAll(Enumerable.Range(0, 25).Select(index =>
            locker.WaitForLockThenAsync($"Resource:{index}", () => Task.CompletedTask)));

        locker.TrackedResourceCount.Should().Be(0);
    }

    [Test]
    public async Task ReleaseLockAsync_WhenCalledTwice_ReleasesOnlyOnce()
    {
        var locker = CreateLocker();
        var held = await locker.WaitForLockAsync("Resource");

        await locker.ReleaseLockAsync(held);
        await locker.ReleaseLockAsync(held);

        var contender = await locker.TryAcquireLockAsync("Resource");
        contender.IsAcquired.Should().BeTrue();
        locker.TrackedResourceCount.Should().Be(1);
    }
}
