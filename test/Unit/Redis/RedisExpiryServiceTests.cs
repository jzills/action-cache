using System.Text.RegularExpressions;
using ActionCache.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Unit.Redis;

[TestFixture]
public class RedisExpiryServiceTests
{
    private Mock<IConnectionMultiplexer> _multiplexerMock;
    private Mock<IDatabase> _databaseMock;
    private Mock<ISubscriber> _subscriberMock;
    private RedisExpiryService _sut;

    [SetUp]
    public void SetUp()
    {
        _databaseMock = new Mock<IDatabase>();
        _subscriberMock = new Mock<ISubscriber>();
        _multiplexerMock = new Mock<IConnectionMultiplexer>();
        _multiplexerMock
            .Setup(multiplexer => multiplexer.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(_databaseMock.Object);
        _multiplexerMock
            .Setup(multiplexer => multiplexer.GetSubscriber(It.IsAny<object?>()))
            .Returns(_subscriberMock.Object);

        _sut = new RedisExpiryService(_multiplexerMock.Object, NullLogger<RedisExpiryService>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _sut.StopAsync(CancellationToken.None);
        _sut.Dispose();
    }

    [Test]
    public void Constructor_Always_InitializesDatabaseAndSubscriber()
    {
        _multiplexerMock.Verify(multiplexer => multiplexer.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()), Times.Once);
        _multiplexerMock.Verify(multiplexer => multiplexer.GetSubscriber(It.IsAny<object?>()), Times.Once);
    }

    [Test]
    public async Task StartAsync_Always_SubscribesToKeyExpiryChannel()
    {
        _subscriberMock
            .Setup(subscriber => subscriber.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);

        _subscriberMock.Verify(subscriber => subscriber.SubscribeAsync(
            It.Is<RedisChannel>(channel => channel == RedisChannel.Literal("__keyevent@0__:expired")),
            It.IsAny<Action<RedisChannel, RedisValue>>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Test]
    public async Task StartAsync_WhenConnectionUsesNonZeroDatabase_SubscribesToThatDatabasesChannel()
    {
        _databaseMock.Setup(database => database.Database).Returns(3);
        _subscriberMock
            .Setup(subscriber => subscriber.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);

        _subscriberMock.Verify(subscriber => subscriber.SubscribeAsync(
            It.Is<RedisChannel>(channel => channel == RedisChannel.Literal("__keyevent@3__:expired")),
            It.IsAny<Action<RedisChannel, RedisValue>>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Test]
    public async Task ExpiryCallback_WhenMessageIsEmpty_DoesNotCallSortedSetRemove()
    {
        Action<RedisChannel, RedisValue>? capturedHandler = null;
        var handlerCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _subscriberMock
            .Setup(subscriber => subscriber.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => { capturedHandler = handler; handlerCaptured.SetResult(); })
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await handlerCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5));

        capturedHandler!.Invoke(RedisChannel.Literal("__keyevent@0__:expired"), new RedisValue(""));

        await Task.Delay(100);

        _databaseMock.Verify(db => db.SortedSetRemoveAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Test]
    public async Task ExpiryCallback_WhenMessageHasNoColon_DoesNotCallSortedSetRemove()
    {
        Action<RedisChannel, RedisValue>? capturedHandler = null;
        var handlerCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _subscriberMock
            .Setup(subscriber => subscriber.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => { capturedHandler = handler; handlerCaptured.SetResult(); })
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await handlerCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5));

        capturedHandler!.Invoke(RedisChannel.Literal("__keyevent@0__:expired"), new RedisValue("keywithnoseparator"));

        await Task.Delay(100);

        _databaseMock.Verify(db => db.SortedSetRemoveAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    // Bug H6: The regex ^(.*):([^:]+)$ splits on the last colon in the full Redis key.
    // The plan flags this as broken for namespaces containing colons. The tests below
    // verify the actual behavior: because (.*) is greedy, it always captures everything
    // up to the last colon, which IS the correct namespace/key boundary — provided the
    // cache key itself (hex-encoded) never contains a colon. These tests document the
    // behavior under multi-colon namespaces so that any future key-format change does
    // not silently break namespace extraction.

    [Test]
    public async Task ExpiryCallback_WhenNamespaceContainsSingleColon_CorrectlySplitsKey_H6()
    {
        Action<RedisChannel, RedisValue>? capturedHandler = null;
        var handlerCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _subscriberMock
            .Setup(subscriber => subscriber.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => { capturedHandler = handler; handlerCaptured.SetResult(); })
            .Returns(Task.CompletedTask);

        RedisKey? capturedSortedSetKey = null;
        RedisValue? capturedMember = null;
        _databaseMock
            .Setup(db => db.SortedSetRemoveAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, CommandFlags>((key, member, _) =>
            {
                capturedSortedSetKey = key;
                capturedMember = member;
            })
            .ReturnsAsync(true);

        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await handlerCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Namespace "ActionCache:Users" with cache key "abc123"
        capturedHandler!.Invoke(
            RedisChannel.Literal("__keyevent@0__:expired"),
            new RedisValue("ActionCache:Users:abc123"));

        await Task.Delay(100);

        capturedSortedSetKey.Should().Be((RedisKey)"ActionCache:Users");
        capturedMember.Should().Be((RedisValue)"abc123");
    }

    [Test]
    public async Task ExpiryCallback_WhenNamespaceContainsMultipleColons_SplitsAtLastColon_H6()
    {
        Action<RedisChannel, RedisValue>? capturedHandler = null;
        var handlerCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _subscriberMock
            .Setup(subscriber => subscriber.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => { capturedHandler = handler; handlerCaptured.SetResult(); })
            .Returns(Task.CompletedTask);

        RedisKey? capturedSortedSetKey = null;
        RedisValue? capturedMember = null;
        _databaseMock
            .Setup(db => db.SortedSetRemoveAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, CommandFlags>((key, member, _) =>
            {
                capturedSortedSetKey = key;
                capturedMember = member;
            })
            .ReturnsAsync(true);

        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await handlerCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Namespace "ActionCache:Area:Controller:Action" with cache key "abc123"
        // The greedy (.*) captures everything up to the last colon, so the split IS correct.
        capturedHandler!.Invoke(
            RedisChannel.Literal("__keyevent@0__:expired"),
            new RedisValue("ActionCache:Area:Controller:Action:abc123"));

        await Task.Delay(100);

        capturedSortedSetKey.Should().Be((RedisKey)"ActionCache:Area:Controller:Action");
        capturedMember.Should().Be((RedisValue)"abc123");
    }

    [Test]
    public async Task ExpiryCallback_WhenMessageMatchesNamespaceKeyPattern_RemovesMemberFromSortedSet()
    {
        Action<RedisChannel, RedisValue>? capturedHandler = null;
        var handlerCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _subscriberMock
            .Setup(subscriber => subscriber.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => { capturedHandler = handler; handlerCaptured.SetResult(); })
            .Returns(Task.CompletedTask);

        _databaseMock
            .Setup(db => db.SortedSetRemoveAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await handlerCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5));

        capturedHandler!.Invoke(RedisChannel.Literal("__keyevent@0__:expired"), new RedisValue("mynamespace:mykey"));

        await Task.Delay(100);

        _databaseMock.Verify(db => db.SortedSetRemoveAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WhenFirstSubscribeFails_RetriesUntilItSucceeds()
    {
        var secondAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        _subscriberMock
            .Setup(subscriber => subscriber.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Returns(() =>
            {
                attempts++;
                if (attempts == 1)
                {
                    return Task.FromException(new InvalidOperationException("redis unavailable"));
                }

                secondAttempt.TrySetResult();
                return Task.CompletedTask;
            });

        _sut.InitialRetryDelay = TimeSpan.FromMilliseconds(20);

        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);

        await secondAttempt.Task.WaitAsync(TimeSpan.FromSeconds(5));

        attempts.Should().BeGreaterThanOrEqualTo(2);
        _subscriberMock.Verify(subscriber => subscriber.SubscribeAsync(
            It.IsAny<RedisChannel>(),
            It.IsAny<Action<RedisChannel, RedisValue>>(),
            It.IsAny<CommandFlags>()), Times.AtLeast(2));
    }

    [Test]
    public void NextRetryDelay_DoublesEachTime_UntilCappedAtMaxRetryDelay()
    {
        _sut.InitialRetryDelay = TimeSpan.FromSeconds(1);
        _sut.MaxRetryDelay = TimeSpan.FromSeconds(8);

        var first = _sut.NextRetryDelay(_sut.InitialRetryDelay);
        var second = _sut.NextRetryDelay(first);
        var third = _sut.NextRetryDelay(second);
        var fourth = _sut.NextRetryDelay(third);

        first.Should().Be(TimeSpan.FromSeconds(2));
        second.Should().Be(TimeSpan.FromSeconds(4));
        third.Should().Be(TimeSpan.FromSeconds(8));   // capped
        fourth.Should().Be(TimeSpan.FromSeconds(8));  // stays at cap
    }
}
