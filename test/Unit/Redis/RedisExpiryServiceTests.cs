using ActionCache.Redis;
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

        _sut = new RedisExpiryService(_multiplexerMock.Object);
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
    public async Task ExpiryCallback_WhenMessageIsEmpty_DoesNotCallSortedSetRemove()
    {
        Action<RedisChannel, RedisValue>? capturedHandler = null;
        _subscriberMock
            .Setup(subscriber => subscriber.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => capturedHandler = handler)
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);

        capturedHandler!.Invoke(RedisChannel.Literal("__keyevent@0__:expired"), new RedisValue(""));

        await Task.Delay(100);

        _databaseMock.Verify(db => db.SortedSetRemoveAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Test]
    public async Task ExpiryCallback_WhenMessageHasNoColon_DoesNotCallSortedSetRemove()
    {
        Action<RedisChannel, RedisValue>? capturedHandler = null;
        _subscriberMock
            .Setup(subscriber => subscriber.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => capturedHandler = handler)
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);

        capturedHandler!.Invoke(RedisChannel.Literal("__keyevent@0__:expired"), new RedisValue("keywithnoseparator"));

        await Task.Delay(100);

        _databaseMock.Verify(db => db.SortedSetRemoveAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Test]
    public async Task ExpiryCallback_WhenMessageMatchesNamespaceKeyPattern_RemovesMemberFromSortedSet()
    {
        Action<RedisChannel, RedisValue>? capturedHandler = null;
        _subscriberMock
            .Setup(subscriber => subscriber.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => capturedHandler = handler)
            .Returns(Task.CompletedTask);

        _databaseMock
            .Setup(db => db.SortedSetRemoveAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);

        capturedHandler!.Invoke(RedisChannel.Literal("__keyevent@0__:expired"), new RedisValue("mynamespace:mykey"));

        await Task.Delay(100);

        _databaseMock.Verify(db => db.SortedSetRemoveAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Once);
    }
}
