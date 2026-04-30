using ActionCache.Common.Concurrency;
using ActionCache.Common.Concurrency.Locks;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using System.Text;

namespace Unit.Common.Concurrency;

[TestFixture]
public class DistributedCacheLockerTests
{
    private Mock<IDistributedCache> _cacheMock;
    private DistributedCacheLocker _sut;

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _sut = new DistributedCacheLocker(
            _cacheMock.Object,
            lockDuration: TimeSpan.FromMilliseconds(500),
            lockTimeout: TimeSpan.FromMilliseconds(200));
    }

    [Test]
    public async Task TryAcquireLockAsync_WhenNoExistingLock_SetsLockInCache()
    {
        string? storedValue = null;
        _cacheMock.Setup(cache => cache.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync((byte[]?)null);
        _cacheMock.Setup(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (key, val, opts, ct) => storedValue = Encoding.UTF8.GetString(val))
            .Returns(Task.CompletedTask);

        await _sut.TryAcquireLockAsync("my-resource");

        _cacheMock.Verify(
            cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default),
            Times.Once);
    }

    [Test]
    public async Task TryAcquireLockAsync_WhenLockAlreadyExists_DoesNotSetLock()
    {
        var existingValue = Encoding.UTF8.GetBytes("some-existing-lock");
        _cacheMock.Setup(cache => cache.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync(existingValue);

        var result = await _sut.TryAcquireLockAsync("my-resource");

        result.IsAcquired.Should().BeFalse();
        _cacheMock.Verify(
            cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default),
            Times.Never);
    }

    [Test]
    public async Task ReleaseLockAsync_WhenCurrentValueMatchesLock_RemovesFromCache()
    {
        var cacheLock = new DistributedCacheLock("my-resource",
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        _cacheMock.Setup(cache => cache.GetAsync(cacheLock.Key, default))
            .ReturnsAsync(Encoding.UTF8.GetBytes(cacheLock.Value));

        await _sut.ReleaseLockAsync(cacheLock);

        _cacheMock.Verify(cache => cache.RemoveAsync(cacheLock.Key, default), Times.Once);
    }

    [Test]
    public async Task ReleaseLockAsync_WhenCurrentValueDiffers_DoesNotRemove()
    {
        var cacheLock = new DistributedCacheLock("my-resource",
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        _cacheMock.Setup(cache => cache.GetAsync(cacheLock.Key, default))
            .ReturnsAsync(Encoding.UTF8.GetBytes("different-value"));

        await _sut.ReleaseLockAsync(cacheLock);

        _cacheMock.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Test]
    public async Task WaitForLockThenAsync_WhenLockTimesOut_DoesNotExecuteAction()
    {
        var sutWithShortTimeout = new DistributedCacheLocker(
            _cacheMock.Object,
            lockDuration: TimeSpan.FromMilliseconds(100),
            lockTimeout: TimeSpan.Zero);

        var existingValue = Encoding.UTF8.GetBytes("existing-lock");
        _cacheMock.Setup(cache => cache.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync(existingValue);

        var actionExecuted = false;
        await sutWithShortTimeout.WaitForLockThenAsync("resource", () => { actionExecuted = true; });

        actionExecuted.Should().BeFalse();
    }

    [Test]
    public async Task WaitForLockAsync_WhenLockCantBeAcquired_ReturnsUnacquiredLock()
    {
        var sutWithShortTimeout = new DistributedCacheLocker(
            _cacheMock.Object,
            lockDuration: TimeSpan.FromMilliseconds(100),
            lockTimeout: TimeSpan.Zero);

        var existingValue = Encoding.UTF8.GetBytes("existing-lock");
        _cacheMock.Setup(cache => cache.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync(existingValue);

        var result = await sutWithShortTimeout.WaitForLockAsync("resource");

        result.IsAcquired.Should().BeFalse();
    }

    [Test]
    public async Task WaitForLockAsync_WhenLockIsAcquired_ReturnsAcquiredLock()
    {
        byte[]? storedValue = null;
        _cacheMock.Setup(cache => cache.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync(() => storedValue);

        _cacheMock.Setup(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (key, val, opts, ct) => storedValue = val)
            .Returns(Task.CompletedTask);

        var result = await _sut.WaitForLockAsync("resource");

        result.IsAcquired.Should().BeTrue();
    }
}
