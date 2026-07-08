using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ActionCache.Redis;

/// <summary>
/// A background service that listens to Redis key expiration events and processes expired keys.
/// </summary>
public class RedisExpiryService : BackgroundService
{
    /// <summary>
    /// Regular expression used to parse keys into their component parts.
    /// </summary>
    protected static readonly Regex KeyExpression = new Regex("^(.*):([^:]+)$");

    /// <summary>
    /// The Redis database instance used for cache operations.
    /// </summary>
    protected readonly IDatabase Cache;

    /// <summary>
    /// The Redis subscriber instance used to subscribe to key expiration notifications.
    /// </summary>
    protected readonly ISubscriber Subscriber;

    /// <summary>
    /// Records subscription failures so a Redis outage does not crash the host.
    /// </summary>
    private readonly ILogger<RedisExpiryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisExpiryService"/> class.
    /// </summary>
    /// <param name="connectionMultiplexer">
    /// The Redis connection multiplexer used to access the database and subscriber.
    /// </param>
    /// <param name="logger">The logger used to record subscription failures.</param>
    public RedisExpiryService(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisExpiryService> logger)
    {
        Cache = connectionMultiplexer.GetDatabase();
        Subscriber = connectionMultiplexer.GetSubscriber();
        _logger = logger;
    }

    /// <summary>
    /// Executes the background service, subscribing to Redis key expiration events and handling them.
    /// </summary>
    /// <param name="stoppingToken">A token used to signal cancellation of the service.</param>
    /// <returns>A task that represents the execution of the service.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var channel = RedisChannel.Literal($"__keyevent@{Cache.Database}__:expired");
            await Subscriber.SubscribeAsync(channel, async (_, message) =>
            {
                var key = (string?)message;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    var match = KeyExpression.Match(key);
                    if (match.Success && match.Groups.Count == 3)
                    {
                        await Cache.SortedSetRemoveAsync(
                            match.Groups[1].Value,
                            match.Groups[2].Value
                        );
                    }
                }
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "ActionCache could not subscribe to Redis keyspace expiry notifications on database {Database}; " +
                "sliding-expiration index cleanup will rely on lazy self-healing.",
                Cache.Database);
            return;
        }

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
